using System;
using gdsFM;

namespace gdsFM 
{
    public class Channel
    {
        public long eventID;  //Unique value assigned during NoteOn event to identify it later during a NoteOff event.  Always > 0
        public BusyState busy=BusyState.FREE;
        public Operator[] ops;

        Voice voice;
        int[] cache;  //As each level of the stack gets processed, the sample cache for each operator is updated.

        public byte midi_note;  //Assigned when the channel is requested and used when enumerating channels to help call early NoteOffs for duplicate notes.

        public short lastSample;

        public static ushort am_offset=0;  //Set by the chip when clocking to pass down when requesting samples from our operators.

        public Channel(byte opCount)
        {
            ops = new Operator[opCount];
            // processOrder = new byte[opCount];
            // Array.Copy(Algorithm.DEFAULT_PROCESS_ORDER, processOrder, opCount);
            // connections = new byte[opCount];
            cache = new int[opCount];

            for(int i=0; i<opCount; i++)
            {
                ops[i] = new Operator();
            }
        }

        public void Clock()
        {
            ProcessNextSample();
            for (byte i=0; i<ops.Length; i++)
            {
                ops[i].Clock();
            }
        }

        ////// PRIORITY SCORE
        public short PriorityScore     
        {
          get{
            int score=(int)256 - (Math.Abs(lastSample >> 6)); //0-127 -- simple volume value; 
            // int score=0; 

            //Check busy state for free.
            bool setFree = true;
            for(int i=0; i<ops.Length; i++)
            {
                var op=ops[i];
                if (busy==BusyState.BUSY ||
                   (busy==BusyState.RELEASED &&  op.egStatus != EGStatus.INACTIVE) )
                        setFree = false;

                score += (int)ops[i].egStatus <<4;
            }
            if (setFree) busy = BusyState.FREE;
            score += (int)busy;   //512 points if BusyState.Released;  1024 if free.


            return (short)score;
            //TODO:  Some sorta thing which enumerates the operators for their envelope status and attenuation.  The higher the score, the higher the priority.
            //      Near-silent and near-finished voices should give the lowest scores.  Use processOrder in reverse, checking connections to output only.
            //      Stop and return the score once we hit the first operator with connections, since these don't factor into the final output level.
          }
        }


        public void NoteOn(byte midi_note=Global.NO_NOTE_SPECIFIED, byte velocity=127)
        {
            eventID = Global.NewEventID();
            if (midi_note < 0x80)  this.midi_note = midi_note;

            var opsToProcess = (byte)Math.Min(voice.alg.opCount, ops.Length); //Prevents out of bounds if the voice changed while notes are still on.
            for(byte i=0; i<opsToProcess; i++)
            {
                var op=ops[i];
                op.eg = new Envelope(voice.egs[i]);  //Make copies of the old EG values so they can be altered on a per-note basis.

                // Adjust the EG based on values from the RTables.
                if (midi_note < 0x80) 
                {
                    op.eg.ksl.Apply(midi_note, ref op.eg.levels[4]);  //Apply KSL to total level

                    for (int j=0; j<op.eg.rates.Length-1; j++)
                    {
                        //Rate scaling is applied double to everything except the release rate. (r = 2r + ksr)
                        var rate = (byte)(op.eg.rates[j] * 2);  
                        op.eg.ksr.Apply(midi_note, ref rate);
                        op.eg.rates[j] = (byte)rate;

                    }
                    op.eg.ksr.Apply(midi_note, ref op.eg.rates[op.eg.rates.Length-1]); //Release rate

                }
                op.eg.velocity.Apply(velocity, ref op.eg.levels[4]);  //Apply velocity to total level



                op.pg = voice.pgs[i];  //Set Increments to the last Voice increments value (ByVal copy)
                op.SetOscillatorType((byte)voice.opType[i]);  //Set the wave function to what the voice says it should be

                //Prepare the note's increment based on calculated pitch
                if (!op.pg.fixedFreq)
                    op.pg.NoteSelect(this.midi_note);
                op.pg.Recalc();
                op.NoteOn();

                busy = BusyState.BUSY;                
            }
        }

