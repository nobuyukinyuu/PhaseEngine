using System;
using PhaseEngine;
using System.Collections.Generic;
using PE_Json;

namespace PhaseEngine
{
    public interface ICachedEnvelopePointTransition<T>
    {
        public T InitialValue {get;set;}
        public T CurrentValue {get;set;}
        public ushort NextPoint {get;set;}

        public int Millisecs {get;}
        public bool Finished {get;}
        public int Spillover {get;}

        public static ICachedEnvelopePointTransition<T> CreatePlaceholder(T value)
        {
            switch(value)
            {
                case float _:
                case double _:
                    var p = new CachedEnvelopePointF();
                    p.initialValue = p.currentValue = (float)Convert.ChangeType(value, typeof(float));
                    return p as ICachedEnvelopePointTransition<T>;
                case int _:
                    var q = new CachedEnvelopePoint();
                    q.initialValue = q.currentValue = (int)Convert.ChangeType(value, typeof(int));
                    return q as ICachedEnvelopePointTransition<T>;
                default:  throw new InvalidCastException($"Attempt to create placeholder of unsupported type {typeof(T).ToString()}");
            };
        }
        public ICachedEnvelopePointTransition<T> Create(TrackerEnvelopePoint A, TrackerEnvelopePoint B, double divider, int nextPoint);
        public ICachedEnvelopePointTransition<T> ScaledBy(float amount);
        public ICachedEnvelopePointTransition<T> Plus(float amount);

        public T Clock();
        public void Reset();
        public void SetSpillover(int pos);  //Moves to a specific point in the transition based on spillover from last transition due to clock miss.
    }

    /// summary:  A struct representing a TrackerEnvelopePoint's transition to get from one point to another over a given timeframe.
    public struct CachedEnvelopePoint : ICachedEnvelopePointTransition<int>
    {
        public int InitialValue {get=>initialValue; set=>initialValue=value;}
        public int CurrentValue {get=>currentValue; set=>currentValue=value;}
        public int initialValue, currentValue;  //Value to snap to when a new point is started, and the length of the transition from one point to another.
        public ushort nextPoint; public ushort NextPoint{get=>nextPoint; set=>nextPoint = value;}
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
        public ICachedEnvelopePointTransition<int> CreatePlaceholder(int value)
        {
            var p = new CachedEnvelopePoint();
            p.initialValue = p.currentValue = value;
            return p;
        }
        public ICachedEnvelopePointTransition<int> Create(TrackerEnvelopePoint A, TrackerEnvelopePoint B, double divider, int nextPoint)
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
        public ICachedEnvelopePointTransition<int> ScaledBy(float amount)
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
        public ICachedEnvelopePointTransition<int> Plus(float amount)
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
        =>  totalProgress = pos;

        public void FastForward(int pos)
        {
            totalProgress = pos;
            double inc = delta * (pos/(double)clockIncrement);
            currentValue = (int)Math.Round(inc);
        }
        public void FastForwardToEnd() => FastForward(length);

        public void Reset()
        {
            currentValue = initialValue;
            totalProgress = 0;
        }
    }



    public struct CachedEnvelopePointF : ICachedEnvelopePointTransition<float>
    {
        public float InitialValue {get=>initialValue; set=>initialValue=value;}
        public float CurrentValue {get=>currentValue; set=>currentValue=value;}
        public float initialValue, currentValue;  //Value to snap to when a new point is started, and the length of the transition from one point to another.
        public ushort nextPoint; public ushort NextPoint{get=>nextPoint; set=>nextPoint = value;}
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
        public ICachedEnvelopePointTransition<float> CreatePlaceholder(float value)
        {
            var p = new CachedEnvelopePointF();
            p.initialValue = p.currentValue = value;
            return p;
        }

        public ICachedEnvelopePointTransition<float> Create(TrackerEnvelopePoint A, TrackerEnvelopePoint B, double divider, int nextPoint)
        {
            var p = new CachedEnvelopePointF();
            p.currentValue = p.initialValue = A.Value; 

            p.nextPoint = (ushort) nextPoint;
            
            p.length = B.Samples - A.Samples;
            p.clockIncrement = (int)divider;

            if (p.length>divider){
                p.delta = (B.Value - A.Value) / (double)(B.Samples - A.Samples) * divider;
            } else p.delta = (B.Value - A.Value);

            p.whole = (int) Math.Truncate(p.delta);
            p.frac =  (p.delta % 1.0); //Remainder, not modulo. When adding frac+whole this results in the original delta
            p.tweakAmt = (SByte) Math.Sign(p.delta); 

            if (p.frac != 0)  //Prefer longer rather than shorter cycle counts;  We'd rather undershoot any tweak amounts than overshoot.
                p.cycles = (int) Math.Ceiling(1.0/Math.Abs(p.frac));  

            return p;
        }

        //Produces another CachedEnvelopePoint scaled by the amount specified, as a multiplier
        public ICachedEnvelopePointTransition<float> ScaledBy(float amount)
        {
            var p = new CachedEnvelopePointF();
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
        public ICachedEnvelopePointTransition<float> Plus(float amount)
        {
            var p = new CachedEnvelopePointF();
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

        public float Clock()
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
        =>  totalProgress = pos;

        public void FastForward(int pos)
        {
            totalProgress = pos;
            double inc = delta * (pos/(double)clockIncrement);
            currentValue = (int)Math.Round(inc);
        }
        public void FastForwardToEnd() => FastForward(length);

        public void Reset()
        {
            currentValue = initialValue;
            totalProgress = 0;
        }
    }


}