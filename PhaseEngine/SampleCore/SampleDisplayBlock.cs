using System;
using System.Numerics;
using System.Collections.Generic;
using PhaseEngine;

namespace PhaseEngine
{
    public class SampleBlock<T> where T:struct, IComparable<T>
    {
        public const short SAMPLES_PER_LUT = 256;
        public const short LUT_SIZE = 512;

        internal T[] samples = new T[ WaveformDisplay<T>.MAX_SAMPLES ];
        int length = WaveformDisplay<T>.MAX_SAMPLES;  // Number of valid samples in this display block

        public int Length { get=>length; }

        internal T[] minLut = new T[LUT_SIZE];
        internal T[] maxLut = new T[LUT_SIZE];

        delegate T MinMaxFunction(T A, T B);
        MinMaxFunction Min = (x,y) => x.CompareTo(y) < 0? x:y;
        MinMaxFunction Max = (x,y) => x.CompareTo(y) > 0? x:y;
        readonly T MIN_VALUE, MAX_VALUE;

        SampleBlock()  //Called no matter what
        {
            switch(Type.GetTypeCode(typeof(T)))
            {
                // Float-based samples
                case TypeCode.Single:  case TypeCode.Double:  case TypeCode.Decimal:
                    // Recalc = RecalcFloat;
                    MIN_VALUE = (T)Convert.ChangeType(float.MinValue, typeof(T));
                    MAX_VALUE = (T)Convert.ChangeType(float.MaxValue, typeof(T));
                    break;
 
                // Integer-based samples
                case TypeCode.Byte:  case TypeCode.SByte:
                case TypeCode.Int16:  case TypeCode.Int32:  case TypeCode.Int64:
                case TypeCode.UInt16:  case TypeCode.UInt32:  case TypeCode.UInt64:
                    // Recalc = RecalcShort;
                    MIN_VALUE = (T)Convert.ChangeType(int.MinValue, typeof(T));
                    MAX_VALUE = (T)Convert.ChangeType(int.MaxValue, typeof(T));
                    break;

                default:
                    if (typeof(T) == typeof(System.Numerics.Vector2)) //Stereo Audio, 32-bit float format
                    {
                        // Recalc = RecalcVector2;
                        MIN_VALUE = (T)Convert.ChangeType(float.MinValue, typeof(T));
                        MAX_VALUE = (T)Convert.ChangeType(float.MaxValue, typeof(T));
                        Min = MinVector2;
                        Max = MaxVector2;
                        break;
                    }
    
                    throw new NotSupportedException(String.Format("Display blocks using type {0} are not supported.", typeof(T).Name));
            }
        }

        // Internal func to dump up to MAX_SAMPLES from a data block to initialize us
        internal SampleBlock(T[] sampleData) : this()
        {
            if (sampleData.Length > WaveformDisplay<T>.MAX_SAMPLES) 
                throw new ArgumentOutOfRangeException("sampleData must be less than " + WaveformDisplay<T>.MAX_SAMPLES.ToString());
            length = sampleData.Length;
            Array.Copy(sampleData, samples, length);
            Recalc();
        }

        // Box functions for Vec2
        T MinVector2(T A, T B)
        {
            var left = (Vector2) Convert.ChangeType(A, typeof(Vector2));
            var right = (Vector2) Convert.ChangeType(B, typeof(Vector2));

            var output = Vector2.Min(left, right);
            return (T)Convert.ChangeType(output, typeof(T));
        }
        T MaxVector2(T A, T B)
        {
            var left = (Vector2) Convert.ChangeType(A, typeof(Vector2));
            var right = (Vector2) Convert.ChangeType(B, typeof(Vector2));

            var output = Vector2.Max(left, right);
            return (T)Convert.ChangeType(output, typeof(T));
        }

        void Recalc()
        {
            int currentSample = 0;
            for (int i=0; i<LUT_SIZE; i++)
            {
                var min = MAX_VALUE;
                var max = MIN_VALUE;

                for(int j=0; j<SAMPLES_PER_LUT; j++)
                {
                    if (currentSample >= length)  break;
                    var sample = samples[currentSample];

                    min = Min(sample, min);
                    max = Max(sample, max);
                    currentSample++;
                }

                minLut[i] = min;
                maxLut[i] = max;
            }
        }




    }
}
