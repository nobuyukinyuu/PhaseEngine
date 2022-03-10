using System;
using PhaseEngine;
using System.Collections.Generic;

namespace PhaseEngine 
{
    public class WaveTableData
    {
        internal const byte TBL_BITS=8;  //Can't be over 10
        internal const ushort TBL_SIZE= 1<<TBL_BITS;  //Must be a power of 2
        List<short[]> tbl;

        internal bool NotInUse {get => tbl==null || tbl.Count==0;}
        public int NumBanks {get => tbl==null? -1 : tbl.Count;}
        public WaveTableData()    {}
        public WaveTableData(uint numBanks)    {tbl= Tools.InitList(numBanks, new short[TBL_SIZE]);}
        public WaveTableData(short[] input) { tbl = new List<short[]>(); tbl.Add(input);}
        public WaveTableData(uint numBanks, short[] input) { tbl = Tools.InitList(numBanks, input); }

        #if GODOT
            public WaveTableData(uint numBanks, Godot.Collections.Array input)
            {
                var output= new short[TBL_SIZE];
                for (int i=0; i<TBL_SIZE; i++)
                    output[i] = (short) input[i];
                tbl = Tools.InitList(numBanks, output);
            }
            public void SetBank(int bank, Godot.Collections.Array input)
            {
                // //Convert godot table to short array
                // var output= new short[TBL_SIZE];
                // for (int i=0; i<TBL_SIZE; i++)
                //     output[i] = (short) input[i];
                tbl[bank] = (short[]) Convert.ChangeType(input, typeof(short[]));
            }

            public void SetTable(int bank, int index, Godot.Collections.Array array)
            {
                short[] row = tbl?[bank];
                if(row != null) 
                    for(int i=0; i<array.Count; i++)
                        SetTable(bank, index, (short) array[i]);
            }
        #endif 

        public void AddBank()
        {
            if(tbl==null) tbl = new List<short[]>();
            tbl.Add(new short[TBL_SIZE]);
        }
        public void RemoveBank(int idx)  { tbl.RemoveAt(idx); }

        public void SetTable(int bank, short[] array)
        {
            short[] row = tbl?[bank];
            if(row != null) tbl[bank] = array;
        }
        public void SetTable(int bank, int index, short value)
        {
            short[] row = tbl?[bank];
            if(row != null) tbl[bank][index] = value;
        }
        public short[] GetTable(int bank)
        {
            if (NotInUse) return Tables.defaultWavetable;            
            try { return tbl[bank]; } catch { return Tables.defaultWavetable; }
        }

        /// Summary:  Quantizes the values of the table to correspond to the total number of samples divided by the amount.
        public void Quantize(int bank, int amt)
        {
            if (amt<=1 || NotInUse) return;

            for (int i=0; i<TBL_SIZE; i+=amt)
            {
                short val=tbl[bank][i];  //Get Quantize value for this portion.

                //Set the next n indices to the value.
                for (int j=0; j<amt && (i+j<TBL_SIZE); j++)
                {
                    tbl[bank][i+j] = val;
                }
            }
        }

        /// Summary:  Crushes the values of the table corresponding to the amount to shift right.
        public void Crush(int bank, int amt)
        {
            if (amt<=0 || NotInUse) return;

            for (int i=0; i<TBL_SIZE; i++)
                tbl[bank][i] >>= amt;
        }


    }
}
