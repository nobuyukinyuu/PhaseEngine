using System;
using gdsFM;

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
        void InitVoice(byte opCount)    
        {
            this.opCount = opCount;
             egs = new Envelope[opCount];

            //Chip should pass these down when pulling a channel
             for (int i=0; i<egs.Length; i++){
                 egs[i] = new Envelope();
             }
        }

        #if GODOT
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
    }

}
