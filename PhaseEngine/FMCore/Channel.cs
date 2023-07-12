using System;
using PhaseEngine;

namespace PhaseEngine 
{
    public class Channel
    {
        public long eventID;  //Unique value assigned during NoteOn event to identify it later during a NoteOff event.  Always > 0
        public BusyState busy=BusyState.FREE;
        public OpBase[] ops;

        Voice voice;
        int[] cache;  //As each level of the stack gets processed, the sample cache for each operator is updated.

        public byte midi_note;  //Assigned when the channel is requested and used when enumerating channels to help call early NoteOffs for duplicate notes.

        public short lastSample;
        public byte lastVelocity;
        public short lastPriorityScore;

        public static ushort am_offset=0;  //Set by the chip when clocking to pass down when requesting samples from our operators.

        readonly ushort chipDivider=1;
        public Channel(byte opCount, ushort chipDivider=1)
        {
            this.chipDivider = chipDivider;
            ops = new OpBase[opCount];
            cache = new int[opCount];

            for(int i=0; i<opCount; i++)
            {
                ops[i] = new Operator();  //Default intent is FM_OP when initializing a channel.
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

        ////// RECALCULATES THE PRIORITY SCORE
        public short CalcPriorityScore()
        {
            int score=(int)256 - (Math.Abs(lastSample >> 6)); //0-127 -- simple volume value; 
            // int score=0; 

            //Check busy state for free.
            bool setFree = true;
            for(byte i=0; i<ops.Length; i++)
            {
                //        Skip over Filter ops when adding egStatus entirely and instead if an operator is connected to a filter,
                //        Determine if the filter's connected to output, and only then process op as if directly connected to output.
                if (!voice.OpReachesOutput(i) || voice.egs[i].mute)  continue;  //Skip over connections not connected to output.

                var op=ops[i];
                if (busy==BusyState.BUSY ||
                   (busy==BusyState.RELEASED &&  (op.egStatus != EGStatus.INACTIVE)) )
                        setFree = false; //Found an active channel.  Don't set our busy state to FREE.

                //Status of the envelope is such that the further along in the envelope it is, the higher the score.
                //Lsh ensures that the value is always positive -- Delay is considered same priority as Decay, Hold the same as Sustained
                score += (int)op.egStatus <<4;  
                score >>= 1;  //Compensate for multiple operators connected to output.
            }
            if (setFree) busy = BusyState.FREE;
            score += (int)busy;   //512 points if BusyState.Released;  1024 if free.

            lastPriorityScore = (short)score;
            return lastPriorityScore;
        }


        //Generates a Note on this channel by setting new properties from a Voice.
        public void NoteOn(byte midi_note=Global.NO_NOTE_SPECIFIED, byte velocity=127)
        {
            eventID = Global.NewEventID();
            if (midi_note >= 0x80)  return;  //Invalid note
            this.midi_note = midi_note;
            this.lastVelocity = velocity;
            busy = BusyState.BUSY;                

            var opsToProcess = (byte)Math.Min(voice.alg.opCount, ops.Length); //Prevents out of bounds if the voice changed while notes are still on.
            for(byte i=0; i<opsToProcess; i++)
            {
                var intent=ops[i].intent;


                //NOTE:  This might be faster as a type-matching switch pattern, but this isn't supported in c# 7.2.  Consider changing it in the future if more efficient
                switch(intent)
                {
                case OpBase.Intents.FM_OP:
                case OpBase.Intents.FM_HQ:
                case OpBase.Intents.BITWISE:
                case OpBase.Intents.WAVEFOLDER:
                    var op = ops[i] as Operator;
                    op.eg.Configure(voice.egs[i]);
                    //FIXME: FILTERS DON'T USE THE PHASE ACCUMULATOR, so rebaking shouldn't need to be full!
                    // ((IBindableDataConsumer)op).Rebake(new IBindableDataSrc[] {op.eg, op.pg}, chipDivider);  //Reset the cached envelopes
                    op.BindStates.Clear();  //Rebind CachedEnvelopes from our data sources to our Operators (the data consumers):
                    ((IBindableDataConsumer)op).AddDataSource(op.eg, chipDivider);
                    ((IBindableDataConsumer)op).AddDataSource(op.pg, chipDivider);

                    // Adjust the EG based on values from the RTables.
                    if (midi_note < 0x80) 
                    {
                        //Apply KSL to total level
                        if (!op.BindStates.ContainsKey("tl"))
                            op.eg.ksl.Apply(midi_note, ref op.eg.levels[4]);  
                        else {  //Total level is bound to an automation envelope.  Scale it.
                            var val = Math.Clamp(op.eg.ksl.ScaledValue(midi_note), 0, Envelope.L_MAX);
                            op.eg.tl = (ushort)(int)op.BindStates["tl"].AddAmount(val);
                        }

                        for (int j=0; j<op.eg.rates.Length-1; j++)
                        {
                            //Rate scaling is applied double to everything except the release rate. (r = 2r + ksr)
                            var rate = (byte)(op.eg.rates[j] * 2);  
                            op.eg.ksr.Apply(midi_note, ref rate);
                            op.eg.rates[j] = (byte)rate;

                        }
                        op.eg.ksr.Apply(midi_note, ref op.eg.rates[op.eg.rates.Length-1]); //Release rate
                    }

                    //Apply velocity to total level
                    if (!op.BindStates.ContainsKey("tl"))
                        op.eg.velocity.Apply(velocity, ref op.eg.levels[4]);  
                    else {  //Total level is bound to an automation envelope.  Scale it.
                        var val = Math.Clamp(op.eg.velocity.ScaledValue(velocity), 0, Envelope.L_MAX);
                        op.eg.tl = (ushort)(int)op.BindStates["tl"].AddAmount(val);
                    }



                    op.pg = voice.pgs[i];  //Set Increments to the last Voice increments value (ByVal copy)
                    op.SetOscillatorType((byte)voice.oscType[i]);  //Set the wave function to what the voice says it should be

                    //Prepare the note's increment based on calculated pitch
                    if (!op.pg.fixedFreq)
                    {
                        op.pg.NoteSelect(this.midi_note);
                        op.pg.ApplyDetuneRandomness();  //Select a detune amount based on specified level of randomness
                    }

                    op.pg.Recalc();
                    op.NoteOn();
                    break;
                
                case OpBase.Intents.FILTER:
                //TODO:  Consider adding a case for FILTER intent to SetOscillatorType here so that it's not relied on user to change the intent of each channel manually.
                    var filter = ops[i] as Filter;
                    filter.eg.Configure(voice.egs[i]);
                    ((IBindableDataConsumer)filter).Rebake(filter.eg, chipDivider);  //Reset the cached envelopes

                    //TODO:  IMPLEMENT KEY FOLLOW AND VELOCITY TABLE ADJUSTMENTS FROM BINDS HERE
                    filter.NoteOn();
                    break;
                }
            }
        }

        public void NoteOff()
        {
            if(busy==BusyState.BUSY)  busy=BusyState.RELEASED;
            for(byte i=0; i<ops.Length; i++)
            {
                ops[i].NoteOff();
            }
        }

        /// Main algorithm processor. ///
        public short RequestSample()    { return lastSample; }
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
                if (voice.egs[src_op].mute)  
                {
                    cache[src_op] = 0;
                    continue;  //Exit early if muted.
                }

                // ApplyTechnique(src_op, ref output);

                //First, convert the source op's accumulated phase up to this point into its sample result value.
                //For termini (top of an op stack), the phase accumulated in the cache will always be 0.
                var operatorResult = ops[src_op].RequestSample( (ushort)cache[src_op], am_offset );

                int c = voice.alg.connections[ src_op ];  //Get Connections for the source operator.
                if (c==0)  //Source op isn't connected to anything, so we assume it's connected to output.
                {
                    output += voice.egs[src_op].bypass? cache[src_op] : operatorResult;  //Accumulate total.
                    continue;
                }

                //Source op has connections. Mix down the sample results to its connections.
                for(byte dest_op=0; c>0; dest_op++)  //Crunch the connection bitmask down, one bit at a time, until there's no more connections.
                {
                    unchecked 
                    {
                        if ( (c & 1) == 1)  
                        { //Flag is set. The destination op receives the result from the source op and is added to the total processing cache for that operator.
                            if (!voice.egs[src_op].bypass)  cache[dest_op] += operatorResult;  else  cache[dest_op] = cache[src_op];
                        }
                        c >>= 1; //Crunch down and process next position. Loop continues until the connection mask has no more connections.
                    }
                }

            }     
            // lastSample = (short)output.Clamp(short.MinValue, short.MaxValue);            
            lastSample = (short)Math.Clamp(output, short.MinValue, short.MaxValue);
        }

