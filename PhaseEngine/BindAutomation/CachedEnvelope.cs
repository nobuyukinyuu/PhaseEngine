using System;
using PhaseEngine;
using System.Collections.Generic;
using PE_Json;

namespace PhaseEngine
{
    public interface CachedEnvelope
    {
        public void Bake(TrackerEnvelope src, int chipDivider=1); //Typically called by a TrackerEnvelope to rebake its cache from scratch when values change.

        //Partially rebakes itself using a prototype. Typically called whenever a copy of a CachedEnvelope is needed
        public void Bake(CachedEnvelope prototype); 

        public System.Reflection.MethodInfo PostUpdateAction {get;}

        public bool Finished {get;}

        //Check after updating an envelope for a data consumer to determine whether to take an action, such as recalculating increments or filters
        public bool JustTicked {get;}

        // ValueType this [int index] {get;}  //Indexer which spits out a value of some kind
        public object CurrentValue {get;}  
        public void Clock();
        public object AddAmount(float amount);
        public void Multiply(float multiplier);
        public void NoteOn();
        public void NoteOff();

    }


    //This is the envelope data that is sent as copies to the operators which perform the clocking. A copy is passed to relevant classes at NoteOn.
    public class CachedEnvelope<T> : List<ICachedEnvelopePointTransition<T>>, CachedEnvelope //where T: ICachedEnvelopePointTransition<T>
    {
        protected CachedEnvelope(){}  //Can't call default ctor for us.  Create one from a TrackerEnvelope.
        public CachedEnvelope(TrackerEnvelope src) => Bake(src, 1); //Typically called by a TrackerEnvelope to rebake its cache from scratch when values change.
        public CachedEnvelope(CachedEnvelope<T> prototype) => Bake(prototype); //Copy constructor for when we don't need to rebake
        public CachedEnvelope(int capacity) : base(capacity) {}

        public System.Reflection.MethodInfo PostUpdateAction {get;set;}

        protected ICachedEnvelopePointTransition<T> currentPoint;
        int idx = 0;  public int EnvelopePosition => idx;
        public bool Finished => idx >= Count;  

        TrackerEnvelope.LoopType looping = TrackerEnvelope.LoopType.None;
        int loopStart, loopEnd, sustainStart, sustainEnd;  //Loop points

        public T currentValue; public object CurrentValue {get=> currentValue;}

        //Check after updating an envelope for a data consumer to determine whether to take an action, such as recalculating increments or filters
        public bool JustTicked => tickCount==0;  
        int tickCount, maxClocks=1;  //When baked, a prototype clock is created to reduce the number of calculations per second to automate the target field
        int chipDivider=1;  //The master clock divider for the chip using this cached envelope.  Separate from the clock divider for automating the target field.


        // void Bake(ICachedEnvelopePointTransition<T> proto)

        // void Bake(CachedEnvelope<T> prototype) //Partially rebakes itself using a prototype.  Typically called whenever a copy of a CachedEnvelope is needed
        public void Bake(CachedEnvelope proto) //Partially rebakes itself using a prototype.  Typically called whenever a copy of a CachedEnvelope is needed
        {
            // CachedEnvelope<T> prototype = (CachedEnvelope<T>)Convert.ChangeType(proto, typeof(T));
            CachedEnvelope<T> prototype = proto as CachedEnvelope<T>;
            //Set up loop points
            looping = prototype.looping;
            loopStart = prototype.loopStart;  loopEnd = prototype.loopEnd;
            sustainStart = prototype.sustainStart;  sustainEnd = prototype.sustainEnd;

            //Set up clocking and deltas
            // this.chipDivider = prototype.chipDivider;  //Copying the chipDivider isn't needed because we're reusing the prototype's precalc'd transition times
            maxClocks = prototype.maxClocks;
            PostUpdateAction = prototype.PostUpdateAction;

            //Set up points
            Clear();
            if (prototype.Count > 0) currentPoint = prototype[0];
            else {currentValue = prototype.currentValue; currentPoint = prototype.currentPoint; return;}

            //Prototype has points.  Set capacity and bring them in.
            Capacity = Math.Max(prototype.Count, 1);
            for(int i=0; i<prototype.Count; i++)
            {
                Add((ICachedEnvelopePointTransition<T>) prototype[i].Clone());  //ByVal copy
            }

            Reset();
        }

        public void Bake(TrackerEnvelope src, int chipDivider=1) //Typically called by a TrackerEnvelope to rebake its cache from scratch when values change.
        {
            //Set up loop points
            looping = src.Looping;
            loopStart = src.LoopStart;  loopEnd = src.LoopEnd;
            sustainStart = src.SustainStart;  sustainEnd = src.SustainEnd;

            //Set up clocking and deltas
            maxClocks = src.ClockDivider < 1?   1 : (int)maxClocks;
            this.chipDivider = chipDivider;
            this.PostUpdateAction = src.PostUpdateAction;

            //Set up points
            Clear();
            for(int i=0; i<src.Pts.Count-1; i++)
                Add(currentPoint.Create(src.Pts[i], src.Pts[i+1], chipDivider, GetNextPoint(i)));

            if(Count>0) 
            {
                currentPoint = this[0];
                idx=0;
            } else if (src.Pts.Count == 1) {
                //If there's only one TrackerEnvelopePoint,  then Finished will return True and the clock will always return whatever the initial value was.
                var initialValue = (T)Convert.ChangeType(src.InitialValue, typeof(T));
                currentPoint = ICachedEnvelopePointTransition<T>.CreatePlaceholder(initialValue);
                currentValue = initialValue;
            } else {  //There were no points in the TrackerEnvelope.  This shouldn't be possible
                throw new InvalidOperationException($"{nameof(TrackerEnvelope)} size is 0!");
            }
        }

