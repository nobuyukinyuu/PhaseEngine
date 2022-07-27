// From Cornucopia.Net,  MIT License
// Some parts also lifted and adapted from dotnet core's BitOperations.cs......


using System.Runtime.CompilerServices;

namespace System.Numerics
{
    public static class BitOperations
    {
        private static ReadOnlySpan<byte> Log2DeBruijn => new byte[]
        {
            00, 09, 01, 10, 13, 21, 02, 29,
            11, 14, 16, 18, 22, 25, 03, 30,
            08, 12, 20, 28, 15, 17, 24, 07,
            19, 27, 23, 06, 26, 05, 04, 31
        };

        public static int Log2(uint value)
        {
            value |= value >> 01;
            value |= value >> 02;
            value |= value >> 04;
            value |= value >> 08;
            value |= value >> 16;

            return Log2DeBruijn[(int) ((value * 0x07C4ACDDu) >> 27)];
        }

        public static int Log2(ulong value)
        {
            var hi = (uint) (value >> 32);

            if (hi == 0)
            {
                return Log2((uint) value);
            }

            return 32 + Log2(hi);
        }


        /// <summary>
        /// Count the number of leading zero bits in a mask.
        /// Similar in behavior to the x86 instruction LZCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(uint value)
        {
            // if (Lzcnt.IsSupported)
            // {
            //     // LZCNT contract is 0->32
            //     return (int)Lzcnt.LeadingZeroCount(value);
            // }

            // if (ArmBase.IsSupported)
            // {
            //     return ArmBase.LeadingZeroCount(value);
            // }

            // Unguarded fallback contract is 0->31
            if (value == 0)
            {
                return 32;
            }

            return 31 - Log2(value);
        }

        /// <summary>
        /// Count the number of leading zero bits in a mask.
        /// Similar in behavior to the x86 instruction LZCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(ulong value)
        {
            // if (Lzcnt.X64.IsSupported)
            // {
            //     // LZCNT contract is 0->64
            //     return (int)Lzcnt.X64.LeadingZeroCount(value);
            // }

            // if (ArmBase.Arm64.IsSupported)
            // {
            //     return ArmBase.Arm64.LeadingZeroCount(value);
            // }

            uint hi = (uint)(value >> 32);

            if (hi == 0)
            {
                return 32 + LeadingZeroCount((uint)value);
            }

            return LeadingZeroCount(hi);
        }

        /// <summary>
        /// Returns the population count (number of bits set) of a mask.
        /// Similar in behavior to the x86 instruction POPCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount(uint value)
        {
            const uint c1 = 0x_55555555u;
            const uint c2 = 0x_33333333u;
            const uint c3 = 0x_0F0F0F0Fu;
            const uint c4 = 0x_01010101u;

            value -= (value >> 1) & c1;
            value = (value & c2) + ((value >> 2) & c2);
            value = (((value + (value >> 4)) & c3) * c4) >> 24;

            return (int)value;
        }
    }
}