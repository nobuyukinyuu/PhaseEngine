using System;
using PhaseEngine;
using PE_Json;
using System.Collections.Generic;
using System.Reflection;


// Phase generator keeps track of tuning information

namespace PhaseEngine 
{
    public struct Increments : IBindableDataSrc
    {
        //TODO:  Consider changing this from a struct to a class, and having a nullable field refer to the "next" increment in a chain.
        //       The idea being to define increment sweeps as linear slides from one increment to another, calculated once based on time
        //       when the next note in a chain of increments is met.  This allows non-interactive (but fast) pitch bends, which can be 
        //       separated back into requisite parts if necessary (for example in a tracked note structure).  During editing, moving
        //       the bend would take the relevant increment references and update it.

        public SortedList<string, TrackerEnvelope> BoundEnvelopes {get;set;}

        public double BaseFrequency {get=>base_hz;}
        internal double hz, base_hz;
        internal double tuned_hz; //Frequency of base_hz * coarse * fine + detune; the tuning without any external modifiers from lfo/controllers

        public long noteIncrement;  // Calculated from the base frequency
        public long tunedIncrement;  //increment of tuned_hz
        public long increment;  // Calculated from the base frequency, multiplier, detune and pitch modifiers.

        //Multiplier values etc
        public bool fixedFreq;
        public float mult; internal float lfoMult;  //lfoMult is set externally from an LFO object.
        public int coarse, fine, increment_offset;

#region Detune
        //PhaseEngine max detune creates a ringing oscillation once per second at 440hz. Most other implementations' max detune (-3 to +3) is ever-so-slightly slower.
        //A detune value of 1 in other cores is close to 1/6 the value (or 16.667%) of max detune here, or around 6 seconds at 440hz.  Detune in other cores does NOT
        //scale linearly with notes, however.  Since it's an adjustment to fnum and increment lookup tables, detune will never be *perfectly* aligned.
        //Detune at 1760hz seems to be 3s when expected to be closer to 3s at 880hz.  Might implement a scaling factor to offset this at NoteOn based on distance from A440.

        //NOTE:  According to this issue, the DX7II manual claims the detune range is ±2 cents....
        //https://github.com/asb2m10/dexed/issues/88
        const double DETUNE_MAX = 1 + 1/440.0;
        const double DETUNE_MIN = 1/DETUNE_MAX;

        //Percentage values.  Randomness is calculated by maximum range from detune to zero detune (100%=anywhere from current detune to no detune).
        internal double _detune, detune_current;
        public float detune_randomness;  

        //User-friendly detune value from -1.0 to 1.0.  Use if you want your detune to conform to PhaseEngine's standard.
        public float Detune {
            get {
                //Detune internally is always a positive value.  Negative values fed to Detune are represented as a reciprocal.
                double output;
                if (_detune>=1)
                    output = Tools.InverseLerp(1, DETUNE_MAX, _detune);
                else 
                    output = -Tools.InverseLerp(1, DETUNE_MIN, _detune);
                return (float)output;
            } set {
                _detune = Tools.Lerp(1, value>0? DETUNE_MAX : DETUNE_MIN, Math.Abs(value));
                detune_current = _detune;  //Also set the current detune, since applying random is not done on recalc
            }
        }        
        //Applies a random detune value based on the current randomness parameter and the max specified detune level.
        public void ApplyDetuneRandomness()
        { detune_current = Tools.Lerp(_detune, 1.0,  (XorShift64Star.NextDouble()) * detune_randomness); }
#endregion

#region io / static constructors
        public void Configure(Increments p)  //Sets our increments value to that of a prototype
        {
            hz = p.hz; base_hz = p.base_hz;
            tuned_hz = p.tuned_hz; 

            noteIncrement = p.noteIncrement;  
            tunedIncrement = p.tunedIncrement;
            increment = p.increment;

            fixedFreq = p.fixedFreq;
            mult = p.mult;  lfoMult = p.lfoMult;
            coarse = p.coarse; fine = p.fine;  increment_offset = p.increment_offset;

            Detune = p.Detune;
        }

        public static Increments Prototype()  //Initialize a new Increments
        {
            var o = new Increments();
            o.BoundEnvelopes = new SortedList<string, TrackerEnvelope>();
            o._detune = o.detune_current = 1;
            o.mult = 1;  
            o.lfoMult = 1;  
            o.base_hz=o.tuned_hz=o.hz = Global.BASE_HZ;
            o.increment = o.tunedIncrement = o.noteIncrement = IncOfFreq(o.hz);
            return o;
        }

        public static Increments FromNote(byte note)
        {
            var o = new Increments();
            o.NoteSelect(note);
            o.increment = o.noteIncrement;
            return o;
        }
        public static Increments FromFreq(double freq)
        {
            var o = new Increments();
            o.FreqSelect(freq);
            o.increment = o.noteIncrement;
            o.fixedFreq = true;
            return o;
        }

