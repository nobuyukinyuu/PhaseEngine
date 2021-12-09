using System;
using PhaseEngine;
using GdsFMJson;

namespace PhaseEngine 
{
    public interface IResponseTable
    {
        // void Apply(byte index, ref object target);
        void UpdateValue(byte index, ushort value);
        void SetScale(float floor, float ceiling);
        public string ToJSONString();
    }

    public abstract class RTable<T> : IResponseTable
    {
        protected const short RTABLE_MAX = 1024;
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
            // val = (floor/100.0f) +  val * (1.0f-(floor/100.0f));  //Apply floor.
            val = (floor/100.0f * RTABLE_MAX) +  val * (1.0f-(floor/100.0f));  //Apply floor.
            return val;
        }

        public void SetScale(float floor, float ceiling)
        {
            //Set defaults if -1 is detected for any value.
            if (floor < 0)  floor = this.floor;
            if (ceiling < 0)  ceiling = this.ceiling;

            if (ceiling >= floor)
            {
                this.ceiling = ceiling;
                this.floor = floor;
            }
        }

        public string ToJSONString()
        {
            JSONObject j = new JSONObject();

            j.AddPrim("intent", intent.ToString());
            j.AddPrim("floor", floor);
            j.AddPrim("ceiling", ceiling);
            
            j.AddPrim( "tbl", Convert.ToBase64String(TableAsBytes()) );

            return j.ToJSONString();
        }

        public virtual byte[] TableAsBytes()  //Returns a table 2x the size of the normal table, every even index being a low byte and odd index a high byte.
        {
            var output = new byte[values.Length * 2]; 
            for(int i=0; i<values.Length; i++)
            {
                output[i*2] =  (byte)((ushort)Convert.ChangeType(values[i], typeof(ushort)) & 0xFF);  //Low byte
                output[i*2+1] =  (byte)((ushort)Convert.ChangeType(values[i], typeof(ushort)) >> 8);
            }
            return output;
        }

        public abstract void Apply(byte index, ref T target);   

        public virtual void UpdateValue(byte index, ushort value)
            { values[index] = (T) Convert.ChangeType(value, typeof(T)); }
    }

    public class RateTable : RTable<ushort>
    {
        public RateTable()  { intent = RTableIntent.RATES; Init(); }


        void Init()
        {
            for(ushort i=0; i<values.Length; i++)
            {
                
                values[i] = (ushort)(i / 4.0);
            }

            ceiling = 0;  //Disable rate scaling by default.
        }
        public override void UpdateValue(byte index, ushort value)
            { 
                if (value >= 0x400) value = 0x3FF;
                values[index] = (byte) Convert.ChangeType(value >> 4, typeof(byte)); //Scale value from 0-63.
            }

        public void Apply (byte index, ref byte target)
        {
            if (target==0) return;  //Rate set to infinity.  Don't mess with it
            ushort marshal = target;
            Apply(index, ref marshal);
            target = (byte) marshal;
        }

        public override void Apply(byte index, ref ushort target)  //Satisfy the abstract method
        {   // Rates add the scaled value to the target rate.
            var val = target;
            target = (byte) Math.Clamp(val + ScaledValue(index), 0, Envelope.R_MAX);
        }

        public override byte[] TableAsBytes() //Single byte table since all values are already 1 byte
        {
            var output = new byte[values.Length]; 
            for(int i=0; i<values.Length; i++)
                output[i] = (byte) Convert.ChangeType(values[i], typeof(byte));
            return output;
        }

    }

    public class LevelTable : RTable<ushort>
    {
        public LevelTable()  { intent = RTableIntent.LEVELS; Init();}

        void Init()
        {
			const double RATIO = 64/12.0; //64 units of attenuation == 6dB per octave
			const int START_NOTE=24;  //Probably 8 actually to produce -60dB at highest octave
            for(ushort i=0; i<values.Length; i++)
                values[i] = (ushort)Math.Max(0, Math.Round((i-START_NOTE) * RATIO));

            ceiling = 0;  //Disable rate scaling by default.
        }


        public override void Apply(byte index, ref ushort tl)
        {   // Velocity takes the total level of the input and attenuates it by the given amount. 
            tl = (ushort) Math.Clamp(tl + ScaledValue(index) , 0, Envelope.L_MAX);
        }
    }


    public class VelocityTable : LevelTable
    {
        public VelocityTable() {intent = RTableIntent.VELOCITY; Init();}

        void Init()
        {
            for(ushort i=0; i<64; i++)
            {
                // const int SCALE = RTABLE_MAX / 128;
                // values[i] = (ushort)(RTABLE_MAX-SCALE -i*SCALE);  //Linear

                values[i] = (ushort)(RTABLE_MAX * Math.Pow((127-i)/127.0, 3) * 0.75);  //Cubic

                // values[i] = (ushort)(RTABLE_MAX * Math.Abs(testVel[(int)Math.Round(i/127.0 * 99)] / 12.0f ));
            }

            for(int i=64; i<values.Length; i++)
            {
                values[i] = (ushort)(RTABLE_MAX * (127-i)/127.0 * 0.1875);  //Linear
            }

            ceiling = 0;  //Disable velocity by default.
        }
    }


    public enum RTableIntent {DEFAULT=-1, RATES, VELOCITY, LEVELS}

}
