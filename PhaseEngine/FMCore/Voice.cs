using System;
using PhaseEngine;
using PE_Json;
using System.Diagnostics;


#if GODOT
using Godot;
#endif

namespace PhaseEngine 
{
    public class Voice : IJSONSerializable
    {
        internal float BindManagerTicksPerSec {get=> Global.MixRate/bindManagerMaxClocks; set{bindManagerMaxClocks=(ushort)(Global.MixRate/value); ResetPreview();}}
        ushort bindManagerMaxClocks=1;  //Used to synchronize preview with chip if we want to update binds more or less times per sec

        ////// Metadata for file I/O and use in user implementations
        public string name, desc;


        float gain=1.0f;
        float pan=0; const float C_PAN=0.5f;
        internal float panL=0.5f, panR=0.5f; 

        public float Gain {get=>gain;set=>gain=value;}
        public float Pan {get=>pan;set=>SetPanning(value);}

        public void SetPanning(float val)
        {
            pan=val;
            var amt = Math.Abs(val);
            float l,r;

            if (val < 0) {  //Pan left channel
                l = amt;
                r = 1.0f-amt;

                panL = Tools.Lerp(C_PAN, 1.0f, l);
                panR = Tools.Lerp(0.0f, C_PAN, r);
                return;

            } else if (val > 0) {  //Pan right
                l = 1.0f-amt;
                r = amt;

                panL = Tools.Lerp(0.0f, C_PAN, l);
                panR = Tools.Lerp(C_PAN, 1.0f, r);
                return;    
            } else {  //Center channel.
                panL = C_PAN;
                panR = C_PAN;
                return;
            }
        }

        public byte opCount = 6; 

        //Keeps track of how to set the operator on a new note.
        public byte[] oscType;   //Typically some kind of waveform value.

        //Consider having an array of envelopes for ops to refer to when initializing their voices as the "canonical" voice, and a temporary copy made for alterables.
        public Envelope[] egs;  //Canonical EG data for each operator.
        public Increments[] pgs;

        //Consider making all Channels use references to these vars, and process them properly whenever IO comes in.
        public Algorithm alg = new Algorithm();
        public LFO lfo = new LFO();

        public WaveTableData wavetable = new WaveTableData();

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
  
            preview = new Channel(opCount, bindManagerMaxClocks);
            preview.SetVoice(this);
            lfo.wavetable = this.wavetable;
        }