        int GetNextPoint(int index, TrackerEnvelope.LoopType? loopType = null)
        {
            if (loopType == null) loopType = looping;
            switch (loopType)
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

            currentPoint = currentPoint.ScaledBy(multiplier);
            currentValue = currentPoint.CurrentValue;
        }
        public object AddAmount(float amount)
        {
            if (amount==0) return currentValue;
            var method = currentPoint.GetType().GetMethod(nameof(ICachedEnvelopePointTransition<T>.Plus));
            for(int i=0; i<Count; i++)
                // this[i] = this[i].Plus(amount);
                this[i] = (ICachedEnvelopePointTransition<T>) method.Invoke(this[i], new object[]{amount});

            // currentPoint = currentPoint.Plus(amount);
            currentPoint = (ICachedEnvelopePointTransition<T>)
                            method.Invoke(currentPoint, new object[]{amount});
            currentValue = currentPoint.CurrentValue;
            return currentValue;
        }


        public void Clock() 
        {
            // System.Diagnostics.Debug.Assert(Count>0);
            if(looping==TrackerEnvelope.LoopType.None && Finished || Count==0) return;
            //Early exit conditions if the transition point ends on a hold (ie: start of loop is also end of loop)
            if(looping>=TrackerEnvelope.LoopType.Sustain && idx >= sustainEnd && sustainEnd==sustainStart)  return;
            else if(looping==TrackerEnvelope.LoopType.Basic && idx >= loopEnd && loopEnd==loopStart)  return;



            tickCount++;
            if(tickCount < maxClocks) return;  //Not time to process yet.  Exit early
            tickCount=0;  //Reset tick counter.  It's now time to process.

            currentPoint = this[idx];  //Yoink a copy of the point at the current index
            if (currentPoint.Finished)  //Next point
            {
                var spillover = this[idx].Spillover;

                //FIXME:  FOR SOME REASON THE METHOD DOES NOT RESOLVE THE PROPER REFERENCE 
                //        AND RESET IS NEVER CALLED HERE, BUT USING REFLECTION WE CAN INVOKE IT ANYWAY??
                // this[idx].Reset();
                this[idx].GetType().GetMethod(nameof(ICachedEnvelopePointTransition<T>.Reset)).Invoke(this[idx],null);

                // Godot.GD.Print($"Transition {idx} finished.  Reset {this[idx].CurrentValue}");
                // this[idx] = currentPoint;  //Shove the modified value back into our collection since it was copied locally by value
                idx = currentPoint.NextPoint;
                if (!this.Finished)
                    // Godot.GD.Print($"Grabbing Transition {idx}.  Initial:{this[idx].InitialValue}  Current:{this[idx].CurrentValue}  Final:{this[idx].FinalValue}");


                if (!Finished)
                {
                    // Godot.GD.Print($"Spilling over {spillover} to {idx}...");
                    this[idx].Spillover = spillover;  //Set the next point's initial position to the spillover from the last point's position.
                    currentValue = this[idx].InitialValue;  //Set envelope value to the next point's initial value.
                } else {
                    //End of the line.  Set current value to the final value.
                    // Godot.GD.Print($"Setting final value to {currentPoint.FinalValue}.");
                    currentValue = currentPoint.FinalValue;
                }

            } else {
                currentValue = this[idx].Clock();
                // this[idx] = currentPoint;  //Shove the modified value back into our collection since it was copied locally by value
            }
            return;
        }

        public void NoteOn() => Reset();
        public void NoteOff()
        {
            //Degrade the loop state so that sustain loops no longer apply
            switch(looping)
            {
                case TrackerEnvelope.LoopType.Compound:
                    looping = TrackerEnvelope.LoopType.Basic;  //Only the basic loop is left.
                    for(int i=0; i<Count; i++)
                    {  //Pull out the points, modify them to match the new looping condition, and shove them back into the collection.
                        var pt = this[i];
                        pt.NextPoint = (ushort) GetNextPoint(i, TrackerEnvelope.LoopType.Basic);
                        this[i] = pt;
                    }
                    //If the loop was behind where our current point is, immediately jump to the beginning of it.
                    if (idx>=loopEnd )
                    {
                        idx = loopStart;
                        if(loopStart < Count)  
                        {
                            currentPoint = this[loopStart];
                            currentValue = currentPoint.InitialValue;
                        }
                        // else{}  //Index is equal to loopEnd and also the loop end is at the end of the envelope.
                    }
                    break;
                case TrackerEnvelope.LoopType.Sustain:
                    looping = TrackerEnvelope.LoopType.None;
                    for(int i=0; i<Count; i++)
                    {  //Pull out the points, modify them to match the new looping condition, and shove them back into the collection.
                        var pt = this[i];
                        pt.NextPoint = (ushort) GetNextPoint(i, TrackerEnvelope.LoopType.None);
                        this[i] = pt;
                    }
                    break;
            }
        }

        public void Reset()
        {
            currentPoint = this.Count>0?  this[0]: ICachedEnvelopePointTransition<T>.CreatePlaceholder(currentValue);
            idx=0;
            currentValue = currentPoint.InitialValue;            
        }
    }


}
