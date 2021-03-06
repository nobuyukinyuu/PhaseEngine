using System;
using PhaseEngine;

namespace PhaseEngine 
{
    public class Oscillator
    {
        public delegate ushort waveFunc(ulong n, ushort duty, ref bool flip, TypedReference auxdata);
        waveFunc wf = Sine;
        short[] customWaveform = new short[128];

        public static readonly waveFunc[] waveFuncs = {Sine2, Tri, Saw, Pulse, White, Pink, Brown, Noise1, Noise2, Wave};
        public enum oscTypes {Sine, Triangle, Saw, Pulse, White, Pink, Brown, Noise1, Noise2, Wave};

        public Oscillator(waveFunc wave)    {wf=wave;}
        // public void SetWaveform(waveFunc wave) {wf=wave;}
        oscTypes _current = oscTypes.Sine;
        public oscTypes CurrentWaveform {get =>_current; set {_current = (oscTypes)value; wf=waveFuncs[(byte)value]; }}


        public ushort Generate(ulong n, ushort duty, ref bool flip, TypedReference auxdata)
        {
            return wf(n, duty, ref flip, auxdata);
        }


        //We'd want to apply these impulses from the blep over a scaled -1.0-1.0 sample. We sample every time there's a discontinuity in the oscillator waveform.
        //For pulses, that's once at phase (0) and again at phase (0.5), which we need to translate later from n (ushort of n<<6 is 0-65535).
        static double PolyBLEP(double t, double dt)  //t=phase;  dt=increment
        {
            // double t = phase * 0.0000152587890625;  // 1.0/65536.0
            // double dt = increment * 0.0000152587890625;
            // 0 <= t < 1
            if (t < dt) {
                t /= dt;
                return t+t - t*t - 1.0;
            }
            // -1 < t < 0
            else if (t > 0xFFFF - dt) {
                t = (t - 0xFFFF) / dt;
                return t*t + t+t + 1.0;
            }
            // 0 otherwise
            else return 0.0;
        }

        // // Branchless -- but tested, takes 2x longer than the original!
        // public static float polyBlep2(float t, float dt)
        // {
        //     float s = Math.Sign(t-0.5);
        //     t = Math.Min((0.5f-s*(t-0.5f))/dt, 1.0f);    
        //     return s*(t*t - 2.0f*t + 1.0f);
        // }


        public static ushort Wave(ulong n, ushort duty, ref bool flip, TypedReference auxdata)
        {
            //TODO:  Change MASK to Tools.Ctz on auxdata2 to get the number of bits to shift by instead to support variable table sizes..
            var auxdata2 = __refvalue(auxdata, short[]);
            // const ushort MASK = WaveTableData.TBL_SIZE -1;
            // const byte BITS = 10 - WaveTableData.TBL_BITS;
            ushort MASK = (ushort) (auxdata2.Length -1);
            byte BITS = (byte) (10 - Tools.Ctz(auxdata2.Length));

            ushort phase = (ushort) unchecked((n>>BITS));  //Scale result to always be the same octave as other oscillators
            var volume = auxdata2[phase & MASK];
            volume |= 1;  //Chop a bit off the end; this is done to stop an overflow if the value is MinValue
            var attenuation = Tables.vol2attenuation[Tools.Abs(volume) >> 2]; //Convert sample to 14-bit and get attenuation.

            flip = volume < 0;
            return attenuation;  //0x7F for tables of 128, or 0xFF for 256 if upped later....
        }

        public static ushort Wave2(ulong n, ref bool flip, short[] auxdata2)
        {
            ushort MASK = (ushort) (auxdata2.Length -1);
            byte BITS = (byte) (10 - Tools.Ctz(auxdata2.Length));

            ushort phase = (ushort) unchecked((n>>BITS));  //Scale result to always be the same octave as other oscillators
            var volume = auxdata2[phase & MASK];
            volume |= 1;  //Chop a bit off the end; this is done to stop an overflow if the value is MinValue
            var attenuation = Tables.vol2attenuation[Tools.Abs(volume) >> 2]; //Convert sample to 14-bit and get attenuation.

            flip = volume < 0;
            return attenuation;  //0x7F for tables of 128, or 0xFF for 256 if upped later....
        }

