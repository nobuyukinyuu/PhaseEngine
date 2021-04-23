using System;
using gdsFM;

    //Thoughts:  Rates probably need fixed integer math to work, then translated to a level at the very end, since increments are very small.

namespace gdsFM 
{
    public class Envelope
    {
        public ushort attenuation;  //5-bit value
        const ushort L_MAX = 1023; //Max attenuation level
        const byte R_MAX = 63;  //Max rate
        public EGStatus status = EGStatus.SUSTAINED;

        public byte ar{get=> rates[0]; set=> rates[0] = value;}
        public byte dr{get=> rates[1]; set=> rates[1] = value;}
        public byte sr{get=> rates[2]; set=> rates[2] = value;}
        public byte rr{get=> rates[3]; set=> rates[3] = value;}
        public byte[] rates = new byte[4];
        public ushort[] levels = new ushort[5];
        public ushort[] precalcLevels = new ushort[5];
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

    }

    public enum EGStatus
    {
        DELAY=-1, ATTACK, HOLD=0xFF, DECAY=1, SUSTAINED, RELEASED, INACTIVE
    }

    // public enum LFOStatus
    // {
    //     DELAY, FADEIN, RUNNING
    // }


}
