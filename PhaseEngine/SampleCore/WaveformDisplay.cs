using System;
using System.Numerics;
using System.Collections.Generic;
using PhaseEngine;

namespace PhaseEngine
{
    public class WaveformDisplay<T> where T:struct, IComparable<T>
    {
        public const int MAX_SAMPLES = SampleBlock<T>.LUT_SIZE * SampleBlock<T>.SAMPLES_PER_LUT;

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
    
                    throw new NotSupportedException(String.Format("Display blocks using type {0} are not supported.", typeof(T).Name));
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
                var remainder=Math.Min(sampleData.Length, MAX_SAMPLES-processed);
                var q = new T[remainder];
                Array.Copy(sampleData, processed, q, 0, remainder);
                output.Add(new SampleBlock<T>(q));  //Ctor should automatically trim .length to the correct value...                

            return output;
        }

        void GetMinMaxOfRange(ref SamplePos pos, int rangeLength, ref T minResult, ref T maxResult)
        {
            var block = blocks[pos.block];

            var min = MAX_VALUE;
            var max = MIN_VALUE;

            //Slow part
            {
                const int lut_item_boundary_mask = SampleBlock<T>.SAMPLES_PER_LUT -1;
                var samplesToNextItemBoundary = (SampleBlock<T>.SAMPLES_PER_LUT - pos.sample) & lut_item_boundary_mask;
                var slowSamples = samplesToNextItemBoundary;
                if (slowSamples > rangeLength)  slowSamples = rangeLength;
                if (slowSamples > block.Length)  slowSamples = block.Length;

                int endIdx = pos.sample + slowSamples;
                while(pos.sample < endIdx)
                {
                        min = Min(block.samples[pos.sample], min);
                        max = Max(block.samples[pos.sample], max);
                        pos.sample++;
                }
                rangeLength = slowSamples;
            }

            //Fast part
            while(block!=null && rangeLength > SampleBlock<T>.SAMPLES_PER_LUT)
            {
                // if (idx==block.Length)
                // {
                //     block=block.nextBlock;
                //     if (block==null)  break;
                //     idx=0;
                // }
            
                // Cases here:
                // 1. There is one or more LUT items in this block that we can use.
                // 2. There is one or more LUT items left, but the pixel doesn't span entirely over them.
                var lutItemsNeeded = rangeLength / SampleBlock<T>.SAMPLES_PER_LUT;
                var currentLutItemIndex = pos.sample / SampleBlock<T>.SAMPLES_PER_LUT;
                var lutItemsInBlock = ((block.Length - 1) / SampleBlock<T>.SAMPLES_PER_LUT) + 1;
                var lutItemsLeft = lutItemsInBlock - currentLutItemIndex;

                var lutItemsToUse = lutItemsNeeded;
                if (lutItemsLeft < lutItemsToUse)  lutItemsToUse = lutItemsLeft;

                var endLutItemIndex = currentLutItemIndex + lutItemsToUse;
                while (currentLutItemIndex < endLutItemIndex)
                {
                    min = Min(block.minLut[currentLutItemIndex], min);
                    max = Max(block.maxLut[currentLutItemIndex], max);
                    currentLutItemIndex++;
                }

                // Calc number of processed samples
                var samplesLeftInBlockBeforeFastBit = block.Length - pos.sample;
                var samplesProcessed = lutItemsToUse * SampleBlock<T>.SAMPLES_PER_LUT;
                if (rangeLength > samplesLeftInBlockBeforeFastBit)  samplesProcessed = samplesLeftInBlockBeforeFastBit;

                rangeLength -= samplesProcessed;
                block = Increment(ref pos, samplesProcessed);
            }

            // if (idx == block.Length)
            // {
            //     block = block.nextBlock;
            //     idx=0;
            // }

            // Slow bit at the end of the range
            if(block!=null && rangeLength>0)
            {
                // We're not at the end of the block, but numSamples is less than the next LUT item boundary

                var sampleIndex = pos.sample;
                var endIndex = sampleIndex + rangeLength;
                if (endIndex > block.Length)  endIndex = block.Length;

                var samplesProcessed = endIndex - sampleIndex;
                while (sampleIndex < endIndex)
                {
                    min = Min(block.samples[pos.sample], min);
                    max = Max(block.samples[pos.sample], max);
                    sampleIndex++;
                }

                block = Increment(ref pos, samplesProcessed);
                rangeLength -= samplesProcessed;
            }

            minResult = min;  maxResult = max;
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

        //Get mins and maxes of this WaveformDisplay for the given index.
        public void GetDisplayData(int sampleIndex, ref T[] mins, ref T[] maxes, int pxWidth, int samplesPerPx)
        {
            var pos = PositionOf(sampleIndex);
            for (int i=0; i < pxWidth; i++)
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

    }
}