using System;
using gdsFM;

namespace gdsFM 
{
    public class Envelope
    {
        public Envelope()    {}
    }



    //Thoughts:  Rates probably need fixed integer math to work, then translated to a level at the very end, since increments are very small.
    public class EGData
    {
        double delay,ar,d1r,d2r,sr,rr;
        
        double[] rates;
        double[] rateIncrement;

        public void Reset()
        {
            // ar=31;dr=31;sr=6;rr=15;
            rates = new double[] {0, 0, 120, 0};
        }

        public void Recalc()
        {
            rateIncrement = new double[4];

            for(int i=0;  i<4;  i++)
            {
                rateIncrement[i] = RateMultiplier(1);
            }
        }


        double RateMultiplier(float secs)
        {
            return secs * Global.MixRate;
        }
    }

}
