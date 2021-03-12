using System;
using gdsFM;

namespace gdsFM 
{
    public class Operator
    {
        public Operator() {}

        public ulong phase;  //Phase accumulator
        public uint increment;  //Phase incrementor, combined value.
        public ulong noteIncrement;  //Frequency multiplier for note number.


        public float fnum;  //Used to calculate the increment, this comes from the note table of f-numbers

        public double[] feedback = new double[2];  //feedback buffer

        public ushort duty = 32767;

        public Oscillator oscillator = new Oscillator(Oscillator.Sine);

        //thought:  separate into carrier and modulator funcs, carrier output being a float and modulator being double

        public void NoteOn()
        {}
        public void NoteOff()
        {}

        public short RequestSample()
        {
            
            // phase = (ulong)unchecked((long)phase + oscillator.Generate(phase, duty));
            phase += noteIncrement;   //FIXME:  CHANGE TO TOTAL INCREMENT

            increment++;

            return oscillator.Generate(unchecked(phase >> Global.FRAC_PRECISION_BITS), duty);
        }


        public void NoteSelect(byte n)
        {
            const int NOTE_A4=69;
            // noteIncrement = IncOfFreq(440.0 * Math.Pow(2, (n-NOTE_A4)/12.0));
            var whole = IncOfFreq(440.0 * Math.Pow(2, (n-NOTE_A4)/12.0));
            var frac = whole - Math.Truncate(whole);
        
            noteIncrement = (uint)(frac * Global.FRAC_SIZE) | ((uint)(whole) << Global.FRAC_PRECISION_BITS);
            
        }
        public void FreqSelect(double freq)
        {
            // noteIncrement = IncOfFreq(freq);
            var whole = IncOfFreq(freq);
            var frac = whole - Math.Truncate(whole);
        
            noteIncrement = (ulong)(frac * Global.FRAC_SIZE) | ((ulong)(whole) << Global.FRAC_PRECISION_BITS);
            // noteIncrement &= Int32.MaxValue;
        }


        public static double FRatio { get =>  (1<<Tables.SINE_TABLE_BITS) / Global.MixRate; } // The increment of a frequency of 1 at the current mixing rate.
        public static double IncOfFreq(double freq)  //Get the increment of a given frequency.
        {
            return FRatio * freq;
        }


    }

}
