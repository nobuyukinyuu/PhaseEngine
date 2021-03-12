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
            return Tables.sin[unchecked((ushort)n & Tables.SINE_TABLE_MASK)];
        }
        public static short Absine(ulong n, ushort duty)
        {
            return (short) (Tables.sin[unchecked((ushort)n>>1 & Tables.SINE_TABLE_MASK >> 1)] );
        }
        public static short Saw(ulong n, ushort duty)
        {
            return unchecked((short) (n<<1));
        }

        public static short Tri(ulong n, ushort duty)
        {
            return Tables.tri[unchecked(n>>10 & Tables.TRI_TABLE_MASK)];
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
                
                return (short)seed; 
                return phase >= duty?  (short)0: (short)seed;
            }
        }


    }

}