        // public void NoteOn(float hz)
        // {
        //     //TODO:  Fixed frequency note on?
        // }

        public void NoteOff()
        {
            if(busy==BusyState.BUSY)  busy=BusyState.RELEASED;
            for(byte i=0; i<ops.Length; i++)
                ops[i].NoteOff();
        }

        /// Main algorithm processor.
        //  TODO:  Pass down LFO status from the chip. 
        public short RequestSample()
        {
            return lastSample;
        }

        public void ProcessNextSample()
        {
            if (voice==null) return;
            int output = 0;


            for (byte i=0; i<cache.Length; i++) cache[i] = 0;   //Clear the modulation cache.  FIXME:  Ensure this is correct!!!

            //TODO:  Consider instead bringing in an ordered array with each operator from the top row down 
            //       and rely solely on this (and the frontend) to keep algorithm sane.
            

            byte opsToProcess = (byte) Math.Min(voice.alg.processOrder.Length, ops.Length); //Prevents out of bounds if the voice changed while notes are still on.

            //For each height level in the stack, look for operators to mix down.
            for (byte o=0; o < opsToProcess;  o++)
            {
                var src_op = voice.alg.processOrder[o];  //Source op number.
                if (ops[src_op].eg.mute)  
                {
                    cache[src_op] = 0;
                    continue;  //Exit early if muted.
                }

                int c = voice.alg.connections[ src_op ];  //Get Connections for the source operator.


                //First, convert the source op's accumulated phase up to this point into its sample result value.
                //For termini (top of an op stack), the phase accumulated in the cache will always be 0.
                var modulation = ops[src_op].RequestSample( (ushort)cache[src_op], am_offset );

                if (c==0)  //Source op isn't connected to anything, so we assume it's connected to output.
                {
                    output += ops[src_op].eg.bypass? cache[src_op] : modulation;  //Accumulate total.
                    continue;
                }

                //Source op has connections. Mix down the sample results to its connections.
                for(byte dest_op=0; c>0; dest_op++)  //Crunch the connection bitmask down, one bit at a time, until there's no more connections.
                {
                    unchecked 
                    {
                        if ( (c & 1) == 1)  
                        { //Flag is set. The destination op receives modulation from the source op and is added to the total output cache for that operator.
                            if (!ops[src_op].eg.bypass)  cache[dest_op] += modulation;  else  cache[dest_op] = cache[src_op];
                        }
                        c >>= 1; //Crunch down and process next position. Loop continues until the connection mask has no more connections.
                    }
                }

            }     
            lastSample = (short)output.Clamp(short.MinValue, short.MaxValue);            
        }

        private void SetOpCount(byte opTarget)
        {
            byte opCount = (byte) ops.Length;
            Array.Resize(ref ops, opTarget);
            Array.Resize(ref cache, opTarget);

            if (opTarget>opCount)
            {
                for (byte i=opCount; i<opTarget; i++)
                {
                    ops[i] = new Operator();

                }
            }

        }

        /// Sets this channel's voice to the specified voice.
        public void SetVoice(Voice voice)
        {
            if(voice.opCount != ops.Length)  SetOpCount(voice.opCount);
            for (int i=0; i<voice.opCount;  i++)
            {
                ops[i].eg = voice.egs[i];
                ops[i].pg = voice.pgs[i];  //ByVal copy
            }

            this.voice = voice;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            string nl = Environment.NewLine;

            byte i=0;
            foreach (Operator op in ops)
            {
                string rising;
                if (op.egStatus>=0 && (int)op.egStatus < op.eg.rising.Length)
                    rising = op.eg.rising[(int)op.egStatus] ? "Rising" : "Falling";
                else rising= "Doing nothing";
                sb.Append( String.Format("Op{0}: {1} and {2} ({3})",  i+1, op.egStatus.ToString(), rising, op.egAttenuation) );
                // sb.Append(String.Format("Op{0}: {1}", i+1, ops[i].pg.increment));
                sb.Append(nl);
                i++;
            }
            return sb.ToString();
        }


    }

}
