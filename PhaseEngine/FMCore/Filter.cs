using System;
using PhaseEngine;

/*
    BIG TODO LIST:
    1.  Support key follow!  0-1 ratio whereby 0 is pure cutoff freq and 1 is the NoteOn frequency times a ratio specifier.
    2.  Ratio specifier for key follow.  Might also affect filter envelopes?
    3.  Consider the following hierarchy:  1.  Base cutoff frequency, which is multiplied by 2. Envelope output, which finally
        is lerped Between itself and 3.  The key follow frequency (specified by NoteOn frequency times the 4. Ratio multiplier).
    4.  Consider whether the filter envelope should be specified as points which cache lerp values or as an extension of the traditional envelope.
    5.  Consider easing the envelope's recalculations so that it's impossible to "snap" the values to unsafe levels.
    6.  Consider a recalc divider similar to LFO which lowers the processing burden on the filter.  Check if processing needs to be done more than 6000hz.
    
*/


namespace PhaseEngine 
{
    public class Filter : OpBase
    {
        const double LOG_OF_2 = 0.693147180559945;

        //TODO:  Replace with delegate func array
        public enum FilterType {NONE, LOWPASS, HIPASS, BANDPASS_CSG, BANDPASS_CZPG, NOTCH, ALLPASS, PEAKING, LOWSHELF, HISHELF}
        public delegate short FilterFunc(ushort input, ushort am_offset);
        public FilterFunc FilterToApply;

        // filter coeffs
        float b0a0=1,b1a0=1,b2a0=1,a1a0=1,a2a0=1;
        // in/out history
        float ou1,ou2,in1,in2;

        public const double GAIN_MAX = 4.0, GAIN_MIN=0.25;  //Use to clamp envelope gain when converting from another Op type to Filter

        public Filter()    {FilterToApply=RequestBypass; Reset(); intent = Intents.FILTER;}
        public void Reset()
        {
            // reset filter coeffs
            b0a0=b1a0=b2a0=a1a0=a2a0= 1.0f;
            // reset in/out history
            ou1=ou2=in1=in2=0;	
        }

        public override void Clock()
        {
            //TODO:  Support recalculating filter envelopes?
            //      Actions are void delegates.  As an IBindableDataConsumer, we can check our valid BindStates at tick time to determine
            //      the action to take.  A full recalc need not be done every audio frame.  Also consider adding a user-customizable clock divider factor.
            //      More likely, to begin with, we can experiment with a coef recalc value as low as 100fps (divider of 480) to judge quality.

            bool recalcCoefficients = false;  //Used to determine if any of our important values changed and we therefore need to recalc our coefficients.
            for(int i=0; i<BindStates.Count; i++)
            {
                CachedEnvelope envelope;
                envelope = BindStates.Values[i];
                if(envelope.PostUpdateAction==null || !envelope.JustTicked || envelope.Finished || eg.mute || eg.bypass) continue;

                envelope.PostUpdateAction.Invoke(this, null);  //Invoke the associated method, if it exists
                recalcCoefficients = true;
            }
            if (recalcCoefficients) RecalcCoefficientsOnly();
            return;
        }
        
        // public override void NoteOff()
        // {
        //     // ((IBindableDataConsumer)this).NoteOff();
        //     // throw new NotImplementedException();
        // }
        public override void NoteOn()
        {  //TODO:  Consider re-using an rTable from EG (KSL?) to implement key follow for the frequency value.
            // TrackerEnvelope envelope;
            // for(int i=0; i<eg.BoundEnvelopes.Count; i++)
            // {
            //     envelope = eg.BoundEnvelopes.Values[i];
            //     switch(eg.BoundEnvelopes.Keys[i])
            //     {
            //         case nameof(eg.cutoff):
            //             eg.cutoff = Convert.ToDouble(envelope.InitialValue);  break;
            //         case nameof(eg.resonance):
            //             eg.resonance = Convert.ToDouble(envelope.InitialValue); break;
            //         case nameof(eg.gain):
            //             eg.gain = Convert.ToDouble(envelope.InitialValue); break;
            //     }
            // }
            Reset();  RecalcAll();
            // throw new NotImplementedException();
        }

        public override short RequestSample(ushort input, ushort am_offset) => FilterToApply(input, am_offset); 
        public short RequestBypass(ushort input, ushort am_offset)  => (short)input; 
        // public short RequestFilteredSample2(ushort input, ushort am_offset)
        // {
        //     var output = Process(Tables.short2float[ (short)input + Tables.SIGNED_TO_INDEX]);
        //     var mix = (int)Tools.Clamp((output * short.MaxValue), short.MinValue, short.MaxValue);
        //     mix = Tools.Lerp16(mix, (short)input, eg.duty);
        //     return (short)(mix);
        // }
        public short RequestFilteredSample(ushort input, ushort am_offset)
        {
            var ins = (short)input;
            var output = Process(ins);
            var mix = Math.Clamp(output, short.MinValue, short.MaxValue);
            // int mix = (short)(output & SHORT_MASK);  //Causes wrap-around on overflow
            mix = Tools.Lerp16((int)mix, ins, eg.duty);
            return (short)(mix);
        }

