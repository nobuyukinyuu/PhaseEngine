using System;
using PhaseEngine;
using PE_Json;

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
            } set {
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

        public string ToJSONString() { return ToJSONObject(true).ToJSONString(); }
        public string ToJSONString(bool includeIntent=true) { return ToJSONObject(includeIntent).ToJSONString(); }
        internal JSONObject ToJSONObject(bool includeIntent=true)
        {
            JSONObject j = new JSONObject();

            //Intent is necessary for clipboard operations and marshalling to GDScript so the UI knows which base64 decoder it should use.
            //The data is redundant if the table is included as part of serialized data from an EG, since the variables will hint at the intent.
            if (includeIntent) j.AddPrim("intent", intent.ToString()); 
            j.AddPrim("floor", floor);
            j.AddPrim("ceiling", ceiling);
            
            j.AddPrim( "tbl", Convert.ToBase64String(TableAsBytes()) );

            return j;
        }

        public bool FromJSON(JSONObject j)
        {
            try 
            {
                if (j.HasItem("intent")) //Probably clipboard data or data from UI.  Check for matching intent.  If not the same, fail operation.
                {   
                    var expectedIntent = (RTableIntent) j.GetItem("intent", -1);
                    if (intent != expectedIntent) throw new Exception("Intent does not match!");
                }

                j.Assign("floor", ref floor);
                j.Assign("ceiling", ref ceiling);

                if (j.HasItem("tbl"))
                    try
                    {   //Call the appropriate override for ValuesFromBase64() for the given intent.
                        values = ValuesFromBase64( j.GetItem("tbl", "") );
                    } catch (Exception e) {  //Catch any exceptions that ValuesFromBase64() might've thrown trying to parse the table.
                        #if DEBUG
                        System.Diagnostics.Debug.Fail( $"{GetType().Name}: Value table copy failed: {e.Message}" );
                        System.Diagnostics.Debugger.Break();
                        throw e;
                        #endif
                        //FIXME:  Consider printing some shit to output in release mode, otherwise this func will just return false.
                    }

            } catch (Exception e) {
                System.Diagnostics.Debug.Fail( $"{GetType().Name}: Copy failed: {e.Message}" ); //DEBUG, REMOVE ME?
                
                return false;
            }

            return true;
        }
        public bool FromString(string input)
        {
            var P = JSONData.ReadJSON(input);
            if (P is JSONDataError) throw new ArgumentException("JSON Data invalid. " + P.ToString());
            var j = (JSONObject) P;
            return FromJSON(j);
        }

        //Returns a table 2x the size of the normal table, every even index being a low byte and odd index a high byte.
        public virtual byte[] TableAsBytes()
        {
            // if (values==null) throw new InvalidOperationException("Table values are not initialized.");  //Should never be null....
            if( !(values[0] is ValueType) ) throw new NotSupportedException(
                    $"{values.GetType().Name}s not supported for base TableAsBytes(). Override this method!" );

            var output = new byte[values.Length * 2]; 
            for(int i=0; i<values.Length; i++)
            {
                output[i*2] =  (byte)((ushort)Convert.ChangeType(values[i], typeof(ushort)) & 0xFF);  //Low byte
                output[i*2+1] =  (byte)((ushort)Convert.ChangeType(values[i], typeof(ushort)) >> 8);
            }
            return output;
        }

        public virtual T[] ValuesFromBase64(string bstr)
        {
            var width = System.Runtime.InteropServices.Marshal.SizeOf<T>();
            #if DEBUG
            if( !(values[0] is ValueType) ) throw new NotSupportedException(
                    $"Can't store the values from this data to a {values.GetType().Name} table. Override this method!" );
            #endif

            var b = Convert.FromBase64String(bstr);
            var output = new T[128];

            //FIXME:  Consider using b.Length to determine correct decode instead of interop.sizeOf, since that's platform-specific
            switch(width)
            {
                case 1:  //Byte array
                    #if DEBUG
                    if (b.Length != values.Length) throw new ArgumentOutOfRangeException("Base64 data returned more/less than 128 bytes...");
                    #endif
                    for(int i=0; i<values.Length; i++)
                        output[i] = (T)Convert.ChangeType(b[i], typeof(byte));
                break;

                case 2: //Two-byte short
                    #if DEBUG
                    if (b.Length != values.Length*2) throw new ArgumentOutOfRangeException("Base64 data returned more/less than 256 bytes...");
                    #endif                
                    for(int i=0; i<output.Length; i++)
                    {
                        //b is len 256, with every even index being a low byte and odd index a high byte.  Merge.
                        output[i] = (T)Convert.ChangeType( b[i*2+1] << 8 | b[i*2], typeof(T)); 
                    }
                break;

                default:
                    throw new NotImplementedException("Tables with 32 bit values and longer are not yet implemented...");
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
                values[i] = (ushort)(i / 4.0);
                // values[i] = (ushort)(i / 2.0);

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
            Apply(index, ref marshal); //Call the implementation of the abstract method from the base
            target = (byte) marshal;
        }

        public override void Apply(byte index, ref ushort target)  //Satisfy the abstract method
        {   // Rates add the scaled value to the target rate.
            var val = target;
            target = (byte) Math.Min(val + ScaledValue(index), Envelope.R_MAX);
        }

        public override byte[] TableAsBytes() //Single byte table since all values are already 1 byte
        {
            var output = new byte[values.Length]; 
            for(int i=0; i<values.Length; i++)
                output[i] = (byte) Convert.ChangeType(values[i], typeof(byte));
            return output;
        }
        public override ushort[] ValuesFromBase64(string bstr) //Since all values expected from this table type are already 1 byte, simplify the method.
        {
            var b = Convert.FromBase64String(bstr);
            var output = new ushort[values.Length];
            for(int i=0; i<values.Length; i++)
                output[i] = b[i];
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
                values[i] = (ushort)(RTABLE_MAX * Math.Pow((127-i)/127.0, 3) * 0.75);  //Cubic
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
