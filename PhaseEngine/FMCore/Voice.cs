using System;
using PhaseEngine;
using PE_Json;

#if GODOT
using Godot;
#endif

namespace PhaseEngine 
{
    public class Voice
    {
        ////// Metadata for file I/O and use in user implementations
        public string name;
        public float gain=1.0f;
        public float pan=0;


        public byte opCount = 6; 

        //Keeps track of how to set the operator on a new note.
        public byte[] oscType;   //Typically some kind of waveform value.

        //Consider having an array of envelopes for ops to refer to when initializing their voices as the "canonical" voice, and a temporary copy made for alterables.
        public Envelope[] egs;  //Canonical EG data for each operator.
        public Increments[] pgs;

        //Consider making all Channels use references to these vars, and process them properly whenever IO comes in.
        public Algorithm alg = new Algorithm();
        public LFO lfo = new LFO();

        public WaveTableData wavetable;

        public Channel preview;

        //TODO:  Voice description / meta probably goes here too....
        public Voice() {InitVoice(this.opCount);}
        public Voice(byte opCount) {InitVoice(opCount);}

        public Voice(JSONObject data, byte opCount=4)
        {
            InitVoice((byte) data.GetItem("opCount", opCount));
            FromJSON(data);
        }

        void InitVoice(byte opCount)    
        {
            this.opCount = opCount;
             alg = new Algorithm(opCount);
             egs = new Envelope[opCount];
             pgs = new Increments[opCount];
             oscType = new byte[opCount];

            //Chip should pass these down when pulling a channel
             for (int i=0; i<opCount; i++){
                 egs[i] = new Envelope();
                 pgs[i] = Increments.Prototype();
             }
  
            preview = new Channel(opCount);
            preview.SetVoice(this);
  
        }

        public bool SetPresetAlgorithm(byte preset)
        {
            if (opCount >= 6)
            {
                alg = Algorithm.FromPreset(preset, Algorithm.PresetType.DX);
                return true;
            } else if (opCount >= 4)
            {
                alg = Algorithm.FromPreset(preset, Algorithm.PresetType.Reface);
                return true;
            }
            return false;
        }



        // Called whenever the voice needs to change its operator count in the algorithm.
        public void SetOpCount(byte opTarget)
        {
            Array.Resize(ref egs, opTarget);
            Array.Resize(ref pgs, opTarget);
            Array.Resize(ref oscType, opTarget);

            if (opTarget>opCount)
            {
                for (byte i=opCount; i<opTarget; i++)
                {
                    egs[i] = new Envelope();
                    pgs[i] = Increments.Prototype();
                }
            }

            alg.SetOpCount(opTarget);

            opCount = opTarget;
            preview.SetVoice(this);
        }

        //Creates a Chip to generate a preview of what this voice would sound like.
        public float[] CalcPreview(int period=12000, int size=256, bool disableLFO=true )
        {
            // const int NOTE_A4=69;
            var output = new float[size];  var oc=0;
            var stride = (period/(double)size);
            var strideCount = stride;
            var c = preview;  //Reduce memory thrash by using our own Channel instance
            // c.SetVoice(this);
            // c.disableLFO = disableLFO;

            //If the channel contains filters or bitwise funcs, they need to be recalculated.
            for (int i=0; i<opCount; i++)
            {
                switch(alg.intent[i])
                {
                    case OpBase.Intents.BITWISE:
                        var op = c.ops[i] as BitwiseOperator;
                        op.OpFuncType = egs[i].aux_func;  //Property has hidden side effect of setting func
                        break;

                    case OpBase.Intents.FILTER:
                        var f = c.ops[i] as Filter;
                        f.SetOscillatorType(egs[i].aux_func); //Sets the filter func
                        f.Recalc();
                        break;
                }
            }

            c.NoteOn(0, 64);
            for (int i=0; oc<size && i<period; i++)
            {
                if (strideCount<1)  // Hit a point where we need to fill up output
                {
                    strideCount += stride;
                    output[oc] = Tables.short2float[c.RequestSample()+Tables.SIGNED_TO_INDEX];  oc++;
                }
                strideCount--;
                c.Clock();
            }
            return output;
        }