        public static ushort Pulse(ulong n, ushort duty, ref bool flip, TypedReference auxdata)
        {
            var auxdata2 = __refvalue(auxdata, long);
            ushort phase = (ushort) unchecked((n<<6));
            var inc = (auxdata2 >> Global.FRAC_PRECISION_BITS) << 6;
            var output = phase >= duty?  -1.0:1.0;
            var bump1=(PolyBLEP(phase, inc&65535));
            var bump2=(PolyBLEP((phase - duty)&65535, inc&65535));

            flip = (output < 0.0);

            output += bump1;
            output -= bump2;

            // output = Tables.vol2attenuation[(int)Math.Abs(output*8191)];
            return Tables.vol2attenuation[(int)Math.Abs(output*8191)];
        }

        //Old, naive Pulse oscillator, using inferior antialiasing to blep
        public static ushort Pulse2(ulong n, ushort duty, ref bool flip, TypedReference auxdata)
        {
            const float scale_pt = 800; //frequency at which the pulse starts sloping off to reduce aliasing
            const float scale_range = scale_pt*4;
            // const float slope = 0.85f;  //Shape of the final waveform after exceeding the interpolation range.  0:  Pure square,  1:  Pure Sine
            const float slope = 0.786f;  //Shape of the final waveform after exceeding the interpolation range.  0:  Pure square,  1:  Pure Sine

            var auxdata2 = __refvalue(auxdata, double);  //Expecting pg.hz (double)

            if (auxdata2 <= scale_pt)
            {
                ushort phase = (ushort) unchecked((n<<6));
                flip = phase >= duty;
                return 0;
            } else if (auxdata2 > scale_range) {
                return (ushort) (Sine2(n,duty, ref flip, auxdata));
            } else {
                var s = Sine2(n,duty, ref flip, auxdata);  //Flip is performed in this func
                return (ushort) Tools.Lerp(0, s*slope, Math.Min( (auxdata2 - scale_pt) / (scale_range), 1));
            }
        }

        public static ushort Sine(ulong input, ushort duty, ref bool flip, TypedReference auxdata)
        {
            // return (short)Tables.sin[unchecked((ushort)n & Tables.SINE_TABLE_MASK)];

            //Don't return attenuation to volume, do that in the compute_volume step of the operator.  We need log scale output to modulate cheap
            // the values here are stored as 4.8 logarithmic values for 1/4 phase
            // this matches the internal format of the OPN chip, extracted from the die
            flip = Tools.BIT(input, 9).ToBool(); //Bit 9 represents which half of the -cycle- we're in

            // if the top bit is set, we're in the second half of the curve
            // which is a mirror image, so invert the index
            if ( Tools.BIT(input, 8).ToBool() )
                input = (ushort) ~input;


            // return the value from the table
            return Tables.s_sin_table[input & 0xff];
        }

        //Sine wave that can be shaped by the duty cycle.  Log domain
        public static ushort Sine2(ulong input, ushort duty, ref bool flip, TypedReference auxdata)
        {
          unchecked {
            var phase = (ushort)(input<<6);

            flip = phase > duty; 
            if (flip)  //Adjust the duty cycle and phase accordingly
            {
                duty = (ushort)(ushort.MaxValue - duty);
                phase += duty;
            }
                input = (ulong)( (ushort)(phase*Tables.dutyRatio[duty]) >> 7);  

                if ( Tools.BIT(input, 8).ToBool() )
                    input = (ushort) ~input;
                return Tables.s_sin_table[input & 0xff];
          }
        }

