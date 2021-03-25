using System;
using gdsFM;

namespace gdsFM 
{
    public class Operator
    {
        public ulong phase;  //Phase accumulator
        public uint increment;  //Phase incrementor, combined value.
        public ulong noteIncrement;  //Frequency multiplier for note number.

        public double[] feedback = new double[2];  //feedback buffer

        public ushort duty = 32767;

        public Oscillator oscillator = new Oscillator(Oscillator.Sine);

        public delegate short sampleOutputFunc(); //Primary function of the operator
        public sampleOutputFunc operatorOutputSample;

        //thought:  separate into carrier and modulator funcs, carrier output being a float and modulator being double

        public Operator(){ operatorOutputSample=OperatorType_ComputeLogOuput; }

        public void NoteOn()
        {}
        public void NoteOff()
        {}

        public void Clock()
        {
            // phase = (ulong)unchecked((long)phase + oscillator.Generate(phase, duty));
            phase += noteIncrement;   //FIXME:  CHANGE TO TOTAL INCREMENT
            increment++;
        }

        public short RequestSample()
        {
            return operatorOutputSample();
            // return oscillator.Generate(unchecked(phase >> Global.FRAC_PRECISION_BITS), duty, ref flip);
        }


        //Sets up the operator to act as an oscillator for FM output.
        public void SetOperatorType(Oscillator.waveFunc waveFunc)
        {
            oscillator.SetWaveform(waveFunc);
            switch(waveFunc.Method.Name)
            {
                case "Brown":
                case "White":
                case "Pink":
                {
                    //Set the operator's sample output function to work in the linear domain.
                    operatorOutputSample = OperatorType_Noise;
                    return;
                }
            }

            operatorOutputSample = OperatorType_ComputeLogOuput;
        }  //TODO:  Operator types for filters and sample-based outputs

        //Operator output type for FM 
        public short OperatorType_ComputeLogOuput()
        {
            return compute_volume(0,0);
        }

        public short OperatorType_Noise()
        {
            return unchecked((short) oscillator.Generate(phase, duty, ref flip));
        }


        public float LinearVolume(short samp)
        {
            var output = Tables.linVol[samp + Tables.SIGNED_TO_INDEX];
            // if (Tools.BIT(phase >> Global.FRAC_PRECISION_BITS, Tables.SINE_HALFWAY_BIT).ToBool())   output = -output;
            // if (Tools.BIT(samp, 14).ToBool())   output = -output;
            return output;
        }


        bool flip=false;
        public short compute_volume(ushort modulation, ushort am_offset)
        {
            // start with the upper 10 bits of the phase value plus modulation
            // the low 10 bits of this result represents a full 2*PI period over
            // the full sin wave
            ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);

            // get the absolute value of the sin, as attenuation, as a 4.8 fixed point value
            // ushort sin_attenuation = Tables.abs_sin_attenuation(phase);
            ushort sin_attenuation = oscillator.Generate(phase, duty, ref flip);

            // get the attenuation from the evelope generator as a 4.6 value, shifted up to 4.8
            // ushort env_attenuation = Test2.envelope_attenuation(am_offset) << 2;
            ushort env_attenuation = 0;

            // combine into a 5.8 value, then convert from attenuation to 13-bit linear volume
            short result = (short) Tables.attenuation_to_volume((ushort)(sin_attenuation + env_attenuation));

            // negate if in the negative part of the sin wave (sign bit gives 14 bits)
            return flip ? (short)-result : result;
            // return Tools.BIT(phase, 9).ToBool() ? (short)-result : result;
        }

        public void NoteSelect(byte n)
        {
            const int NOTE_A4=69;
            // noteIncrement = IncOfFreq(440.0 * Math.Pow(2, (n-NOTE_A4)/12.0));
            var whole = IncOfFreq(Global.BASE_HZ * Math.Pow(2, (n-NOTE_A4)/12.0));
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
