using System;
using PhaseEngine;
using System.Collections.Generic;
using PE_Json;

namespace PhaseEngine
{
    //Contains the general definitions for serialization and copying purposes, and can create binds for bindable interfaces to use.

    public interface TrackerEnvelope : IBindableData
    {
        // public new IBindableData.Abilities BindType {get => IBindableData.Abilities.Envelope;}

        [Flags] public enum PtMarker {None=0, LoopStart=1, LoopEnd=2, SustainStart=4, SustainEnd=8}
        [Flags] public enum LoopType {None=0, Basic=1, Sustain=2, Compound=3}
        public LoopType Looping {get;set;}
        public int LoopStart{get;set;}  public int LoopEnd{get;set;}
        public int SustainStart{get;set;}  public int SustainEnd{get;set;}

        public object MinValue {get;}  public object MaxValue{get;}
        public object InitialValue{get;set;}
        public List<TrackerEnvelopePoint> Pts{get;}

       public double ClockDivider {get;}
 
        //Data sourcing helpers
        public Type UnderlyingType{get;}
        public void SetDataSource(Type dataSourceType, System.Reflection.MemberInfo dataMember);
        public Type DataSource{get;}   //The type of data source which wants its associated value to be automated by this envelope.
        public System.Reflection.MemberInfo DataMember{get;} //The field or property this envelope is expected to bind to. Used by BindManager to update data consumers
        public System.Reflection.MethodInfo PostUpdateAction {get;set;}


        // TODO:  Consider changing this to MethodInfo so it can be serialized properly.  Invocation might be easier using boxing provided we limit the parameters
        //        Moreover, by using MethodInfo we can omit invocation entirely if the field returns null instead of setting a dummy action
        // public Action<double?> PostUpdateAction {get;set;}

        public CachedEnvelope CachedEnvelopeCopy(int chipDivider=1);
        public bool Cached {get;set;}  //Invalidate this whenever we are modified in some way.

        internal void Insert(int index, ValueTuple<float, float> value) {Pts.Insert(index, new TrackerEnvelopePoint(value)); Cached = false;}
        internal void Remove(int index) {Pts.RemoveAt(index); Cached = false;}


        public void SetPoint(int index, ValueTuple<float, float> value);
        public bool SetLoopPt(TrackerEnvelope.PtMarker type, int index);
        public bool SetLoopPt(TrackerEnvelope.LoopType type, ValueTuple<int,int> index);

        //Godot helpers
        #if GODOT
        public void SetPoint(int index, Godot.Vector2 value);
        public bool SetLoopPt(TrackerEnvelope.LoopType type, Godot.Vector2 index) => SetLoopPt(type, ((int)index.x, (int)index.y));
        #endif


        //IO
        public bool Configure(JSONObject j);  //Loads points in without caring about associated types or whatever
        public JSONObject ToJSONObject();
        public string ToJSONString() => ToJSONObject().ToJSONString();
    }

    public class TrackerEnvelope<T> : TrackerEnvelope where T:struct, IComparable
    {
        public IBindableData.Abilities BindType {get => IBindableData.Abilities.Envelope;}
        public Type UnderlyingType{get=>typeof(T);}
        public TrackerEnvelope.LoopType Looping {get;set;} = TrackerEnvelope.LoopType.None;

        // internal int loopStart, loopEnd, sustainStart, sustainEnd;  //Loop points
        public int LoopStart {get;set;}       public int LoopEnd {get;set;}
        public int SustainStart {get;set;}    public int SustainEnd {get;set;}

        readonly T minValue, maxValue; //Values used for clamping and maybe some other calcs. Assigned from bind invoker
        public object MinValue {get=>minValue;}      public object MaxValue{get=>maxValue;}
        public object InitialValue{get=> Convert.ChangeType(pts[0].Value, typeof(T)); set 
        { 
            pts[0] = new TrackerEnvelopePoint(0, (float)value);
            Cached = false;
        } }

        public double ClockDivider {get=>divider; set{if(value<=0) throw new ArgumentOutOfRangeException("Clock divider must be >0"); divider=value;}}
        // public double TicksPerSecond {get=> (Global.MixRate/divider); set=> ClockDivider=(Global.MixRate/value);}
        double divider=1;

