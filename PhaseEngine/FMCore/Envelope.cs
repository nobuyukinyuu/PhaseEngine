using System;
using PhaseEngine;
using GdsFMJson;

    //Thoughts:  Rates probably need fixed integer math to work, then translated to a level at the very end, since increments are very small.

namespace PhaseEngine 
{
    public class Envelope
    {
        public bool mute;
        public bool bypass;

        public const ushort TL_MAX = 1919; //Max total attenuation level.  Should really be 2048 for -48dB, but non-clamped/unchecked rollover can create noise
        public const ushort L_MAX = 1023; //Max attenuation level
        public const byte R_MAX = 63;  //Max rate


        public byte ar{get=> rates[0]; set=> rates[0] = value;}
        public byte dr{get=> rates[1]; set=> rates[1] = value;}
        public byte sr{get=> rates[2]; set=> rates[2] = value;}
        public byte rr{get=> rates[3]; set=> rates[3] = value;}
        public byte[] rates = new byte[4];
        public ushort[] levels = new ushort[5];
        public bool[] rising= {true, false, false, false};  //Precalculates which way to increment the envelope based on the target state.

        public ushort delay, hold;
        
        // ushort tl, al, dl, sl;  // Attenuation target levels

        //TODO:  Consider adding a 6th level for the total scaled attenuation from the ksl and velocity tables. Since EG is copied to channel's ops on new note...
        //       This would allow users to change the TL after a note's been generated on a channel and still retain the original scaled level.
        //       OTOH, perhaps only supporting TL "slides" for potential tracker-related envelope tweaking would be preferable...
        public ushort tl{get=> levels[4]; set=>levels[4] = value;}
        public ushort al{get=> levels[0]; set=>RecalcLevel(0, value);}
        public ushort dl{get=> levels[1]; set=>RecalcLevel(1, value);}
        public ushort sl{get=> levels[2]; set=>RecalcLevel(2, value);}
        public ushort rl{get=> levels[3]; set=>RecalcLevel(3, value);}

        public byte feedback = 0;
        public ushort duty=32767;

        public double cutoff=Global.MixRate, resonance=1.0;
        public double gain=1.0;  //Used by Wavefolder and some filters.

        public byte ams;  //Amplitude modulation sensitivity (used to determine how much LFO to mix in)
        public bool osc_sync=true;  //Oscillator sync.  Oscillator phase will reset on NoteOn if true.
        public double phase_offset;  //Percentage of phase offset.

        public byte aux_func;  //ONLY used for bitwise function operators, filters etc as an additional value specifier.

       //Response tables.  These are references from the canonical Voice.
        public RateTable ksr = new RateTable();
        public LevelTable ksl = new LevelTable();
        public VelocityTable velocity = new VelocityTable();


        public IResponseTable GetTable(RTableIntent intent)
        {
            switch(intent)
            {
                case RTableIntent.RATES:
                    return ksr;
                case RTableIntent.LEVELS:
                    return ksl;
                case RTableIntent.VELOCITY:
                    return velocity;
                default:
                    return null;
            }
        }


        public Envelope() { Reset(); }

        //Copy constructor
        public Envelope(Envelope prototype, bool deserializeRTables=false) 
        { 
            var data = prototype.ToJSONString();
            if(this.FromString(data,deserializeRTables)) //if importation of copy succeeded...
            {
                    RecalcLevelRisings();
                    //Grab response table references from the prototype.  
                    //We don't need to copy the actual response tables as we want to share them between all channels.
                    //TODO:  Consider if each channel should have fully customizable RTables and remove the internal re-use option entirely....

                    if (!deserializeRTables) //Reuse the RTables from the prototype. Saves resources when copying the rest of the EG to a channel.
                    {
                        ksr = prototype.ksr;
                        ksl = prototype.ksl;
                        velocity = prototype.velocity;
                    }
            }
            else Reset(); //Attempt copy.  If failure, reinit envelope.
        }

        public void Reset(bool rates=true, bool levels=true)
        {
            if (rates){
                ar=R_MAX;dr=R_MAX;sr=0;rr=R_MAX;
                delay=0; hold=0; 
            }

            if (levels){
                tl=0; al=0; dl=0; sl=L_MAX; rl=L_MAX;
                RecalcLevelRisings();
            }

        }

