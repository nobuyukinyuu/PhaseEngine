//Some functions (BIT, make_bitmask) taken from MAME's coretmpl.h and adapted to C#.  BSD-3 license.

using System;
using PhaseEngine;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Collections.Generic;

namespace PhaseEngine 
{
    public static partial class Tools
    {
        public static double SincN(double x) { if(x==0) return 1; x*=Math.PI; return Math.Sin(x)/x; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Lerp(float value1, float value2, float amount) { return value1 + (value2 - value1) * amount; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Lerp(double value1, double value2, double amount) { return value1 + (value2 - value1) * amount; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerp(float first, float last, float value) { return (value - first) / (last - first); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double InverseLerp(double first, double last, double value) { return (value - first) / (last - first); }


        /// summary:  No-multiply / division linear interpolation in 8-bit and 16-bit integer forms. X must be a value spanning the range of the bit width.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Lerp8(byte A,byte B, byte x)	{ return (byte) ((A*(255-x)+B*x+255) >> 8); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Lerp16(short A, short B, short x) { return (short)((A*(65535-x)+B*x+65535) >> 16); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Lerp16(ushort A, ushort B, ushort x) { return (ushort)((A*(65535-x)+B*x+65535) >> 16); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Lerp16(int A, int B, int x) { return ((A*(65535-x)+B*x+65535) >> 16); }


        //NOTE:  Slow but accurate log2. For fast log2, use BitOperations.Log2 (we use from cornucopia.NET)
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double Log2(double n){ return Math.Log(n) / Math.Log(2); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double Log10(double n){ return Math.Log(n) / Math.Log(10); }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Remap(double value, double inFrom, double inTo, double outFrom, double outTo)
        { return Lerp(outFrom, outTo, InverseLerp(inFrom, inTo, value)); }


        // public static double RoundToSignificantDigits(double d, int digits)
        // {
        //     if(d == 0)
        //         return 0;
        //     double scale = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(d))) + 1);
        //     return scale * Math.Round(d / scale, digits);
        // }
        // public static double TruncToSignificantDigits(double d, int digits)
        // {
        //     if(d == 0)
        //         return 0;
        //     double scale = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(d))) + 1);
        //     return scale * Math.Truncate(d / scale);
        // }


        /// summary:  Slow converstion from float to fixed point.
        public static long ToFixedPoint(float n, byte decimalBitPrecision=Global.FRAC_PRECISION_BITS, bool preserveSignBit=false)
        {
            var whole = Math.Abs(Math.Truncate(n));
            var frac = Math.Abs(n) - whole;
            uint fracSize = (1u << decimalBitPrecision) - 1u;

            long output = (long)(frac * fracSize) | ((long)(whole) << decimalBitPrecision);
            if(preserveSignBit)  output *= Math.Sign(n);

            return output;
        }
       public static double FromFixedPoint(long n, byte decimalBitPrecision=Global.FRAC_PRECISION_BITS)
        {
            uint fracSize = (1u << decimalBitPrecision) - 1u;
            var whole = n >> decimalBitPrecision;
            double frac = (n & fracSize) / (double) fracSize;

            return whole+frac;
        }
       public static double FromFixedPoint(ulong n, byte decimalBitPrecision=Global.FRAC_PRECISION_BITS)
        {
            uint fracSize = (1u << decimalBitPrecision) - 1u;
            var whole = n >> decimalBitPrecision;
            double frac = (n & fracSize) / (double) fracSize;

            return whole+frac;
        }


        // Clamp for c# 7.2
        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if(val.CompareTo(max) > 0) return max;
            else return val;
        }

        //1d array initializer.  Parameterless
        public static void InitArray<T>(this T[] instance) where T : class, new()
        {
            for(int i=0; i<instance.Length; i++)
                instance[i] = new T();
        }

        //2d array initializer for type prims
        public static void InitArray<T>(this T[][] instance, uint x, uint y) where T : struct
        {
            var output = new T[x][];
            for(uint i=0; i<x; i++)
                output[i] = new T[y];
            instance = output;
        }
        //Fills an array with arrays that are copies of defaultData
        public static void InitArray<T>(this T[][] instance, uint length, T[] defaultData) where T : struct
        { instance = InitArray(length, defaultData); }
        public static T[][] InitArray<T>(uint length, T[] defaultData) where T : struct
        {
            var output = new T[length][];
            for(uint i=0; i<length; i++)
            {
                output[i] = new T[defaultData.Length];
                defaultData.CopyTo(output[i], 0);
            }
            return output;
        }

        // public static void InitArray<T[]>(this List<T[]> instance, uint length, T[] defaultData)
        // { instance = InitList(length, defaultData); }
        public static List<T[]> InitList<T>(uint length, T[] defaultData) 
        {
            var output = new List<T[]>((int)length);
            for(uint i=0; i<length; i++)
            {
                var p = new T[defaultData.Length];
                defaultData.CopyTo(p, 0);
                output.Add(p);
            }
            return output;
        }
    


        /// \defgroup bitutils Useful functions for bit shuffling
        /// \{

        /// \brief Generate a right-aligned bit mask
        ///
        /// Generates a right aligned mask of the specified width.  Works with
        /// signed and unsigned integer types.
        /// \tparam T Desired output type.
        /// \tparam U Type of the input (generally resolved by the compiler).
        /// \param [in] n Width of the mask to generate in bits.
        /// \return Right-aligned mask of the specified width.

        // TODO:  Consider rewriting these to not rely on rollover but instead return unsigned maxValue if width is exceeded and (1<<n)-1 otherwise...
        public static uint make_bitmask(int n) { return (uint)((n < (32) ? ((1u) << n) : (0u)) - 1); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static byte unsigned_bitmask(byte n) { return (byte)((1u << n) - 1); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static ushort make_bitmask(short n) { return (ushort)((n < (16) ? ((1u) << n) : (0u)) - 1); }
        public static ulong make_bitmask(long n) { return (ulong)((n < (64) ? ((1ul) << (int)n) : unchecked((ulong)-1))); }  // Uhhhhh.....




        /// \brief Extract a single bit from an integer
        ///
        /// Extracts a single bit from an integer into the least significant bit
        /// position.
        ///
        /// \param [in] x The integer to extract the bit from.
        /// \param [in] n The bit to extract, where zero is the least
        ///   significant bit of the input.
        /// \return Zero if the specified bit is unset, or one if it is set.
        /// \sa bitswap

        // public static T BIT(T x, T n) { return (x >> n) & (T)1; }
        public static short BIT(short x, byte n) { return (short)((x >> n) & 1); }
        public static ushort BIT(ushort x, byte n) { return (ushort)((x >> n) & 1); }
        public static int BIT(int x, byte n) { return (int)((x >> n) & 1); }
        public static uint BIT(uint x, byte n) { return (uint)((x >> n) & 1); }
        public static long BIT(long x, byte n) { return (long)((x >> n) & 1); }
        public static ulong BIT(ulong x, byte n) { return (ulong)((x >> n) & 1); }


        /// \brief Generate a right-aligned bit mask
        ///
        /// Generates a right aligned mask of the specified width.  Works with
        /// signed and unsigned integer types.
        /// \tparam T Desired output type.
        /// \tparam U Type of the input (generally resolved by the compiler).
        /// \param [in] n Width of the mask to generate in bits.
        /// \return Right-aligned mask of the specified width.
        public static byte BIT(byte x, byte n, byte w) { return (byte)((x >> n) & make_bitmask(w)); }
        public static short BIT(short x, byte n, byte w) { return (short)((x >> n) & make_bitmask(w)); }
        public static ushort BIT(ushort x, byte n, byte w) { return (ushort)((x >> n) & make_bitmask(w)); }
        public static int BIT(int x, byte n, byte w) { return (int)((x >> n) & make_bitmask(w)); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static uint BIT(uint x, byte n, byte w) { return (uint)((x >> n) & make_bitmask(w)); }
        public static long BIT(long x, byte n, byte w) { return (long)((x >> n) & make_bitmask(w)); }
        public static ulong BIT(ulong x, byte n, byte w) { return (ulong)((x >> n) & make_bitmask(w)); }




        //Boolean conversion extension methods
        public static bool ToBool(this short x) {return x!=0;}
        public static bool ToBool(this ushort x) {return x>0;}
        public static bool ToBool(this int x) {return x!=0;}
        public static bool ToBool(this uint x) {return x>0;}
        public static bool ToBool(this long x) {return x!=0;}
        public static bool ToBool(this ulong x) {return x>0;}
        public static bool ToBool(this byte x) {return x>0;}






        /// summary:  Produces a string representing the bitmask of value n.
        public static string ToBinStr(short n, bool insert_space=true)
        {
            var sb = new System.Text.StringBuilder(16);
            for (int i=0; i<16;  i++)
            {
                sb.Insert(0, n & 1);
                n >>= 1;
            }

            if(insert_space)
            {
                for (int i=4; i<16; i+=4)
                {
                    sb.Insert(i, " ");
                    i++;
                }
            }
           return sb.ToString();     
        }

        // public static int[] Range(int start, int end)
        // {
        //     List<int> output = new List<int>();
        //     if(end<start) {var temp=end; end=start; start=temp; }
        //     for(int i=start; i<end; i++)  output.Add(i);
        //     return output.ToArray();
        // }


        //Branchless absolute value and sign methods
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static short Abs(short n) {int o=n; int s=(short)(n>>31); o^=s; o-=s; return (short)(o);}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Abs(int n) {int s=(n>>31); n^=s; n-=s; return n;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Abs(long n) {long s=(n>>63); n^=s; n-=s; return n;}
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Sign(int value) => (value >> 31) | (int)((uint)(-value) >> 31);


        //Modulus function where the values are always positive (`%%` operator in Python)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mod(int a, int b)
        {   int c = a % b;
            if ((c < 0 && b > 0) || (c > 0 && b < 0))  c += b;
            return c;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Mod(float a, float b)
        {   float c = a % b;
            if ((c < 0 && b > 0) || (c > 0 && b < 0))  c += b;
            return c;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Mod(double a, double b)
        {   double c = a % b;
            if ((c < 0 && b > 0) || (c > 0 && b < 0))  c += b;
            return c;
        }



        // Count trailing zeroes based on de bruijn sequence and multiply operation.....  For values up to 32-bit
        private static ReadOnlySpan<byte> MultiplyDeBruijnBitPosition => new byte[]
        { 0, 1, 28, 2, 29, 14, 24, 3, 30, 22, 20, 15, 25, 17, 4, 8, 
          31, 27, 13, 23, 21, 19, 16, 7, 26, 12, 18, 6, 11, 5, 10, 9 };

        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Ctz(int v) =>
            MultiplyDeBruijnBitPosition[ Convert.ToInt32((UInt32)((v & -v) * 0x077CB531U) >> 27) ];


        //Find nearest power of 2
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Pow2Ceil(int n)
        {
            n--;
            n |= n >> 1;
            n |= n >> 2;
            n |= n >> 4;
            n |= n >> 8;
            n |= n >> 16;
            n++; 
            return n;           
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] static int NearestPow2(int n)
        {
            int next = Pow2Ceil(n);
            int prev = next >> 1;
            return next-n < n-prev?  next:prev;
        }

        //Returns the least common multiple of the arguments.
        public static ulong LCM(long[] args)
        {
            switch(args.Length)
            {
                case 0: case 1:
                    throw new ArgumentOutOfRangeException("args", "Collection must have 2 or more elements.");
                case 2:
                    return LCM((ulong)args[0], (ulong) args[1]);
            }
            
            ulong runningTotal = LCM((ulong)args[0], (ulong) args[1]);
            for (int i=2; i<args.Length; i++)
                runningTotal = LCM(runningTotal, (ulong) args[i]);
            
            return runningTotal;
        }

        static ulong LCM(ulong a, ulong b)
        {
            if(a>b)
                return (a/GCD(a,b))*b;
            else
                return (b/GCD(a,b))*a;   
        }          
            //  return a / GCD(a,b) * b; }


        //Returns the greatest common divisor between 2 arguments.
        static ulong GCD(ulong a, ulong b)
        {

            while (a != 0 && b != 0)
            {
                if (a > b)
                    a %= b;
                else
                    b %= a;
            }
            return a | b;
        }

    }
}
