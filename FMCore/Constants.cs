using System;
using gdsFM;

using Godot;

namespace gdsFM 
{
    public static class Global
    {
        public const int VERSION = 10;

        public static float MixRate = 48000;
        public static float FracMixRate = 1/44100f;

        public const double BaseHz = 440;
        public const double BaseMult = 1 / BaseHz;

        public const double Ln10 = 2.30258509299;



        public const byte FRAC_PRECISION_BITS = 20;
        public const ulong FRAC_SIZE = (1 << FRAC_PRECISION_BITS) - 1;
        public const double ONE_PER_FRAC_SIZE = 1.0 / FRAC_SIZE;


    //This measurement was done against DX7 detune at A-4, where every 22 cycles the tone would change (-detune) samples at a recording rate of 44100hz.
    //See const definitions in glue.cs for more information about the extra-fine detune increment.
        const Decimal DETUNE_440 = 2205M / 22M;
        const Decimal DETUNE_MIN = (2198M / 22M) / DETUNE_440 ;  //Smallest detune multiplier, a fraction of 1.0
        const Decimal DETUNE_MAX = (2212M / 22M) / DETUNE_440;   //Largest detune multiplier, a multiple of 1.0    



        public static readonly byte[] multTable = {1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 20, 24, 24, 30, 30};
        public static readonly ushort[] expTable=init_tbl(expgen), sinTable=init_tbl(lsgen);

        static ushort expgen (ushort input)
        { return (ushort) (Math.Round((Math.Pow(2,input/256.0f)-1)*1024)); }
        static ushort lsgen (ushort input)
        { return (ushort) (Math.Round(-Math.Log(Math.Sin((input+0.5f)*Math.PI/256/2))/Math.Log(2)*256)); }

        public static ushort[] init_tbl(Func<ushort, ushort> inputFunc)
        {
            var exp= new ushort[0x100];
            for (ushort i = 0; i < 0x100; i++)
                exp[i] = inputFunc(i);

            return exp;      
        }




    public static short Exp(ushort expVal)
        {
            ushort signBit = (ushort) (expVal & 0x8000);
            short result = (short) (expVal & 0xFF);

            expVal &= 0x7FFF;
            result =  (short) (expTable[result ^ 0xFF] << 1);
            result |= 0x0800;
            result >>= (expVal >> 8);
            if (signBit>0)
                result = (short)(-result - 1);

            return result;
        }     
    }


    static internal class XorShift64Star
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static uint rotl(uint x, int k) 
        {
            return unchecked((x << k) | (x >> (32 - k)));
        }


        static uint[] s= {1,2};

        public static uint Current() {
            return s[0] * 0x9E3779BB;
        }

        public static uint Next() {
            unchecked{
                uint s0 = s[0];
                uint s1 = s[1];
                uint result = s0 * 0x9E3779BB;

                s1 ^= s0;
                s[0] = rotl(s0, 26) ^ s1 ^ (s1 << 9); // a, b
                s[1] = rotl(s1, 13); // c

                return result;
            }
        }   
}



    public enum EGStatus
    {
        DELAY, ATTACK, DECAY, SUSTAINED, RELEASED, INACTIVE
    }

    public enum LFOStatus
    {
        DELAY, FADEIN, RUNNING
    }


    //Thoughts:  Rates probably need fixed integer math to work, then translated to a level at the very end, since increments are very small.
    struct EGData
    {
        double ar,dr,sr,rr;
        double[] rates;
        double[] rateIncrement;

        public void Reset()
        {
            // ar=31;dr=31;sr=6;rr=15;
            rates = new double[] {0, 0, 120, 0};
        }

        public void Recalc()
        {
            rateIncrement = new double[4];

            for(int i=0;  i<4;  i++)
            {
                rateIncrement[i] = RateMultiplier(1);
            }
        }


        double RateMultiplier(float secs)
        {
            return secs * Global.MixRate;
        }
    }
}
