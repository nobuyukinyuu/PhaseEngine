using System;
using gdsFM;
using GdsFMJson;

    //Thoughts:  Rates probably need fixed integer math to work, then translated to a level at the very end, since increments are very small.

namespace gdsFM 
{
    public class Envelope
    {
        public bool mute;
        public bool bypass;

        public const ushort TL_MAX = 1919; //Max total attenuation level
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
        public ushort tl{get=> levels[4]; set=>levels[4] = value;}
        public ushort al{get=> levels[0]; set=>RecalcLevel(0, value);}
        public ushort dl{get=> levels[1]; set=>RecalcLevel(1, value);}
        public ushort sl{get=> levels[2]; set=>RecalcLevel(2, value);}
        public ushort rl{get=> levels[3]; set=>RecalcLevel(3, value);}

        public byte feedback = 0;
        public ushort duty=32767;

        public Envelope() { Reset(); }

        //Copy constructor
        public Envelope(Envelope prototype) 
        { 
            var data = prototype.ToJSONString();
            if(this.FromString(data))  RecalcLevelRisings();
            else Reset(); //Attempt copy.  If failure, reinit envelope.
        }

        public void Reset(bool rates=true, bool levels=true)
        {
            if (rates){
                ar=R_MAX;dr=R_MAX;sr=16;rr=R_MAX;
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

        public bool FromString(string input)
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

                j.Assign("mute", ref mute);
                j.Assign("bypass", ref bypass);
            } catch {
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

            o.AddPrim("mute", mute);
            o.AddPrim("bypass", bypass);

            return o.ToJSONString();
        }

        #if GODOT
        public Godot.Collections.Dictionary GetDictionary()
        {
            var o = new Godot.Collections.Dictionary();
            o.Add("rates", rates);
            int[] lv = {al,dl,sl,rl,tl}; //Bussing ushort[] returns null
            o.Add("levels", lv);
            // o.Add("rising", rising);

            o.Add("delay", delay);
            o.Add("hold", hold);
            o.Add("feedback", feedback);
            o.Add("duty", duty);

            o.Add("mute", mute);
            o.Add("bypass", bypass);
            return o;
        }
        #endif 

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