        public static Increments FromString(string s) 
        { 
            var P = JSONData.ReadJSON(s);
            if (P is JSONDataError)
            {
                System.Diagnostics.Debug.Fail("Increments.FromJSON:  Parsing JSON string failed.");
                return Increments.Prototype();
            } 
            var j = (JSONObject) P;
            return FromJSON(j);
        }
        public static Increments FromJSON(JSONObject j, double chipTicksPerSec=1)
        {
            var o = Increments.Prototype();
            try
            {
                j.Assign("hz", ref o.hz);
                j.Assign("base_hz", ref o.base_hz);
                j.Assign("tuned_hz", ref o.tuned_hz);
                j.Assign("fixedFreq", ref o.fixedFreq);

                j.Assign("mult", ref o.mult);
                j.Assign("coarse", ref o.coarse);
                j.Assign("fine", ref o.fine);

                o.Detune = j.GetItem("detune", o.Detune); //Set up real detune values using the setter
                j.Assign("detune_randomness", ref o.detune_randomness);
                j.Assign("increment_offset", ref o.increment_offset);

                if (j.HasItem("binds"))
                {
                    var bindArray = (JSONArray)j.GetItem("binds");
                    foreach(JSONObject jsonBind in bindArray)
                    {
                        var memberName = jsonBind.GetItem("memberName", "[invalid]");
                        //Get the type of this bind first so we know what to do with it.
                        IBindableData.Abilities bindType = IBindableData.Abilities.None;
                        jsonBind.Assign("type", ref bindType);

                        switch(bindType)
                        {
                            case IBindableData.Abilities.Envelope:  //TrackerEnvelope
                                //First, bind the data member.  Then, configure it from our proto.
                                var success = o.Bind(memberName, chipTicksPerSec);
                                if (!success)
                                {
                                    System.Diagnostics.Debug.Print($"Rebinding to PG Member {memberName} failed!");
                                    continue;
                                }
                                //Finally, reconfigure the fresh bind with our envelope data.
                                o.BoundEnvelopes[memberName].Configure(jsonBind);
                                break;

                            //TODO:  OTHER ABILITIES HERE
                            default:
                                System.Diagnostics.Debug.Print($"PG Member {memberName} has unsupported bind ability {bindType}!");
                                break;
                        }
                    }
                }  //End of deserializing binds

                o.Recalc();
            } catch (Exception e) {
                System.Diagnostics.Debug.Assert(false, "PG Copy failed:  " + e.Message);
            }

            return o;
        }


        internal JSONObject ToJSONObject()
        {
            var o = new JSONObject();

            if (fixedFreq || base_hz!=Global.BASE_HZ) //Fixed frequency or some other kinda custom note.  Record the frequency values.
            {
                // if (hz!=Global.BASE_HZ)  o.AddPrim("hz", hz);
                if (base_hz!=Global.BASE_HZ)  o.AddPrim("base_hz", base_hz);
                // if (tuned_hz!=Global.BASE_HZ)  o.AddPrim("tuned_hz", tuned_hz);
                o.AddPrim("fixedFreq", fixedFreq);
            }

            o.AddPrim("mult", mult);
            o.AddPrim("coarse", coarse);
            o.AddPrim("fine", fine);
            o.AddPrim("detune", Detune);  //NOT the raw value, but the value PhaseEngine UI expects to map the raw value from -1 to 1.
            o.AddPrim("detune_randomness", detune_randomness);
            o.AddPrim("increment_offset", increment_offset);

            //Fetch the BoundEnvelopes 
            var binds = new JSONArray();
            foreach(TrackerEnvelope t in BoundEnvelopes.Values)             //TODO:  Do this foreach for BoundTables.Values once that exists!!!!!
            {
                var e = t.ToJSONObject();
                e.RemoveItem("dataSource");  //Remove the dataSource when serializing out since we can safely assume the data source is us when serializing in or out.
                binds.AddItem(e);
            }
            if (binds.Length > 0)  o.AddItem("binds", binds);

            return o;
       }
        public string ToJSONString() => ToJSONObject().ToJSONString();
#endregion


