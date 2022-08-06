using System;
using System.Numerics;
using System.Collections.Generic;
using PhaseEngine;

namespace PhaseEngine
{
    public class WaveformDisplay<T> where T:struct, IComparable<T>
    {
        public const int MAX_SAMPLES = SampleBlock<T>.LUT_SIZE * SampleBlock<T>.SAMPLES_PER_LUT;  //Typically 512 * 256

        public List<SampleBlock<T>> blocks = new List<SampleBlock<T>>();

        delegate T MinMaxFunction(T A, T B);
        MinMaxFunction Min = (x,y) => x.CompareTo(y) < 0? x:y;
        MinMaxFunction Max = (x,y) => x.CompareTo(y) > 0? x:y;
        readonly T MIN_VALUE, MAX_VALUE, ZERO;


        WaveformDisplay()  //Should be called by all other Ctors.  Sets up our constants based on the numeric type of T.
        {
            switch(Type.GetTypeCode(typeof(T)))
            {
                // Float-based waveform
                case TypeCode.Single:  case TypeCode.Double:  case TypeCode.Decimal:
                    // Recalc = RecalcFloat;
                    MIN_VALUE = (T)Convert.ChangeType(float.MinValue, typeof(T));
                    MAX_VALUE = (T)Convert.ChangeType(float.MaxValue, typeof(T));
                    ZERO = (T)Convert.ChangeType(0.0f, typeof(T));
                    break;

                // Integer-based waveform
                case TypeCode.Byte:  case TypeCode.SByte:
                case TypeCode.Int16:  case TypeCode.Int32:  case TypeCode.Int64:
                case TypeCode.UInt16:  case TypeCode.UInt32:  case TypeCode.UInt64:
                    // Recalc = RecalcShort;
                    MIN_VALUE = (T)Convert.ChangeType(int.MinValue, typeof(T));
                    MAX_VALUE = (T)Convert.ChangeType(int.MaxValue, typeof(T));
                    ZERO = (T)Convert.ChangeType(0, typeof(T));
                    break;

                default:
                    // if (typeof(T) == typeof(System.Numerics.Vector2)) //Stereo Audio, 32-bit float format
                    // {
                    //     // Recalc = RecalcVector2;
                    //     MIN_VALUE = (T)Convert.ChangeType(float.MinValue, typeof(T));
                    //     MAX_VALUE = (T)Convert.ChangeType(float.MaxValue, typeof(T));
                    //     Min = MinVector2;
                    //     Max = MaxVector2;
                    //     break;
                    // }
    
                    throw new NotSupportedException($"Generic classes of WaveformDisplay<{typeof(T).Name}> are not supported.");
            }
        }

        //TODO:  Functions to load large amounts of streamed audio in chunks (perhaps asynchronously) and display sparse LUTs / calculate as we go...
        public WaveformDisplay(T[] sampleData): this()  {this.blocks = MakeBlocks(sampleData);}
        public static WaveformDisplay<T> FromSampleData(T[] sampleData) => new WaveformDisplay<T>(sampleData);

        //Convenience func to turn sample data into an array of display blocks.
        static List<SampleBlock<T>> MakeBlocks(T[] sampleData)
        {
            var output = new List<SampleBlock<T>>();

            //Start adding blocks to the output based on MAX_SAMPLES length.
            var processed = 0;
            while(processed < sampleData.Length - MAX_SAMPLES)
            {
                var p = new T[MAX_SAMPLES];
                Array.Copy(sampleData, processed, p, 0, Math.Min(sampleData.Length, MAX_SAMPLES));
                output.Add(new SampleBlock<T>(p));
                processed += MAX_SAMPLES;
            }
                //Fill the final block with the remainder of the samples.
                var remainder=Math.Min(sampleData.Length, sampleData.Length-processed);
                var q = new T[remainder];
                Array.Copy(sampleData, processed, q, 0, remainder);
                output.Add(new SampleBlock<T>(q));  //Ctor should automatically trim .length to the correct value...                

            return output;
        }


        //============================== METHODS ==============================
#region PUBLIC
        //Get mins and maxes of this WaveformDisplay for the given index.
        public void GetDisplayData(int sampleIndex, int samplesPerPx, ref T[] mins, ref T[] maxes, int totalWidth)
        {
            var pos = PositionOf(sampleIndex);
            for (int i=0; i < totalWidth; i++)
            {
                if (pos.block >= blocks.Count)  //Out of bounds.  Return 0.
                    { mins[i] = ZERO; maxes[i] = ZERO;  continue; }
                GetMinMaxOfRange(ref pos, samplesPerPx, ref mins[i], ref maxes[i]);
            }
        }

        //Represents a position in blocks and samples in a given WaveformDisplay.
        public struct SamplePos
        {
            public int block;
            public int sample;

            public SamplePos(int blockPos, int samplePos)
            {
                block = blockPos;
                sample = samplePos;
            }
        }


