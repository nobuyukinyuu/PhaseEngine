//Some functions (BIT, make_bitmask) taken from MAME's coretmpl.h and adapted to C#.  BSD-3 license.

using System;
using gdsFM;
using System.Runtime.CompilerServices;
using System.Numerics;

namespace gdsFM 
{
    public static class Tools
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Lerp(float value1, float value2, float amount) { return value1 + (value2 - value1) * amount; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Lerp(double value1, double value2, double amount) { return value1 + (value2 - value1) * amount; }


        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double Log2(double n){ return Math.Log(n) / Math.Log(2); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double Log10(double n){ return Math.Log(n) / Math.Log(10); }
        // [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double dbToLinear(double p_db) { return Math.Exp(p_db * 0.11512925464970228420089957273422);}
        // [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double linear2db(double p_lin) { return Math.Log(p_lin) * 8.6858896380650365530225783783321;}
  
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double dbToLinear(double p_db) { return Math.Pow(2, p_db );}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double linear2db(double p_lin) { return Tools.Log2(p_lin) ;}

        //summary:  Slow converstion from float to fixed point.
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

        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if(val.CompareTo(max) > 0) return max;
            else return val;
        }


        // public static class Bits<T> where T: struct
        // {
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

            public static uint make_bitmask(int n) { return (uint)((n < (32) ? ((1u) << n) : (0u)) - 1); }
            public static ushort make_bitmask(short n) { return (ushort)((n < (16) ? ((1u) << n) : (0u)) - 1); }
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
            public static byte BIT(byte x, byte n, byte w) { return (byte)((x >> n) & w); }
            public static short BIT(short x, byte n, byte w) { return (short)((x >> n) & w); }
            public static ushort BIT(ushort x, byte n, byte w) { return (ushort)((x >> n) & w); }
            public static int BIT(int x, byte n, byte w) { return (int)((x >> n) & w); }
            public static uint BIT(uint x, byte n, byte w) { return (uint)((x >> n) & w); }
            public static long BIT(long x, byte n, byte w) { return (long)((x >> n) & w); }
            public static ulong BIT(ulong x, byte n, byte w) { return (ulong)((x >> n) & w); }



            /// \brief Extract a bit field from an integer
            /// \brief Extract bits in arbitrary order
            ///
            /// Extracts bits from an integer.  Specify the bits in the order they
            /// should be arranged in the output, from most significant to least
            /// significant.  The extracted bits will be packed into a right-aligned
            /// field in the output.
            ///
            /// \param [in] val The integer to extract bits from.
            /// \param [in] b The first bit to extract from the input
            ///   extract, where zero is the least significant bit of the input.
            ///   This bit will appear in the most significant position of the
            ///   right-aligned output field.
            /// \param [in] c The remaining bits to extract, where zero is the
            ///   least significant bit of the input.
            /// \return The extracted bits packed into a right-aligned field.
            // template <typename T, typename U, typename... V> constexpr T bitswap(T val, U b, V... c) noexcept
            // {
            //     return (BIT(val, b) << sizeof...(c)) | bitswap(val, c...);
            // }

        // }


        //Boolean conversion extension methods
        public static bool ToBool(this short x) {return x!=0;}
        public static bool ToBool(this ushort x) {return x>0;}
        public static bool ToBool(this int x) {return x!=0;}
        public static bool ToBool(this uint x) {return x>0;}
        public static bool ToBool(this long x) {return x!=0;}
        public static bool ToBool(this ulong x) {return x>0;}
        public static bool ToBool(this byte x) {return x>0;}


        //-------------------------------------------------
        //  linear_to_fp - given a 32-bit signed input
        //  value, convert it to a signed 10.3 floating-
        //  point value
        //-------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static short linear_to_fp(Int32 value)
        {
            // start with the absolute value
            Int32 avalue = Math.Abs(value);

            // compute shift to fit in 9 bits (bit 10 is the sign)
            int shift = (32 - 9) - BitOperations.LeadingZeroCount((uint) avalue);

            // if out of range, just return maximum; note that YM3012 DAC does
            // not support a shift count of 7, so we clamp at 6
            if (shift >= 7)
                {shift = 6; avalue = 0x1ff;}
            else if (shift > 0)
                avalue >>= shift;
            else
                shift = 0;

            // encode with shift in low 3 bits and signed mantissa in upper
            return (short) (shift | (((value < 0) ? -avalue : avalue) << 3));
        }


        

        //-------------------------------------------------
        //  fp_to_linear - given a 10.3 floating-point
        //  value, convert it to a signed 16-bit value,
        //  clamping
        //-------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Int32 fp_to_linear(short value)
        {
            return (value >> 3) << BIT(value, 0, 3);
        }


        

    }
}
