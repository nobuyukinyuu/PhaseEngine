using System;
using gdsFM;


// Phase generator keeps track of tuning information

namespace gdsFM 
{
    public struct Increments
    {

        public double hz, base_hz;
        double tuned_hz; //Frequency of base_hz * coarse * fine + detune; the tuning without any external modifiers from lfo/controllers

        public long noteIncrement;  // Calculated from the base frequency
        public long tunedIncrement;  //increment of tuned_hz
        public long increment;  // Calculated from the base frequency, multiplier, detune and pitch modifiers.


        public bool fixedFreq;
        public float mult;
        public int coarse, fine, detune;

        public static Increments Prototype()
        {
            var o = new Increments();
            o.mult = 1;  
            o.base_hz=o.hz=Global.BASE_HZ;
            o.increment = o.tunedIncrement = o.noteIncrement = IncOfFreq(o.hz);
            // o.fixedFreq = true;
            return o;
        }

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
                this.hz=this.tuned_hz = base_hz * mult * t; //Add any modifiers to the frequency here if necessary and split into 2 lines
            }

            tunedIncrement = IncOfFreq(this.tuned_hz) + detune;
            increment=tunedIncrement;

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
            return o;
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

        // public static double FRatio { get =>  (1<<Tables.SINE_TABLE_BITS) / Global.MixRate; } // The increment of a frequency of 1 at the current mixing rate.
        public static double FRatio { get =>  0x400 / Global.MixRate; } //The increment of a frequency of 1 at the current mixing rate. We assume default table size of 1024.
        private static double IncOfFreqD(double freq)  //Get the increment of a given frequency as a double.
            {return FRatio * freq;}

    }
}
