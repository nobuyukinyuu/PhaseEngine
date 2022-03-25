using System;
using PhaseEngine;
using System.Collections.Generic;
using PE_Json;

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
                var output= new short[input.Count];
                for (int i=0; i<input.Count; i++)
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

        public void AddBank(byte sampleWidthInBits=10)
        {
            if(tbl==null) tbl = new List<short[]>();
            tbl.Add(new short[ 1<<sampleWidthInBits ]);
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

            for (int i=0; i<tbl[bank].Length; i+=amt)
            {
                short val=tbl[bank][i];  //Get Quantize value for this portion.

                //Set the next n indices to the value.
                for (int j=0; j<amt && (i+j<tbl[bank].Length); j++)
                {
                    tbl[bank][i+j] = val;
                }
            }
        }

        /// Summary:  Crushes the values of the table corresponding to the amount to shift right.
        public void Crush(int bank, int amt)
        {
            if (amt<=0 || NotInUse) return;

            for (int i=0; i<tbl[bank].Length; i++)
                tbl[bank][i] >>= amt;
        }


        ///////////////////////////////////////////////  IO  ///////////////////////////////////////////////

        public bool FromString(string input)
        {
            var P = JSONData.ReadJSON(input);
            if (P is JSONDataError) return false;
            var j = (JSONObject) P;
            return FromJSON(j);
        }
        public bool FromJSON(JSONObject input)
        {
            var j=input;
            try
            {  //Test for empty object.  If so, don't do anything.
                if (input.Names().Count==0) return false;
                if (!input.HasItem("data")) throw new ArgumentException("Wavetable has no data.", "input[data]");
                
                //First, we must retrieve all of the table data from the input string. All samples are in one array, so afterward we must recreate the table.
                var data = DeflatedZ85ToTable( input.GetItem("data").ToString() );

                //The bankWidths value determines the size of each sample.  If its length is 1 then every sample has the same size.
                var numBanks = input.GetItem("size", 0);
                int[] bankWidths = ((JSONArray) input.GetItem("bankWidths")).AsArrayOf<int>();
                var t = new List<short[]>(numBanks);  //The table that will eventually replace our own.
                if(numBanks<=0) throw new ArgumentException("Invalid number of banks in wavetable.", "input[size]");

                var dataStartPosition = 0;
                for(int i=0; i<numBanks; i++)
                {
                    //Dimension the array based on the width we grab from the bankWidths value. Modulo to the length as a lazy hack to support 1-value arrays.
                    var sample = new short[1 << bankWidths[i % bankWidths.Length]];
                    Array.Copy(data, dataStartPosition, sample, 0, sample.Length);
                    t.Add(sample);
                    dataStartPosition += sample.Length;
                }

                tbl = t;

            } catch (Exception e) {
                System.Diagnostics.Debug.Fail("Wavetable fromJSON failed:  " + e.Message); 
                return false;
            }
            return true;
        }


        public string ToJSONString(){ return ToJSONObject().ToJSONString(); }
        public JSONObject ToJSONObject()
        {
            var o = new JSONObject();
            if (NotInUse) return o;

            o.AddPrim("size", NumBanks);

            var bankWidths = new List<byte> ( new byte[]{ (byte)Tools.Ctz(tbl[0].Length) } );  //Set first value to first bank's size.
            var fixedWidth = true;
            var defaultWidth = bankWidths[0]; 
            var totalLength = tbl[0].Length;  

            //Iterate over the banks.  If any bank is a different size than the default, then we have variable width banks
            //and have to store the representation differently.
            for (int i=1; i<tbl.Count; i++)
            {
                var bits = (byte) Tools.Ctz(tbl[i].Length);
                bankWidths.Add( bits );
                if ( defaultWidth != bits ) fixedWidth = false;
                totalLength += tbl[i].Length;
            }

            o.AddPrim<byte>("bankWidths", fixedWidth?  new byte[]{defaultWidth} : bankWidths.ToArray());


            //Consolidate all of the banks into one big data chunk and convert them.
            var data = new List<short>(totalLength);
            for (int i=0; i<tbl.Count; i++) data.AddRange( tbl[i] );

            o.AddPrim("data", TableAsString( data.ToArray() ));

            return o;
        }

        private string TableAsString(short[] arr)
        {
            // var arr = tbl?[bank];
            if (arr==null || arr.Length==0) return "";
            //  Wavetable banks are compressed using a few techniques. Each bank is encoded as a delta waveform which is then DEFLATEd and encoded in Z85.
            //  Variable bank sizes:  Use Tools.Ctz to find bit width for each bank, then add to a bankWidths field.  The field should have a format
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

            var padAmt = 4 - (input2.Length % 4);
            if (padAmt==4) padAmt = 0;
            if (padAmt > 0)
            {   //Pad the compressed array to a 4 byte multiple to meet Z85 spec
                Array.Resize(ref input2, input2.Length + padAmt );
            }

            //Finally, convert the data to a Z85-encoded string.
            var output = Z85.Encode(input2);
            return String.Format("{0},{1}", padAmt, output);
        }

        //DEBUG:  Convert a compressed string back into a table.  Currently assumes only one bank is in the compressed string.
        public short[] DeflatedZ85ToTable(string input)
        {
            //Do the encoding process in reverse.  TODO
            var split = input.Split(",", StringSplitOptions.RemoveEmptyEntries);
            var padAmt = Convert.ToInt32(split[0]);
            var decoded = Z85.Decode(split[1]);
            Array.Resize(ref decoded, decoded.Length - padAmt);
            var uncompressed = Z85.BytesToShorts( Glue.Deflate(decoded, System.IO.Compression.CompressionMode.Decompress) );
            


            //Each value in the bytestream represents a delta coded value.  Translate.
            var output = new short[uncompressed.Length];
            output[0] = uncompressed[0];
            for (int i=1; i<output.Length; i++)
                output[i] = (short) (output[i-1] + uncompressed[i]);

            return output;
        }


    }
}
