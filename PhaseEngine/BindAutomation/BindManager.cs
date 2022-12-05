using System;
using PhaseEngine;
using System.Collections.Generic;
using PE_Json;

namespace PhaseEngine
{
    /// summary:  Provides static methods for handling binds in PhaseEngine
    public static class BindManager
    {
        public static readonly Action NO_ACTION = ()=> {}; //Provided to binds which have no special action associated with an update tick.

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

        //Create a TrackerEnvelope that is bound to one of an IBindable's properties.  
        //The output is used later for generating copies of the delta envelope for an IBindableDataConsumer instance to apply to its own properties.
        public static TrackerEnvelope Bind(IBindableDataSrc dataSource, String memberName, int minValue, int maxValue)
        {
            var output = new TrackerEnvelope<int>(minValue, maxValue);
            output.dataSourceType = dataSource.GetType();

            switch(dataSource.GetType().GetMember(memberName)[0].MemberType)
            {
                case System.Reflection.MemberTypes.Property:
                    output.associatedProperty = dataSource.GetType().GetProperty(memberName);
                    break;
                case System.Reflection.MemberTypes.Field:
                    output.associatedProperty = dataSource.GetType().GetField(memberName);
                    break;
                default:
                    throw new ArgumentException("Can't bind to this member.");
            }

            //Determine whether the bind member is valid for automation by envelope
            Type type=null;
            switch(output.associatedProperty){
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

        public static TrackerEnvelope Bind(IBindableDataSrc dataSource, String memberName, float minValue, float maxValue)
        {
            var output = new TrackerEnvelope<float>(minValue, maxValue);
            output.dataSourceType = dataSource.GetType();

            switch(dataSource.GetType().GetMember(memberName)[0].MemberType)
            {
                case System.Reflection.MemberTypes.Property:
                    output.associatedProperty = dataSource.GetType().GetProperty(memberName);
                    break;
                case System.Reflection.MemberTypes.Field:
                    output.associatedProperty = dataSource.GetType().GetField(memberName);
                    break;
                default:
                    throw new ArgumentException("Can't bind to this member.");
            }

            //Determine whether the bind member is valid for automation by envelope
            Type type=null;
            switch(output.associatedProperty){
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

        //Used to update the fields we're bound to in the specified data source. Done whenever invoker's clock says it's OK to update.
        //Invoker's clock method should probably be where we also call recalcs for things like filters and increments...
        public static bool Update<T>(IBindableDataConsumer invoker, ref T dataSource)  where T:struct,IBindableDataSrc //IMPORTANT
        { /*unchecked{*/
            //Only sets the value directly to current state.  Clocking must be done in invokers
            var envelope = dataSource.BoundEnvelopes;
            var state = invoker.BindStates;
            var updated = false;  //Flagged true if a value changes in the invoker, used to call a post-process action only once.

            for(int i=0; i<envelope.Count; i++)
            {
                var item=envelope.Keys[i];
                if(!state.ContainsKey(item)) continue;
                if(!state[item].JustTicked) { state[item].Clock(); continue; }  //Proceed without updating value if no tick
                updated = true;
                switch(envelope[item].DataMember)  //FIXME:  TYPE COERSION IS REALLY SLOW; IT DOESN'T WORK ON STRUCTS YET, USE SETVAL<T> INSTEAD
                {
                    case System.Reflection.PropertyInfo property:
                        object box = System.Runtime.CompilerServices.RuntimeHelpers.GetObjectValue(dataSource);
                        switch(state[item])  //FIXME LATER, SEE BELOW
                        {  //WARNING:  This shit is pure evil and probably slow.  Increments.Detune is probably the only thing which relies on it. Stack thrasher?
                            case CachedEnvelope<int> pInt:
                                property.SetValue(dataSource, CoerceValue((int)pInt.CurrentValue, property.PropertyType, envelope.Values[i]));
                                break;
                            case CachedEnvelope<float> pFloat:
                                property.SetValue(dataSource, CoerceValue((float)pFloat.CurrentValue, property.PropertyType, envelope.Values[i]));
                                break;
                            case CachedEnvelope<double> pDouble:
                                property.SetValue(dataSource, CoerceValue((double)pDouble.CurrentValue, property.PropertyType, envelope.Values[i]));
                                break;                                
                        }
                        dataSource = (T)box;   //1000 screaming babies
                        break;
                    case System.Reflection.FieldInfo field:
                        switch(state[item])
                        {
                            case CachedEnvelope<int> pInt:
                                field.SetValueDirect(__makeref(dataSource), CoerceValue((int)pInt.CurrentValue, field.FieldType, envelope.Values[i]));
                                break;
                            case CachedEnvelope<float> pFloat:
                                field.SetValueDirect(__makeref(dataSource), CoerceValue((float)pFloat.CurrentValue, field.FieldType, envelope.Values[i]));
                                break;
                            case CachedEnvelope<double> pDouble:
                                field.SetValueDirect(__makeref(dataSource), CoerceValue((double)pDouble.CurrentValue, field.FieldType, envelope.Values[i]));
                                break;                                
                        }
                        break;
                }
                state[item].Clock();
            }
            return updated;
        /*}*/}
        public static bool Update<T>(IBindableDataConsumer invoker, T dataSource)  where T:class,IBindableDataSrc //IMPORTANT
        { /*unchecked{*/
            //Only sets the value directly to current state.  Clocking must be done in invokers
            var envelope = dataSource.BoundEnvelopes;
            var state = invoker.BindStates;
            var updated = false;  //Flagged true if a value changes in the invoker, used to call a post-process action only once.

            for(int i=0; i<envelope.Count; i++)
            {
                var item=envelope.Keys[i];
                if(!state.ContainsKey(item)) continue;
                if(!state[item].JustTicked) { state[item].Clock(); continue; }  //Proceed without updating value if no tick
                updated = true;

                switch(envelope[item].DataMember)  //FIXME:  TYPE COERSION IS REALLY SLOW
                {
                    case System.Reflection.PropertyInfo property:
                        switch(state[item])
                        {
                            case CachedEnvelope<int> pInt:
                                property.SetValue(dataSource, CoerceValue((int)pInt.CurrentValue, property.PropertyType, envelope.Values[i]));
                                break;
                            case CachedEnvelope<float> pFloat:
                                property.SetValue(dataSource, CoerceValue((float)pFloat.CurrentValue, property.PropertyType, envelope.Values[i]));
                                break;
                            case CachedEnvelope<double> pDouble:
                                property.SetValue(dataSource, CoerceValue((double)pDouble.CurrentValue, property.PropertyType, envelope.Values[i]));
                                break;                                
                        }
                        break;
                    case System.Reflection.FieldInfo field:
                        switch(state[item])
                        {
                            case CachedEnvelope<int> pInt:
                                field.SetValue(dataSource, CoerceValue((int)pInt.CurrentValue, field.FieldType, envelope.Values[i]));
                                break;
                            case CachedEnvelope<float> pFloat:
                                field.SetValue(dataSource, CoerceValue((float)pFloat.CurrentValue, field.FieldType, envelope.Values[i]));
                                break;
                            case CachedEnvelope<double> pDouble:
                                field.SetValue(dataSource, CoerceValue((double)pDouble.CurrentValue, field.FieldType, envelope.Values[i]));
                                break;                                
                        }
                        break;
                }
                state[item].Clock();
            }
            // if (updated)
            // /*perform the*/ action();
            return updated;
        /*}*/}



        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static object CoerceValue(int input, Type type, TrackerEnvelope minMax) =>  Convert.ChangeType(Math.Clamp(input, (int)minMax.MinValue, (int)minMax.MaxValue), type);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static object CoerceValue(float input, Type type, TrackerEnvelope minMax) => 
                Convert.ChangeType(Math.Clamp(input, (float)minMax.MinValue, (float)minMax.MaxValue), type);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static object CoerceValue(double input, Type type, TrackerEnvelope minMax) =>
                Convert.ChangeType(Math.Clamp(input, (double)minMax.MinValue, (double)minMax.MaxValue), type);


    }
}