        //Get a block and sample position from 1d data index        
        public SamplePos PositionOf(long sampleIndex)
        {
            int blockIndex=0;

            while (sampleIndex >= blocks[blockIndex].Length)
            {   //Stride
                sampleIndex -= blocks[blockIndex].Length;
                blockIndex++;
                if (blockIndex > blocks.Count)
                {
                    #if DEBUG
                        throw new IndexOutOfRangeException();
                    #else
                        return new SamplePos(-1,-1);
                    #endif
                }
            }
            return new SamplePos(blockIndex, (int)sampleIndex); //Downcast OK;  MAX_SAMPLES is int
        }
        
#endregion

#region PRIVATE
        void GetMinMaxOfRange(ref SamplePos pos, int rangeLength, ref T minResult, ref T maxResult)
        {
            var block = blocks[pos.block];

            var min = MAX_VALUE;
            var max = MIN_VALUE;

            //Slow part.  Process the number of samples at the current position until the start of the next block or LUT boundary.
            {
                const int lut_item_boundary_mask = SampleBlock<T>.SAMPLES_PER_LUT -1;
                //Find the number of samples left in the current block until the next LUT item.
                var slowSamples = (SampleBlock<T>.SAMPLES_PER_LUT - pos.sample) & lut_item_boundary_mask;

                //Account for short total ranges
                if (slowSamples > rangeLength)  slowSamples = rangeLength;
                if (slowSamples > block.Length)  slowSamples = block.Length;

                // int idx = pos.sample;
                int endIdx = Math.Min(pos.sample + slowSamples, block.Length);
                for(int idx=pos.sample; idx < endIdx; idx++)
                {
                        min = Min(block.samples[idx], min);
                        max = Max(block.samples[idx], max);
                }
                rangeLength -= slowSamples;
                block = Increment(ref pos, slowSamples);
            }

            //Fast part.  Now that we are aligned to a boundary, we can stride using the LUTs in each SampleBlock.
            while(block!=null && rangeLength > SampleBlock<T>.SAMPLES_PER_LUT)
            {            
                // Cases here:
                // 1. One or more usable LUT items in this block.  Stride.
                // 2. There's LUT items left in the block, but the range length doesn't span entirely over them. Calculate the rest for the next slow part
                var lutItemsToUse = rangeLength / SampleBlock<T>.SAMPLES_PER_LUT;
                var currentLutItemIndex = pos.sample / SampleBlock<T>.SAMPLES_PER_LUT;
                var lutItemsInBlock = ((block.Length - 1) / SampleBlock<T>.SAMPLES_PER_LUT) + 1;
                var lutItemsLeft = lutItemsInBlock - currentLutItemIndex;

                if (lutItemsToUse >= lutItemsLeft)  lutItemsToUse = lutItemsLeft;

                var endIdx = currentLutItemIndex + lutItemsToUse;
                while (currentLutItemIndex < endIdx)
                {
                    min = Min(block.minLut[currentLutItemIndex], min);
                    max = Max(block.maxLut[currentLutItemIndex], max);
                    currentLutItemIndex++;
                }

                // Calc number of processed samples.  Adjust for potential LUT fragmentation on future data operations to SampleBlocks
                var samplesLeftInBlockBeforeStride = block.Length - pos.sample;
                var samplesProcessed = lutItemsToUse * SampleBlock<T>.SAMPLES_PER_LUT;
                if (rangeLength > samplesLeftInBlockBeforeStride)  samplesProcessed = samplesLeftInBlockBeforeStride;

                rangeLength -= samplesProcessed;
                block = Increment(ref pos, samplesProcessed);
            }


            // Final slow part at the end of the range.  This is all the rest of the data beyond the last stride to check.
            if(block!=null && rangeLength>0)
            {
                var idx = pos.sample;
                var endIdx = idx + rangeLength;
                if (endIdx > block.Length)  endIdx = block.Length;

                var samplesProcessed = endIdx - idx;
                while (idx < endIdx)
                {
                    min = Min(block.samples[idx], min);
                    max = Max(block.samples[idx], max);
                    idx++;
                }

                block = Increment(ref pos, samplesProcessed);
                rangeLength -= samplesProcessed;
            }

            minResult = min;  maxResult = max;
        }

        //Adjusts head position by rangeLength and fetches a SampleBlock at the given position.
        SampleBlock<T> Increment(ref SamplePos pos, long rangeLength)
        {
            if (pos.block >= blocks.Count) return null;

            SampleBlock<T> block = blocks[pos.block];

            System.Diagnostics.Debug.Assert(pos.sample < block.Length, "Sample index out of range.");

            while(rangeLength > 0)
            {
                //Does block have >= numSamples available?
                if (pos.sample + rangeLength < block.Length)
                { 
                    pos.sample += (int)rangeLength;  //OK to downcast, provided MAX_SAMPLES is int
                    rangeLength = 0;
                } else {
                    //Block doesn't have enough samples to move forward.  Increment.
                    rangeLength -= block.Length - pos.sample;
                    pos.block ++;
                    if  (pos.block >= blocks.Count)  return null;

                    block = blocks[pos.block];
                    pos.sample = 0;
                }
            }

            return block;
        }

#endregion

    }
}