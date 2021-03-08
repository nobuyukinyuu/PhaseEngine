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

        //TODO:  Set oscillator based on index for a list/dictionary of delegates, including particular delegates for whether duty cycle/etc is used

        public short Generate(ulong n, ushort duty)
        {
            return wf(n, duty);
        }


        public static short Pulse(ulong n, ushort duty)
        {
            var phase = unchecked((ushort) n);
            return phase >= duty? short.MaxValue : short.MinValue;
        }
        public static short Sine(ulong n, ushort duty)
        {
            return Tables.sin[unchecked((ushort)n & Tables.SINE_TABLE_MASK)];
        }
        public static short Saw(ulong n, ushort duty)
        {
            return unchecked((short) n);
        }

        public static short CrushedSine(ulong n, ushort bitsLost)
        {
            return Tables.sin[unchecked((((ushort)n >> bitsLost) << bitsLost) & Tables.SINE_TABLE_MASK)];
        }


    }

}
