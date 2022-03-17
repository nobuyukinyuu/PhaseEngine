using PhaseEngine;

//  Z85 C# implementation for PhaseEngine, by Nobuyuki
//  Based on the iMatix Z85 reference implementation. 
//  MIT License.  See THIRDPARTY.md for details.

namespace PhaseEngine
{
    public static class Z85
    {
        //Maps base 256 to base 85
        static char[] encoder = {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 
            'u', 'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D', 
            'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N',
            'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 
            'Y', 'Z', '.', '-', ':', '+', '=', '^', '!', '/',
            '*', '?', '&', '<', '>', '(', ')', '[', ']', '{',
            '}', '@', '%', '$', '#',
        };

        //  Maps base 85 to base 256
        //  We chop off lower 32 and higher 128 ranges
        static byte[] decoder = {
            0x00, 0x44, 0x00, 0x54, 0x53, 0x52, 0x48, 0x00, 
            0x4B, 0x4C, 0x46, 0x41, 0x00, 0x3F, 0x3E, 0x45, 
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 
            0x08, 0x09, 0x40, 0x00, 0x49, 0x42, 0x4A, 0x47, 
            0x51, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2A, 
            0x2B, 0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x32, 
            0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 
            0x3B, 0x3C, 0x3D, 0x4D, 0x00, 0x4E, 0x43, 0x00, 
            0x00, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 
            0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 
            0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 
            0x21, 0x22, 0x23, 0x4F, 0x00, 0x50, 0x00, 0x00
        };

        //  --------------------------------------------------------------------------
        //  Encode a byte array as a string

        public static string Encode(byte[] data)
        {
            if (data.Length % 4 !=0) throw new System.FormatException("Input size must be a multiple of 4.");

            int encoded_size = data.Length * 5 / 4;
            var output = new char[encoded_size];

            uint charNum=0, byteNum=0;
            uint value = 0;
            while (byteNum<data.Length)
            {
                // Accumulate value in base 256 (binary)
                value = value * 256 + data[byteNum++];
                if (byteNum % 4 ==0) 
                {
                    // Output value in base 85
                    uint divisor = 85 * 85 * 85 * 85;
                    while (divisor>0)
                    {
                        output [charNum++] = encoder [value / divisor % 85];
                        divisor /= 85;
                    }
                    value = 0;
                }
            }
            System.Diagnostics.Debug.Assert(charNum == encoded_size);
            // output [charNum] = (char)(0);  //Null Terminator
            return new string(output);
        }

        public static byte[] Decode(string input)
        {
            input = input.Trim();
            if (input.Length % 5 != 0) throw new System.FormatException("Input size must be a multiple of 5.");
            var s = input.ToCharArray();

            int decoded_size = input.Length * 4 / 5;
            var output = new byte[decoded_size];

            uint charNum=0, byteNum=0;
            uint value = 0;
            while (charNum < input.Length)
            {
                // Accumulate value in base 85
                value = value * 85 + decoder [(byte) s[charNum++] - 32];
                if (charNum % 5 == 0)
                {
                    // Output value in base 256
                    uint divisor = 256 * 256 * 256;
                    while (divisor>0)
                    {
                        output [byteNum++] = (byte)(value / divisor % 256);
                        divisor /= 256;
                    }
                    value = 0;
                }
            }

            System.Diagnostics.Debug.Assert(byteNum == decoded_size);
            return output;
        }

        public static string Encode(short[] data) => Encode(ShortsToBytes(data));


        // Below functions convert Short arrays to byte arrays and vice-versa in order of high 8 bits, then low 8 bits.

        public static byte[] ShortsToBytes(short[] data)
        {
            var output = new byte[data.Length * 2];
            for(int i=0; i<data.Length; i++)
            {
                output[i*2] = (byte)(data[i] >> 8);
                output[i*2 + 1] = (byte)(data[i] & 0xFF);
            }

            return output;
        }

        public static short[] BytesToShorts(byte[] data)
        {
            if (data.Length % 2 != 0) throw new System.IndexOutOfRangeException("Input size must be a multiple of 2.");
            var output = new short[data.Length / 2];
            for(int i=0; i<output.Length; i++)
                output[i] = (short)((data[i*2] << 8) | data[i*2 + 1]);

            return output;
        }

        // public static short[] BytesToInt16(byte[] data)
        // {
        //     if (data.Length % 2 != 0) throw new System.IndexOutOfRangeException("Input size must be a multiple of 2.");
        //     var output = new short[data.Length / 2];
        //     for(int i=0; i<data.Length; i++)
        //         output[i/2] = (short)( data[i] << 8 | data[i+1] );

        //     return output;
        // }
        

    }



}