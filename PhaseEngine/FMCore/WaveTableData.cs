using System;
using PhaseEngine;
using System.Collections.Generic;

#if GODOT
using Godot;
#endif

namespace PhaseEngine 
{
    public class WaveTableData
    {
        internal const byte TBL_BITS=10;  //Can't be over 10
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

        public string TableAsString(int bank=0)
        {
            var arr = tbl?[bank];
            if (arr==null) return "";

            //  Wavetable banks are compressed using a few techniques. Each bank is encoded as a delta waveform which is then DEFLATEd and encoded in Z85.
            //  Variable bank sizes:  Use a lookup table to find bit width for each bank, then add to a bankWidths field.  The field should have a format
            //  Where if the first value is 0, the next index is the width for ALL. 
            //  Otherwise we will iterate each 4 bits to determine width of each bank and number of banks.
            //  number 1 starts at 4 bits and so on, with top number being 7 for 10 bits. Encode 2 banks per byte, padding the low bits if necessary.
            //  Then Z85 encode it.  If last 4 bits are 0 again, we know to reduce the number of target banks by 1.

            //  The wavetable data itself should be concat'd from all banks into one large array so that the compression method used is the most efficient.
            //  Then we can use the decoded lengths from earlier to determine the size of each table


            //To get optimal compression size, first we reinterpret the data as deltas.  The first entry in the array is the index (doesn't change)
            var input = new short[arr.Length];
            input[0] = arr[0];
            for (int i=1; i<arr.Length; i++)
                input[i] = (short)(arr[i] - arr[i-1]);


            var input2 = Z85.ShortsToBytes(input);  //Convert our delta bank into a bytestream.
            GD.PrintS("uncompressed", input2.Length);
            input2 = Glue.Deflate(input2, System.IO.Compression.CompressionMode.Compress);
            GD.PrintS("compressed", input2.Length);

            var padAmt = input2.Length % 4;
            if (padAmt > 0)
            {   //Pad the compressed array to a 4 byte multiple to meet Z85 spec
                Array.Resize(ref input2, input2.Length + (4-padAmt) );
            }

            //Finally, convert the data to a Z85-encoded string.
            var output = Z85.Encode(input2);
            return output;
        }

        //DEBUG:  Convert a compressed string back into a table.  Currently assumes only one bank is in the compressed string.
        public short[] DeflatedZ85ToTable(string input)
        {
            //Do the encoding process in reverse.  TODO
            var decoded = Z85.Decode(input);
            var uncompressed = Glue.Deflate(decoded);
            
            //Each value in the bytestream represents a delta coded value.  Translate.
            var output = new short[uncompressed.Length];
            output[0] = uncompressed[0];
            for (int i=1; i<output.Length; i++)
                output[i] = (short) (output[i-1] + uncompressed[i]);

            return output;
        }


    }
}
