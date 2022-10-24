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

        //Create a TrackerEnvelope that is bound to one of an IBindable's properties.  
        //The output is used later for generating copies of the delta envelope for an IBindable instance to apply to its own properties.
        public static TrackerEnvelope Bind(IBindableDataSrc dataSource, String property, int minValue, int maxValue)
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

            switch(dataSource.GetType().GetMember(property)[0].MemberType)
            {
                case System.Reflection.MemberTypes.Property:
                    output.associatedProperty = dataSource.GetType().GetProperty(property);
                    break;
                case System.Reflection.MemberTypes.Field:
                    output.associatedProperty = dataSource.GetType().GetField(property);
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
        public static void Update(IBindableDataConsumer invoker, IBindableDataSrc dataSource, Action action)  //IMPORTANT
        {
            //Only sets the value directly to current state.  Clocking must be done in invokers
            var envelope = dataSource.BoundEnvelopes;
            var state = invoker.BindStates;
            // for(int i=0; i<envelope.Count; i++)
            foreach(string item in envelope.Keys)
            {
                switch(envelope[item].associatedProperty)  //FIXME:  TYPE COERSION IS REALLY SLOW
                {
                    case System.Reflection.PropertyInfo property:
                        property.SetValue(dataSource, CoerceValue(state[item].currentPoint.currentValue, property.PropertyType));
                        break;
                    case System.Reflection.FieldInfo field:
                        field.SetValue(dataSource, CoerceValue(state[item].currentPoint.currentValue, field.FieldType));
                        break;
                }
                state[item].Clock();
            }

            action();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static object CoerceValue(int input, Type type)
        {
            // input = input.Clamp(Convert.ToInt32(type.GetField("MinValue").GetValue(null)), Convert.ToInt32(type.GetField("MaxValue").GetValue(null)));
            return Convert.ChangeType(input, type);
        }

        //Used for resetting a consumer's target field value to an initial value (Such as defined by TrackerEnvelope)
        public static void ResetValue(IBindableDataSrc dataSource, string key)
        {
            var envelope = dataSource.BoundEnvelopes;
            var initialValue = envelope[key].InitialValue;
            SetTargetValue(dataSource, key, initialValue);
        }

        public static void SetTargetValue(IBindableDataSrc dataSource, string key, ValueType value)
        {
            var envelope = dataSource.BoundEnvelopes;
                switch(envelope[key].associatedProperty)
                {
                    case System.Reflection.PropertyInfo property:
                        property.SetValue(dataSource, value);
                        break;
                    case System.Reflection.FieldInfo field:
                        field.SetValue(dataSource, value);
                        break;
                }

        }

    }
}