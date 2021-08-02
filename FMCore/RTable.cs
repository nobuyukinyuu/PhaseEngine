using System;
using gdsFM;

namespace gdsFM 
{
    public abstract class RTable<T>
    {
        public RTableIntent intent = RTableIntent.DEFAULT;
        public float floor=0, ceiling=100;
        public T[] values = new T[128];

        public RTable()    {}

        //Indexer.  Returns a precalculated value which corresponds to the rTable value scaled by the floor and ceiling.
        public T this [int i] 
        {
            get {
                float val = Convert.ToInt32(values[i]);
                val = val * (ceiling/100.0f);  //Apply ceiling.
                val = (floor/100.0f) +  val * (1.0f-(floor/100.0f));  //Apply floor.
                return (T) Convert.ChangeType(val, typeof(T));  //Stuff a square peg into a round hole
            }
        }

        public abstract void Apply(byte index, ref T target);
    }

    public class RateTable : RTable<byte>
    {
        public RateTable()  { intent = RTableIntent.RATES; }

        public override void Apply(byte index, ref byte target)
        {   // Rates add the scaled value to the target rate.
            target = (byte) Math.Clamp(target + this[index], 0, Envelope.R_MAX);
        }
    }

    public class LevelTable : RTable<ushort>
    {
        public LevelTable()  { intent = RTableIntent.LEVELS; }
        public override void Apply(byte index, ref ushort tl)
        {   // Velocity takes the total level of the input and attenuates it by the given amount. 
            tl = (ushort) Math.Clamp(tl + this[index], 0, Envelope.L_MAX);
        }
    }

    public class VelocityTable : LevelTable
    {
        public VelocityTable() {intent = RTableIntent.VELOCITY; }
    }


    public enum RTableIntent {DEFAULT, RATES, VELOCITY, LEVELS}

}