        void ResetPreview() //Used to create a new preview channel at a different tick rate
        {
            preview = new Channel(opCount, bindManagerMaxClocks);
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
            var output = new float[size*3];  var oc=0; //Output min/max values in the upper thirds of the array.

            //Determine if any attacks to output are slow. Increase the period until we have a visual of sound.
            int speed = 0;
            for(byte i=0;  i<opCount;  i++)
                {
                    if(alg.connections[i] != 0) continue;
                    if(egs[i].ar > speed) speed = egs[i].ar;
                }
            if (speed<2) return new float[size]; //Skip infinite output and output that takes a long time to process
            else if (speed < 24)
            {
                speed = 1 + (Envelope.R_MAX >> 1) - speed;  //Speed is now transformed into a multiplier.
                period = (int)(period*speed/3.2f);
            }

            var stride = (period/(double)size);
            var strideCount = stride;
            // var preview = this.preview;  //Reduce memory thrash by using our own Channel instance

            //If the channel contains filters or bitwise funcs, they need to be recalculated.
            for (int i=0; i<opCount; i++)
            {
                switch(alg.intent[i])
                {
                    case OpBase.Intents.BITWISE:
                        var op = preview.ops[i] as BitwiseOperator;
                        op.OpFuncType = (byte)egs[i].aux_func;  //Property has hidden side effect of setting func
                        break;
                }
            }


            preview.NoteOn(0, 64);  //preview.NoteOn
            if (disableLFO)   //Mess with the operators in the preview channel to not be AMS sensitive
                for (int i=0; i<preview.ops.Length; i++)   preview.ops[i].eg.ams = 0;

            int bindTicks=0;
            for (int i=0; oc<size && i<period; i++)  //Don't exceed our preview period and keep going until all output checks are completed
            {
                //Check binds for necessary updates
                bindTicks++;
                if (bindTicks >= bindManagerMaxClocks)
                {
                    bindTicks = 0;
                      for(byte op=0;  op<opCount; op++)
                      {
                        BindManager.Update(preview.ops[op], preview.ops[op].eg);
                        var updated = BindManager.Update(preview.ops[op], ref preview.ops[op].pg);
                        if (updated) preview.ops[op].pg.Recalc();
                      }
                }

                //Assign minmaxes (Y range for the given pixel slice)
                var sample = Tables.short2float[preview.RequestSample() + Tables.SIGNED_TO_INDEX];
                output[oc+size] = Math.Min(sample, output[oc+size]);
                output[oc+(size<<1)] = Math.Max(sample, output[oc+(size<<1)]);

                if (strideCount<1)  // Hit a point where we need to fill up output
                {
                    strideCount += stride;
                    // output[oc] = Tables.short2float[preview.RequestSample() + Tables.SIGNED_TO_INDEX];
                    output[oc] = sample;
        
                    oc++;

                    if (oc<size) //Prime the next samples' minmaxes.
                    {
                        output[oc+size] = sample;
                        output[oc+(size<<1)] = sample;
                    }
                }
                strideCount--;

                preview.Clock();
            }
            preview.NoteOff();

            return output;
        }

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
                    GD.PrintErr($"No property handler for op{opTarget}.pg.{property}.");
                #else
                    System.Diagnostics.Debug.Print($"No property handler for op{opTarget}.pg.{property}.");
                #endif
            }            
        }
        public void SetEG(int opTarget, string property, float val)
        {
            if (opTarget >= opCount) return;
            var eg = egs[opTarget];
            eg.ChangeValue(property, val);
        }

        //Called from EG controls on an HQ Operator to indicate there is auxiliary rate envelope decimal data to set.
        public void SetRateExtension(int opTarget, string property, float val) 
        {
        if (opTarget >= opCount) return;
        if (alg.intent[opTarget] != OpBase.Intents.FM_HQ) {
            System.Diagnostics.Debug.Print(
                $"Attempt to set rate extensions on operator {opTarget}, which has intent {alg.intent[opTarget].ToString()}. (Expecting FM_HQ)");
            return;
        }
        var eg = egs[opTarget];

        // First, get the fractional component as a value from 0-255.
        var whole = Math.Abs(Math.Truncate(val));
        var frac = (byte)((Math.Abs(val) - whole) * 256);
        // Next, determine which part to knock out and replace.
        var env =  property switch {
            "ar" => 0,
            "dr" => 1,
            "sr" => 2,
            "rr" => 3,
            _    => 0
        };
        
        int mask = ~(255 << (env*8));  //All 1s except the knockout part.
        var frac_bits = frac << (env*8); //The part to apply after masking out the knockout bits.
        eg.aux_func = (eg.aux_func & mask) | frac_bits;  //32 bits HQ_OP decimal extension data, 8 bits for each: AR|DR|SR|RR
        eg.rates[env] = (byte)whole;
    }
    public float GetRateExtension(int opTarget, string property)
    {
        var env =  property switch {
            "ar" => 0,
            "dr" => 1,
            "sr" => 2,
            "rr" => 3,
            _    => throw new ArgumentException($"GetRateExtension: Property must be one of either 'ar', 'dr', 'sr', or 'rr'. Got {property}"),
        };
        
        var eg=egs[opTarget];
        var frac_bits = (eg.aux_func >> (env*8)) & 255;
        return eg.rates[env] + frac_bits/255f;
    }

        internal void ResetIntents(bool toDefault=false)
        {
            for (byte i=0; i<opCount; i++)
                SetIntent(i, toDefault? OpBase.Intents.FM_OP : alg.intent[i]);
        }

        internal void SetIntent(byte opTarget, OpBase.Intents intent)  //Sets up envelopes for a new usage intent to saner defaults.
        {
            //Update the preview and the intent.
            var oldIntent = alg.intent[opTarget];
            alg.SetIntent(opTarget, intent);

            if (oldIntent == OpBase.Intents.FM_HQ && intent != OpBase.Intents.FM_HQ)
            {   //Old intent was HQ, shrink down FB levels.
                egs[opTarget].feedback = (byte)Math.Round(egs[opTarget].feedback/25.5);
            }
            else if (intent == OpBase.Intents.FM_HQ && oldIntent != OpBase.Intents.FM_HQ)
            {   //Increase the feedback and AMS levels to extended values
                egs[opTarget].feedback = (byte)Math.Min(Math.Round(egs[opTarget].feedback*25.5), 255);
                // egs[opTarget].ams = Math.Clamp(egs[opTarget].ams, (byte)0, (byte)10);
            }

            switch (intent)
            {
                case OpBase.Intents.FM_OP:
                case OpBase.Intents.FM_HQ:
                case OpBase.Intents.BITWISE:
                    if (oldIntent==OpBase.Intents.WAVEFOLDER || oldIntent==OpBase.Intents.FILTER)
                        egs[opTarget].duty = 32767;  //Reset duty cycle to default.
                    egs[opTarget].osc_sync = true;  //Enable oscillator sync to reduce popping.

                    if (intent==OpBase.Intents.FM_HQ) egs[opTarget].aux_func = 0; //We use aux_func in FM_HQ to add fidelity to the rate envelopes.
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
        public void SetOscillator(int opTarget, int val)
        {   // NOTE:  This does NOT actually set an operator's waveFunc!  This is done in NoteOn when referencing this value from Voice.
            oscType[opTarget] = (byte)val;
        }    


        //////////////////////////////////////////////////  IO  //////////////////////////////////////////////////
        public bool FromJSON(JSONObject data)
        {
            try
            {
                if (data.HasItem("name"))  data.Assign("name", ref name);
                if (data.HasItem("desc"))  data.Assign("desc", ref desc);
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

                if (data.HasItem("wavetable"))
                {
                    var wavedata = new WaveTableData();
                    if (wavedata.FromJSON((JSONObject) data.GetItem("wavetable")))  //If parsing fails, we don't wanna clobber our wavetable...
                        wavetable = wavedata;
                        lfo.wavetable = wavetable;
                }

            } catch (Exception e) {
                System.Diagnostics.Debug.Print("Voice.FromJSON:  Malformed JSON or missing data.. " + e.Data.ToString());
                return false;
            }

            return true;
        }

        public string ToJSONString() => ToJSONObject().ToJSONString();
        public JSONObject ToJSONObject()
        {
            var o = new JSONObject();

            // Don't add the OpCount here.  Rely on the Algorithm for that.
            o.AddPrim("FORMAT", Global.FORMAT_VERSION);
            if(name!=null && name!=String.Empty) o.AddPrim("name", name);
            if(desc!=null && desc!=String.Empty) o.AddPrim("desc", desc);
            o.AddPrim("gain", gain);
            o.AddPrim("pan", pan);
            o.AddItem("algorithm", alg.ToJSONObject() );

            o.AddItem("lfo", lfo.ToJSONObject());

            if (wavetable!=null && !wavetable.NotInUse)
                o.AddItem("wavetable", wavetable.ToJSONObject() );

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

            //Only add increments object if the intent is FM_OP, FM_HQ, BITWISE, or the operator utilizes adjustments to the mult ratio.
            switch(alg.intent[opNum]){
                case OpBase.Intents.FM_OP:
                case OpBase.Intents.FM_HQ:
                case OpBase.Intents.BITWISE:
                    output.AddItem("increments", pgs[opNum].ToJSONObject());
                    break;
            }

            return output;
        }
        internal void SetOpFromJSON(byte idx, JSONObject op)
        {
            if (op.HasItem("increments"))  pgs[idx] = Increments.FromJSON((JSONObject) op.GetItem("increments"), BindManagerTicksPerSec);

            var e = (JSONObject) op.GetItem("envelope");
            bool success = egs[idx].FromJSON(e, true, BindManagerTicksPerSec);
            if (!success)
            {
                System.Diagnostics.Debug.Print( $"Voice.FromJSON:  Problem parsing envelope {idx}" );
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
            // public Godot.Collections.Dictionary GetEG(int opTarget) {return egs[opTarget >= opCount? 0:opTarget].GetDictionary();}
            // public Godot.Collections.Dictionary GetPG(int opTarget) {return pgs[opTarget >= opCount? 0:opTarget].GetDictionary();}
        #endif


    }

}