        //Debug;  linear domain sine used to test duty cycle shaping algorithms
        public static ushort Sine3(ulong input, ushort duty, ref bool flip, TypedReference auxdata)
        {
            ushort phase = (ushort) unchecked((input<<6));
            double phaseMult=1;
            if (phase < duty) //First half
            {
                //Multiply phase by how squished the first half of the sine should be compared to normal (short.MaxValue)
                phaseMult = unchecked(duty/(float)short.MaxValue) ;
            } else {
                phaseMult = unchecked(-(ushort.MaxValue-duty)/(float)short.MaxValue);  //Negate the phase multiplier so the next line works
                phase = (ushort) unchecked(phase - duty);  //Offset the phase so that it's aligned to the end of the waveform.
            }
            var output = (ushort) (Math.Sin(phase / (double)ushort.MaxValue / phaseMult * Tables.TAU ) * short.MaxValue/4);
            return output;  //This should be TL_MAX in the log context


            // // More accurate but less efficient code below

            // var angle = (duty / (double)ushort.MaxValue) * 2 - 1 ;
            // var phase = ( (input <<6) / (double)ushort.MaxValue) * Tables.TAU + (Math.PI/2);

            // var output = (angle + Math.Cos(phase)) / Math.Sqrt(  Math.Pow(angle + Math.Cos(phase), 2) + Math.Pow(Math.Sin(phase),2) );

            // // var square = output>=0?  1: -1;
            // // output = Tools.Lerp(output, square, 0.5);

            // return unchecked( (ushort)(output * short.MaxValue/4) );
        }
                


        //Uses arbitrary (default 10) bit lookup table to produce higher quality waveforms.  Disabled by default.
        //May need to be paired with a deeper exp table to work properly!!  This would mean it might need a separate operator mode...
        public static ushort SineHQ(ulong input, ushort duty, ref bool flip, TypedReference auxdata)
        {
            input <<= Tables.SINE_RATIO;
            flip = Tools.BIT(input, Tables.SINE_SIGN_BIT).ToBool();

            // if the top bit is set, we're in the second half of the curve
            // which is a mirror image, so invert the index
            if ( Tools.BIT(input, Tables.SINE_HIGH_BIT).ToBool() )
                input = (ushort) ~input;


            // return the value from the table
            return Tables.sin[(input) & Tables.SINE_TABLE_MASK];
        }

        //Depreciated in favor of Sine2, which can create absine waveforms when the duty cycle is at min/max
        public static ushort Absine(ulong input, ushort duty, ref bool flip, TypedReference auxdata)
        {
            input = (ulong) (input >> 1);
            if ( Tools.BIT(input, 8).ToBool() )
                input = (ushort) ~input;


            // return the value from the table
            return Tables.s_sin_table[input & 0xff];
        }


        public static ushort Saw(ulong n, ushort duty, ref bool flip, TypedReference auxdata)
        {
            var auxdata2 = __refvalue(auxdata, long);
            ushort phase = (ushort) unchecked((n<<6));
            var inc = (auxdata2 >> Global.FRAC_PRECISION_BITS) << 6;
            var output = phase / 32768.0 - 1.0;
            var bump1=(PolyBLEP(phase, inc&65535));

            flip= (output < 0);
            output -= bump1;

            return Tables.vol2attenuation[(int)Math.Abs(output*8191)];
        }

        public static ushort Saw2(ulong n, ushort duty, ref bool flip, TypedReference auxdata)  //Older, non-antialiased saw.
        {
            flip=false;
            if ( Tools.BIT(n, 9).ToBool() )
            {
                flip=true;
                n = (ushort) ~n;
            }
          var output = Tables.saw[(n>>2) & 0xFF];
            // short output = unchecked((short)(n<<1));
            return (ushort)output;
        }

        public static ushort Tri(ulong n, ushort duty, ref bool flip, TypedReference auxdata)
        {
            flip = Tools.BIT(n, 9).ToBool();

            if ( Tools.BIT(n, 8).ToBool() )
            {
                n = (ushort) ~n;
            }

            return Tables.tri[unchecked(n>>4 & Tables.TRI_TABLE_MASK)];
            // return Tables.logVol[Tables.tri[unchecked(n>>10 & Tables.TRI_TABLE_MASK)] + Tables.SIGNED_TO_INDEX];
        }


