using System;
using gdsFM;

namespace gdsFM 
{
    public class Channel
    {
        public bool busy;
        public Operator[] ops;

        byte[] processOrder;  //The processing order of the operators.  This should be able to be fetched from wiring grid, or a convenience func in Voice...
        byte[] connections;  //Connections for each operator.  Stored as a bitmask.  NOTE:  If we have more than 8 ops, this won't work....
        int[] cache;  //As each level of the stack gets processed, the sample cache for each operator is updated.

        public Channel(byte opCount)
        {
            ops = new Operator[opCount];
            processOrder = new byte[opCount];
            connections = new byte[opCount];
        }

        public void Clock()
        {
            for (byte i=0; i<ops.Length; i++)
            {
                ops[i].Clock();
            }
        }

        /// Main algorithm processor.
        //  TODO:  Pass down LFO status from the chip.   Reset the operator caches to 0 on NoteOn!
        public short RequestSample()
        {
            int output = 0;

            //For each height level in the stack, look for operators to mix down.
            //TODO:  Consider instead bringing in an ordered array with each operator from the top row down and rely solely on this (and the frontend) to keep algorithm sane.
            for (byte o=0; o<processOrder.Length;  o++)
            {
                var src_op = processOrder[o];  //Source op number.
                int c = connections[ src_op ];  //Connections for the source operator.

                //First, convert the source op's accumulated phase up to this point into its sample result value.
                //For termini (top of an op stack), the phase accumulated in the cache will always be 0.
                cache[src_op] = ops[src_op].RequestSample( (ushort)cache[src_op] );

                if (c==0)  //Source op isn't connected to anything, so we assume it's connected to output.
                {
                    output += cache[src_op];  //Accumulate total.
                    continue;
                }

                //Source op has connections. Mix down the sample results to its connections.
                for(byte dest_op=0; c>0; dest_op++)  //Crunch the connection bitmask down, one bit at a time, until there's no more connections.
                {
                    unchecked 
                    {
                        if ( (c & 1) == 1)  //Flag is set. The destination op receives modulation from the source op and is added to the total output cache for that operator.
                        {
                            cache[dest_op] += cache[src_op];
                        }
                        c >>= 1; //Crunch down and process next position. Loop continues until the connection mask has no more connections.
                    }
                }

            }     
            return (short)output.Clamp(short.MinValue, short.MaxValue);
        }


    }

}
