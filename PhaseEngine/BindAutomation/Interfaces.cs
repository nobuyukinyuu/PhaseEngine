using System;
using PhaseEngine;
using System.Collections.Generic;
using PE_Json;

namespace PhaseEngine
{
    public interface IBindableDataSrc  //Objects that can assign TrackerEnvelope binds to fields.  Typically EGs and increment data.

    {
        //Gets a copy of every cached envelope bound to a property in this IBindable.
        public Dictionary<string, TrackerEnvelope> BoundEnvelopes {get;} //References to envelope bind data needed to create caches.


        //Calls the bind manager to add a TrackerEnvelope to BoundEnvelopes
        //It's up to each implementation on how to specify min and max values
        public bool Bind(string property, int minValue, int maxValue)
        {
            TrackerEnvelope e = BindManager.Bind(this, property, minValue, maxValue); 
            var bindAlreadyExists = !BoundEnvelopes.TryAdd(property, e);
            if(bindAlreadyExists) return false;  //Binding failed.  User must Unbind the value first.
            e.dataSourceType = this.GetType();
            return true;
        }
        public bool Bind(string property, int minValue, int maxValue, int initialValue)
        {
            TrackerEnvelope e = BindManager.Bind(this, property, minValue, maxValue); 
            var bindSuccessful = BoundEnvelopes.TryAdd(property, e);
            if (bindSuccessful)  e.SetPoint(0, (0, initialValue) );
            e.dataSourceType = this.GetType();
            return bindSuccessful;
        }

        // FIXME:  Consider using BindManager instead to fix CachedEnvelopes to not carry these properties.  Or partial rebake on NoteOn?
        bool Unbind(string property) => BoundEnvelopes.Remove(property);

    }


    public interface IBindableDataConsumer  //Object which consumes data from an IBindableDataSrc.  Typically an operator
    {
        //BindStates below are used to cycle through the cached envelopes for 
        //When IBindableDataSrc invokes an update to one of its fields, these are used to determine how to update it.
        public Dictionary<string, CachedEnvelope> BindStates {get;} 
        public ushort BindClockDivider {get;}

        //Methods used to signal the start and release of the bound envelopes so they can loop properly.
        public void NoteOn();
        public void NoteOff();
        void NoteOn(IBindableDataSrc dataSource)  //Default method, Called by the NoteOn() in implementations to set up general housekeeping functions
        {
            Rebake(dataSource);
        }

        //Default method,  called by methods which need to update a data consumer to contain the latest data source's cached envelopes
        internal void Rebake(IBindableDataSrc dataSource)  
        {
            //TODO:  Consider options in the envelope editor to specify an action on how to deal with an rTable.
            //       Creating baked envelopes of scaled data could consist of an addition to only the first point, all points (clamped), or
            //       to all points save for the initial value multiplied by a ratio of the initial value to the rTable scaled value.

            //FIXME:  Horribly inefficient.  Consider using a copy of the cache from TrackerEnvelope if properly baked!
            // BindStates[property].Bake( data[property] );  
            BindStates.Clear();
            foreach(string property in dataSource.BoundEnvelopes.Keys)
            {
                BindStates[property]= dataSource.BoundEnvelopes[property].CachedEnvelopeCopy;
            }
        }

        public void Clock();  //Where all the local caches are clocked and where the instance calls BindManager.Update() to modify its bound members.
    }
}