        //Since filters don't have an oscillator, we can re-use this function to recalculate our filter type based on the index.
        public override void SetOscillatorType(byte index)
        {
            if (index==0)  FilterToApply=RequestBypass; else FilterToApply=RequestFilteredSample;
            // eg.aux_func = index;
            RecalcAll();
        }

        public float Process(float in0)
        {
            // filter
            var yn = (b0a0*in0 + b1a0*in1 + b2a0*in2 - a1a0*ou1 - a2a0*ou2);
            // var n = Tools.InverseLerp(0, short.MaxValue+1, Math.Abs(yn));
            // yn = Tools.Lerp(0, short.MaxValue, n - (float)Math.Pow(n, 3)/3.0f) * Math.Sign(yn);
            yn = Tables.softClip[(int)Math.Clamp(yn+0x7FFF, 0, ushort.MaxValue)];


            // push in/out buffers
            in2=in1;
            in1=in0;
            ou2=ou1;
            ou1=yn;

            // return output
            return yn;
        }




        public void RecalcAll() => Recalc((FilterType)eg.aux_func, eg.cutoff, eg.resonance, eg.gain);
        public void RecalcCoefficientsOnly() => Recalc();
        void Recalc() => Recalc((FilterType)eg.aux_func);  //Recalculate the coefficients for the given filter type only.

        double omega, tsin, tcos, q=1.0;
        double alpha;  //Derived from Q
        double beta, gain;

        public void RecalcFrequency() => RecalcFrequency(eg.cutoff);
        void RecalcFrequency(double frequency, bool recalcCoefficients=true)  //Use when frequency changes
        {
            const double TWOPI=6.28318530717959;
            omega=	TWOPI*frequency/Global.MixRate;
            tsin	=	Math.Sin(omega);
            tcos	=	Math.Cos(omega);

            if (recalcCoefficients) Recalc();
        }

        public void RecalcQFactor() => RecalcQFactor(eg.resonance);
        void RecalcQFactor(double q, bool q_is_bandwidth=false, bool recalcCoefficients=true)  //Use when Q factor changes
        {
            this.q=q;
            if(q_is_bandwidth)
                alpha=tsin * Math.Sinh(LOG_OF_2 / 2.0*q*omega/tsin);
            else
                alpha=tsin/(2.0*q);

            if (recalcCoefficients) Recalc();
        }
        public void RecalcGain() => RecalcGain(eg.gain);
        void RecalcGain(double input, bool recalcCoefficients=true)
        {
                gain   	=	input;
                beta	=	Math.Sqrt(input)/q;            

            if (recalcCoefficients) Recalc();
        }

        //Recalc everything
        void Recalc(FilterType type, double frequency, double q, double db_gain, bool q_is_bandwidth=false)
        {
            RecalcFrequency(frequency);
            RecalcQFactor(q, q_is_bandwidth);

            // for peaking, lowshelf and hishelf
            if((int)type>6)
                // A   	=	Math.Pow(10.0,(db_gain/40.0));
                RecalcGain(db_gain);

            Recalc(type);
        }

        void Recalc(FilterType type)  //Recalc coefficients only after some other value changes
        {
            // temp coef vars
            double a0=0,a1=0,a2=0,b0=0,b1=0,b2=0;

            switch(type)
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
                    b0 = (float) (1.0+alpha*gain);
                    b1 = (float) (-2.0*tcos);
                    b2 = (float) (1.0-alpha*gain);
                    a0 = (float) (1.0+alpha/gain);
                    a1 = (float) (-2.0*tcos);
                    a2 = (float) (1.0-alpha/gain);
                    break;
                
                case FilterType.LOWSHELF:
                    b0 = (float) (gain*((gain+1.0)-(gain-1.0)*tcos+beta*tsin));
                    b1 = (float) (2.0*gain*((gain-1.0)-(gain+1.0)*tcos));
                    b2 = (float) (gain*((gain+1.0)-(gain-1.0)*tcos-beta*tsin));
                    a0 = (float) ((gain+1.0)+(gain-1.0)*tcos+beta*tsin);
                    a1 = (float) (-2.0*((gain-1.0)+(gain+1.0)*tcos));
                    a2 = (float) ((gain+1.0)+(gain-1.0)*tcos-beta*tsin);
                    break;

                case FilterType.HISHELF:
                    b0 = (float) (gain*((gain+1.0)+(gain-1.0)*tcos+beta*tsin));
                    b1 = (float) (-2.0*gain*((gain-1.0)+(gain+1.0)*tcos));
                    b2 = (float) (gain*((gain+1.0)+(gain-1.0)*tcos-beta*tsin));
                    a0 = (float) ((gain+1.0)-(gain-1.0)*tcos+beta*tsin);
                    a1 = (float) (2.0*((gain-1.0)-(gain+1.0)*tcos));
                    a2 = (float) ((gain+1.0)-(gain-1.0)*tcos-beta*tsin);
                    break;
            }

            // set filter coeffs
            b0a0 = (float) (b0/a0);
            b1a0 = (float) (b1/a0);
            b2a0 = (float) (b2/a0);
            a1a0 = (float) (a1/a0);
            a2a0 = (float) (a2/a0);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static float Undenormalize(float f)  {if(Single.IsSubnormal(f)) return 0.0f; else return f;}

    }

}
