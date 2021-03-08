using System;
using gdsFM;

namespace gdsFM 
{
    public class Operator
    {
        public Operator() {}

        public double phase;  //Phase accumulator
        public uint increment;  //Phase incrementor, combined value.
        public double noteIncrement;  //Frequency multiplier for note number.


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
            phase = (phase + noteIncrement);   //FIXME:  CHANGE TO TOTAL INCREMENT

            increment++;

            return oscillator.Generate(unchecked((ulong) phase), duty);
        }


        public void NoteSelect(byte n)
        {
            const int NOTE_A4=69;
            noteIncrement = IncOfFreq(440.0 * Math.Pow(2, (n-NOTE_A4)/12.0));
            
        }
        public void FreqSelect(double freq)
        {
            noteIncrement = IncOfFreq(freq);
        }


        public static double FRatio { get =>  (1<<Tables.SINE_TABLE_BITS) / Global.MixRate; } // The increment of a frequency of 1 at the current mixing rate.
        public static double IncOfFreq(double freq)  //Get the increment of a given frequency.
        {
            return FRatio * freq;
        }


    }

}
