using System;
using gdsFM;

namespace gdsFM 
{
    public static class Tables
    {
        public const double TAU = Math.PI * 2;
        //Thoughts:  Use unchecked() to rely on rollover behavior for indexes.  Work in ushort for most of the operation.
        //      increments for sine lookup of phase can be masked off if the size is a power of 2.  (12-bit lookup?)

        public const int SINE_TABLE_BITS = 15;  //We bit-shift right by the bit width of the phase counter minus this value to check the table.
        public const int SINE_TABLE_SHIFT = 32 - SINE_TABLE_BITS;  //How far to shift a phase counter value to be in range of the table.
        public const int SINE_TABLE_MASK = (1 << SINE_TABLE_BITS) - 1;  //Mask for creating a rollover.

        public const ushort SIGNED_TO_INDEX = short.MaxValue+1;  //Add this value to an output of the oscillator to get an index for a 16-bit table.

        public static readonly float[] short2float = new float[ushort.MaxValue+1];  //Representation in float of all values of ushort
        public static short[] sin = new short[(1 << SINE_TABLE_BITS) +1];  //integral Increment/decrement can use add/sub operations to alter phase counter.

        public const byte TRI_TABLE_BITS = 5;
        public const byte TRI_TABLE_MASK = (1 << TRI_TABLE_BITS) - 1;
        public static readonly short[] tri = {-32768,-28673,-24577,-20481,-16385,-12289,-8193,-4097,-1,4095,8191,12287,16383,20479,24575,28671,32767,
                                                28671,24575,20479,16383,12287,8191,4095,-1,-4097,-8193,-12289,-16385,-20481,-24577,-28673,};
        public static float[] linVol = new float[ushort.MaxValue+1];



        static Tables()
        {
            for(int i=0; i<short2float.Length; i++)
            {
                short2float[i] = (float) (i / 32767.5) - 1;
            }

            for(int i=0; i<sin.Length; i++)
            {
                sin[i] =  (short) Math.Round(Math.Sin(TAU * (i/(double)(sin.Length-0)) )*short.MaxValue);
            }

            System.Diagnostics.Debug.Print("OK Static");
        }
    }

}