        //White noise generator, but uses duty cycle to oscillate between on and off state
        // static ushort seed=1;
        public static ushort White(ulong n, ushort duty, ref bool flip, TypedReference auxdata)
        {
            unchecked
            {
                var phase = unchecked((ushort) n);
                var seed = __refvalue(auxdata, int);

                if (phase > duty) return 0;  // Comment this out if you don't want the "buzzing" duty behavior and instead would rather use the counter

                {
                    seed ^= (ushort)(seed << 7);
                    seed ^= (ushort)(seed >> 9);
                    seed ^= (ushort)(seed << 8);
                    __refvalue(auxdata, int) = seed;
                }

                // flip = Tools.BIT(seed, 15).ToBool();

                
                return (ushort)((seed>>1) | (seed & 0x8000));  
                // return (ushort)(Tables.logVol[seed & 0xFF]); 
            }
        }

        //Essentially the same generator as the white noise generator but only clocks based on the duty cycle rather than only outputs noise on the duty cycle
        public static ushort Noise1(ulong n, ushort duty, ref bool flip, TypedReference auxdata)
        {
            unchecked
            {
                var phase = unchecked((ushort) n);
                var seed = __refvalue(auxdata, int);

                if (phase % ((byte)(duty)+1)==0)  
                {
                    seed ^= (ushort)(seed << 7);
                    seed ^= (ushort)(seed >> 9);
                    seed ^= (ushort)(seed << 8);
                    __refvalue(auxdata, int) = seed;
                }

                // flip = Tools.BIT(seed, 15).ToBool();
                
                return (ushort)((seed>>1) | (seed & 0x8000));  //The OR (seed & 0x8000) bit preserves the sign.
            }
        }


        static PinkNoise pgen = new PinkNoise();
        public static ushort Pink(ulong n, ushort duty, ref bool flip, TypedReference auxdata)
        {
            short v = pgen.Next();

            // flip = Tools.BIT(v, 14).ToBool();
            return unchecked((ushort)( v >> 1));
        }

        static P_URand bgen = new P_URand();
        static int bval = 0x1A500;
        public static ushort Brown(ulong n, ushort duty, ref bool flip, TypedReference auxdata)
        {
            bval +=  ( (ushort)bgen.urand() ) >> 5 ;
            bval = (int) (bval * 0.99);
            var output = (bval - 0x7FFF);
            return (ushort) (output);
        }

        public static APU_Noise gen2 = new APU_Noise();
        public static ushort Noise2(ulong n, ushort duty, ref bool flip, TypedReference auxdata)
        {
            gen2.ModeBit = unchecked((byte)(duty >>12));  //Sets the mode to a value 0-15 from high 4 bits.
            gen2.pLen = (ushort)(((duty<<2) & 0x7F) +((duty>>5) & 0x7F) );  //Sets counter len to to bits 0-4 * 4, plus bits 5-11.
            ushort gen = gen2.Current((uint)n );
            return gen;
        }



    }

}


public class APU_Noise
{
    ushort[] periods=ntsc_periods; //Determined from bits 0-3 of input val, mask 0xF
    readonly static ushort[] ntsc_periods = {4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068};
    readonly static ushort[] pal_periods = {4, 8, 14, 30, 60, 88, 118, 148, 188, 236, 354, 472, 708,  944, 1890, 3778};

    public byte ModeBit{get=> mode_bit;  set=> mode_bit = (byte)((value & 0xF)+1); }
    byte mode_bit = 1;  //1-8, determines the periodicity of the waveform.

    ushort seed = 1;
    ushort counter=0;

    public void SetPeriod(byte pos) { pLen = periods[pos & 0xF]; }
    public ushort pLen = 4068;


    void Clock()
    {
        while (counter>0){
        var fb = ( Tools.BIT(seed, 0) ^ Tools.BIT(seed, mode_bit) );
        seed = (ushort)(seed>>1);

        seed = (ushort) (seed | (fb << 14));

        counter--;
        }
    }

    public ushort Current(uint phase)
    {
        if (counter > pLen)  Clock();
        counter++;

        ushort output = (ushort)((seed & 0xff));
        output = (ushort)((output<<7) | ((seed & 0x80) << 8)); 

        return output;
    }

    public override string ToString()
    {
        return Tools.ToBinStr(unchecked((short)seed)) + "\npLen: " + pLen.ToString() + " mode: " + mode_bit.ToString();
    }
}