using System;
using PhaseEngine;


// Phase generator keeps track of tuning information

namespace PhaseEngine 
{
    public struct Increments
    {

        //TODO:  Consider changing this from a struct to a class, and having a nullable field refer to the "next" increment in a chain.
        //       The idea being to define increment sweeps as linear slides from one increment to another, calculated once based on time
        //       when the next note in a chain of increments is met.  This allows non-interactive (but fast) pitch bends, which can be 
        //       separated back into requisite parts if necessary (for example in a tracked note structure).  During editing, moving
        //       the bend would take the relevant increment references and update it.

        public double hz, base_hz;
        double tuned_hz; //Frequency of base_hz * coarse * fine + increment_offset; the tuning without any external modifiers from lfo/controllers

        public long noteIncrement;  // Calculated from the base frequency
        public long tunedIncrement;  //increment of tuned_hz
        public long increment;  // Calculated from the base frequency, multiplier, detune and pitch modifiers.

        //Multiplier values etc
        public bool fixedFreq;
        public float mult,  lfoMult;  //lfoMult is set externally from an LFO object.
        public int coarse, fine, increment_offset;

        //PhaseEngine max detune creates a ringing oscillation once per second at 440hz. Most other implementations' max detune (-3 to +3) is ever-so-slightly slower.
        //A detune value of 1 in other cores is close to 1/6 the value (or 16.667%) of max detune here, or around 6 seconds at 440hz.  Detune in other cores does NOT
        //scale linearly with notes, however.  Since it's an adjustment to fnum and increment lookup tables, detune will never be *perfectly* aligned.
        //Detune at 1760hz seems to be 3s when expected to be closer to 3s at 880hz.  Might implement a scaling factor to offset this at NoteOn based on distance from A440.
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
                    output = Tools.InverseLerp(DETUNE_MIN, 1, _detune);
                return (float)output;
            } set {
                _detune = Tools.Lerp(1, value>0? DETUNE_MAX : DETUNE_MIN, Math.Abs(value));
                detune_current = _detune;  //Also set the current detune, since applying random is not done on recalc
            }
        }        

        const int NOTE_A4=69;

        #if GODOT
            public Godot.Collections.Dictionary GetDictionary()
            {
                var o = new Godot.Collections.Dictionary();

                o.Add("hz", hz);
                o.Add("base_hz", base_hz);
                o.Add("tuned_hz", tuned_hz);
                o.Add("fixedFreq", fixedFreq);

                o.Add("mult", mult);
                o.Add("coarse", coarse);
                o.Add("fine", fine);
                o.Add("detune", Detune);  //NOT the raw value, but the value PhaseEngine UI expects to map the raw value from -1 to 1.
                o.Add("detune_randomness", detune_randomness);
                o.Add("increment_offset", increment_offset);

                return o;
            }
        #endif 

        public static Increments Prototype()
        {
            var o = new Increments();
            o._detune = o.detune_current = 1;
            o.mult = 1;  
            o.lfoMult = 1;  
            o.base_hz=o.tuned_hz=o.hz = Global.BASE_HZ;
            o.increment = o.tunedIncrement = o.noteIncrement = IncOfFreq(o.hz);
            return o;
        }

        //Applies a random detune value based on the current randomness parameter and the max specified detune level.
        public void ApplyDetune()
        { detune_current = Tools.Lerp(_detune, 1.0,  (XorShift64Star.NextDouble()) * detune_randomness); }


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

            tunedIncrement = IncOfFreq(this.tuned_hz) + increment_offset;
            increment=tunedIncrement;

        }

        /// summary:  Returns a partial increment for use with phase-offset NoteOn events
        public static long PhaseOffsetOf(Increments prototype, double percent)
        {
            return (long)((Global.MixRate/prototype.hz) * prototype.increment * Math.Clamp(percent,0,1));
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


        /// summary:  Given a MIDI note value 0-127,  produce an increment appropriate to oscillate at the tone of the note.
        public void NoteSelect(byte n)
        {
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

        // public static double FRatio { get =>  (1<<Tables.SINE_TABLE_BITS) / Global.MixRate; } // The increment of a frequency of 1 at the current mixing rate.
        public static double FRatio { get =>  0x400 / Global.MixRate; } //The increment of a frequency of 1 at the current mixing rate. We assume default table size of 1024.
        private static double IncOfFreqD(double freq)  //Get the increment of a given frequency as a double.
            {return FRatio * freq;}

    }
}
