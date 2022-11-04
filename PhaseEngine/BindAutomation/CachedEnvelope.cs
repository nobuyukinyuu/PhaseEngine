using System;
using PhaseEngine;
using System.Collections.Generic;
using PE_Json;

namespace PhaseEngine
{
    //This is the envelope data that is sent as copies to the operators which perform the clocking. A copy is passed to relevant classes at NoteOn.
    public class CachedEnvelope : List<CachedEnvelopePoint>
    {
        TrackerEnvelope.LoopType looping = TrackerEnvelope.LoopType.None;
        int loopStart, loopEnd, sustainStart, sustainEnd;  //Loop points

        private CachedEnvelope(){}  //Can't call default ctor for us.  Create one from a TrackerEnvelope.
        public CachedEnvelope(TrackerEnvelope src) => Bake(src);
        public CachedEnvelope(CachedEnvelope prototype) => Bake(prototype); //Copy constructor for when we don't need to rebake
        public CachedEnvelope(int capacity) : base(capacity) {}

        public CachedEnvelopePoint currentPoint;
        int idx = 0;  public int EnvelopePosition => idx;
        public int currentValue;

        int tickCount, maxClocks=1;  //When baked, a prototype clock is created to reduce the number of calculations per second to automate the target field
        int chipDivider=1;  //The master clock divider for the chip using this cached envelope.  Separate from the clock divider for automating the target field.

        public bool Finished => idx >= Count;  //TODO:  && isLooping == false

        //Check after updating an envelope for a data consumer to determine whether to take an action, such as recalculating increments or filters
        public bool JustTicked => tickCount==0;  

        void Bake(CachedEnvelope prototype) //Partially rebakes itself using a prototype.  Typically called whenever a copy of a CachedEnvelope is needed
        {
            //Set up loop points
            looping = prototype.looping;
            loopStart = prototype.loopStart;  loopEnd = prototype.loopEnd;
            sustainStart = prototype.sustainStart;  sustainEnd = prototype.sustainEnd;

            //Set up clocking and deltas
            // this.chipDivider = prototype.chipDivider;  //Copying the chipDivider isn't needed because we're reusing the prototype's transitions
            maxClocks = prototype.maxClocks;
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

        public void Bake(TrackerEnvelope src, int chipDivider=1)  //Typically called by a TrackerEnvelope to rebake its cache from scratch when values change.
        {
            //Set up loop points
            looping = src.looping;
            loopStart = src.loopStart;  loopEnd = src.loopEnd;
            sustainStart = src.sustainStart;  sustainEnd = src.sustainEnd;

            //Set up clocking and deltas
            maxClocks = src.ClockDivider < 1?   1 : (int)maxClocks;
            this.chipDivider = chipDivider;
            Clear();
            for(int i=0; i<src.Pts.Count-1; i++)
                Add(CachedEnvelopePoint.Create(src.Pts[i], src.Pts[i+1], chipDivider, GetNextPoint(i)));

            if(Count>0) 
            {
                currentPoint = this[0];
                idx=0;
            } else if (src.Pts.Count == 1) {
                //If there's only one TrackerEnvelopePoint,  then Finished will return True and the clock will always return whatever the initial value was.
                currentPoint = CachedEnvelopePoint.CreatePlaceholder(src.InitialValue);
                currentValue = src.InitialValue;
            } else {  //There were no points in the TrackerEnvelope.  This shouldn't be possible
                throw new InvalidOperationException($"{nameof(TrackerEnvelope)} size is 0!");
            }
        }

        int GetNextPoint(int index)
        {
            switch (looping)
            {
                case TrackerEnvelope.LoopType.Compound:
                case TrackerEnvelope.LoopType.Sustain:
                    if (index+1 == sustainEnd)  return sustainStart;
                    break;
                case TrackerEnvelope.LoopType.Basic:
                    if (index+1 == loopEnd) return loopStart;
                    break;
            }
            return index+1;  //Default:  No loop
        }

        //Rescales a cached envelope with a multiplier supplied by rTables (Velocity, KSL / Key follow)
        public void Multiply(float multiplier)
        {
            for(int i=0; i<Count; i++)
                this[i] = this[i].ScaledBy(multiplier);

            currentValue = (int)(currentValue * multiplier);
            currentPoint = currentPoint.ScaledBy(multiplier);
        }
        public void AddAmount(float amount)
        {
            for(int i=0; i<Count; i++)
                this[i] = this[i].Plus(amount);

            currentValue = (int)(currentValue + amount);
            currentPoint = currentPoint.Plus(amount);
        }


        public void Clock() 
        {
            // System.Diagnostics.Debug.Assert(Count>0);
            if(Finished || Count==0) return;
            // if(looping==TrackerEnvelope.LoopType.Basic && (loopEnd==0 || (loopEnd>Count && loopStart>Count)))  return;
            // if(looping!=TrackerEnvelope.LoopType.None && sustainEnd==0)  return;  //Check sustain and compound loops.  FIXME:  disable this if noteOff

            tickCount++;
            if(tickCount < maxClocks) return;  //Not time to process yet.  Exit early
            tickCount=0;  //Reset tick counter.  It's now time to process.

            currentPoint = this[idx];  //Yoink a copy of the point at the current index
            currentValue = currentPoint.Clock();
            if (currentPoint.Finished)  //Next point
            {
                currentPoint.Reset();  //May not be necessary if we're not using copy constructors but baking new envelopes each time, depends on loop type
                this[idx] = currentPoint;  //Shove the modified value back into our collection since it was copied locally by value
                idx = currentPoint.nextPoint;

                // currentValue = this[Math.Min(idx, Count-1)].initialValue;
                if (!this.Finished)
                {
                    this[idx].SetSpillover(currentPoint.Spillover);  //Set the next point's initial position to the spillover from the last point's position.
                    currentValue = this[idx].initialValue;  //Set envelope value to the next point's initial value.
                }
            } else {
                this[idx] = currentPoint;  //Shove the modified value back into our collection since it was copied locally by value
            }
            return;
        }

    }

