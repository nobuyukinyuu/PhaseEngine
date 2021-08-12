using System;
using gdsFM;

namespace gdsFM 
{
    public interface IResponseTable
    {
        // void Apply(byte index, ref object target);
        void UpdateValue(byte index, byte value);
    }

    public abstract class RTable<T> : IResponseTable
    {
        public RTableIntent intent = RTableIntent.DEFAULT;
        public float floor=0, ceiling=100;
        public T[] values = new T[128];

        public RTable()    {}

        //Indexer.  Returns a precalculated value which corresponds to the rTable value scaled by the floor and ceiling.
        public T this [int i] 
        {
            get {
                var val = ScaledValue(i);
                return (T) Convert.ChangeType(val, typeof(T));  //Stuff a square peg into a round hole
            }
            set {
                values[i] = value;
            }
        }

        public float ScaledValue(int index)
        {
            float val = Convert.ToInt32(values[index]);
            val = val * (ceiling/100.0f);  //Apply ceiling.
            val = (floor/100.0f) +  val * (1.0f-(floor/100.0f));  //Apply floor.
            return val;
        }

        public abstract void Apply(byte index, ref T target);   

        public void UpdateValue(byte index, byte value)
        {
            values[index] = (T) Convert.ChangeType(value, typeof(T));
        }
    }

    public class RateTable : RTable<byte>
    {
        public RateTable()  { intent = RTableIntent.RATES; }

        public override void Apply(byte index, ref byte target)
        {   // Rates add the scaled value to the target rate.
            var val = (byte) target;
            target = (byte) Math.Clamp(val + ScaledValue(index), 0, Envelope.R_MAX);
        }
    }

    public class LevelTable : RTable<ushort>
    {
        public LevelTable()  { intent = RTableIntent.LEVELS; }
        public override void Apply(byte index, ref ushort tl)
        {   // Velocity takes the total level of the input and attenuates it by the given amount. 
            tl = (ushort) Math.Clamp(tl + ScaledValue(index) * 8, 0, Envelope.TL_MAX);
        }
    }

    public class VelocityTable : LevelTable
    {
        public VelocityTable() {intent = RTableIntent.VELOCITY; Init();}

        void Init()
        {
            for(byte i=0; i<values.Length; i++)
            {
                values[i] = (ushort)(127-i);
            }

            // ceiling = 0;  //Disable velocity by default.
        }
    }


    public enum RTableIntent {DEFAULT=-1, RATES, VELOCITY, LEVELS}

}