        //Helpers
        public void SetDataSource(Type source, System.Reflection.MemberInfo dataMember)  { dataSourceType = source;  associatedProperty = dataMember; }
        internal System.Type dataSourceType;  //The type of data source which wants its associated value to be automated by this envelope.
        internal System.Reflection.MemberInfo associatedProperty; //The field or property this envelope is expected to bind to. Used by BindManager to update data consumers
        public Type DataSource{get=>dataSourceType;}  public System.Reflection.MemberInfo DataMember{get=>associatedProperty;}

        //These properties help with identifying what this envelope might've been bound to when serializing out data.
        public string AssociatedDataSource{get => dataSourceType?.Name ?? "None";}
        public string AssociatedDataMember{get => associatedProperty?.Name ?? "Unknown";}
        public System.Reflection.MethodInfo PostUpdateAction {get;set;}

        //TODO:  Method for UnboundCopy() to get serialized data without the associations, in a more portable format....

        public List<TrackerEnvelopePoint> Pts {get=>pts; set=>pts=value;}
        protected List<TrackerEnvelopePoint> pts = new List<TrackerEnvelopePoint>();
        protected CachedEnvelope<T> cache;
        public bool Cached {get;set;} = false;  //Invalidate this whenever we are modified in some way.


        protected TrackerEnvelope()    {pts.Add(new TrackerEnvelopePoint(0)); cache=new CachedEnvelope<T>(this);}
        public TrackerEnvelope(T minValue, T maxValue) : this()
            {this.minValue = minValue; this.maxValue = maxValue; }
        public TrackerEnvelope(T minValue, T maxValue, T initialValue) : this(minValue, maxValue)
            {InitialValue = initialValue;}


        //Convenient property to rebake a cached envelope
        // public CachedEnvelope CachedEnvelope{ get{if(!cached) Bake();  return cache;} }
        // public CachedEnvelope CachedEnvelopeCopy{ get{if(!cached) Bake();  return new CachedEnvelope(cache);} }
        public CachedEnvelope CachedEnvelopeCopy(int chipDivider=1)
        {
            if(!Cached) Bake(chipDivider);
            return new CachedEnvelope<T>(cache);
        }

        public void Bake(int chipDivider=1)
        {
            cache.Bake(this, chipDivider);
            Cached = true;
        }

        internal void Insert(int index, ValueTuple<float, float> value) {pts.Insert(index, new TrackerEnvelopePoint(value)); Cached = false;}
        internal void Remove(int index) {pts.RemoveAt(index); Cached = false;}

        internal void SetPoint(int index, TrackerEnvelopePoint value) {pts[index] = value; Cached = false;}
        internal void SetPoint(int index, System.Numerics.Vector2 value) {pts[index] = new TrackerEnvelopePoint(value); Cached = false;}
        public void SetPoint(int index, ValueTuple<float, float> value) {pts[index] = new TrackerEnvelopePoint(value); Cached = false;}

        //"Direct" set of loop points.  Does not check if the indices are sane
        public bool SetLoopPt(TrackerEnvelope.PtMarker type, int index)
        {
            switch(type) {   //Get the pointer to the target loop point
                case TrackerEnvelope.PtMarker.LoopStart:    LoopStart = index;     break;
                case TrackerEnvelope.PtMarker.LoopEnd:      LoopEnd = index;       break;
                case TrackerEnvelope.PtMarker.SustainStart: SustainStart = index;  break;
                case TrackerEnvelope.PtMarker.SustainEnd:   SustainEnd = index;    break;
                default:  return false;
            }
            Cached = false;
            return true;
        }
        public bool SetLoopPt(TrackerEnvelope.LoopType type, ValueTuple<int,int> index)
        {   
            if (index.Item1 > index.Item2) return false;
            if (index.Item1 < 0 || index.Item1 >= pts.Count) return false;
            if (index.Item2 < 0 || index.Item2 >= pts.Count) return false;
            switch(type) {
                case TrackerEnvelope.LoopType.Basic:  LoopStart = index.Item1;  LoopEnd = index.Item2;  break;
                case TrackerEnvelope.LoopType.Sustain:  SustainStart = index.Item1;  SustainEnd = index.Item2;  break;
                case TrackerEnvelope.LoopType.Compound:
                    LoopStart = index.Item1;  LoopEnd = index.Item2;
                    SustainStart = index.Item1;  SustainEnd = index.Item2;
                    break;
                default:  return false; }
            Cached = false;
            return true;
        }
        public bool SetLoopPt(TrackerEnvelope.LoopType type, System.Numerics.Vector2 index) => SetLoopPt(type, ((int)index.X, (int)index.Y));

