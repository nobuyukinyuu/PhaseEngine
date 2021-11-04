using System;
using gdsFM;

namespace gdsFM 
{
    public class Filter : OpBase
    {
        //TODO:  Replace with delegate func array
        public enum FilterType {NONE, LOWPASS, HIPASS, BANDPASS_CSG, BANDPASS_CZPG, NOTCH, ALLPASS, PEAKING, LOWSHELF, HISHELF}
        public delegate short FilterFunc(ushort input, ushort am_offset);
        public FilterFunc FilterToApply;

        // public static readonly FilterFunc[] operations = {Bypass, Lowpass, Hipass, Bandpass_Csg, Bandpass_Czpg, Notch, AllPass, Peaking, LowShelf, HiShelf};

        // public byte OpFuncType {get => eg.aux_func;  set { if (value<operations.Length)  {ApplyFilter = operations[value]; eg.aux_func=value;} }}


        // filter coeffs
        float b0a0=1,b1a0=1,b2a0=1,a1a0=1,a2a0=1;
        // in/out history
        float ou1,ou2,in1,in2;


        public Filter()    {FilterToApply=RequestBypass; Reset();}
        public void Reset()
        {
            // reset filter coeffs
            b0a0=b1a0=b2a0=a1a0=a2a0=1.0f;
            // reset in/out history
            ou1=ou2=in1=in2=0.0f;	
        }

        public override void Clock()
        {
            //TODO:  Support recalculating filter envelopes
            return;
        }

        public override short RequestSample(ushort input, ushort am_offset) { return FilterToApply(input, am_offset); }
        public short RequestBypass(ushort input, ushort am_offset)  { return (short)input; }
        public short RequestFilteredSample(ushort input, ushort am_offset)
        {
            var output = Process(Tables.short2float[ (short)input + Tables.SIGNED_TO_INDEX]);
            var mix = (int)Tools.Clamp((output * short.MaxValue), short.MinValue, short.MaxValue);
            mix = Tools.Lerp16(mix, (short)input, eg.duty);
            return (short)(mix);
        }

        //Since filters don't have an oscillator, we can re-use this function to recalculate our filter type based on the index.
        public override void SetOscillatorType(byte index)
        {
            if (index==0)  FilterToApply=RequestBypass; else FilterToApply=RequestFilteredSample;
            eg.aux_func = index;
            Recalc();
        }

        public float Process(float in0)
        {
            // filter
            float yn = b0a0*in0 + b1a0*in1 + b2a0*in2 - a1a0*ou1 - a2a0*ou2;

            // push in/out buffers
            in2=in1;
            in1=in0;
            ou2=ou1;
            ou1=yn;

            // return output
            return yn;
        }



        //TODO:  Consider splitting out some derived values like omega etc to its own update function or otherwise simplifying this so less processing is done on recalc!!

        public void Recalc() {Recalc((FilterType)eg.aux_func, eg.cutoff, eg.resonance, eg.gain);}

        public void Recalc(FilterType type, double frequency, double q, double db_gain, bool q_is_bandwidth=false)
        {
            // temp pi
            const double PI=3.1415926535897932384626433832795;
            // temp coef vars
            double alpha=0,a0=0,a1=0,a2=0,b0=0,b1=0,b2=0;

            double omega=	2.0*PI*frequency/Global.MixRate;
            double tsin	=	Math.Sin(omega);
            double tcos	=	Math.Cos(omega);

            if(q_is_bandwidth)
                alpha=tsin * Math.Sinh(Math.Log(2.0)/2.0*q*omega/tsin);
            else
                alpha=tsin/(2.0*q);

            double A=0, beta=0;
            // for peaking, lowshelf and hishelf
            if((int)type>6)
            {
                A   	=	Math.Pow(10.0,(db_gain/40.0));
                beta	=	Math.Sqrt(A)/q;
            }

            switch((FilterType)eg.aux_func)
            {
                case FilterType.NONE:
                    b0=1;
                    b1=1;
                    b2=1;
                    a0=1;
                    a1=1;
                    a2=1;
                    break;
                                
                case FilterType.LOWPASS:
                    b0=(1.0-tcos)/2.0;
                    b1=1.0-tcos;
                    b2=(1.0-tcos)/2.0;
                    a0=1.0+alpha;
                    a1=-2.0*tcos;
                    a2=1.0-alpha;
                    break;

                case FilterType.HIPASS:
                    b0=(1.0+tcos)/2.0;
                    b1=-(1.0+tcos);
                    b2=(1.0+tcos)/2.0;
                    a0=1.0+ alpha;
                    a1=-2.0*tcos;
                    a2=1.0-alpha;
                    break;

                case FilterType.BANDPASS_CSG:
                    b0=tsin/2.0;
                    b1=0.0;
                    b2=-tsin/2;
                    a0=1.0+alpha;
                    a1=-2.0*tcos;
                    a2=1.0-alpha;
                    break;

                case FilterType.BANDPASS_CZPG:
                    b0=alpha;
                    b1=0.0;
                    b2=-alpha;
                    a0=1.0+alpha;
                    a1=-2.0*tcos;
                    a2=1.0-alpha;
                    break;

                case FilterType.NOTCH:
                    b0=1.0;
                    b1=-2.0*tcos;
                    b2=1.0;
                    a0=1.0+alpha;
                    a1=-2.0*tcos;
                    a2=1.0-alpha;
                    break;

                case FilterType.ALLPASS:
                    b0=1.0-alpha;
                    b1=-2.0*tcos;
                    b2=1.0+alpha;
                    a0=1.0+alpha;
                    a1=-2.0*tcos;
                    a2=1.0-alpha;
                    break;

                case FilterType.PEAKING:
                    b0 = (float) (1.0+alpha*A);
                    b1 = (float) (-2.0*tcos);
                    b2 = (float) (1.0-alpha*A);
                    a0 = (float) (1.0+alpha/A);
                    a1 = (float) (-2.0*tcos);
                    a2 = (float) (1.0-alpha/A);
                    break;
                
                case FilterType.LOWSHELF:
                    b0 = (float) (A*((A+1.0)-(A-1.0)*tcos+beta*tsin));
                    b1 = (float) (2.0*A*((A-1.0)-(A+1.0)*tcos));
                    b2 = (float) (A*((A+1.0)-(A-1.0)*tcos-beta*tsin));
                    a0 = (float) ((A+1.0)+(A-1.0)*tcos+beta*tsin);
                    a1 = (float) (-2.0*((A-1.0)+(A+1.0)*tcos));
                    a2 = (float) ((A+1.0)+(A-1.0)*tcos-beta*tsin);
                    break;

                case FilterType.HISHELF:
                    b0 = (float) (A*((A+1.0)+(A-1.0)*tcos+beta*tsin));
                    b1 = (float) (-2.0*A*((A-1.0)+(A+1.0)*tcos));
                    b2 = (float) (A*((A+1.0)+(A-1.0)*tcos-beta*tsin));
                    a0 = (float) ((A+1.0)-(A-1.0)*tcos+beta*tsin);
                    a1 = (float) (2.0*((A-1.0)-(A+1.0)*tcos));
                    a2 = (float) ((A+1.0)-(A-1.0)*tcos-beta*tsin);
                    break;
            }

            // set filter coeffs
            b0a0 = (float) (b0/a0);
            b1a0 = (float) (b1/a0);
            b2a0 = (float) (b2/a0);
            a1a0 = (float) (a1/a0);
            a2a0 = (float) (a2/a0);
        }


    }

}
