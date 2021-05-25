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

        //Consider having an array of envelopes for operators to refer to when initializing their voices as the "canonical" voice, and a temporary copy made for alterables.
        public Envelope[] egs;  //Canonical EG data for each operator.

        //Consider making all Channels use references to these vars, and process them properly whenever IO comes in.
        public Algorithm alg = new Algorithm();
        string wiringGrid;

        //TODO:  Consider making this class a struct if all it contains is the algorithm and envelope values.  Voice description IO probably goes here too....
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
             egs = new Envelope[opCount];

            //Chip should pass these down when pulling a channel
             for (int i=0; i<egs.Length; i++){
                 egs[i] = new Envelope();
             }
        }

        public bool ChangeAlgorithm(byte preset)
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

        public void ChangeAlgorithm(Godot.Collections.Dictionary d)
        {
            wiringGrid = (String) d["grid"];  //Provide a description of the wiring grid for serialization
            opCount = (byte) d["opCount"];
            var order = (Godot.Collections.Array<int>) d["processOrder"];
            var c = (Godot.Collections.Array<int>) d["connections"];

            alg.processOrder = new byte[opCount];
            alg.connections = new byte[opCount];
            for(int i=0; i<opCount; i++)
            {
                alg.processOrder[i] = (byte) order[i];
                alg.connections[i] = (byte) c[i];
            }
        }
        #endif


        //TODO:  Front-end IO that de/serializes the wiring grid configuration from an array (user-friendly) to processOrder and connections (code-friendly)
        public void FromJSON(JSONObject data)
        {
            try
            {
                wiringGrid = data.GetItem("grid")?.ToJSONString();
                var order = data.GetItem<byte>("processOrder", null);
                var c = data.GetItem<byte>("connections", null);

                var a = new Algorithm((byte) data.GetItem("opCount", opCount));

                //Assumes default processOrder if none found.  This is fine for most preset connections.
                if (order != null){ for(int i=0; i<opCount; i++)  alg.processOrder[i] = (byte) order[i]; }
                if (c != null){ for(int i=0; i<opCount; i++)  alg.connections[i] = (byte) c[i]; }  //If no data found, assume all ops connect to output
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
                    //Extra operator etc processing here
                }


            } catch {
                System.Diagnostics.Debug.Print("Voice.FromJSON:  Malformed JSON or missing data");
            }
        }


    }

}