        //Godot helpers
        #if GODOT
        public void SetPoint(int index, Godot.Vector2 value)
        {
            var pt = new TrackerEnvelopePoint();
            pt.Millisecs = value.x;  pt.Value = value.y;
            pts[index] = pt;
            Cached = false;
        } 
        public bool SetLoopPt(TrackerEnvelope.LoopType type, Godot.Vector2 index) => SetLoopPt(type, ((int)index.x, (int)index.y));
        #endif



#region IO
        public bool Configure(JSONObject j)  //Loads points in without caring about associated types or whatever
        {
            try{
                var pts = j.GetItem<float>("pts", new float[]{0f, 0f});  //Get the points array so we can set the initial value in output's ctor

                //Load the points in, save for the first point, which we already specified in the ctor with an initial value.
                for(int i=2; i<pts.Length; i+=2)
                    Pts.Add(new TrackerEnvelopePoint( (pts[i], pts[i+1]) ));
                if (Pts.Count < 1)  throw new PE_ImportException(IOErrorFlags.Corrupt, $"Failed to import {nameof(TrackerEnvelope)}: no points found");
                
                if (j.HasItem("loopStart") && j.HasItem("loopEnd"))  //Import loop
                {                    
                    LoopStart = j.GetItem("loopStart").ToInt();
                    LoopEnd = j.GetItem("loopEnd").ToInt();
                    if (LoopEnd >= LoopStart)  //Loop is probably sane, greenlight it  
                        Looping |= TrackerEnvelope.LoopType.Basic;
                    //Finally, sanitize the loop to fit within the number of points we have
                    LoopStart = Math.Max(0, LoopStart);
                    LoopEnd = Math.Min(Pts.Count-1, LoopEnd);
                }
                if (j.HasItem("sustainStart") && j.HasItem("sustainEnd"))  //Import sustain
                {  
                    SustainStart = j.GetItem("sustainStart").ToInt();
                    SustainEnd = j.GetItem("sustainEnd").ToInt();
                    if (SustainEnd >= SustainStart)  //Loop is probably sane, greenlight it  
                        Looping |= TrackerEnvelope.LoopType.Sustain;
                    //Finally, sanitize the sustain to fit within the number of points we have
                    SustainStart = Math.Max(0, SustainStart);
                    SustainEnd = Math.Min(Pts.Count-1, SustainEnd);
                }

                return true;
            } catch (Exception e) {
                System.Diagnostics.Debug.Print(e.Message);
                return false;
            }
        }

