using System;
using PhaseEngine;
using System.Runtime.CompilerServices;
using PE_Json;

namespace PhaseEngine 
{
    /// Summary:  Tracks the wiring of an FM algorithm.
    public class Algorithm
    {
        public byte opCount = 6;

        public OpBase.Intents[] intent;  //What type of operator is this?  (See OpBase.Intents)
        public static readonly byte[] DEFAULT_PROCESS_ORDER = {0,1,2,3,4,5,6,7};
        public byte[] processOrder;  //The processing order of the operators.  This should be able to be fetched from wiring grid, or a convenience func in Voice...
        public byte[] connections;  //Connections for each operator.  Stored as a bitmask.  NOTE:  If we have more than 8 ops, this won't work....
        public byte[] wiringGrid;  //Description of where each operator goes on a wiring grid, in format [y1|x1, ..., yn|xn] where n==opCount
        

        public byte NumberOfConnectionsToOutput { get {
                byte output=0;
                for(byte i=0; i<connections.Length; i++)
                    if(connections[i]==0) output++;
                return output;
        }   }


        public Algorithm()    {Reset(true);}
        public Algorithm(byte opCount)    {this.opCount = opCount;  Reset(true);}
        void Reset(bool hard_init=false)
        {
            Array.Resize(ref intent, opCount);
            if(hard_init) 
            {
                Array.Fill(intent, OpBase.Intents.FM_OP);               
                processOrder = DefaultProcessOrder(opCount);
                connections = new byte[opCount];
                wiringGrid = DefaultWiringGrid(opCount);
            } else {
                Array.Resize(ref processOrder, opCount);
                Array.Resize(ref connections, opCount);
                Array.Resize(ref wiringGrid, opCount);
            } 
        }
        public void SetOpCount(byte opTarget)
        {
            if (opTarget == opCount) return;  //Stops unnecessary processing when Voice calls this method after deserializing an algorithm and setting its own count.

            //If the op size is smaller than before, break the algorithm apart.
            //This is necessary in case any one connection relies on another which is deleted.
            if (opTarget<opCount)
            {
                //First, find bad connections and remove them.
                var badConnections = false;
                for(int src_op=0; src_op<opTarget;  src_op++)
                {
                    var c=connections[src_op];
                    for(int missingOp=opTarget; missingOp<opCount; missingOp++)
                    {
                        if ( (c>>missingOp & 1) == 1 ) //The src op has a bad connection.  Fix it.
                        {
                            connections[src_op] &= (byte)~(1<<missingOp);  //Turn off the offending bit.
                            badConnections = true;
                        }
                    }
                }

                opCount = opTarget;
                Reset(false); //Soft reset by resizing all of the arrays to the target.  

                //If any op had a bad connection, we need to move them.  Going with the former processOrder in reverse,
                //We assign a free slot for every operator, skipping over the missing ops.
                if(badConnections)
                {
                    // var opsToProcess = new byte[opCount];
                    // Array.Copy(processOrder, opsToProcess, opCount);
                    // Array.Reverse(opsToProcess);
                    // var hops = new byte[opTarget];  //Safe Y position of each operator.  Indexed from 1.

                    //We'll need to recreate the wiringGrid and processOrder.
                    wiringGrid = FabricateGrid();
                } else {
                    //No bad connections, but we need to shift up all the remaining operators on the wiring grid anyway to represent the new lower bounds.
                    for(int i=0; i<opCount; i++)
                    {
                        var X = wiringGrid[i] & 0xF;
                        var Y = (wiringGrid[i] >> 4) - 1;
                        wiringGrid[i] = (byte)(Y << 4 | X);
                    }
                }

                //Finally, restore the process order based on the grid positions assigned.
                processOrder = DefaultProcessOrder(opTarget);
                Array.Sort(processOrder, CompareByGridPos);

            } else {  //opTarget >= opCount
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

        //Sets the intent of a specific operator.  DOES NOT update channels.  Do that from Chip.
        public void SetIntent(byte opTarget, OpBase.Intents intent)  { this.intent[opTarget] = intent; }

        /// Returns an array of the default process order for a given size op count.
        public static byte[] DefaultProcessOrder(byte opCount)
        {
            var output = new byte[opCount];
            Array.Copy(DEFAULT_PROCESS_ORDER, output, opCount);
            return output;
        }


        /// summary:  Returns an array of bytes representing the grid position of every operator.  4 high bits represent Y, low bits represent X.
        public static byte[] DefaultWiringGrid(byte opCount)
        {
            var output = new byte[opCount];
            //For each operator, set the x/y value to (opNum, grid height).
            for (byte i=0; i<opCount; i++)   output[i] = g2b(i, (byte)(opCount -1));
            return output;
        }

        /// summary:  Creates a grid from the existing connections.
        public byte[] FabricateGrid()
        {
            System.Diagnostics.Debug.Assert(opCount!=0);
            var output= new byte[opCount];

            var hops = new byte[opCount];  //Safe Y position of each operator.  Indexed from 1.

            for(byte i=0; i<opCount; i++)
                hops[i] = MaxHopsToOutput(i, hops);  

            //Now we need to assign grid spaces for each operator based on their levels.  Remember that hops > 0 and carriers start at y-pos opCount-1.
            var freeslot = new byte[opCount];

            for(byte i=0; i<opCount; i++)
            {
                var Y = opCount - hops[i];
                var X = freeslot[Y];
                freeslot[Y]++;  //Indicate next free slot for this level.

                output[i] = (byte)(Y<<4 | X);
            }

            return output;
        }

        //Optimized(?) recursive algorithm checking hops of an operator to output.  Values of 0 are considered uninitialized; all ops must make at least 1 hop.
        //This is used to determine a safe level (y-pos) on the wiring grid to place an op.
        byte MaxHopsToOutput(byte opNum, byte[] hops)
        {
            if (hops[opNum] > 0) return hops[opNum];  //Exit early if we already checked hops for a previous op. FIXME: Check if this wrecks multi-connections
            var c = connections[opNum];
            if (c==0)
            {
                hops[opNum] = 1;
                return 1;
            } else {   //Op has connections.  Find the connection with the largest number of hops.
                var maxHops = 0;
                for(byte dest_op=0; c>0; dest_op++)
                {
                    if ((c&1) == 1)  //Connection here.  Recurse to get max hops.
                        maxHops = Math.Max(maxHops, MaxHopsToOutput(dest_op, hops));
                    c >>= 1;  //Crunch down the connections and check next op.
                }
                hops[opNum] = (byte)++maxHops;  //All connections explored.  Return the max number of hops to output plus ourself.
                return hops[opNum];
            }
        }

        /// summary: Compares the encoded positions of two ops in the wiring grid. Returns the value higher on the process order.
        protected int CompareByGridPos(byte A, byte B) { if (wiringGrid[A]==wiringGrid[B]) return 0;  else return wiringGrid[A]>wiringGrid[B]? 1 : -1;}

        /// summary:  Checks this algorithm's wiring grid for a free slot on the specified row. Will consider ops marked src_op as free.
        public int FreeSlot(byte y, byte startFrom=0, byte src_op=0xFF)
        {
            var row = new int[opCount];
            Array.Fill(row, -1);
            for(int i=0; i<opCount; i++)  //Populate a row to check.
            {
                int yPos, xPos;
                xPos = wiringGrid[i] & 0xF;
                yPos = wiringGrid[i] >>  4;

                if (yPos==y && i!=src_op)
                    row[xPos] = i;
            }

            for(int x=startFrom; x<opCount; x++) {
                if (row[x] != -1) continue;
                return y<<4 | x;  // 0bYYYYXXXX encoded grid position
            }                   
            //Uh-oh.  Couldn't find a spot to the right of the operator.  Let's look to the left.
            for(int x=startFrom; x>-1; x--) {
                if (row[x] != -1) continue;
                return y<<4 | x;  // 0bYYYYXXXX encoded grid position
            }
            #if DEBUG
                throw new ArgumentException(String.Format("No free slot found at {0}!", y));
            #else
                return -1;
            #endif            
        }


        public static Algorithm FromPreset(byte preset, PresetType type)
        {
            byte length;
            byte[][] presets;

            switch(type) {
                case PresetType.DX:
                    presets = dx_presets;
                    length = 6;
                    break;
                case PresetType.Reface:
                    presets = reface_presets;
                    length = 4;
                    break;
                case PresetType.OPL:
                    presets= opl_4op_presets;
                    length = 4;
                    break;
                default:
                    presets = ym2xxx_presets;
                    length = 4;
                    break;
            }

            System.Diagnostics.Debug.Assert(preset>=0 && preset < presets.Length, 
                    String.Format("Algorithm preset {0} outside expected range of {2}'s {1} presets!", preset, presets.Length, type.ToString()));

            var output = new Algorithm(length);
            
            for(byte i=0; i<length; i++)     output.processOrder[i] = i;            
            Array.Copy(presets[preset], output.connections, length);
            Array.Fill(output.intent, OpBase.Intents.FM_OP);
            output.wiringGrid = output.FabricateGrid();

            return output;
        }

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


        // public bool FromString

        public void FromJSON(JSONObject data, bool fabricateGrid=false, bool reinit=false)
        {
            try
            {
                opCount = (byte) data.GetItem("opCount", opCount);
                Reset(reinit);

                var c = data.GetItem<byte>("connections", null); 
                if (c!=null) connections = c;  //If no connections are specified then default to behavior specified by the reinit arg

                //Check if the wiring grid exists.  If the grid doesn't exist, fabricate one.
                if (!data.HasItem("grid")) wiringGrid = FabricateGrid();
                else wiringGrid = data.GetItem<byte>("grid", Algorithm.DefaultWiringGrid(opCount) );

                processOrder = data.GetItem<byte>("processOrder", Algorithm.DefaultProcessOrder(opCount) );

                if (data.HasItem("intent"))
                {
                    string[] new_intents = data.GetItem("intent", new string[opCount]);
                    for(int i=0; i<opCount; i++)  intent[i] = (OpBase.Intents) Enum.Parse(typeof(OpBase.Intents), new_intents[i]);
                } else {
                    //No intents?  Reset all intents to default.  The only reason to omit intents now is to save space for traditional algorithms.
                    for(int i=0; i<opCount; i++)  intent[i] = OpBase.Intents.FM_OP;
                }

                // j.Assign("increment_offset", ref o.increment_offset);

            } catch (Exception e) {
                System.Diagnostics.Debug.Fail("Algorithm.FromJSON failed:  " + e.Message);
            }

        }

        public string ToJSONString() { return ToJSONObject().ToJSONString(); }
        internal JSONObject ToJSONObject()
        {
            var o=new JSONObject();
            o.AddPrim("opCount", opCount);

            for(byte i=0; i<opCount; i++)
                if (intent[i] != OpBase.Intents.FM_OP)  //Don't bother writing intents if there's no custom operator types.
                {
                    //FIXME:  Figure out if the generic func can be resolved for the enum-specific condition without the extra argument
                    o.AddPrim("intent", intent, true);  
                    break;
                }

            o.AddPrim("grid", wiringGrid);
            o.AddPrim("processOrder", processOrder);
            o.AddPrim("connections", connections);

            return o;
        }

#region presets
        //Info on preset algorithms adapted from:  https://gist.github.com/bryc/e997954473940ad97a825da4e7a496fa
        //Operator 1 (the first operator to be processed) never has anything connected to it. If we specified 1 here, it would get processed as an infinite loop.
        //Processing order is always done from the first operator to the last.  Connections to 0 are assumed to be connected to output.
        public enum PresetType {Reface=4, DX=6, OPL=18, OPM=20, OPN=20}

        public static readonly byte[][] reface_presets = {  //New 4-op presets
            Preset(2,3,4,0),    Preset(3,3,4,0),    Preset(2,4,4,0),    Preset(Multi(2,3), 4, 4, 0),
            Preset(4,4,4,0),    Preset(2,3,0,0),    Preset(2, Multi(3,4), 0, 0),    Preset(3,4,0,0),
            Preset(Multi(2,3,4), 0,0,0),    Preset(Multi(3,4), 0,0,0),    Preset(4,0,0,0),    Preset(0,0,0,0),
        };

        public static readonly byte[][] dx_presets = {  //The classic 32 6-op presets.  Sy77 and FS1R compatible
            Preset(2,4,5,6,0,0),   Preset(2,4,5,6,0,0),   Preset(3,4,5,6,0,0),   Preset(3,4,5,6,0,0),   // 0-3
            Preset(4,5,6,0,0,0),   Preset(4,5,6,0,0,0),   Preset(4,5,6,6,0,0),   Preset(4,5,6,6,0,0),   // 4-7
            Preset(4,5,6,6,0,0),   Preset(4,5,5,6,0,0),   Preset(4,5,5,6,0,0),   Preset(5,5,5,6,0,0),   // 8-11
            Preset(5,5,5,6,0,0),   Preset(4,4,5,6,0,0),   Preset(4,4,5,6,0,0),   Preset(4,5,6,6,6,0),   // 12-15
            Preset(4,5,6,6,6,0),   Preset(2,5,6,6,6,0),   Preset(2,4,Multi(5,6),0,0,0),   Preset(Multi(4,5),6,6,0,0,0),   // 16-19
            Preset(Multi(3,4), Multi(5,6), 0,0,0,0),   Preset(3,Multi(4,5,6), 0,0,0,0),   Preset(4, Multi(5,6), 0,0,0,0),   Preset(Multi(4,5,6),0,0,0,0,0),   // 20-23
            Preset(Multi(5,6), 0,0,0,0,0),   Preset(5,6,6,0,0,0),   Preset(5,6,6,0,0,0),   Preset(3,4,5,0,0,0),   // 24-27
            Preset(5,6,0,0,0,0),   Preset(2,5,0,0,0,0),   Preset(6,0,0,0,0,0),   Preset(0,0,0,0,0,0),   // 28-31
        };

        public static readonly byte[][] ym2xxx_presets = {  //Most vintage 4-op chips use these algorithm presets.  Use for loading legacy FM formats.
            Preset(2,3,4,0),    Preset(3,3,4,0),    Preset(4,3,4,0),    Preset(2,4,4,0),
            Preset(2,0,4,0),    Preset(Multi(2,3,4),0,0,0),    Preset(2,0,0,0),    Preset(0,0,0,0),
        };

        public static readonly byte[][] opl_4op_presets = { Preset(2,3,4,0), Preset(2,0,4,0), Preset(0,3,4,0), Preset(0,3,0,0) };

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