    /// summary:  A struct representing a TrackerEnvelopePoint's transition to get from one point to another over a given timeframe.
    public struct CachedEnvelopePoint
    {
        public int initialValue, currentValue;  //Value to snap to when a new point is started, and the length of the transition from one point to another.
        public ushort nextPoint;
        int totalProgress, clockIncrement, length;  //Fixed-point counters for traversing the length of the transition
        public int Millisecs {get=> length / clockIncrement;} 
        public bool Finished => totalProgress >= length;
        public int Spillover {get=> totalProgress - length;}


        //The whole and fractional parts of the value delta.  
        double delta; //Delta impulse that would be applied every frame if we worked in floats.  We don't, though.
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
        public static CachedEnvelopePoint Create(TrackerEnvelopePoint A, TrackerEnvelopePoint B, double divider, int nextPoint=0xFFFF)
        {
            var p = new CachedEnvelopePoint();
            p.currentValue = p.initialValue = (int) Math.Round(A.Value);
            p.nextPoint = (ushort) nextPoint;
            
            // var length = (B.Samples - A.Samples) / divider;
            // p.length = (int)((B.Samples - A.Samples) * divider);
            p.length = B.Samples - A.Samples;
            p.clockIncrement = (int)divider;

            if (p.length>divider){
                p.delta = (B.Value - A.Value) / (double)(B.Samples - A.Samples) * divider;
                // p.delta += (deltaTweak % 1.0) / p.length;  //Add the tiniest bit more to the delta to compensate for partial frames
            } else p.delta = (B.Value - A.Value);

            p.whole = (int) Math.Truncate(p.delta);
            p.frac =  (p.delta % 1.0); //Remainder, not modulo. When adding frac+whole this results in the original delta
            p.tweakAmt = (SByte) Math.Sign(p.delta); 

            if (p.frac != 0)  //Prefer longer rather than shorter cycle counts;  We'd rather undershoot any tweak amounts than overshoot.
                p.cycles = (int) Math.Ceiling(1.0/Math.Abs(p.frac));  

            return p;
        }

        //Produces another CachedEnvelopePoint scaled by the amount specified, as a multiplier
        public CachedEnvelopePoint ScaledBy(float amount)
        {
            var p = new CachedEnvelopePoint();
            p.length = length;
            p.initialValue = (int)(initialValue*amount);
            p.currentValue = (int)(currentValue*amount);
            p.delta = delta * amount;
            p.whole = (int) Math.Truncate(p.delta);
            p.frac =  (p.delta % 1.0); //Remainder, not modulo. When adding frac+whole this results in the original delta
            p.tweakAmt = (SByte) Math.Sign(p.delta);
            p.nextPoint = nextPoint;
            p.clockIncrement = clockIncrement;

            if (p.frac != 0)  //Prefer longer rather than shorter cycle counts;  We'd rather undershoot any tweak amounts than overshoot.
                p.cycles = (int) Math.Ceiling(1.0/Math.Abs(p.frac));  
            return p;
        }
        public CachedEnvelopePoint Plus(float amount)
        {
            var p = new CachedEnvelopePoint();
            p.length = length;
            p.initialValue = (int)(initialValue+amount);
            p.currentValue = (int)(currentValue+amount);
            p.delta = delta;
            p.whole = (int) Math.Truncate(p.delta);
            p.frac =  (p.delta % 1.0); //Remainder, not modulo. When adding frac+whole this results in the original delta
            p.tweakAmt = (SByte) Math.Sign(p.delta);
            p.nextPoint = nextPoint;
            p.clockIncrement = clockIncrement;
            
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
            totalProgress+= clockIncrement;
            return currentValue;
        }

        public void SetSpillover(int pos)  //Moves to a specific point in the transition based on spillover from last transition due to clock miss.
        {
            totalProgress = pos;
            // double inc = delta * (pos/(double)clockIncrement);
            // currentValue = (int)Math.Round(inc);
        }

        public void Reset()
        {
            currentValue = initialValue;
            totalProgress = 0;
        }
    }
}
