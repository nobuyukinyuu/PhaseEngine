using System;
using PhaseEngine;
using System.Runtime.CompilerServices;
// using Godot;

namespace PhaseEngine 
{
    internal static class Global
    {
        //Automagically generated string based on the most recent GitHub commit.  Can be attached to various things for debugging purposes.
        public const string BUILD_VERSION = "b8c4f91"; //Generated on 9 Nov 2022, 04:52 PM
        public const int FORMAT_VERSION = 10;  //Version number attached to JSON output of voices/instruments
        public const byte DEFAULT_SEED = 1;  //Default reset state for most noise generators; used when the phase is reset on an oscillator with sync enabled

        private static float mixRate = 48000;  //The native rate which Chips are expected to output audio
        public static float MixRate {get => mixRate;
            set
            {
                mixRate = value;
                fracMixRate = 1/mixRate;
                clockMult = 48000.0f/mixRate;
            }
        }

        public static float FracMixRate{get => fracMixRate;}
        static float fracMixRate = 1/MixRate;

        // Number of times clock should be updated per sample. Fixed-point frac balancing may be complicated here.
        // 32-bit precision drift over the course of 60 secs is about 42402 samples slow at 44100hz.
        // TODO:  Speed tests using double-precision clocking to determine how much slower it would be (how much more strain on CPU).
        // Clock multipliers for some different chips are provided for compatibility purposes when translating envelope timings.
        public static float ClockMult{get => clockMult;} 
        static float clockMult = 48000.0f/MixRate; 

        // // Number of clock cycles needed for each clock of PhaseEngine to have compatible timing
        // public static double ClockMult_OPN = 53267.0/MixRate*ClockMult;  
        // public static double ClockMult_OPM = 55930.0/MixRate*ClockMult;  


        //Base hz used for frequency increment calculations
        public const double BASE_HZ = 440;
        public const double BASE_MULT = 1 / BASE_HZ;


        //Precision spec values for the fixed-point phase accumulator
        public const byte FRAC_PRECISION_BITS = 20;
        public const ulong FRAC_SIZE = (1 << FRAC_PRECISION_BITS) - 1;
        public const double ONE_PER_FRAC_SIZE = 1.0 / FRAC_SIZE;


        public static readonly byte[] multTable = {1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 20, 24, 24, 30, 30};


        //MIDI note special designations
        public const byte NO_NOTE_SPECIFIED = 0xFF;
        public const byte FIXED_NOTE_SPECIFIED = 0xFE;


        public const float NORMALIZED_EPSILON = Single.Epsilon * 0x800000;  //The smallest number that represents a non-subnormal float value.


        //Global Event ID counter
        static long eventID=0;
        public static long EventID { get => eventID; }
        public static long NewEventID()
        {
            unchecked { System.Threading.Interlocked.Increment(ref eventID); }
            return eventID;
        }

    }

    public enum BusyState{BUSY=128, RELEASED=512, FREE=1024} //Order of BusyState in increasing yoink priority 
    [Flags] public enum BindTypes {None=0, TrackerEnvelope=1, KeyFollow=2, VelocityTable=4}  //Types of binds available or specified


    static internal class XorShift64Star
    {
        const double REAL_UNIT = 1.0/((double)int.MaxValue+1.0);        

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static uint rotl(uint x, int k) 
        {
            return unchecked((x << k) | (x >> (32 - k)));
        }

        static XorShift64Star()
        {
            Seed();  Next(); Next();  //Seed and fill the buffer
        }

        static void Seed()  { s[0] = unchecked((uint)Environment.TickCount); }  //Does System.DateTime.Now.Ticks have more entropy?

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

        public static double NextDouble(){
            return REAL_UNIT*(int)(0x7FFFFFFF & Next());
        }
        public static double CurrentDouble(){
            return REAL_UNIT*(int)(0x7FFFFFFF & Current());
        }

    }


}
