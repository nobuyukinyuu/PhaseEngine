using System;
using gdsFM;
using System.Runtime.CompilerServices;
using Godot;  //FIXME:  This is really only here to make easier to debug using godot print statements.  Remove me

namespace gdsFM 
{
    public static class Tables
    {
        public const double TAU = Math.PI * 2;
        //Thoughts:  Use unchecked() to rely on rollover behavior for indexes.  Work in ushort for most of the operation.
        //      increments for sine lookup of phase can be masked off if the size is a power of 2.  (12-bit lookup?)

        public const int SINE_TABLE_BITS = 10;  //We bit-shift right by the bit width of the phase counter minus this value to check the table.
        public const int SINE_TABLE_SHIFT = 32 - SINE_TABLE_BITS;  //How far to shift a phase counter value to be in range of the table.
        public const int SINE_TABLE_MASK = (1 << SINE_TABLE_BITS) - 1;  //Mask for creating a rollover.
        public const int SINE_HALFWAY_BIT = SINE_TABLE_BITS - 1;

        public const ushort SIGNED_TO_INDEX = short.MaxValue+1;  //Add this value to an output of the oscillator to get an index for a 16-bit table.

        public static readonly float[] short2float = new float[ushort.MaxValue+1];  //Representation in float of all values of ushort
        public static short[] sin = new short[(1 << SINE_TABLE_BITS) +1];  //integral Increment/decrement can use add/sub operations to alter phase counter.

        public const byte TRI_TABLE_BITS = 5;
        public const byte TRI_TABLE_MASK = (1 << TRI_TABLE_BITS) - 1;


        public static readonly ushort[] tri = new ushort[TRI_TABLE_MASK+1];
                                            /*= {-32768,-28673,-24577,-20481,-16385,-12289,-8193,-4097,-1,4095,8191,12287,16383,20479,24575,28671,32767,
                                                28671,24575,20479,16383,12287,8191,4095,-1,-4097,-8193,-12289,-16385,-20481,-24577,-28673,}; */


//TODO:   Create an exponent table for values in linear increments 0-1 corresponding to the total attenuation (in decibels) an operator or final mix should have.
//      Attenuation can be added together cheaply and converted to a value godot likes in the end stage. Bits of depth can be however many we like (16 is best),
//      and the table mapping should go from 0-72dB attenuation, with max values clampped, perhaps corresponding to 0 at max?
//      All other lookup tables will have to have their values converted from 0-maxValue to decibel attenuation (0 being max volume).
//      In the linear domain, Attenuated envelope C=A*B (0-1 float).  In the log domain, log(C) = log(A) + log(b).

        public const byte ATTENUATION_BITS = 16;

        public const uint MAX_ATTENUATION_SIZE = (1 << ATTENUATION_BITS) -1 ;
        public const float MAX_DB = 80;  //Maximum Decibels of attenuation
        public const double ATTENUATION_UNIT = 1.0 / (double)(MAX_ATTENUATION_SIZE) * MAX_DB; //One attenuation unit in the system
        
        public static readonly short[] logVol = new short[ushort.MaxValue+1];  //scaled decibel equivalent of a given short value..
        public static readonly float[] linVol = new float[MAX_ATTENUATION_SIZE+1];  //Attenuation table scaled from 0-ATTENUATION_BITS.

        public static readonly ushort[] saw = new ushort[256];

