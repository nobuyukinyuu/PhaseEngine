using System;
using PhaseEngine;
using System.Runtime.CompilerServices;
using System.Diagnostics;
// using Godot;  //FIXME:  This is really only here to make easier to debug using godot print statements.  Remove me

namespace PhaseEngine 
{
    public static class Tables
    {
        public const double TAU = Math.PI * 2;
        //Thoughts:  Use unchecked() to rely on rollover behavior for indexes.  Work in ushort for most of the operation.
        //      increments for sine lookup of phase can be masked off if the size is a power of 2.  (12-bit lookup?)


        //16bit -> float stuff
        public static readonly float[] short2float = new float[ushort.MaxValue+1];  //Representation in float of all values of ushort
        public const ushort SIGNED_TO_INDEX = short.MaxValue+1;  //Add this value to an output of the oscillator to get an index for a 16-bit table.


        //16kb tables
        public static readonly float[] vol2pitchDown = new float[8192];  //Converts an LFO's oscillator output to float mapping from 1-0.5
        public static readonly float[] vol2pitchUp = new float[8192];  //Converts an LFO's oscillator output to float mapping from 1-2
        public static readonly ushort[] vol2attenuation = new ushort[8192+1];  //Converts a 12-bit (positive) volume to an attenuation value.




        //HQ Sine table  (probably unused)
        public const int SINE_TABLE_BITS = 12;  //We bit-shift right by the bit width of the phase counter minus this value to check the table.
        // public const int SINE_TABLE_SHIFT = 32 - SINE_TABLE_BITS;  //How far to shift a phase counter value to be in range of the table.
        public const int SINE_RATIO = SINE_TABLE_BITS - 8;  //How far to shift a phase counter value to be in range of the table.
        public const int SINE_HIGH_BIT = SINE_TABLE_BITS;
        public const int SINE_SIGN_BIT = SINE_TABLE_BITS+1;
        public const int SINE_TABLE_MASK = (1 << SINE_TABLE_BITS) - 1;  //Mask for creating a rollover.
        public const int SINE_HALFWAY_BIT = SINE_TABLE_BITS - 1;

        public static ushort[] sin = new ushort[SINE_TABLE_MASK +1];  //integral Increment/decrement can use add/sub operations to alter phase counter.


        //Triangle and saw table
        public const byte TRI_TABLE_BITS = 5;
        public const byte TRI_TABLE_MASK = (1 << TRI_TABLE_BITS) - 1;
        public static readonly ushort[] tri = new ushort[TRI_TABLE_MASK+1];
        public static readonly ushort[] saw = new ushort[256];


        public static readonly short[] defaultWavetable = new short[256];
 

        //Note transposition ratio table
        public static readonly double[] transpose = new double[1300];  //10kb

        //Duty cycle increment ratio table
        public static readonly float[] dutyRatio = new float[ushort.MaxValue+1];  //256kb

        //Amplitude modulation depth scaling ratio table
        public static readonly float[] amdScaleRatio = new float[1024];  //4kb.  Scale used to multiply against an oscillator to produce a given amplitude depth.
        public static readonly short[] amdPushupRatio = new short[1024];  //Amount used to push a value scaled by the above table to a positive range.

