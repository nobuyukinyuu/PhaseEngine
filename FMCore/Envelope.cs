using System;
using gdsFM;

    //Thoughts:  Rates probably need fixed integer math to work, then translated to a level at the very end, since increments are very small.

namespace gdsFM 
{
    public class Envelope
    {
        public ushort attenuation;  //5-bit value
        public EGStatus status = EGStatus.SUSTAINED;

        public byte ar{get=> rates[0]; set=> rates[0] = value;}
        public byte dr{get=> rates[1]; set=> rates[1] = value;}
        public byte sr{get=> rates[2]; set=> rates[2] = value;}
        public byte rr{get=> rates[3]; set=> rates[3] = value;}
        public byte[] rates = new byte[4];
        public ushort delay, hold;
        
        ushort tl, al, dl, sl;  // Attenuation target levels

        // double[] rates;
        // double[] rateIncrement;

        public void Reset(bool rates=true, bool levels=true)
        {
            if (rates){
                ar=63;dr=63;sr=0;rr=63;
                delay=0; hold=0; 
            }

            if (levels){
                tl=0; al=0; dl=0; sl=0;
            }

            // rates = new double[] {0, 0, 120, 0};
        }

        // public void Recalc()
        // {
        //     rateIncrement = new double[4];

        //     for(int i=0;  i<4;  i++)
        //     {
        //         rateIncrement[i] = RateMultiplier(1);
        //     }
        // }


        double RateMultiplier(float secs)
        {
            return secs * Global.MixRate;
        }

        #if GODOT
        /// Property indexer.  Used to talk to/from Godot for convenience.  
        /// In GDScript you can use obj.set(prop, val) or obj.get(prop); this is a similar feature for c#.
        public object this[string propertyName]  //TODO:  Make this safer
        {
            get {
                Type type = typeof(Envelope);
                System.Reflection.PropertyInfo property = type.GetProperty(propertyName);
                return property.GetValue(this);
            } set {
                Type type = typeof(Envelope);
                System.Reflection.PropertyInfo property = type.GetProperty(propertyName);

                //Try to force unchecked conversion to the target type
                var unboxedVal = Convert.ChangeType(value, property.PropertyType);

                property.SetValue(this, unboxedVal);

            }
        }
        #endif

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