    // void TechniqueFM(byte src_op, ref int output)
    // {
    //     //First, convert the source op's accumulated phase up to this point into its sample result value.
    //     //For termini (top of an op stack), the phase accumulated in the cache will always be 0.
    //     var modulation = ops[src_op].RequestSample( (ushort)cache[src_op], am_offset );

    //     int c = voice.alg.connections[ src_op ];  //Get Connections for the source operator.
    //     if (c==0)  //Source op isn't connected to anything, so we assume it's connected to output.
    //     {
    //         output += ops[src_op].eg.bypass? cache[src_op] : modulation;  //Accumulate total.
    //         return;
    //     }

    //     //Source op has connections. Mix down the sample results to its connections.
    //     for(byte dest_op=0; c>0; dest_op++)  //Crunch the connection bitmask down, one bit at a time, until there's no more connections.
    //     {
    //         unchecked 
    //         {
    //             if ( (c & 1) == 1)  
    //             { //Flag is set. The destination op receives modulation from the source op and is added to the total output cache for that operator.
    //                 if (!ops[src_op].eg.bypass)  cache[dest_op] += modulation;  else  cache[dest_op] = cache[src_op];
    //             }
    //             c >>= 1; //Crunch down and process next position. Loop continues until the connection mask has no more connections.
    //         }
    //     }
    // }



