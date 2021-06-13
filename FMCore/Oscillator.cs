using System;
using gdsFM;

namespace gdsFM 
{
    public class Oscillator
    {
        public delegate ushort waveFunc(ulong n, ushort duty, ref bool flip);
        waveFunc wf = Sine;
        short[] customWaveform = new short[128];

        public static readonly waveFunc[] waveFuncs = {Sine, Saw, Tri, Pulse, Absine, Noise1, Pink, Brown, Noise2};

        public Oscillator(waveFunc wave)    {wf=wave;}
        public void SetWaveform(waveFunc wave) {wf=wave;}

        //TODO:  Set oscillator based on index for a list/dictionary of delegates, including particular delegates for whether duty cycle/etc is used

        public ushort Generate(ulong n, ushort duty, ref bool flip)
        {
            return wf(n, duty, ref flip);
        }


        public static ushort Pulse(ulong n, ushort duty, ref bool flip)
        {
            ushort phase = (ushort) unchecked((n<<6));
            flip = phase >= duty;
            return 0;
            // return phase >= duty? short.MaxValue : short.MinValue;
        }
        public static ushort Sine(ulong input, ushort duty, ref bool flip)
        {
            // return (short)Tables.sin[unchecked((ushort)n & Tables.SINE_TABLE_MASK)];

            //Don't return attenuation to volume, do that in the compute_volume step of the operator.  We need log scale output to modulate cheap
            // the values here are stored as 4.8 logarithmic values for 1/4 phase
            // this matches the internal format of the OPN chip, extracted from the die
            flip = Tools.BIT(input, 9).ToBool();

            // if the top bit is set, we're in the second half of the curve
            // which is a mirror image, so invert the index
            if ( Tools.BIT(input, 8).ToBool() )
                input = (ushort) ~input;


            // return the value from the table
            return Tables.s_sin_table[input & 0xff];
        }


        //Uses arbitrary (default 10) bit lookup table to produce higher quality waveforms.  Disabled by default.
        //May need to be paired with a deeper exp table to work properly!!  This would mean it might need a separate operator mode...
        public static ushort SineHQ(ulong input, ushort duty, ref bool flip)
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

        public static ushort Absine(ulong input, ushort duty, ref bool flip)
        {
            input = (ulong) (input >> 1);
            if ( Tools.BIT(input, 8).ToBool() )
                input = (ushort) ~input;


            // return the value from the table
            return Tables.s_sin_table[input & 0xff];
        }


        public static ushort Saw(ulong n, ushort duty, ref bool flip)
        {
        //    flip = Tools.BIT(n, 9).ToBool();
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

        public static ushort Tri(ulong n, ushort duty, ref bool flip)
        {
            flip = Tools.BIT(n, 9).ToBool();

            if ( Tools.BIT(n, 8).ToBool() )
            {
                n = (ushort) ~n;
            }

            return Tables.tri[unchecked(n>>4 & Tables.TRI_TABLE_MASK)];
            // return Tables.logVol[Tables.tri[unchecked(n>>10 & Tables.TRI_TABLE_MASK)] + Tables.SIGNED_TO_INDEX];
        }

        // public static ushort CrushedSine(ulong n, ushort bitsLost)
        // {
        //     return Tables.sin[unchecked((((ushort)n >> bitsLost) << bitsLost) & Tables.SINE_TABLE_MASK)];
        // }


        //White noise generator, but uses duty cycle to oscillate between on and off state
        static ushort seed=1;
        public static ushort White(ulong n, ushort duty, ref bool flip) 
        {
            unchecked
            {
                var phase = unchecked((ushort) n);

                if (phase > duty) return 0;  // Comment this out if you don't want the "buzzing" duty behavior and instead would rather use the counter

                // if (phase % ((byte)(duty)+1)==0)  //Using duty cycle to specify randomize interval.  FIXME:  Consider a 3rd variable so more info can be suppllied
                {
                    seed ^= (ushort)(seed << 7);
                    seed ^= (ushort)(seed >> 9);
                    seed ^= (ushort)(seed << 8);
                }

                // flip = Tools.BIT(seed, 15).ToBool();
                
                return (ushort)((seed>>1) | (seed & 0x8000));  
                // return (ushort)(Tables.logVol[seed & 0xFF]); 
            }
        }

        //Essentially the same generator as the white noise generator but only clocks based on the duty cycle rather than only outputs noise on the duty cycle
        public static ushort Noise1(ulong n, ushort duty, ref bool flip) 
        {
            unchecked
            {
                var phase = unchecked((ushort) n);

                if (phase % ((byte)(duty)+1)==0)  //Using duty cycle to specify randomize interval.  FIXME:  Consider a 3rd variable so more info can be suppllied
                {
                    seed ^= (ushort)(seed << 7);
                    seed ^= (ushort)(seed >> 9);
                    seed ^= (ushort)(seed << 8);
                }

                // flip = Tools.BIT(seed, 15).ToBool();
                
                return (ushort)((seed>>1) | (seed & 0x8000));  
            }
        }


        static PinkNoise pgen = new PinkNoise();
        public static ushort Pink(ulong n, ushort duty, ref bool flip)
        {
            short v = pgen.Next();

            // flip = Tools.BIT(v, 14).ToBool();
            return unchecked((ushort)( v >> 1));
        }

        static P_URand bgen = new P_URand();
        static int bval = 0x7fff;
        public static ushort Brown(ulong n, ushort duty, ref bool flip)
        {
            bval +=  ( (ushort)bgen.urand() ) >> 5 ;
            bval = (int) (bval * 0.99);
            var output = (bval - 0x7FFF);
            // flip = Tools.BIT(output, 15).ToBool();
            return (ushort) (output);
        }

        public static APU_Noise gen2 = new APU_Noise();
        public static ushort Noise2(ulong n, ushort duty, ref bool flip)
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