        public void RecalcLevel(byte level, ushort amt)
        {
            levels[level] = amt;
            RecalcLevelRisings();
        }
        public void RecalcLevelRisings()
        {
            //Set volume to rising if attenuation level is greater than the next envelope phase's level.
            for (int i=0; i<3; i++)
            {
                    rising[i+1] = (levels[i] > levels[i+1]);
            }

            // rising[(int)EGStatus.DECAY] = (al > dl);
            // rising[(int)EGStatus.SUSTAINED] = (dl > sl);
            // rising[(int)EGStatus.RELEASED] = (sl > rl);            
        }

        public bool FromString(string input, bool deserializeRTables=false)
        {
            var P = JSONData.ReadJSON(input);
            if (P is JSONDataError) return false;
            var j = (JSONObject) P;

            try
            {
                rates = j.GetItem<byte>("rates", rates);
                levels = j.GetItem<ushort>("levels", levels);
                // rising = j.GetItem<bool>("rising", rising);

                j.Assign("delay", ref delay);
                j.Assign("hold", ref hold);
                j.Assign("feedback", ref feedback);
                j.Assign("duty", ref duty);
                j.Assign("ams", ref ams);
                j.Assign("osc_sync", ref osc_sync);
                j.Assign("phase_offset", ref phase_offset);
                j.Assign("cutoff", ref cutoff);
                j.Assign("resonance", ref resonance);

                j.Assign("mute", ref mute);
                j.Assign("bypass", ref bypass);
                j.Assign("aux_func", ref aux_func);
                j.Assign("gain", ref gain);

                if (deserializeRTables)
                {
                    //TODO/FIXME:  ADD A METHOD .FromJSONString IN RTable.cs TO DESERIALIZE THE OUTPUT OF .ToJSONString
                }
            } catch {
                System.Diagnostics.Debug.Assert(false,"EG Copy failed");  //FIXME:  DEBUG, REMOVE ME ONCE RTABLE IMPORT IS KNOWN WORKING
                return false;
            }
            
            return true;
        }

        public string ToJSONString()
        {
            var o = new JSONObject();
            o.AddPrim<byte>("rates", rates);
            o.AddPrim<ushort>("levels", levels);
            // o.AddPrim<bool>("rising", rising);

            o.AddPrim("delay", delay);
            o.AddPrim("hold", hold);
            o.AddPrim("feedback", feedback);
            o.AddPrim("duty", duty);
            o.AddPrim("ams", ams);
            o.AddPrim("osc_sync", osc_sync);
            o.AddPrim("phase_offset", phase_offset);
            o.AddPrim("cutoff", cutoff);
            o.AddPrim("resonance", resonance);

            o.AddPrim("mute", mute);
            o.AddPrim("bypass", bypass);
            o.AddPrim("aux_func", aux_func);
            o.AddPrim("gain", gain);

            o.AddItem("ksr", ksr.ToJSONObject());
            o.AddItem("ksl", ksl.ToJSONObject());
            o.AddItem("velocity", velocity.ToJSONObject());

            return o.ToJSONString();
        }

        #if GODOT
        public Godot.Collections.Dictionary GetDictionary()
        {
            var o = new Godot.Collections.Dictionary();
            o.Add("rates", rates);
            int[] lv = {al,dl,sl,rl,tl}; //Bussing ushort[] returns null
            o.Add("levels", lv);
            o.Add("ams", ams);
            // o.Add("rising", rising);

            o.Add("delay", delay);
            o.Add("hold", hold);
            o.Add("feedback", feedback);
            o.Add("duty", duty);
            o.Add("osc_sync", osc_sync);
            o.Add("phase_offset", phase_offset);
            o.Add("cutoff", cutoff);
            o.Add("resonance", resonance);

            o.Add("mute", mute);
            o.Add("bypass", bypass);
            o.Add("aux_func", aux_func);
            o.Add("gain", gain);
            return o;
        }
        #endif 

        //Convenience function for setting any property or field by specifying its name.  Not efficient for realtime use!
        public void ChangeValue(string property, float val)
        {
            try
            {
                this.SetVal(property, unchecked(val));
                // GD.Print(String.Format("Set op{0}.eg.{1} to {2}.", opTarget, property, val));
            } catch(NullReferenceException) {
                #if GODOT
                    Godot.GD.PrintErr(String.Format("No property handler for eg.{0}.", property));
                #else
                    System.Diagnostics.Debug.Print(String.Format("No property handler for eg.{0}.", property));
                #endif
            }            
        }


    }

    public enum EGStatus
    {
        DELAY=-1, ATTACK, HOLD=-2, DECAY=1, SUSTAINED, RELEASED, INACTIVE
    }

    // public enum LFOStatus
    // {
    //     DELAY, FADEIN, RUNNING
    // }


}
