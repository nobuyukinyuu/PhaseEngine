using System;
using gdsFM;
using System.Runtime.CompilerServices;

namespace gdsFM 
{
    /// Summary:  Tracks the wiring of an FM algorithm.
    public class Algorithm
    {
        public byte opCount = 6;

        public OpBase.Intents[] intent;  //What type of operator is this?  (See OpBase.Intents)
        public byte[] processOrder;  //The processing order of the operators.  This should be able to be fetched from wiring grid, or a convenience func in Voice...
        public byte[] connections;  //Connections for each operator.  Stored as a bitmask.  NOTE:  If we have more than 8 ops, this won't work....
        public byte NumberOfConnectionsToOutput { get {
                byte output=0;
                for(byte i=0; i<connections.Length; i++)
                    if(connections[i]==0) output++;
                return output;
        }   }

        public static readonly byte[] DEFAULT_PROCESS_ORDER = {0,1,2,3,4,5,6,7};

        public byte[] wiringGrid;  //Description of where each operator goes on a wiring grid, in format [x1, y1 ... xn, yn] where n==opCount
        

        public Algorithm()    {Reset(true);}
        public Algorithm(byte opCount)    {this.opCount = opCount;  Reset(true);}
        void Reset(bool hard_init=false)
        {
            Array.Resize(ref intent, opCount);
            if(hard_init) Array.Fill(intent, OpBase.Intents.FM_OP);
            processOrder = DefaultProcessOrder(opCount);
            connections = new byte[opCount];
            wiringGrid = DefaultWiringGrid(opCount);  //FIXME:  Determine default wiring grid for the number of operators
        }
        public void SetOpCount(byte opTarget)
        {
            //If the op size is smaller than before, break the algorithm back to the default.
            //This is necessary in case any one connection relies on another which is deleted.
            if (opTarget<opCount)
            {
                opCount = opTarget;
                Reset();
            } else {
                Array.Resize(ref intent, opTarget);
                Array.Resize(ref connections, opTarget);
                Array.Resize(ref processOrder, opTarget);
                for (byte i=opCount; i<opTarget; i++)
                {
                    processOrder[i] = i;
                    intent[i] = OpBase.Intents.FM_OP;
                }

                opCount = opTarget;
            }
        }

        //Sets the intent of a specific operator.  DOES NOT update channels.  Do this from Chip.
        public void SetIntent(byte opTarget, OpBase.Intents intent)  { this.intent[opTarget] = intent; }

        /// Returns an array of the default process order for a given size op count.
        public static byte[] DefaultProcessOrder(byte opCount)
        {
            var output = new byte[opCount];
            Array.Copy(DEFAULT_PROCESS_ORDER, output, opCount);
            return output;
        }

        public static byte[] DefaultWiringGrid(byte opCount)
        {
            var output = new byte[opCount];
            for (byte i=0; i<opCount; i++)   output[i] = g2b(i, (byte)(opCount -1));
            return output;
        }

        public static Algorithm FromPreset(byte preset, bool useSix=false)
        {
            var length = useSix? (byte)6 : (byte)4;
            var presets = useSix?  dx_presets : reface_presets;
            var output = new Algorithm(length);
            
            for(byte i=0; i<length; i++)     output.processOrder[i] = i;            
            Array.Copy(presets[preset], output.connections, length);
            Array.Fill(output.intent, OpBase.Intents.FM_OP);

            return output;
        }


        //TODO:  Use this function when changing opCount to determine how to rearrange algorithms that had their operator count reduced.
        //      ANY operator connected to a value higher than opCount needs its connections reset and put at the end of processOrder.

        /// summary:  Checks an operator for invalid connections.  Any connections made to operators greater than current opCount will result in null access...
        public bool ConnectionsOK(byte opTarget)
        {
            var invalid_connections = connections[opTarget] >> opCount;
            return invalid_connections == 0;
        }

        /// summary:  Will mask out any connections considered invalid to this algorithm.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FixConnections(byte opTarget)  { connections[opTarget] &= Tools.unsigned_bitmask(opCount); }
        public void FixConnections()  { for(byte i=0; i<opCount; i++)  FixConnections(i); }


#region presets
        //Info on preset algorithms adapted from:  https://gist.github.com/bryc/e997954473940ad97a825da4e7a496fa
        //Operator 1 (the first operator to be processed) never has anything connected to it. If we specified 1 here, it would get processed as an infinite loop.
        //Processing order is always done from the first operator to the last.  Connections to 0 are assumed to be connected to output.
        public static readonly byte[][] reface_presets = {
            Preset(2,3,4,0),    Preset(3,3,4,0),    Preset(2,4,4,0),    Preset(Multi(2,3), 4, 4, 0),
            Preset(4,4,4,0),    Preset(2,3,0,0),    Preset(2, Multi(3,4), 0, 0),    Preset(3,4,0,0),
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
                output |= ( 1 << (input[i]-1) );

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

        static byte g2b(byte x, byte y) { return (byte)((y << 4) | x); }  //Converts a 4-bit x and y grid position (0-F) to an 8 bit value.
    }
#endregion
}