        public void SetOpCount(byte opTarget)
        {
            byte opCount = (byte) ops.Length;
            Array.Resize(ref ops, opTarget);
            Array.Resize(ref cache, opTarget);

            if (opTarget>opCount)
                SetIntents(opCount, opTarget);
        }

        /// Sets this channel's voice to the specified voice.
        public void SetVoice(Voice voice)
        {
            this.voice = voice;
            if(voice.opCount != ops.Length)  SetOpCount(voice.opCount);
            SetIntents(0, voice.opCount);

            for(int i=0; i<voice.opCount; i++) ops[i].wavetable = voice.wavetable;

        }

        public void SetIntents(byte first, byte last, Voice voice=null)
        {
            voice??= this.voice;
            for (int i=first; i<last; i++)
            {
                if (ops[i]==null || ops[i].intent != voice.alg.intent[i])
                {
                    switch((OpBase.Intents)voice.alg.intent[i])
                    {
                        case OpBase.Intents.FM_OP:
                        case OpBase.Intents.FM_HQ:
                            var op = new Operator();
                            ops[i] = op;
                            op.eg.Configure(voice.egs[i]);
                            op.pg = voice.pgs[i];  //ByVal copy
                            op.wavetable = voice.wavetable;
                            break;
                        // case OpBase.Intents.FM_HQ:
                        //     var hop = new OperatorHQ();
                        //     ops[i] = hop;
                        //     hop.eg.Configure(voice.egs[i]);
                        //     hop.pg = voice.pgs[i];  //ByVal copy
                        //     hop.wavetable = voice.wavetable;
                        //     break;
                        case OpBase.Intents.FILTER:
                            var f = new Filter();
                            ops[i] = f;
                            f.eg.Configure(voice.egs[i]);
                            f.SetOscillatorType(f.eg.aux_func);  //TODO:  Check if values out of range cause the filter to freak out
                            f.wavetable = voice.wavetable;
                            break;
                        case OpBase.Intents.BITWISE:  //Extends FM_OP
                            var op2 = new BitwiseOperator();
                            ops[i] = op2;
                            op2.eg.Configure(voice.egs[i]);
                            op2.pg = voice.pgs[i];  //ByVal copy
                            op2.wavetable = voice.wavetable;
                            break;
                        case OpBase.Intents.WAVEFOLDER:
                            ops[i] = new WaveFolder();
                            ops[i].eg.Configure(voice.egs[i]);
                            ops[i].wavetable = voice.wavetable;
                            break;
                            
                        default:
                            throw new NotImplementedException("Channel:  Attempting to create a new operator with no/invalid intent...");
                    }
                }
            }
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            string nl = Environment.NewLine;

            byte i=0;
            foreach (OpBase op in ops)
            {
                // if (op==null) continue;
                string rising;
                if (op.egStatus>=0 && (int)op.egStatus < op.eg.rising.Length)
                    rising = op.eg.rising[(int)op.egStatus] ? "Rising" : "Falling";
                else rising= "Doing nothing";
                sb.Append( $"Op{i+1}: {op.egStatus} and {rising} ({op.egAttenuation})"  );
                // sb.Append(String.Format("\nOp{0}: {1}", i+1, ops[i].pg.increment));
                sb.Append(nl);
                i++;
            }
            return sb.ToString();
        }
    }
}