        public static double[] atbl = new double[sin.Length];
        static Tables()
        {
            for(int i=0; i<short2float.Length; i++)
            {
                short2float[i] = (float) (i / 32767.5) - 1;
            }


            //log
            for(int i=0; i<logVol.Length; i++)
            {
                // var lin = -i;
                // double db = Tools.Clamp(-Tools.linear2db(i/(double)(short.MaxValue-1)), 0, MAX_DB);
                // var log = (db/(double)MAX_DB * short.MaxValue) - short.MaxValue;
                // atbl[(int)i] = log ;

                // logVol[i] = (short) Tools.Lerp(log,lin, 0)  ;
                // logVol[logVol.Length-1-i] = (short) logVol[i] ;

                double attenuation = Tools.Clamp( Tools.Log2((1.5* i/(double)logVol.Length) + 0.5),  -1, 1);
                double dbScaled = attenuation  * short.MaxValue;
                logVol[i] = (short) Math.Round(dbScaled);

            }


            //sin
            for(int i=0; i<sin.Length/1; i++)
            {
                sin[i] =  (short) Math.Round(Math.Sin(TAU * (i/(double)sin.Length) )*short.MaxValue);

                // double db = Tools.Clamp(Tools.Log2( 1+Math.Sin(TAU * (i+0.5) / (double)(sin.Length)) ), -1, 1);
                
                // // double att= Math.Round( db/(double)MAX_DB * MAX_ATTENUATION_SIZE ) - (MAX_ATTENUATION_SIZE/2) -1 ;
                
                // double att= db * short.MaxValue ;
                // att = logVol[(int)Math.Round(Math.Sin(TAU * (i/(double)sin.Length) )*short.MaxValue) + SIGNED_TO_INDEX];

                // // sin[(int)i] = (short) (Math.Sin(theta) * short.MaxValue);

                // sin[i] =  (short) (att);
                // // sin[sin.Length-i-1] =  (short) (~(short)att);
            }

            //exp
            for (int i=0; i<MAX_ATTENUATION_SIZE;  i++)
            {
                //Should the table be from minVolume to maxVolume?   
                // double attenuation = Tools.dbToLinear( i/(double)(MAX_ATTENUATION_SIZE+1) * -MAX_DB)  ;
                double attenuation = Math.Pow(2,  i/(double)(MAX_ATTENUATION_SIZE + 1)) - 1  ;
                linVol[i] = (float) attenuation * 2 - 1;
                }
            linVol[0] = -1.0f;
            linVol[MAX_ATTENUATION_SIZE] = 1.0f;  //haha eat pant

            
            //tri
            // for(int i=0;  i<tri.Length/1; i++)
            // {
            //     // double attenuation = Tools.Clamp( Tools.linear2db(i/(double)(tri.Length/4) +1),  0, MAX_DB);
            //     double attenuation = Tools.Clamp( Tools.Log2(0.5 * i/(double)(tri.Length) + 0.5)+1,  0, 1);
            //     double dbScaled = attenuation * MAX_ATTENUATION_SIZE - MAX_ATTENUATION_SIZE;
            //     tri[i] = (ushort) Math.Round(dbScaled);
            //     // tri[i+tri.Length/4] = (short) Math.Round(dbScaled);
            //     // tri[tri.Length/2+i] = (short)-tri[i];
            //     // tri[tri.Length-i-1] = (short) tri[i];
            // }
            for (int i=0; i<tri.Length; i++)
            {
                var inc = ((i+0.5)/(double)(tri.Length)) ;
                // saw[i] = (ushort)(Tools.ToFixedPoint( inc, 8) | (uint)inc  & (ushort.MaxValue>>0));
                tri[i] = (ushort) ( (float) Math.Round(-Tools.Log2(inc*2) * 256));
            }

            //saw table
            for (int i=0; i<saw.Length; i++)
            {
                var inc = ((i+0.5)/(double)(saw.Length + 1)) ;
                // saw[i] = (ushort)(Tools.ToFixedPoint( inc, 8) | (uint)inc  & (ushort.MaxValue>>0));
                saw[i] = (ushort) ( (float) Math.Round(-Tools.Log2(inc*2) * 256));
            }

            System.Diagnostics.Debug.Print("Shornlf");

        }

        ///summary:  Returns a linear volume for a given position in an operator's phase accumulator and given attenuation. 
        public static float LinearVolume(ulong phase, short dbValue)
        {
            var output = Tables.linVol[dbValue + Tables.SIGNED_TO_INDEX];
            if (Tools.BIT(phase >> Global.FRAC_PRECISION_BITS, Tables.SINE_HALFWAY_BIT).ToBool())
                output = -output;
            return output;
        }



