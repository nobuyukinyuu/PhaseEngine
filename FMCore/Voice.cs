using System;
using gdsFM;
using GdsFMJson;

#if GODOT
using Godot;
#endif

namespace gdsFM 
{
    public class Voice
    {
        public byte opCount = 6; 

        //Keeps track of how to set the operator on a new note.  TODO: Consider an enum of all waveforms and filters for serialization purposes
        public byte[] opType;   //Typically some kind of waveform value.


        //Consider having an array of envelopes for ops to refer to when initializing their voices as the "canonical" voice, and a temporary copy made for alterables.
        public Envelope[] egs;  //Canonical EG data for each operator.
        public Increments[] pgs;

        //Consider making all Channels use references to these vars, and process them properly whenever IO comes in.
        public Algorithm alg = new Algorithm();
        // string wiringGrid;

        //TODO:  Voice description / meta probably goes here too....
        public Voice() {InitVoice(this.opCount);}
        public Voice(byte opCount) {InitVoice(opCount);}

        public Voice(JSONObject data)
        {
            InitVoice((byte) data.GetItem("opCount", opCount));
            FromJSON(data);
        }

        void InitVoice(byte opCount)    
        {
            this.opCount = opCount;
             alg = new Algorithm(opCount);
             egs = new Envelope[opCount];
             pgs = new Increments[opCount];
             opType = new byte[opCount];

            //Chip should pass these down when pulling a channel
             for (int i=0; i<opCount; i++){
                 egs[i] = new Envelope();
                 pgs[i] = Increments.Prototype();
             }
        }

        public bool SetPresetAlgorithm(byte preset)
        {
            if (opCount >= 6)
            {
                alg = Algorithm.FromPreset(preset, true);
                return true;
            } else if (opCount >= 4)
            {
                alg = Algorithm.FromPreset(preset, false);
                return true;
            }
            return false;
        }

        #if GODOT
            //Initializes new voice from a pure godot dict containing json data.  Necessary if changing opCount
            public Voice(Godot.Collections.Dictionary d)
            {
                InitVoice((byte) d["opCount"]);
                FromJSON( (JSONObject) JSONData.ReadJSON(d.ToString()) );
            }

            /// Changes the algorithm without changing the opCount.
            public void SetAlgorithm(Godot.Collections.Dictionary d)
            {
                // FIXME:  Don't set opCount here.  Use a separate function to change the op count.....
                // TODO:  Consider replacing this function with one which converts d to json and just call FromJSON()
                alg.wiringGrid =  Convert.ToUInt32(d["grid"]);

                var order = d["processOrder"] as Godot.Collections.Array;
                var c = d["connections"] as Godot.Collections.Array;

                alg.processOrder = new byte[opCount];
                alg.connections = new byte[opCount];
                var opsToProcess = Math.Min(alg.opCount, order.Count);
                for(int i=0; i<opsToProcess; i++)
                {
                    alg.processOrder[i] = Convert.ToByte(order[i]);
                    alg.connections[i] = Convert.ToByte(c[i]);
                }
            }

            public Godot.Collections.Dictionary GetAlgorithm()
            {
                var d = new Godot.Collections.Dictionary();

                d["opCount"] = opCount;
                d["grid"] = alg.wiringGrid;

                d["processOrder"] = alg.processOrder;
                d["connections"] = alg.connections;

                return d;

            }

            public Godot.Collections.Dictionary GetEG(int opTarget) {return egs[opTarget >= opCount? 0:opTarget].GetDictionary();}
            public Godot.Collections.Dictionary GetPG(int opTarget) {return pgs[opTarget >= opCount? 0:opTarget].GetDictionary();}
        #endif


        // Called whenever the voice needs to change its operator count in the algorithm.
        public void SetOpCount(byte opTarget)
        {
            Array.Resize(ref egs, opTarget);
            Array.Resize(ref pgs, opTarget);
            Array.Resize(ref opType, opTarget);

            if (opTarget>opCount)
            {
                for (byte i=opCount; i<opTarget; i++)
                {
                    egs[i] = new Envelope();
                    pgs[i] = Increments.Prototype();
                }
            }

            alg.SetOpCount(opTarget);

            opCount = opTarget;
        }



        /// Called from EG controls to bus to the appropriate tuning properties.
        public void SetPG(int opTarget, string property, float val)
        {
            // Increments is a struct, so we need to update the canonical info from the Voice and grab a copy whenever notes turn on. 
            // A consequence of this is that values in Increments (pitch, mainly) can't be adjusted on the fly, only on a new note.
            
            try
            {
                pgs[opTarget].SetVal(property, val);
                // pg.Recalc();  // In Voice we don't need to recalc since note selection occurs at NoteOn only

                // GD.Print(String.Format("Set op{0}.eg.{1} to {2}.", opTarget, property, val));
            } catch(NullReferenceException) {
                #if GODOT
                    GD.PrintErr(String.Format("No property handler for op{0}.pg.{1}.", opTarget, property, val));
                #else
                    System.Diagnostics.Debug.Print(String.Format("No property handler for op{0}.pg.{1}.", opTarget, property, val));
                #endif
            }            
        }
        public void SetEG(int opTarget, string property, float val)
        {
            if (opTarget >= opCount) return;
            var eg = egs[opTarget];

            try
            {
                eg.SetVal(property, unchecked((int) val));
                // GD.Print(String.Format("Set op{0}.eg.{1} to {2}.", opTarget, property, val));
            } catch(NullReferenceException) {
                #if GODOT
                    GD.PrintErr(String.Format("No property handler for op{0}.eg.{1}.", opTarget, property, val));
                #else
                    System.Diagnostics.Debug.Print(String.Format("No property handler for op{0}.eg.{1}.", opTarget, property, val));
                #endif
            }            
        }

        /// Sets the canonical waveform to reference when setting an operator's waveFunc on NoteOn.
        public void SetWaveform(int opTarget, float val)
        {   // NOTE:  This does NOT actually set an operator's waveFunc!  This is done in NoteOn when referencing this value from Voice.
            opType[opTarget] = (byte)val;
        }    


        //TODO:  Front-end IO that de/serializes the wiring grid configuration from an array (user-friendly) to processOrder and connections (code-friendly)
        public void FromJSON(JSONObject data)
        {
            try
            {
                opCount = (byte) data.GetItem("opCount", opCount);
                alg.wiringGrid = (uint) data.GetItem("grid", 0);
                var order = data.GetItem<byte>("processOrder", Algorithm.DefaultProcessOrder(opCount) );

                var c = data.GetItem<byte>("connections", null);
                if (c != null){ for(int i=0; i<opCount; i++)  alg.connections[i] = (byte) c[i]; }  //If no data found, assume all ops connect to output

                var a = new Algorithm(opCount);
                alg = a;

                var ops = (JSONArray) data.GetItem("operators");
                for (int i=0; i<opCount; i++)
                {
                    var op = (JSONObject) ops[i];
                    var e = (JSONObject) op.GetItem("envelope");

                    bool success = egs[i].FromString(e.ToJSONString());
                    if (!success)
                    {
                        System.Diagnostics.Debug.Print(String.Format("Voice.FromJSON:  Problem parsing envelope {0}", i));
                        continue;
                    }


                //TODO:  Phase generator processing, metadata, etc
                }


            } catch {
                System.Diagnostics.Debug.Print("Voice.FromJSON:  Malformed JSON or missing data");
            }
        }


    }

}