        public static TrackerEnvelope<T> FromJSON(JSONObject j, string dataSource="", string dataMember="")
        {
            try{
                TrackerEnvelope output;
                //First, we need to determine the type of our associated property/field.  We create a TrackerEnvelope<T> based on the most compatible type.
                var dataSourceType = Type.GetType(j.GetItem("dataSource", dataSource));
                var member = dataSourceType.GetMember(j.GetItem("memberName", dataMember))[0];
                var pts = j.GetItem<float>("pts", new float[]{0f, 0f});  //Get the points array so we can set the initial value in output's ctor

                switch(member.GetUnderlyingType())
                {
                    case var pFloat when pFloat == typeof(float):
                    case var pDouble when pDouble == typeof(double):
                        output = new TrackerEnvelope<float>(j.GetItem("minValue", -1.0f), j.GetItem("maxValue", 1.0f), pts[1]);
                        break;

                    default:  //Probably an int or int-compatible field
                        output = new TrackerEnvelope<int>(j.GetItem("minValue", short.MinValue), j.GetItem("maxValue", short.MaxValue), (int)pts[1]);
                        break;
                    
                }

                //Load the points in, save for the first point, which we already specified in the ctor with an initial value.
                for(int i=2; i<pts.Length; i+=2)
                    output.Pts.Add(new TrackerEnvelopePoint( (pts[i], pts[i+1]) ));
                if (output.Pts.Count < 1)  throw new PE_ImportException(IOErrorFlags.Corrupt, $"Failed to import {nameof(TrackerEnvelope)}: no points found");

                //Try to figure out where this envelope came from and what it should be bound to.
                switch(member.MemberType)
                {
                    case System.Reflection.MemberTypes.Property:
                        output.SetDataSource(dataSourceType, dataSourceType.GetType().GetProperty(member.Name));
                        break;
                    case System.Reflection.MemberTypes.Field:
                        output.SetDataSource(dataSourceType, dataSourceType.GetType().GetField(member.Name));
                        break;
                    default:
                        System.Diagnostics.Debug.Print(
                            $"{nameof(TrackerEnvelope)}: Import failed to find member {member?.Name?? "[invalid]"} in {dataSourceType?.Name?? "(invalid data source)"}.");
                        break;
                }                
                
                if (j.HasItem("loopStart") && j.HasItem("loopEnd"))  //Import loop
                {                    
                    output.LoopStart = j.GetItem("loopStart").ToInt();
                    output.LoopEnd = j.GetItem("loopEnd").ToInt();
                    if (output.LoopEnd >= output.LoopStart)  //Loop is probably sane, greenlight it  
                        output.Looping |= TrackerEnvelope.LoopType.Basic;
                    //Finally, sanitize the loop to fit within the number of points we have
                    output.LoopStart = Math.Max(0, output.LoopStart);
                    output.LoopEnd = Math.Min(output.Pts.Count-1, output.LoopEnd);
                }
                if (j.HasItem("sustainStart") && j.HasItem("sustainEnd"))  //Import sustain
                {  
                    output.SustainStart = j.GetItem("sustainStart").ToInt();
                    output.SustainEnd = j.GetItem("sustainEnd").ToInt();
                    if (output.SustainEnd >= output.SustainStart)  //Loop is probably sane, greenlight it  
                        output.Looping |= TrackerEnvelope.LoopType.Sustain;
                    //Finally, sanitize the sustain to fit within the number of points we have
                    output.SustainStart = Math.Max(0, output.SustainStart);
                    output.SustainEnd = Math.Min(output.Pts.Count-1, output.SustainEnd);
                }

                return (TrackerEnvelope<T>) output;
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
        public JSONObject ToJSONObject()
        {
            var o = new JSONObject();
            o.AddPrim("type", BindType);

            switch (associatedProperty.GetUnderlyingType())
            {
                case var pFloat when pFloat == typeof(float):
                case var pDouble when pDouble == typeof(double):
                    o.AddPrim("minValue", (float)Convert.ChangeType(minValue, typeof(float)));
                    o.AddPrim("maxValue", (float)Convert.ChangeType(maxValue, typeof(float)));
                    break;
                default:
                    o.AddPrim("minValue", (int)Convert.ChangeType(minValue, typeof(int)));
                    o.AddPrim("maxValue", (int)Convert.ChangeType(maxValue, typeof(int)));
                    break;
            }

            var pts = new JSONArray();
            for (int i=0; i < this.pts.Count; i++)
            {   //Every odd value is millisecs (x), every even value is the raw value
                pts.AddPrim(this.pts[i].Millisecs);
                pts.AddPrim(this.pts[i].Value);
            }
            o.AddItem("pts", pts);

            o.AddPrim("dataSource", AssociatedDataSource); //Added for the sake of when the front-end fetches a bind directly.  Maybe could be done on the frontend...
            o.AddPrim("memberName", AssociatedDataMember);

            //lööps
            if (Looping.HasFlag(TrackerEnvelope.LoopType.Basic))
            {
                o.AddPrim("loopStart", LoopStart);
                o.AddPrim("loopEnd", LoopEnd);
            }
            if (Looping.HasFlag(TrackerEnvelope.LoopType.Sustain))
            {
                o.AddPrim("sustainStart", SustainStart);
                o.AddPrim("sustainEnd", SustainEnd);
            }

            return o;
        }
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