        public static readonly ushort[] s_sin_table =
        {
            0x859,0x6c3,0x607,0x58b,0x52e,0x4e4,0x4a6,0x471,0x443,0x41a,0x3f5,0x3d3,0x3b5,0x398,0x37e,0x365,
            0x34e,0x339,0x324,0x311,0x2ff,0x2ed,0x2dc,0x2cd,0x2bd,0x2af,0x2a0,0x293,0x286,0x279,0x26d,0x261,
            0x256,0x24b,0x240,0x236,0x22c,0x222,0x218,0x20f,0x206,0x1fd,0x1f5,0x1ec,0x1e4,0x1dc,0x1d4,0x1cd,
            0x1c5,0x1be,0x1b7,0x1b0,0x1a9,0x1a2,0x19b,0x195,0x18f,0x188,0x182,0x17c,0x177,0x171,0x16b,0x166,
            0x160,0x15b,0x155,0x150,0x14b,0x146,0x141,0x13c,0x137,0x133,0x12e,0x129,0x125,0x121,0x11c,0x118,
            0x114,0x10f,0x10b,0x107,0x103,0x0ff,0x0fb,0x0f8,0x0f4,0x0f0,0x0ec,0x0e9,0x0e5,0x0e2,0x0de,0x0db,
            0x0d7,0x0d4,0x0d1,0x0cd,0x0ca,0x0c7,0x0c4,0x0c1,0x0be,0x0bb,0x0b8,0x0b5,0x0b2,0x0af,0x0ac,0x0a9,
            0x0a7,0x0a4,0x0a1,0x09f,0x09c,0x099,0x097,0x094,0x092,0x08f,0x08d,0x08a,0x088,0x086,0x083,0x081,
            0x07f,0x07d,0x07a,0x078,0x076,0x074,0x072,0x070,0x06e,0x06c,0x06a,0x068,0x066,0x064,0x062,0x060,
            0x05e,0x05c,0x05b,0x059,0x057,0x055,0x053,0x052,0x050,0x04e,0x04d,0x04b,0x04a,0x048,0x046,0x045,
            0x043,0x042,0x040,0x03f,0x03e,0x03c,0x03b,0x039,0x038,0x037,0x035,0x034,0x033,0x031,0x030,0x02f,
            0x02e,0x02d,0x02b,0x02a,0x029,0x028,0x027,0x026,0x025,0x024,0x023,0x022,0x021,0x020,0x01f,0x01e,
            0x01d,0x01c,0x01b,0x01a,0x019,0x018,0x017,0x017,0x016,0x015,0x014,0x014,0x013,0x012,0x011,0x011,
            0x010,0x00f,0x00f,0x00e,0x00d,0x00d,0x00c,0x00c,0x00b,0x00a,0x00a,0x009,0x009,0x008,0x008,0x007,
            0x007,0x007,0x006,0x006,0x005,0x005,0x005,0x004,0x004,0x004,0x003,0x003,0x003,0x002,0x002,0x002,
            0x002,0x001,0x001,0x001,0x001,0x001,0x001,0x001,0x000,0x000,0x000,0x000,0x000,0x000,0x000,0x000
        };
        // the values here are 10-bit mantissas with an implied leading bit
        // this matches the internal format of the OPN chip, extracted from the die
        static readonly ushort[] s_power_table =
        {
            0x000,0x003,0x006,0x008,0x00b,0x00e,0x011,0x014,0x016,0x019,0x01c,0x01f,0x022,0x025,0x028,0x02a,
            0x02d,0x030,0x033,0x036,0x039,0x03c,0x03f,0x042,0x045,0x048,0x04b,0x04e,0x051,0x054,0x057,0x05a,
            0x05d,0x060,0x063,0x066,0x069,0x06c,0x06f,0x072,0x075,0x078,0x07b,0x07e,0x082,0x085,0x088,0x08b,
            0x08e,0x091,0x094,0x098,0x09b,0x09e,0x0a1,0x0a4,0x0a8,0x0ab,0x0ae,0x0b1,0x0b5,0x0b8,0x0bb,0x0be,
            0x0c2,0x0c5,0x0c8,0x0cc,0x0cf,0x0d2,0x0d6,0x0d9,0x0dc,0x0e0,0x0e3,0x0e7,0x0ea,0x0ed,0x0f1,0x0f4,
            0x0f8,0x0fb,0x0ff,0x102,0x106,0x109,0x10c,0x110,0x114,0x117,0x11b,0x11e,0x122,0x125,0x129,0x12c,
            0x130,0x134,0x137,0x13b,0x13e,0x142,0x146,0x149,0x14d,0x151,0x154,0x158,0x15c,0x160,0x163,0x167,
            0x16b,0x16f,0x172,0x176,0x17a,0x17e,0x181,0x185,0x189,0x18d,0x191,0x195,0x199,0x19c,0x1a0,0x1a4,
            0x1a8,0x1ac,0x1b0,0x1b4,0x1b8,0x1bc,0x1c0,0x1c4,0x1c8,0x1cc,0x1d0,0x1d4,0x1d8,0x1dc,0x1e0,0x1e4,
            0x1e8,0x1ec,0x1f0,0x1f5,0x1f9,0x1fd,0x201,0x205,0x209,0x20e,0x212,0x216,0x21a,0x21e,0x223,0x227,
            0x22b,0x230,0x234,0x238,0x23c,0x241,0x245,0x249,0x24e,0x252,0x257,0x25b,0x25f,0x264,0x268,0x26d,
            0x271,0x276,0x27a,0x27f,0x283,0x288,0x28c,0x291,0x295,0x29a,0x29e,0x2a3,0x2a8,0x2ac,0x2b1,0x2b5,
            0x2ba,0x2bf,0x2c4,0x2c8,0x2cd,0x2d2,0x2d6,0x2db,0x2e0,0x2e5,0x2e9,0x2ee,0x2f3,0x2f8,0x2fd,0x302,
            0x306,0x30b,0x310,0x315,0x31a,0x31f,0x324,0x329,0x32e,0x333,0x338,0x33d,0x342,0x347,0x34c,0x351,
            0x356,0x35b,0x360,0x365,0x36a,0x370,0x375,0x37a,0x37f,0x384,0x38a,0x38f,0x394,0x399,0x39f,0x3a4,
            0x3a9,0x3ae,0x3b4,0x3b9,0x3bf,0x3c4,0x3c9,0x3cf,0x3d4,0x3da,0x3df,0x3e4,0x3ea,0x3ef,0x3f5,0x3fa
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort attenuation_to_volume(ushort input)
        {
            unchecked
            {
                // look up the fractional part, then shift by the whole
                return (ushort)(((s_power_table[~input & 0xff] | (ushort)0x400) << 2) >> (input >> 8));
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort abs_sin_attenuation(ushort input)
        {
            // the values here are stored as 4.8 logarithmic values for 1/4 phase
            // this matches the internal format of the OPN chip, extracted from the die

            // if the top bit is set, we're in the second half of the curve
            // which is a mirror image, so invert the index
            if ( Tools.BIT(input, 8).ToBool() )
                input = (ushort) ~input;

            // return the value from the table
            return s_sin_table[input & 0xff];
        }    


        static readonly uint[] s_increment_table =           //Envelope increment table
        {
            0x00000000, 0x00000000, 0x10101010, 0x10101010,  // 0-3    (0x00-0x03)
            0x10101010, 0x10101010, 0x11101110, 0x11101110,  // 4-7    (0x04-0x07)
            0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 8-11   (0x08-0x0B)
            0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 12-15  (0x0C-0x0F)
            0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 16-19  (0x10-0x13)
            0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 20-23  (0x14-0x17)
            0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 24-27  (0x18-0x1B)
            0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 28-31  (0x1C-0x1F)
            0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 32-35  (0x20-0x23)
            0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 36-39  (0x24-0x27)
            0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 40-43  (0x28-0x2B)
            0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 44-47  (0x2C-0x2F)
            0x11111111, 0x21112111, 0x21212121, 0x22212221,  // 48-51  (0x30-0x33)
            0x22222222, 0x42224222, 0x42424242, 0x44424442,  // 52-55  (0x34-0x37)
            0x44444444, 0x84448444, 0x84848484, 0x88848884,  // 56-59  (0x38-0x3B)
            0x88888888, 0x88888888, 0x88888888, 0x88888888   // 60-63  (0x3C-0x3F)
        };

        //-------------------------------------------------
        //  attenuation_increment - given a 6-bit ADSR
        //  rate value and a 3-bit stepping index,
        //  return a 4-bit increment to the attenutaion
        //  for this step (or for the attack case, the
        //  fractional scale factor to decrease by)
        //-------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte attenuation_increment(byte rate, byte index)
        {
            return (byte) Tools.BIT(s_increment_table[rate], (byte) (4*index), 4);
        }

    }

}
