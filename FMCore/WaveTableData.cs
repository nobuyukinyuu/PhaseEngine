using System;
using PhaseEngine;

namespace PhaseEngine 
{
    public class WaveTableData
    {
        const ushort TBL_SIZE=256;
        short[] tbl = Tables.defaultWavetable;

        bool NotInUse {get => tbl==Tables.defaultWavetable;}
        public WaveTableData()    {}
        public WaveTableData(short[] input) { tbl = input; }

        #if GODOT
            public WaveTableData(Godot.Collections.Array input)
            {
                //Convert godot table to short array
                var output= new short[TBL_SIZE];
                for (int i=0; i<TBL_SIZE; i++)
                {
                    output[i] = (short) input[i];
                }
                tbl = output;
            }
        #endif 

        ///Summary:  Quantizes the values of the table to correspond to the total number of samples divided by the amount.
        public void Quantize(int amt)
        {
            if (amt<=1 || NotInUse) return;

            for (int i=0; i<TBL_SIZE; i+=amt)
            {
                short val=tbl[i];  //Get Quantize value for this portion.

                //Set the next n indices to the value.
                for (int j=0; j<amt && (i+j<TBL_SIZE); j++)
                {
                    tbl[i+j] = val;
                }
            }
        }

        //Summary:  Crushes the values of the table corresponding to the amount to shift right.
        public void Crush(int amt)
        {
            if (amt<=0 || NotInUse) return;

            for (int i=0; i<TBL_SIZE; i++)
                tbl[i] >>= amt;
        }


    }
}
