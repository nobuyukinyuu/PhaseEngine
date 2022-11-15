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
        public enum PtMarker {None=0, LoopStart=1, LoopEnd=2, SustainStart=4, SustainEnd=8}
        [Flags] public enum LoopType {None=0, Basic=1, Sustain=2, Compound=3}
        public LoopType looping = LoopType.None;
        internal int loopStart, loopEnd, sustainStart, sustainEnd;  //Loop points

        public readonly int minValue, maxValue; //Values used for clamping and maybe some other calcs. Assigned from bind invoker
        public int InitialValue{get=> (int)pts[0].Value; set 
        { 
            pts[0] = new TrackerEnvelopePoint(0, (float)value);
            cached = false;
        } }

        public double ClockDivider {get=>divider; set{if(value<=0) throw new ArgumentOutOfRangeException("Clock divider must be >0"); divider=value;}}
        // public double TicksPerSecond {get=> (Global.MixRate/divider); set=> ClockDivider=(Global.MixRate/value);}
        double divider=1;

        //Helpers
        internal System.Type dataSourceType;  //The type of data source which wants its associated value to be automated by this envelope.
        internal System.Reflection.MemberInfo associatedProperty; //The field or property this envelope is expected to bind to. Used by BindManager to update data consumers


        //These properties help with identifying what this envelope might've been bound to when serializing out data.
        public string AssociatedDataType{get => dataSourceType?.Name ?? "None";}
        public string AssociatedProperty{get => associatedProperty?.Name ?? "Unknown";}
        //TODO:  Method for UnboundCopy()


        public List<TrackerEnvelopePoint> Pts {get=>pts;}
        protected List<TrackerEnvelopePoint> pts = new List<TrackerEnvelopePoint>();
        protected CachedEnvelopeInt cache;
        public bool cached = false;  //Invalidate this whenever we are modified in some way.


        protected TrackerEnvelope()    {pts.Add(new TrackerEnvelopePoint(0)); cache=new CachedEnvelopeInt(this);}
        public TrackerEnvelope(int minValue, int maxValue) : this()
            {this.minValue = minValue; this.maxValue = maxValue; }
        public TrackerEnvelope(int minValue, int maxValue, int initialValue) : this(minValue, maxValue)
            {InitialValue = initialValue;}


        //Convenient property to rebake a cached envelope
        // public CachedEnvelope CachedEnvelope{ get{if(!cached) Bake();  return cache;} }
        // public CachedEnvelope CachedEnvelopeCopy{ get{if(!cached) Bake();  return new CachedEnvelope(cache);} }
        public CachedEnvelopeInt CachedEnvelopeCopy(int chipDivider=1)
        {
            if(!cached) Bake(chipDivider);
            return new CachedEnvelopeInt(cache);
        }

        public void Bake(int chipDivider=1)
        {
            cache.Bake(this, chipDivider);
            cached = true;
        }

        internal void Insert(int index, ValueTuple<float, float> value) {pts.Insert(index, new TrackerEnvelopePoint(value)); cached = false;}
        internal void Remove(int index) {pts.RemoveAt(index); cached = false;}

        internal void SetPoint(int index, TrackerEnvelopePoint value) {pts[index] = value; cached = false;}
        internal void SetPoint(int index, System.Numerics.Vector2 value) {pts[index] = new TrackerEnvelopePoint(value); cached = false;}
        public void SetPoint(int index, ValueTuple<float, float> value) {pts[index] = new TrackerEnvelopePoint(value); cached = false;}

        //"Direct" set of loop points.  Does not check if the indices are sane
        public bool SetLoopPt(PtMarker type, int index)
        {
            TypedReference targetRef;
            switch(type) {   //Get the pointer to the target loop point
                case PtMarker.LoopStart:    targetRef= __makeref(loopStart);     break;
                case PtMarker.LoopEnd:      targetRef= __makeref(loopEnd);       break;
                case PtMarker.SustainStart: targetRef= __makeref(sustainStart);  break;
                case PtMarker.SustainEnd:   targetRef= __makeref(sustainEnd);    break;
                default:  return false;
            }
            var target = __refvalue(targetRef, int);
            target = index;
            cached = false;
            return true;
        }
        public bool SetLoopPt(LoopType type, ValueTuple<int,int> index)
        {   
            if (index.Item1 > index.Item2) return false;
            if (index.Item1 < 0 || index.Item1 >= pts.Count) return false;
            if (index.Item2 < 0 || index.Item2 >= pts.Count) return false;
            switch(type) {
                case LoopType.Basic:  loopStart = index.Item1;  loopEnd = index.Item2;  break;
                case LoopType.Sustain:  sustainStart = index.Item1;  sustainEnd = index.Item2;  break;
                case LoopType.Compound:
                    loopStart = index.Item1;  loopEnd = index.Item2;
                    sustainStart = index.Item1;  sustainEnd = index.Item2;
                    break;
                default:  return false; }
            cached = false;
            return true;
        }
        public bool SetLoopPt(LoopType type, System.Numerics.Vector2 index) => SetLoopPt(type, ((int)index.X, (int)index.Y));

        //Godot helpers
        #if GODOT
        public void SetPoint(int index, Godot.Vector2 value)
        {
            var pt = new TrackerEnvelopePoint();
            pt.Millisecs = value.x;  pt.Value = value.y;
            pts[index] = pt;
            cached = false;
        } 
        public bool SetLoopPt(LoopType type, Godot.Vector2 index) => SetLoopPt(type, ((int)index.x, (int)index.y));
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
                if (output.pts.Count < 1)  throw new PE_ImportException(IOErrorFlags.Corrupt, $"Failed to import {nameof(TrackerEnvelope)}: no points found");

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
                            $"{nameof(TrackerEnvelope)}: Import failed to find member {property} in {output.dataSourceType?.Name ?? "(invalid data source)"}.");
                        break;
                }                
                
                if (j.HasItem("loopStart"))  //Import loop
                {
                    j.Assign("loopStart", ref output.loopStart);
                    j.Assign("loopEnd", ref output.loopEnd);
                    if (output.loopEnd >= output.loopStart)  //Loop is probably sane, greenlight it  
                        output.looping |= LoopType.Basic;
                    //Finally, sanitize the loop to fit within the number of points we have
                    output.loopStart = Math.Max(0, output.loopStart);
                    output.loopEnd = Math.Min(output.Pts.Count-1, output.loopEnd);
                }
                if (j.HasItem("sustainStart"))  //Import sustain
                {
                    j.Assign("sustainStart", ref output.sustainStart);
                    j.Assign("sustainEnd", ref output.sustainEnd);
                    if (output.sustainEnd >= output.sustainStart)  //Loop is probably sane, greenlight it  
                        output.looping |= LoopType.Sustain;
                    //Finally, sanitize the sustain to fit within the number of points we have
                    output.sustainStart = Math.Max(0, output.sustainStart);
                    output.sustainEnd = Math.Min(output.Pts.Count-1, output.sustainEnd);
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
        internal virtual JSONObject ToJSONObject()
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

            //lööps
            if (looping.HasFlag(LoopType.Basic))
            {
                o.AddPrim("loopStart", loopStart);
                o.AddPrim("loopEnd", loopEnd);
            }
            if (looping.HasFlag(LoopType.Sustain))
            {
                o.AddPrim("sustainStart", sustainStart);
                o.AddPrim("sustainEnd", sustainEnd);
            }

            return o;
        }
        public string ToJSONString() => ToJSONObject().ToJSONString();
#endregion
    }

    public class TrackerEnvelopeF : TrackerEnvelope
    {
        public new readonly float minValue, maxValue; //Values used for clamping and maybe some other calcs. Assigned from bind invoker
        public new float InitialValue{get=> pts[0].Value; set 
        { 
            pts[0] = new TrackerEnvelopePoint(0, (float)value);
            cached = false;
        } }
        protected new CachedEnvelopeFloat cache;

        protected TrackerEnvelopeF()    {cache=new CachedEnvelopeFloat(this);}
        public TrackerEnvelopeF(float minValue, float maxValue) : this()
            {this.minValue = minValue; this.maxValue = maxValue; }
        public TrackerEnvelopeF(float minValue, float maxValue, float initialValue) : this(minValue, maxValue)
            {InitialValue = initialValue;}

        public new CachedEnvelopeFloat CachedEnvelopeCopy(int chipDivider=1)
        {
            if(!cached) Bake(chipDivider);
            return new CachedEnvelopeFloat(cache);
        }

        internal override JSONObject ToJSONObject()
        {
            var o = base.ToJSONObject();
            //Replace the base object's min and max values with our floating point ones.
            o.AddPrim("minValue", minValue);
            o.AddPrim("maxValue", maxValue);
            return o;
        }

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
