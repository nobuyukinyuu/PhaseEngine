using System;
using gdsFM;

namespace gdsFM 
{
    /// Summary:  Tracks the wiring of an FM algorithm.
    public class Algorithm
    {
        public byte opCount = 6;

        public byte[] processOrder;  //The processing order of the operators.  This should be able to be fetched from wiring grid, or a convenience func in Voice...
        public byte[] connections;  //Connections for each operator.  Stored as a bitmask.  NOTE:  If we have more than 8 ops, this won't work....


        public string wiringGrid; //FIXME:  Change to something sane

        public Algorithm()    {Reset();}
        public Algorithm(byte opCount)    {this.opCount = opCount;  Reset();}
        void Reset()
        {
            processOrder = new byte[opCount];
            connections = new byte[opCount];
        }


        public static Algorithm FromPreset(byte preset, bool useSix=false)
        {
            var length = useSix? (byte)6 : (byte)4;
            var presets = useSix?  dx_presets : reface_presets;
            var output = new Algorithm(length);
            
            for(byte i=0; i<length; i++)     output.processOrder[i] = i;            
            Array.Copy(presets[preset], output.connections, length);

            return output;
        }

        //Info on preset algorithms adapted from:  https://gist.github.com/bryc/e997954473940ad97a825da4e7a496fa
        //Operator 1 (the first operator to be processed) never has anything connected to it. If we specified 1 here, it would get processed as an infinite loop.
        //Processing order is always done from the first operator to the last.  Connections to 0 are assumed to be connected to output.
        public static readonly byte[][] reface_presets = {
            Preset(2,3,4,0),    Preset(3,3,4,0),    Preset(2,4,4,0),    Preset(Multi(2,3), 4, 4, 0),
            Preset(4,4,4,0),    Preset(2,3,0,0),    Preset(2, Multi(3,4), 0, 0),    Preset(3,0,4,0),
            Preset(Multi(2,3,4), 0,0,0),    Preset(Multi(3,4), 0,0,0),    Preset(4,0,0,0),    Preset(0,0,0,0),
        };

        public static readonly byte[][] dx_presets = {
            Preset(2,4,5,6,0,0),   Preset(2,4,5,6,0,0),   Preset(3,4,5,6,0,0),   Preset(3,4,5,6,0,0),   // 0-3
            Preset(4,5,6,0,0,0),   Preset(4,5,6,0,0,0),   Preset(4,5,6,6,0,0),   Preset(4,5,6,6,0,0),   // 4-7
            Preset(4,5,6,6,0,0),   Preset(4,5,5,6,0,0),   Preset(4,5,5,6,0,0),   Preset(5,5,5,6,0,0),   // 8-11
            Preset(5,5,5,6,0,0),   Preset(4,4,5,6,0,0),   Preset(4,4,5,6,0,0),   Preset(4,5,6,6,6,0),   // 12-15
            Preset(4,5,6,6,6,0),   Preset(2,5,6,6,6,0),   Preset(2,4,Multi(5,6),0,0,0),   Preset(Multi(4,5),6,6,0,0,0),   // 16-19
            Preset(Multi(3,4), Multi(5,6), 0,0,0,0),   Preset(3,Multi(4,5,6), 0,0,0,0),   Preset(4, Multi(5,6), 0,0,0,0),   Preset(Multi(4,5,6),0,0,0,0,0),   // 20-23
            Preset(Multi(5,6), 0,0,0,0,0),   Preset(5,6,6,0,0,0),   Preset(5,6,6,0,0,0),   Preset(3,4,5,0,0,0),   // 24-27
            Preset(5,6,0,0,0,0),   Preset(2,5,0,0,0,0),   Preset(6,0,0,0,0,0),   Preset(0,0,0,0,0,0),   // 28-31
        };

        //Produces an operator's connection mask for a given series of inputs.  Output is negated to flag the preset function to process it.
        static short Multi(params byte[] input) {
            int output = 0;
            for(int i=0; i<input.Length; i++)
                output |= (1<<input[i]);

            return (short) -output;
        }
        static byte[] Preset(params short[] input) {  //Produces an array of connections.
            byte[] output = new byte[input.Length];

            for(int i=0; i<input.Length; i++)
            {
                switch ( Math.Sign(input[i]) )
                {
                    case 1: //Value is a single connection.  Process mask.
                        output[i] = (byte)(1 << (input[i]-1));  break;
                    case -1:  //Value is an already-processed multi connection. Negate.
                        output[i] = unchecked( (byte)-input[i] );  break;
                    case 0:  //No connections.  Operator connects to output;
                        output[i] = 0;  break;
                }
            }
            return output;
        }
    }
}
