using System;
using gdsFM;

namespace gdsFM 
{
    public class Oscillator
    {


        public delegate short waveFunc(ulong n, ushort duty);
        waveFunc wf = Pulse;
        double[] customWaveform = new double[128];

        public Oscillator(waveFunc wave)    {wf=wave;}
        public void SetWaveform(waveFunc wave) {wf=wave;}

        //TODO:  Set oscillator based on index for a list/dictionary of delegates, including particular delegates for whether duty cycle/etc is used

        public short Generate(ulong n, ushort duty)
        {
            return wf(n, duty);
        }


        public static short Pulse(ulong n, ushort duty)
        {
            var phase = unchecked((ushort) (n<<1));
            return phase >= duty? short.MaxValue : short.MinValue;
        }
        public static short Sine(ulong n, ushort duty)
        {
            return (short)Tables.sin[unchecked((ushort)n & Tables.SINE_TABLE_MASK)];
        }
        public static short Absine(ulong n, ushort duty)
        {
            return (short) (Tables.sin[unchecked((ushort)n>>1 & Tables.SINE_TABLE_MASK >> 1)] );
        }
        public static short Saw(ulong n, ushort duty)
        {
            short output = Tables.logVol[unchecked((ushort)(n<<1)) ];
            // short output = unchecked((short)(n<<1));
            return output;
        }

        public static short Tri(ulong n, ushort duty)
        {
            return Tables.tri[unchecked(n>>10 & Tables.TRI_TABLE_MASK)];
            // return Tables.logVol[Tables.tri[unchecked(n>>10 & Tables.TRI_TABLE_MASK)] + Tables.SIGNED_TO_INDEX];
        }

        public static short CrushedSine(ulong n, ushort bitsLost)
        {
            return Tables.sin[unchecked((((ushort)n >> bitsLost) << bitsLost) & Tables.SINE_TABLE_MASK)];
        }


        static ushort seed=1;
        public static short White(ulong n, ushort duty) 
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
                
                return Tables.logVol[seed]; 
                return phase >= duty?  (short)0: Tables.logVol[seed];
            }
        }

        static PinkNoise pgen = new PinkNoise();
        public static short Pink(ulong n, ushort duty)
        {
            return pgen.Next();
        }

        static P_URand bgen = new P_URand();
        static int bval = 0x7fff;
        public static short Brown(ulong n, ushort duty)
        {
            bval +=  ( (ushort)bgen.urand() ) >> 5 ;
            bval = (int) (bval * 0.99);
            var output = (short)(bval - 0x7FFF);
            // return unchecked((ushort)n) >= duty?  (short)(output>>1): (short)output;
            return output;
        }


    }

}
