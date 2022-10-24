using System;
using PhaseEngine;
using System.Collections.Generic;
using PE_Json;

namespace PhaseEngine
{
    //Contains the general definitions for serialization and copying purposes, and can create binds for bindable interfaces to use.
    //TODO:  Serialization routines
    public class TrackerEnvelope
    {
        [Flags]
        enum EnvelopePtMarker {None=0, LoopStart=1, LoopEnd=2, SustainStart=4, SustainEnd=8}

        public readonly int minValue, maxValue; //Values used for clamping and maybe some other calcs. Assigned from bind invoker
        public int InitialValue{get=> (int)pts[0].Value; set 
        { 
            pts[0] = new TrackerEnvelopePoint(0, value);
            cached = false;
        } }

        public int ClockDivider {get=>divider; set{if(value<1) throw new ArgumentOutOfRangeException("Clock divider must be >=1"); divider=value;}}
        public int TicksPerSecond {get=> (int)(Global.MixRate/divider); set=> ClockDivider=(int)(Global.MixRate/value);}
        int divider=1;

        //Helpers
        internal System.Type dataSourceType;  //The type of data source which wants its associated value to be automated by this envelope.
        internal System.Reflection.MemberInfo associatedProperty; //The field or property this envelope is expected to bind to. Used by BindManager to update data consumers


        //These properties help with identifying what this envelope might've been bound to when serializing out data.
        public string AssociatedDataType{get => dataSourceType?.Name ?? "None";}
        public string AssociatedProperty{get => associatedProperty?.Name ?? "Unknown";}
        //TODO:  Method for UnboundCopy()


        public List<TrackerEnvelopePoint> Pts {get=>pts;}
        private List<TrackerEnvelopePoint> pts = new List<TrackerEnvelopePoint>();
        private CachedEnvelope cache;
        public bool cached = false;  //Invalidate this whenever we are modified in some way.


        private TrackerEnvelope()    {pts.Add(new TrackerEnvelopePoint(0)); cache=new CachedEnvelope(this);}
        public TrackerEnvelope(int minValue, int maxValue) : this()
            {this.minValue = minValue; this.maxValue = maxValue; }
        public TrackerEnvelope(int minValue, int maxValue, int initialValue) : this(minValue, maxValue)
            {InitialValue = initialValue; SetPoint(0, (0, initialValue));}


        //Convenient property to rebake a cached envelope
        // public CachedEnvelope CachedEnvelope{ get{if(!cached) Bake();  return cache;} }
        public CachedEnvelope CachedEnvelopeCopy{ get{if(!cached) Bake();  return new CachedEnvelope(cache);} }

        public void Bake()
        {
            cache.Bake(this);
            cached = true;
        }

        internal void Insert(int index, ValueTuple<float, float> value) {pts.Insert(index, new TrackerEnvelopePoint(value)); cached = false;}
        internal void Remove(int index) {pts.RemoveAt(index); cached = false;}

        internal void SetPoint(int index, TrackerEnvelopePoint value) {pts[index] = value; cached = false;}
        internal void SetPoint(int index, System.Numerics.Vector2 value) {pts[index] = new TrackerEnvelopePoint(value); cached = false;}
        public void SetPoint(int index, ValueTuple<float, float> value) {pts[index] = new TrackerEnvelopePoint(value); cached = false;}

        #if GODOT
        public void SetPoint(int index, Godot.Vector2 value)
        {
            var pt = new TrackerEnvelopePoint();
            pt.Millisecs = value.x;  pt.Value = value.y;
            pts[index] = pt;
            cached = false;
        } 
        #endif


#region IO
        public static TrackerEnvelope FromJSON(JSONObject j)
        {
            try{
                TrackerEnvelope output;
                var min=j.GetItem("minValue", short.MinValue);
                var max=j.GetItem("maxValue", short.MaxValue);
                var pts = j.GetItem<float>("pts", new float[]{0f, 0f});

                output = new TrackerEnvelope(min, max, (int)pts[1]);

                //Load the points in, save for the first point, which we already specified in the ctor with an initial value.
                for(int i=2; i<pts.Length; i+=2)
                    output.pts.Add(new TrackerEnvelopePoint( (pts[i], pts[i+1]) ));

                //Try to figure out where this envelope came from and what it should be bound to.
                output.dataSourceType = Type.GetType(j.GetItem("dataSource", ""));
                var property = j.GetItem("associatedValue", "");
                switch(output.dataSourceType?.GetType().GetMember(property)?[0]?.MemberType)
                {
                    case System.Reflection.MemberTypes.Property:
                        output.associatedProperty = output.dataSourceType.GetType().GetProperty(property);
                        break;
                    case System.Reflection.MemberTypes.Field:
                        output.associatedProperty = output.dataSourceType.GetType().GetField(property);
                        break;
                    default:
                        System.Diagnostics.Debug.Print(
                            $"TrackerEnvelope: Import failed to find member {property} in {output.dataSourceType?.Name ?? "(invalid data source)"}.");
                        break;
                }                
                
                return output;
            } catch (Exception e) {
                System.Diagnostics.Debug.Print(e.Message);
                return null;
            }
        }
        internal static TrackerEnvelope FromString(string input)
        {
            var P = JSONData.ReadJSON(input);
            if (P is JSONDataError) return null;
            var j = (JSONObject) P;
            return FromJSON(j);
        }
        internal JSONObject ToJSONObject()
        {
            var o = new JSONObject();
            // o.AddPrim<bool>("rising", rising);
            o.AddPrim("minValue", minValue);
            o.AddPrim("maxValue", maxValue);


            var pts = new JSONArray();
            for (int i=0; i < this.pts.Count; i++)
            {   //Every odd value is millisecs (x), every even value is the raw value
                pts.AddPrim(this.pts[i].Millisecs);
                pts.AddPrim(this.pts[i].Value);
            }
            o.AddItem("pts", pts);
            o.AddPrim("dataSource", AssociatedDataType);
            o.AddPrim("associatedValue", AssociatedProperty);
            return o;
        }
        public string ToJSONString() => ToJSONObject().ToJSONString();
#endregion
    }


    /// summary:  typical representation of a point on a tracker envelope.  
    public struct TrackerEnvelopePoint 
    {
        System.Numerics.Vector2 vec;
        // public static readonly TrackerEnvelopePoint INITIAL = new TrackerEnvelopePoint(0);

        public TrackerEnvelopePoint(float n) => vec=new System.Numerics.Vector2(n);
        public TrackerEnvelopePoint(System.Numerics.Vector2 n) => vec=n;
        public TrackerEnvelopePoint(float n, float m) => vec=new System.Numerics.Vector2(n,m);
        public TrackerEnvelopePoint(ValueTuple<float, float> t) => vec=new System.Numerics.Vector2(t.Item1, t.Item2);

        public float Millisecs {get=>vec.X; set=>vec.X = value;}
        public float Value {get=>vec.Y; set=>vec.Y = value;}
        public int Samples => (int)(vec.X*Global.MixRate/1000f);  //Number of sample frames elapsed needed to reach this envelope point.

        // public override string ToString() => String.Format("<{0}, {1}>", Millisecs, Value);
        public override string ToString() => vec.ToString();
    }

}
