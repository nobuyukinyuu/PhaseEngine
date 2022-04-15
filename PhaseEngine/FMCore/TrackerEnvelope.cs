using System;
using PhaseEngine;
using System.Collections.Generic;

namespace PhaseEngine
{
    public interface IBindable  //Objects that can assign TrackerEnvelope binds to fields.  Typically EGs and increment data.

    {
        //Gets a copy of every cached envelope bound to a property in this IBindable.
        public Dictionary<string, TrackerEnvelope> BoundEnvelopes {get;} //References to envelope bind data needed to create caches.


        //Calls TrackerEnvelope.Create to add a TrackerEnvelope to BoundEnvelopes
        //It's up to the implementation on how to specify min and max values
        public bool Bind(string property, int minValue, int maxValue);  //This would have a default implementation in c# 8.0....
        public void Unbind(string property); 

    }
    public interface IBindableDataConsumer  //Object which consumes data from an IBindable.  Typically an operator
    {
        //BindStates below are used to cycle through the cached envelopes for 
        public Dictionary<string, CachedEnvelope> BindStates {get;} //When IBindable invokes an update to one of its fields, these are used to determine how to update it.


        //Methods used to signal the start and release of the bound envelopes so they can loop properly.
        public void NoteOn();
        public void NoteOff();

        public void Clock();  //Where all the local caches are clocked and where the instance calls TrackerEnvelope.Update() to modify its bound members.
    }

    /// summary:  Provides static methods for handling binds in PhaseEngine
    public static class BindManager
    {
        public static readonly Action NO_ACTION = ()=> {}; //Provided to binds which have no special action associated with an update tick.

        //Create a TrackerEnvelope that is bound to one of an IBindable's properties.  
        //The output is used later for generating copies of the delta envelope for an IBindable instance to apply to its own properties.
        public static TrackerEnvelope Bind(IBindable invokerPattern, String property, int minValue, int maxValue)
        {
            //Considerations:  When NoteOn occurss, Channel should check
            //Each Envelope's "cached/baked" field to see if caches need recalculating. Each Operator and Filter should
            //on its own Clock, cycle through the appropriate bind list as necessary and apply them to the bound fields.
            //Calling Envelope.ChangeValue should pause any envelopes bound to the same value automatically.  Consider
            //Writing methods to pause and resume a TrackerEnvelope in Envelope so uers manually changing values can 
            //resume where they left off instead of killing off the functionality entirely until next NoteOn.

            //Consider creating an IBindable interface to request the target have its own method to bake cached points.
            //Increments need to be calculated in an operation that could be expensive, and some fields in other objects
            //Would be better served by having ranges that are more user friendly that can be taken as input and baked
            //into values more appropriate for the target field/property.


            var output = new TrackerEnvelope(minValue, maxValue);


            switch(invokerPattern.GetType().GetMember(property)[0].MemberType)
            {
                case System.Reflection.MemberTypes.Property:
                    output.associatedValue = invokerPattern.GetType().GetProperty(property);
                    break;
                case System.Reflection.MemberTypes.Field:
                    output.associatedValue = invokerPattern.GetType().GetField(property);
                    break;
                default:
                    throw new ArgumentException("Can't bind to this member.");
            }

            //Determine whether the bind member is valid for automation by envelope
            Type type=null;
            switch(output.associatedValue){
                case System.Reflection.PropertyInfo p:
                    type = p.PropertyType;
                    break;
                case System.Reflection.FieldInfo f:
                    type = f.FieldType;
                    break;
            }

            if(!type.IsValueType) throw new NotSupportedException("Bound field must be a value type.");

            return output;
        }

        //Used to update the fields we're bound to in the specified invoker. Done whenever invoker's clock says it's OK to update.
        //Invoker's clock method should probably be where we also call recalcs for things like filters and increments...
        public static void Update(IBindableDataConsumer invoker, IBindable dataSource, Action action)  //IMPORTANT
        {
            //Only sets the value directly to current state.  Clocking must be done in invokers
            var envelope = dataSource.BoundEnvelopes;
            var state = invoker.BindStates;
            // for(int i=0; i<envelope.Count; i++)
            foreach(string item in envelope.Keys)
                switch(envelope[item].associatedValue)
                {
                    case System.Reflection.PropertyInfo property:
                        property.SetValue(invoker, state[item].currentPoint.currentValue);
                        break;
                    case System.Reflection.FieldInfo field:
                        field.SetValue(invoker, state[item].currentPoint.currentValue);
                        break;
                }

            action();
        }

    }


    //Contains the general definitions for serialization and copying purposes, and can create binds for bindable interfaces to use.
    //TODO:  Serialization routines
    public class TrackerEnvelope
    {
        public readonly int minValue, maxValue; //Values used for clamping and maybe some other calcs. Assigned from bind invoker