        static Tables()
        {
            for(int i=0; i<short2float.Length; i++)
            {
                short2float[i] = (float) (i / 32767.5) - 1;
            }


            for(int i=1; i<dutyRatio.Length; i++)  //Start at 1 to avoid divide by zero!
            {
                dutyRatio[i] = (float) (0xFFFF / (double)i) ;
            }


            //Transpose
            for(int i=0; i<transpose.Length; i++)
            {
                transpose[i] = Math.Pow(2, (i * 0.01)/12.0);
            }

            //sin
            for(int i=0; i<sin.Length; i++)
            {
                // sin[i] =  (short) Math.Round(Math.Sin(TAU * (i/(double)sin.Length) )*short.MaxValue);
                sin[i] =  unchecked( (ushort) Math.Round( -Tools.Log2(Math.Sin((i+0.5) * Math.PI/sin.Length/2.0)) * 256) );
            }
            
            //tri
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

            //LFOs, 16kb tables etc
            for (int i=1; i<8192; i++)
            {
                vol2pitchDown[i] = Tools.Lerp(1, 0.5f, i/8192.0f);
                vol2pitchUp[i]   = Tools.Lerp(1, 2, i/8192.0f);
            }

            for (int i=0; i<amdScaleRatio.Length; i++)
            {
                amdScaleRatio[i] = 1.0f - i / (float)Envelope.L_MAX;  // Value from 1.0f-0 representing how much to scale volume by from the raw AMD value
                amdPushupRatio[i] = (short)(0x1FFF * amdScaleRatio[i]); 
            }


            //Volume to attenuation table
            var attVol = new ushort[2048];
            for(ushort i=0; i<attVol.Length; i++)
            {
                attVol[i] = attenuation_to_volume(i);
                vol2attenuation[attVol[i]] = i;
            }
            //Above we calculated out all of the reasonable values from the known table, now we'll fill all the other values in between with similar attenuations.
            //We added one to vol2attenuation's array length to accomodate possible overflows but don't want it offsetting the calcs, so subtract 1 in the loop.
            ushort lastVal = 2047;
            int first_instance=0;
            for(ushort i=0; i<vol2attenuation.Length-1; i++)  
            {
                if(vol2attenuation[i] == 0) 
                    vol2attenuation[i] = lastVal; 
                else if (first_instance==0)
                    {
                        first_instance = i;
                        lastVal = vol2attenuation[i];
                    }
                else
                    lastVal = vol2attenuation[i];
            }

            for (ushort i=0; i<first_instance; i++)  //FIXME:  This block may not be necessary.  Check against very slow LFOs with saw waves.
            {
                vol2attenuation[i] = (ushort)Tools.Lerp(2047, vol2attenuation[first_instance], i/(first_instance-1));
            }



            // Stopwatch sw = new Stopwatch();
            // bool flip=false;
            // long inc2= (long)(Global.FRAC_SIZE << 2);
            // double inc3 = (double)inc2;
            // sw.Start();
            // for (uint i=0; i<48000*24*6; i++)  
            //     Oscillator.Saw(i, 32767, ref flip, __makeref(inc2));
            // sw.Stop();
            // Console.WriteLine("Saw1 Elapsed={0}ms",sw.Elapsed.Milliseconds);

            // sw.Restart();
            // for (uint i=0; i<48000*24*6; i++)  Oscillator.Saw2(i, 32768, ref flip, __makeref(inc3));
            // sw.Stop();
            // Console.WriteLine("Saw2 Elapsed={0}ms",sw.Elapsed.Milliseconds);

            // System.Diagnostics.Debug.Print("Shornlf");

        }


        //  Reface DX compatible LFO speed values 0-127 as measured by Martin Tarenskeen.  Used by PhaseEngine as the default LFO range.
        //  Source:  https://www.yamahasynth.com/ask-a-question/generating-specific-lfo-frequencies-on-dx
        //  Most other YM chips have their own LFO specs which aren't very compatible with each other. We may be able to specify them elsewhere.
        public static readonly double[] LFOSpeed= 
        {
            0.026,	0.042,	0.084,	0.126,	0.168,	0.210,	0.252,	0.294,	0.336,	0.372,	0.412,	0.456,	0.505,	0.542,	0.583,	0.626,	
            0.673,	0.711,	0.752,	0.795,	0.841,	0.880,	0.921,	0.964,	1.009,	1.049,	1.090,	1.133,	1.178,	1.218,	1.259,	1.301,	
            1.345,	1.386,	1.427,	1.470,	1.514,	1.554,	1.596,	1.638,	1.681,	1.722,	1.764,	1.807,	1.851,	1.891,	1.932,	1.975,	
            2.018,	2.059,	2.101,	2.143,	2.187,	2.227,	2.269,	2.311,	2.354,	2.395,	2.437,	2.480,	2.523,	2.564,	2.606,	2.648,	
            2.691,	2.772,	2.854,	2.940,	3.028,	3.108,	3.191,	3.275,	3.362,	3.444,	3.528,	3.613,	3.701,	3.858,	4.023,	4.194,	
            4.372,	4.532,	4.698,	4.870,	5.048,	5.206,	5.369,	5.537,	5.711,	6.024,	6.353,	6.701,	7.067,	7.381,	7.709,	8.051,	
            8.409,	8.727,	9.057,	9.400,	9.756,	10.291,	10.855,	11.450,	12.077,	12.710,	13.376,	14.077,	14.815,	15.440,	16.249,	17.100,	
            17.476,	18.538,	19.663,	20.857,	22.124,	23.338,	24.620,	25.971,	27.397,	28.902,	30.303,	31.646,	33.003,	34.364,	37.037,	39.682,	 
        };

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