        /// Called from EG controls to bus to the appropriate tuning properties.
        public void SetPG(int opTarget, string property, float val)
        {
            // Increments is a struct, so we need to update the canonical info from the Voice and grab a copy whenever notes turn on. 
            // A consequence of this is that values in Increments (pitch, mainly) can't be adjusted on the fly, only on a new note.
            
            try
            {
                pgs[opTarget].SetVal(property, val);
                // pgs[opTarget].Recalc();  // In Voice we don't need to recalc since note selection occurs at NoteOn only, but it's useful for previewing frequencies...

                // GD.Print(String.Format("Set op{0}.eg.{1} to {2}.", opTarget, property, val));
            } catch(NullReferenceException) {
                #if GODOT
                    GD.PrintErr(String.Format("No property handler for op{0}.pg.{1}.", opTarget, property, val));
                #else
                    System.Diagnostics.Debug.Print(String.Format("No property handler for op{0}.pg.{1}.", opTarget, property, val));
                #endif
            }            
        }
        public void SetEG(int opTarget, string property, float val)
        {
            if (opTarget >= opCount) return;
            var eg = egs[opTarget];
            eg.ChangeValue(property, val);
        }

        internal void ResetIntents(bool toDefault=false)
        {
            for (byte i=0; i<opCount; i++)
                SetIntent(i, toDefault? OpBase.Intents.FM_OP : alg.intent[i]);
        }

        internal void SetIntent(byte opTarget, OpBase.Intents intent)  //Sets up envelopes for a new usage intent to saner defaults.
        {
            //Update the preview and the intent.
            alg.SetIntent(opTarget, intent);

            switch (intent)
            {
                case OpBase.Intents.FM_OP:
                case OpBase.Intents.BITWISE:
                    egs[opTarget].duty = 32767;  //Reset duty cycle to default.
                    egs[opTarget].osc_sync = true;  //Enable oscillator sync to reduce popping.
                    break;

                case OpBase.Intents.WAVEFOLDER:
                    egs[opTarget].duty = 32767;  //Reset bias to default.
                    egs[opTarget].osc_sync = false;  //Disable limiting (clamping).
                    egs[opTarget].aux_func = 0;  //Disable bit crushing.
                    break;
                case OpBase.Intents.FILTER:
                    egs[opTarget].duty = 0;  //Set dry mix to 0.  Default for FM ops is 0x7FFF (50%) and may confuse new users.
                    egs[opTarget].gain = Math.Clamp(egs[opTarget].gain, Filter.GAIN_MIN, Filter.GAIN_MAX);  //Prevent extreme gain from wavefolders
                    break;


            }
            preview.SetIntents(opTarget, (byte)(opTarget + 1));
        }

        /// Sets the canonical waveform to reference when setting an operator's waveFunc on NoteOn.
        public void SetWaveform(int opTarget, int val)
        {   // NOTE:  This does NOT actually set an operator's waveFunc!  This is done in NoteOn when referencing this value from Voice.
            oscType[opTarget] = (byte)val;
        }    


        //TODO:  Front-end IO that de/serializes the wiring grid configuration from an array (user-friendly) to processOrder and connections (code-friendly)
        public void FromJSON(JSONObject data)
        {
            try
            {
                if (data.HasItem("name"))  data.Assign("name", ref name);
                data.Assign("gain", ref gain);
                data.Assign("pan", ref pan);

                alg.FromJSON((JSONObject) data.GetItem("algorithm"));
                SetOpCount(alg.opCount);

                var ops = (JSONArray) data.GetItem("operators");
                for (byte i=0; i<opCount; i++)
                {
                    var op = (JSONObject) ops[i];
                    SetOpFromJSON(i, op);
                }

                lfo.FromJSON((JSONObject) data.GetItem("lfo"));

            } catch (Exception e) {
                System.Diagnostics.Debug.Print("Voice.FromJSON:  Malformed JSON or missing data.. " + e.Data.ToString());
            }
        }

        public string ToJSONString() {return ToJSONObject().ToJSONString();}
        public JSONObject ToJSONObject()
        {
            var o = new JSONObject();

            // Don't add the OpCount here.  Rely on the Algorithm for that.
            o.AddPrim("FORMAT", Global.VERSION);
            if(name!=null) o.AddPrim("name", name);
            o.AddPrim("gain", gain);
            o.AddPrim("pan", pan);
            o.AddItem("algorithm", alg.ToJSONObject() );

            o.AddItem("lfo", lfo.ToJSONObject());

            var ops = new JSONArray();  //Operator array
            for (byte i=0; i<opCount; i++) 
                ops.AddItem(OpToJSON(i, false));

            o.AddItem("operators", ops);

            return o;
        }