        // Recalculates the total increment given our current tuning settings.
        public void Recalc()
        {
            // TODO:  once notes can be specified, consider storing separate fixed_hz and removing intermediary tuned_hz, maybe even base_hz --
            //          If we use 2 copies of the envelope state instead (one for temporary changes) in Operator, less instructions are used to recalc.
            //          OTOH, these intermediaries may be necessary to keep track of the LFO state, depending on how and where we adjust the phase....
            //          Channel-wide/Patch-wide LFO would track phase offset separately and add them in every requested sample.

            //      More LFO thoughts.... Pull full LFO cycle from the pg increment >> some amount based on PMS, then map the offset on a clock from
            //      ± the PMS output range.  Add this lfo value to the phase.  Consider creating a PMS ratio table to avoid divisions (pre-divide)
            //      if Rsh doesn't work to cull pitch to reasonable ranges.


            if (fixedFreq)
            {
                this.hz=this.tuned_hz=base_hz;
            } else {
                var transpose = Math.Clamp((coarse*100) + fine, -1299, 1299);
                
                var t = Tables.transpose[Tools.Abs(transpose)];  
                if (transpose < 0)  t = 1/t;  //Negative transpose values are equal to the reciprocal of the positive transpose ratio
                this.hz=this.tuned_hz = base_hz * mult * lfoMult * t * detune_current; 
            }

            tunedIncrement = IncOfFreq(this.tuned_hz);
            increment = tunedIncrement + increment_offset;

        }

        /// summary:  Returns a partial increment for use with phase-offset NoteOn events
        public static long PhaseOffsetOf(in Increments prototype, double percent)
        {//TODO: Profile the performance gain/penalty by flagging prototype as an IN parameter. Defensive copies may be used regardless of pass byRef due to mutability
            return (long)((Global.MixRate/prototype.hz) * prototype.increment * Math.Clamp(percent,0,1));
        }


        /// summary:  Given a MIDI note value 0-127,  produce an increment appropriate to oscillate at the tone of the note.
        public void NoteSelect(byte n)
        {
            const int NOTE_A4=69;

            base_hz = Global.BASE_HZ * Math.Pow(2, (n-NOTE_A4)/12.0);
            hz = base_hz;
            var whole = IncOfFreqD(hz);
            var frac = whole - Math.Truncate(whole);
        
            noteIncrement = (uint)(frac * Global.FRAC_SIZE) | ((uint)(whole) << Global.FRAC_PRECISION_BITS);            
        }
        /// summary:  Given a hz rate, produce an increment appropriate to tune the oscillator to this rate.
        public void FreqSelect(double freq)
        {
            base_hz = hz = freq;
            noteIncrement = IncOfFreq(freq);
        }

        /// Get the increment of a given frequency.
        public static long IncOfFreq(double freq)
        {
            var whole = IncOfFreqD(freq);
            var frac = whole - Math.Truncate(whole);        
            return (long)(frac * Global.FRAC_SIZE) | ((long)(whole) << Global.FRAC_PRECISION_BITS);
        }

        //The increment of a frequency of 1 at the current mixing rate. We assume default table size of 1024 as this is the sine table size once mirrored to a full period.
        // THE ABOVE LINE OF COMMENT IS DEPRECIATED:  FIXME -- REMOVE IT
        //The increment of a frequency of 1 at the current mixing rate. The value of 65536 is enough to accomodate a full cycle of an oscillator table at 16bit precision.
        public static double FRatio { get =>  UNIT_SIZE / Global.MixRate; set => f_ratio = UNIT_SIZE / value;}
        private static double f_ratio = FRatio; //Cached value.  Setting MixRate should change this by calling the above setter.
        public const int UNIT_SIZE = (1<<UNIT_BIT_WIDTH) +1;
        public const int UNIT_BIT_WIDTH = 16;  //Width of an incremental unit, in bits

        private static double IncOfFreqD(double freq)  //Get the increment of a given frequency as a double.
            {return f_ratio * freq;}


/////////////////////////////////////////  BINDABLE INTERFACE  /////////////////////////////////////////
        public bool Bind(string property, double chipTicksPerSec=1)
        {
            float min=0,max=0;
            double clockCount = (chipTicksPerSec/120);  //Default tickrate,  120 ticks per sec
            void SetTicks(double x) => clockCount = chipTicksPerSec / x; //Set clock counter to a multiple of our max ticks/sec
            var val = this.GetVal(property);
            //Set range values here.  wavetable_bank can't know max banks for a voice from here, so use Voice's bind method to specify it instead?
            switch(property)
            {
                // case "feedback":  case "ams":
                //     SetTicks(24); max=10;  break;

                case "mult":
                    min=0.25f; max=16.0f;  break;
                case "coarse":
                    SetTicks(30); min=-12; max=12;  //break;
                    return ((IBindableDataSrc)this).Bind(property, (int)min, (int)max, (int)val, (int)clockCount); 
                case "fine":
                    SetTicks(60); min=-100; max=100;  //break;
                    return ((IBindableDataSrc)this).Bind(property, (int)min, (int)max, (int)val, (int)clockCount); 

                case "Detune":
                    min=-1.0f; max=1.0f;  break;

                default:
                    throw new KeyNotFoundException($"Increments.Bind:  Unsupported data member {property}!");
            }
            // return ((IBindableDataSrc)this).Bind(property, min, max, (int)val);  //Call the default bind implementation to handle the rest.
            return ((IBindableDataSrc)this).Bind(property, min, max, (float)val, (int)clockCount);  //Call the default bind implementation to handle the rest.
        }



    }
}