        public System.Reflection.MemberInfo associatedValue;  //The field or property this envelope is bound to.
        public bool cached = false;  //Invalidate this whenever we are modified in some way.

        public List<TrackerEnvelopePoint> pts = new List<TrackerEnvelopePoint>();
        private CachedEnvelope cache;


        public TrackerEnvelope()    {cache = new CachedEnvelope(this);}
        public TrackerEnvelope(int minValue, int maxValue) 
            {this.minValue = minValue; this.maxValue = maxValue; cache = new CachedEnvelope(this);}


        // public TrackerEnvelopePoint this[int i] {get=>pts[i];  
        //     set{
        //         pts[i]=value;
        //         cached=false;
        //     }
        // }

        public CachedEnvelope CachedEnvelope{ get{if(!cached) Bake();  return cache;} }

        public void Bake()
        {
            cache.Bake(this);
            cached = true;
        }


        public void Start() {}

        public void Clock() {}

    }

    //This is the envelope data that is sent as copies to the operators which perform the clocking. A copy is passed to relevant classes at NoteOn.
    public class CachedEnvelope : List<CachedEnvelopePoint>
    {
        private CachedEnvelope(){}  //Can't call default ctor for us.  Create one from a TrackerEnvelope.
        public CachedEnvelope(TrackerEnvelope src) => Bake(src);
        public CachedEnvelope(CachedEnvelope prototype) : base(prototype) {} //Copy constructor for when we don't need to rebake
        public CachedEnvelope(int capacity) : base(capacity) {}

        public CachedEnvelopePoint currentPoint;

        void Start() {}  //TODO:  reset 
        void Restart() {}

        public void Bake(TrackerEnvelope src)
        {
            Clear();
            for(int i=0; i<src.pts.Count-1; i++)
                Add(CachedEnvelopePoint.Create(src.pts[i], src.pts[i+1]));

            if(Count>0) currentPoint = this[0];
        }

        //TODO:  Rebake methods for individual points?

        public void Clock() {}

    }

    /// summary:  typical representation of a point on a tracker envelope.  
    public struct TrackerEnvelopePoint 
    {
        System.Numerics.Vector2 vec;
        // public static readonly TrackerEnvelopePoint INITIAL = new TrackerEnvelopePoint(0);


        TrackerEnvelopePoint(float n) => vec=new System.Numerics.Vector2(n);
        TrackerEnvelopePoint(float n, float m) => vec=new System.Numerics.Vector2(n,m);


        public float Millisecs {get=>vec.X; set=>vec.X = value;}
        public float Value {get=>vec.Y; set=>vec.Y = value;}
        public int Samples => (int)(vec.X*Global.MixRate/1000f);  //Number of sample frames elapsed needed to reach this envelope point.

    }

    /// summary:  A struct representing a TrackerEnvelopePoint's transition to get from one point to another over a given timeframe.
    public struct CachedEnvelopePoint
    {
        public int initialValue, length;  //Value to snap to when a new point is started, and the length of the transition from one point to another.
        int totalProgress;   
        public bool Finished => totalProgress >= length;
        public int currentValue;  //Current delta Y value in the transition's lifetime.
        double delta; //Delta impulse that would be applied every frame if we worked in floats.  We don't, though.

        //The whole and fractional parts of the value delta.  
        int whole; 
        double frac;  

        int cycles, c;  //Number of samples to count up to before adding ±1 and associated counter.  This value should probably be 1/frac.
        SByte tweakAmt;  //The amount to add or subtract from the current value in a TrackerEnvelope when the cycle counter resets.

        public static CachedEnvelopePoint Create(TrackerEnvelopePoint A, TrackerEnvelopePoint B)
        {
            var p = new CachedEnvelopePoint();
            p.currentValue = p.initialValue = (int) Math.Round(A.Value);
            p.length = B.Samples - A.Samples;

            p.delta = (B.Value - A.Value) / p.length;
            p.whole = (int) Math.Truncate(p.delta);
            p.frac =  (p.delta % 1.0);
            p.tweakAmt = (SByte) Math.Sign(p.frac); 

            p.cycles = (int) Math.Ceiling(1.0/p.frac);  //Prefer longer rather than shorter cycle counts;  We'd rather undershoot any tweak amounts than overshoot.

            return p;
        }

        public int Clock()
        {
            c++;
            if (c>=cycles)
            {
                c=0;
                currentValue += tweakAmt;
            }
            currentValue += whole;  //This value can be 0 and in long transitions, often is
            totalProgress++;
            return currentValue;
        }

        public void Reset()
        {
            currentValue = initialValue;
            totalProgress = 0;
        }
    }

}
