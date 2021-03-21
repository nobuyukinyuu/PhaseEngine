using System;
using gdsFM;
using Godot;

namespace gdsFM 
{
    public static class Tables
    {
        public const double TAU = Math.PI * 2;
        //Thoughts:  Use unchecked() to rely on rollover behavior for indexes.  Work in ushort for most of the operation.
        //      increments for sine lookup of phase can be masked off if the size is a power of 2.  (12-bit lookup?)

        public const int SINE_TABLE_BITS = 15;  //We bit-shift right by the bit width of the phase counter minus this value to check the table.
        public const int SINE_TABLE_SHIFT = 32 - SINE_TABLE_BITS;  //How far to shift a phase counter value to be in range of the table.
        public const int SINE_TABLE_MASK = (1 << SINE_TABLE_BITS) - 1;  //Mask for creating a rollover.
        public const int SINE_HALFWAY_BIT = SINE_TABLE_BITS - 1;

        public const ushort SIGNED_TO_INDEX = short.MaxValue+1;  //Add this value to an output of the oscillator to get an index for a 16-bit table.

        public static readonly float[] short2float = new float[ushort.MaxValue+1];  //Representation in float of all values of ushort
        public static short[] sin = new short[(1 << SINE_TABLE_BITS) +1];  //integral Increment/decrement can use add/sub operations to alter phase counter.

        public const byte TRI_TABLE_BITS = 5;
        public const byte TRI_TABLE_MASK = (1 << TRI_TABLE_BITS) - 1;


        public static readonly short[] tri = {-32768,-28673,-24577,-20481,-16385,-12289,-8193,-4097,-1,4095,8191,12287,16383,20479,24575,28671,32767,
                                                28671,24575,20479,16383,12287,8191,4095,-1,-4097,-8193,-12289,-16385,-20481,-24577,-28673,};


//TODO:   Create an exponent table for values in linear increments 0-1 corresponding to the total attenuation (in decibels) an operator or final mix should have.
//      Attenuation can be added together cheaply and converted to a value godot likes in the end stage. Bits of depth can be however many we like (16 is best),
//      and the table mapping should go from 0-72dB attenuation, with max values clampped, perhaps corresponding to 0 at max?
//      All other lookup tables will have to have their values converted from 0-maxValue to decibel attenuation (0 being max volume).
//      In the linear domain, Attenuated envelope C=A*B (0-1 float).  In the log domain, log(C) = log(A) + log(b).

        public const byte ATTENUATION_BITS = 16;

        public const uint MAX_ATTENUATION_SIZE = (1 << ATTENUATION_BITS) -1 ;
        public const float MAX_DB = 80;  //Maximum Decibels of attenuation
        public const double ATTENUATION_UNIT = 1.0 / (double)(MAX_ATTENUATION_SIZE) * MAX_DB; //One attenuation unit in the system
        
        public static readonly short[] logVol = new short[ushort.MaxValue+1];  //scaled decibel equivalent of a given short value..
        public static readonly float[] linVol = new float[MAX_ATTENUATION_SIZE+1];  //Attenuation table scaled from 0-ATTENUATION_BITS.



        public static double[] atbl = new double[sin.Length];
        static Tables()
        {
            for(int i=0; i<short2float.Length; i++)
            {
                short2float[i] = (float) (i / 32767.5) - 1;
            }


            //log
            for(int i=0; i<logVol.Length/2; i++)
            {
                var lin = -i;
                double db = Tools.Clamp(-Tools.linear2db(i/(double)(short.MaxValue-1)), 0, MAX_DB);
                var log = (db/(double)MAX_DB * short.MaxValue) - short.MaxValue;
                atbl[(int)i] = log ;

                logVol[i] = (short) Tools.Lerp(log,lin, 0.05)  ;
                logVol[logVol.Length-1-i] = (short) logVol[i] ;
            }


            var thetaIncrement = TAU * (1/sin.Length);
            for(double i=0, theta=0; i<sin.Length; i++, theta += thetaIncrement)
            {
                // sin[i] =  (short) Math.Round(Math.Sin(TAU * (i/(double)sin.Length) )*short.MaxValue);
                double db = Tools.Clamp(-Tools.linear2db( Math.Abs(Math.Sin(TAU * (i+0.5) / (double)(sin.Length))) ), 0, MAX_DB);
                double att= Math.Round( db/(double)MAX_DB * MAX_ATTENUATION_SIZE ) - (MAX_ATTENUATION_SIZE/2) -1 ;


                // sin[(int)i] = (short) (Math.Sin(theta) * short.MaxValue);

                // atbl[(int)i] = att ;
                sin[(int)i] =  (short) (att);
            }


            for (int i=0; i<MAX_ATTENUATION_SIZE;  i++)
            {
                //Should the table be from minVolume to maxVolume?   
                // double attenuation = ((Math.Pow(2, i/(double)(MAX_ATTENUATION_SIZE+1)) -1) * 1) ;
                double attenuation = Tools.dbToLinear( i/(double)(MAX_ATTENUATION_SIZE+1) * -MAX_DB)  ;
                linVol[i] = (float) attenuation;
                }
            linVol[0] = 1.0f;
            linVol[MAX_ATTENUATION_SIZE] = 0.0f;  //haha eat pant

            

            System.Diagnostics.Debug.Print("Shornlf");

        }
        ///summary:  Returns a linear volume for a given position in an operator's phase accumulator and given attenuation. 
        public static float LinearVolume(ulong phase, short dbValue)
        {
            var output = Tables.linVol[dbValue + Tables.SIGNED_TO_INDEX];
            if (Tools.BIT(phase >> Global.FRAC_PRECISION_BITS, Tables.SINE_HALFWAY_BIT).ToBool())
                output = -output;
            return output;
        }

    }


}