        /// Returns a serialized description of a particular operator's envelope and increment values.
        internal JSONObject OpToJSON(byte opNum, bool includeIntent)
        {
            var output = new JSONObject();
            //Intent is mainly for clipboard operations only, as it's stored in the Algorithm. Importing an op should check if the intent exists and fail if incorrect.
            //In the main importer, Voice should call SetIntent() after everything else is set up.
            if (includeIntent) output.AddPrim("intent", alg.intent[opNum]);

            output.AddPrim("oscillator", (Oscillator.oscTypes)oscType[opNum]);
            output.AddItem( "envelope", egs[opNum].ToJSONObject(alg.intent[opNum] != OpBase.Intents.FILTER) );  //Don't include RTables if op is a filter.
            if( ((int)alg.intent[opNum] & 1) == 1) //Only add increments object if the intent is FM_OP or BITWISE (1 or 3)
                output.AddItem("increments", pgs[opNum].ToJSONObject());

            return output;
        }
        internal void SetOpFromJSON(byte idx, JSONObject op)
        {
            if (op.HasItem("increments"))  pgs[idx] = Increments.FromJSON((JSONObject) op.GetItem("increments"));

            var e = (JSONObject) op.GetItem("envelope");
            bool success = egs[idx].FromJSON(e);
            if (!success)
            {
                System.Diagnostics.Debug.Print(String.Format("Voice.FromJSON:  Problem parsing envelope {0}", idx));
                return;
            }
            
            //Try parsing in the osc type from the operator, now that the EG is confirmed good.
            var osc = Oscillator.oscTypes.Sine;
            if (op.Assign("oscillator", ref osc)) oscType[idx] = (byte)osc;
        }


        #if GODOT
            /// Changes the algorithm without changing the opCount.
            public void SetAlgorithm(Godot.Collections.Dictionary d)
            {
                // TODO:  Consider replacing this function with one which converts d to json and just call FromJSON()

                var grid = d["grid"];  //Should be a PoolByteArray, otherwise the below code will throw an exception (no converter interface)
                if(grid !=null)
                    alg.wiringGrid = (byte[]) Convert.ChangeType(grid, typeof(byte[]));

                var order = d["processOrder"] as Godot.Collections.Array;
                var c = d["connections"] as Godot.Collections.Array;

                alg.processOrder = new byte[opCount];
                alg.connections = new byte[opCount];
                var opsToProcess = Math.Min(alg.opCount, order.Count);
                for(int i=0; i<opsToProcess; i++)
                {
                    alg.processOrder[i] = Convert.ToByte(order[i]);
                    alg.connections[i] = Convert.ToByte(c[i]);
                }
            }

            public Godot.Collections.Dictionary GetAlgorithm()
            {
                var d = new Godot.Collections.Dictionary();

                d["opCount"] = opCount;
                d["grid"] = alg.wiringGrid;

                d["processOrder"] = alg.processOrder;
                d["connections"] = alg.connections;

                return d;
            }
            public Godot.Collections.Dictionary GetEG(int opTarget) {return egs[opTarget >= opCount? 0:opTarget].GetDictionary();}
            public Godot.Collections.Dictionary GetPG(int opTarget) {return pgs[opTarget >= opCount? 0:opTarget].GetDictionary();}
        #endif

        /// summary:  Will return True if the operator is directly connected to output, or indirectly via a filter, folder, or through a bypassed operator.
        ///           Used to determine things like how much to scale the preview waveform, check when all ops are quiet, or determine priority score of all generator ops.
        public bool OpReachesOutput(byte src_op)
        {
            if (alg.intent[src_op]!=OpBase.Intents.FILTER && alg.connections[src_op] == 0) return true;

            //Determine if the op is connected to a filter that's connected to output or the child op is bypassed to output..
            var c= alg.connections[src_op];
            for (int child_op=0; c>0; child_op++)
            {
                //Check the child op for connections to output.
                if ( (c&1)==1)
                    if ( alg.connections[child_op]==0 && 
                        (alg.intent[child_op]==OpBase.Intents.FILTER || egs[child_op].bypass==true ) )
                        return true;
                c >>=1;
            }
            return false;
        }


    }

}