        // The values here are 10-bit mantissas with an implied leading bit
        // this matches the internal format of the OPN chip, extracted from the die.
        // As a nod to performance, the implicit 0x400 bit is pre-incorporated, and
        // the values are left-shifted by 2 so that a simple right shift is all that
        // is needed; also the order is reversed to save a NOT on the input        
        static ushort P(ushort a) => (ushort)((a|0x400) << 2);
        public static readonly ushort[] s_power_table =
        {
            P(0x3fa),P(0x3f5),P(0x3ef),P(0x3ea),P(0x3e4),P(0x3df),P(0x3da),P(0x3d4),
            P(0x3cf),P(0x3c9),P(0x3c4),P(0x3bf),P(0x3b9),P(0x3b4),P(0x3ae),P(0x3a9),
            P(0x3a4),P(0x39f),P(0x399),P(0x394),P(0x38f),P(0x38a),P(0x384),P(0x37f),
            P(0x37a),P(0x375),P(0x370),P(0x36a),P(0x365),P(0x360),P(0x35b),P(0x356),
            P(0x351),P(0x34c),P(0x347),P(0x342),P(0x33d),P(0x338),P(0x333),P(0x32e),
            P(0x329),P(0x324),P(0x31f),P(0x31a),P(0x315),P(0x310),P(0x30b),P(0x306),
            P(0x302),P(0x2fd),P(0x2f8),P(0x2f3),P(0x2ee),P(0x2e9),P(0x2e5),P(0x2e0),
            P(0x2db),P(0x2d6),P(0x2d2),P(0x2cd),P(0x2c8),P(0x2c4),P(0x2bf),P(0x2ba),
            P(0x2b5),P(0x2b1),P(0x2ac),P(0x2a8),P(0x2a3),P(0x29e),P(0x29a),P(0x295),
            P(0x291),P(0x28c),P(0x288),P(0x283),P(0x27f),P(0x27a),P(0x276),P(0x271),
            P(0x26d),P(0x268),P(0x264),P(0x25f),P(0x25b),P(0x257),P(0x252),P(0x24e),
            P(0x249),P(0x245),P(0x241),P(0x23c),P(0x238),P(0x234),P(0x230),P(0x22b),
            P(0x227),P(0x223),P(0x21e),P(0x21a),P(0x216),P(0x212),P(0x20e),P(0x209),
            P(0x205),P(0x201),P(0x1fd),P(0x1f9),P(0x1f5),P(0x1f0),P(0x1ec),P(0x1e8),
            P(0x1e4),P(0x1e0),P(0x1dc),P(0x1d8),P(0x1d4),P(0x1d0),P(0x1cc),P(0x1c8),
            P(0x1c4),P(0x1c0),P(0x1bc),P(0x1b8),P(0x1b4),P(0x1b0),P(0x1ac),P(0x1a8),
            P(0x1a4),P(0x1a0),P(0x19c),P(0x199),P(0x195),P(0x191),P(0x18d),P(0x189),
            P(0x185),P(0x181),P(0x17e),P(0x17a),P(0x176),P(0x172),P(0x16f),P(0x16b),
            P(0x167),P(0x163),P(0x160),P(0x15c),P(0x158),P(0x154),P(0x151),P(0x14d),
            P(0x149),P(0x146),P(0x142),P(0x13e),P(0x13b),P(0x137),P(0x134),P(0x130),
            P(0x12c),P(0x129),P(0x125),P(0x122),P(0x11e),P(0x11b),P(0x117),P(0x114),
            P(0x110),P(0x10c),P(0x109),P(0x106),P(0x102),P(0x0ff),P(0x0fb),P(0x0f8),
            P(0x0f4),P(0x0f1),P(0x0ed),P(0x0ea),P(0x0e7),P(0x0e3),P(0x0e0),P(0x0dc),
            P(0x0d9),P(0x0d6),P(0x0d2),P(0x0cf),P(0x0cc),P(0x0c8),P(0x0c5),P(0x0c2),
            P(0x0be),P(0x0bb),P(0x0b8),P(0x0b5),P(0x0b1),P(0x0ae),P(0x0ab),P(0x0a8),
            P(0x0a4),P(0x0a1),P(0x09e),P(0x09b),P(0x098),P(0x094),P(0x091),P(0x08e),
            P(0x08b),P(0x088),P(0x085),P(0x082),P(0x07e),P(0x07b),P(0x078),P(0x075),
            P(0x072),P(0x06f),P(0x06c),P(0x069),P(0x066),P(0x063),P(0x060),P(0x05d),
            P(0x05a),P(0x057),P(0x054),P(0x051),P(0x04e),P(0x04b),P(0x048),P(0x045),
            P(0x042),P(0x03f),P(0x03c),P(0x039),P(0x036),P(0x033),P(0x030),P(0x02d),
            P(0x02a),P(0x028),P(0x025),P(0x022),P(0x01f),P(0x01c),P(0x019),P(0x016),
            P(0x014),P(0x011),P(0x00e),P(0x00b),P(0x008),P(0x006),P(0x003),P(0x000)
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort attenuation_to_volume(ushort input)  //FIXME: Values exceeding 14-bit maxvalue (0x1FFF, 8191) wrap around producing incorrect output
        {
            unchecked
            {
                // look up the fractional part, then shift by the whole
                // return (ushort)(((s_power_table[~input & 0xff] | (ushort)0x400) << 2) >> (input >> 8));
                return (ushort)( s_power_table[input & 0xff] >> (input >> 8) );
            }
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
