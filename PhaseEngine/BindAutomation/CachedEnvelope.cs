using System;
using PhaseEngine;
using System.Collections.Generic;
using PE_Json;

namespace PhaseEngine
{
    //This is the envelope data that is sent as copies to the operators which perform the clocking. A copy is passed to relevant classes at NoteOn.
    public class CachedEnvelope : List<CachedEnvelopePoint>
    {
        [Flags]
        enum PtMarker {None=0, LoopStart=1, LoopEnd=2, SustainStart=4, SustainEnd=8}

        private CachedEnvelope(){}  //Can't call default ctor for us.  Create one from a TrackerEnvelope.
        public CachedEnvelope(TrackerEnvelope src) => Bake(src);
        public CachedEnvelope(CachedEnvelope prototype) => Bake(prototype); //Copy constructor for when we don't need to rebake
        public CachedEnvelope(int capacity) : base(capacity) {}

        public CachedEnvelopePoint currentPoint;
        int idx = 0;  public int EnvelopePosition => idx;
        int currentValue;

        public bool Finished => idx >= Count;

        void Start() {}  //TODO:  reset 
        void Restart() {}

        void Bake(CachedEnvelope prototype) //Rebakes itself using a prototype
        {
            Clear();
            if (prototype.Count > 0) currentPoint = prototype[0];
            else {currentValue = prototype.currentValue; currentPoint = prototype.currentPoint; return;}

            Capacity = Math.Min(prototype.Count, 1);
            for(int i=0; i<prototype.Count; i++)
                Add(prototype[i]);  //ByVal copy

            currentPoint = this[0];
            idx=0;
            currentValue = prototype[0].initialValue;
        }

        public void Bake(TrackerEnvelope src)
        {
            Clear();
            for(int i=0; i<src.pts.Count-1; i++)
                Add(CachedEnvelopePoint.Create(src.pts[i], src.pts[i+1]));

            if(Count>0) 
            {
                currentPoint = this[0];
                idx=0;
            } else if (src.pts.Count == 1) {
                //If there's only one TrackerEnvelopePoint,  then Finished will return True and the clock will always return whatever the initial value was.
                currentPoint = CachedEnvelopePoint.CreatePlaceholder(src.InitialValue);
                currentValue = src.InitialValue;
            } else {  //There were no points in the TrackerEnvelope.  This shouldn't be possible
                throw new InvalidOperationException("TrackerEnvelope size is 0!");
            }
        }

        //TODO:  Rebake methods for individual points?

        public int Clock() 
        {
            // System.Diagnostics.Debug.Assert(Count>0);
            if(Finished || Count==0) return currentValue;

            //TODO:  Stuff here to handle clocking at rates lower than the mix rate
            //TODO:  Stuff here to handle lööps
            currentPoint = this[idx];  //Yoink a copy of the point at the current index
            currentValue = currentPoint.Clock();
            if (currentPoint.Finished)  //Next point
            {
                // currentPoint.Reset();  //May not be necessary if we're not using copy constructors but baking new envelopes each time
                this[idx] = currentPoint;  //Shove the modified value back into our collection since it was copied locally by value
                idx++;
                currentValue = this[Math.Min(idx, Count-1)].initialValue;
            } else {
                this[idx] = currentPoint;  //Shove the modified value back into our collection since it was copied locally by value
            }
            return currentValue;
        }

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

        int cycles, tweakCounter;  //Number of samples to count up to before adding ±1 and associated counter.  This value should probably be 1/frac.
        SByte tweakAmt;  //The amount to add or subtract from the current value in a TrackerEnvelope when the cycle counter resets.

        //Used to create a placeholder point for CachedEnvelopes of length 0 or prototypes that don't have delta data
        public static CachedEnvelopePoint CreatePlaceholder(int value)
        {
            var p = new CachedEnvelopePoint();
            p.initialValue = p.currentValue = value;
            return p;
        }
        public static CachedEnvelopePoint Create(TrackerEnvelopePoint A, TrackerEnvelopePoint B)
        {
            var p = new CachedEnvelopePoint();
            p.currentValue = p.initialValue = (int) Math.Round(A.Value);
            p.length = B.Samples - A.Samples;

            p.delta = p.length>1? (B.Value - A.Value) / p.length:  (B.Value - A.Value);
            p.whole = (int) Math.Truncate(p.delta);
            p.frac =  (p.delta % 1.0); //Remainder, not modulo. When adding frac+whole this results in the original delta
            p.tweakAmt = (SByte) Math.Sign(p.delta); 

            if (p.frac != 0)  //Prefer longer rather than shorter cycle counts;  We'd rather undershoot any tweak amounts than overshoot.
                p.cycles = (int) Math.Ceiling(1.0/Math.Abs(p.frac));  

            return p;
        }

        public int Clock()
        {
            tweakCounter++;
            if (tweakCounter>=cycles)  //Tick
            {
                tweakCounter=0;
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
