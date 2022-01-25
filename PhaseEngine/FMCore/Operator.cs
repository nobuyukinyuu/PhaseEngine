using System;
using PhaseEngine;

namespace PhaseEngine 
{
    
    public abstract class OpBase  //Base for operator, LFO and filter classes
    {
        public enum Intents { LFO=-1, NONE, FM_OP, FILTER, BITWISE, WAVEFOLDER };
        public Intents intent = Intents.NONE;


        protected Oscillator oscillator = new Oscillator(Oscillator.Sine2);
        public delegate short SampleOutputFunc(ushort modulation = 0, ushort am_offset=0); //Primary function of the oscillator
        public SampleOutputFunc operatorOutputSample;


        protected long phase;  //Phase accumulator
        protected bool flip=false;  // Used by the oscillator to flip the waveform's values.  TODO:  User-specified waveform inversion
        protected int seed=1;  //LFSR state sent ByRef to oscillators which produce noise

        public Envelope eg = new Envelope();
        public EGStatus egStatus = EGStatus.INACTIVE;
        public ushort egAttenuation = Envelope.L_MAX;  //5-bit value


        public Increments pg = Increments.Prototype();
 
        // public abstract void SetOscillatorType(Oscillator.waveFunc waveFunc);
        public abstract void SetOscillatorType(byte waveform_index);
        // public abstract short RequestSample(ushort modulation = 0);
        public abstract void Clock();

        public abstract short RequestSample(ushort input, ushort am_offset);

    }




    public class Operator : OpBase
    {
        // public long phase;  //Phase accumulator
        // bool flip=false;  // Used by the oscillator to flip the waveform's values.  TODO:  User-specified waveform inversion
        public uint env_counter;  //Envelope counter
        public uint env_hold_counter=0;  //Counter during the hold phase of an envelope


        //Parameters specific to Operator
        public short[] fbBuf = new short[2];  //feedback buffer


        public Operator(){operatorOutputSample=ComputeVolume; intent=Intents.FM_OP; }
        // public Operator(){ operatorOutputSample=OperatorType_ComputeLogOuput; }


        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)] 
            void ResetPhase() {if (eg.osc_sync) phase=0;  phase += Increments.PhaseOffsetOf(pg, eg.phase_offset);}

        public void NoteOn(Increments increments){ pg = increments;  NoteOn(); }
        public void NoteOn()
        {
            ResetPhase();
            env_counter = 0;
            env_hold_counter = 0;

            egAttenuation = Envelope.L_MAX;
            egStatus = EGStatus.DELAY;
        }
        public void NoteOff()
        {
            egStatus = EGStatus.RELEASED;
        }

        public override void Clock()
        {
            phase += pg.increment;  


            // increment the envelope count; low two bits are the subcount, which
            // only counts to 3, so if it reaches 3, count one more time
            env_counter++;
            if (Tools.BIT(env_counter, 0, 2) == 3)
                env_counter++;
            // clock the envelope if on an envelope cycle
            if (Tools.BIT(env_counter, 0, 2) == 0)
                EGClock(env_counter >> 2);
                // EGClock(env_counter);
        }

        public override short RequestSample(ushort modulation = 0, ushort am_offset = 0)
        {
            return operatorOutputSample(modulation, am_offset);
            // return oscillator.Generate(unchecked(phase >> Global.FRAC_PRECISION_BITS), duty, ref flip);
        }

        public Oscillator.oscTypes GetOscillatorType(){ return oscillator.CurrentWaveform; }

        //Sets up the operator to act as an oscillator for FM output.
        public void SetOscillatorType(Oscillator.oscTypes type)
        {
            oscillator.CurrentWaveform = type;
            switch(type.ToString())
            {
                case "Brown":
                case "White":
                case "Pink":
                case "Noise1":
                case "Noise2":
                // case "Sine3":
                {
                    seed = 1;  //Reset the seed.
                    //Set the operator's sample output function to work in the linear domain.
                    operatorOutputSample = OperatorType_Noise;
                    return;
                }
            }

            //Feedback causes amplitude oscillation issues when applied to sounds, so we don't use this function if the feedback's off.
            if (eg.feedback>0) operatorOutputSample = ComputeFeedback; else operatorOutputSample = ComputeVolume;
        }

        public override void SetOscillatorType(byte waveform_index)
        {
            try{
                SetOscillatorType((Oscillator.oscTypes)waveform_index);
            } catch(IndexOutOfRangeException e) {
                System.Diagnostics.Debug.Print(String.Format("Waveform {0} not implemented: {1}", waveform_index, e.ToString()));
            }
        }


        //=============Oscillator output types.  Either standard waveform (log domain), noise, or sample.=========================


        //Noise generators produce asymmetrical data.  Values must be translated to/from the log domain.
        public short OperatorType_Noise(ushort modulation, ushort am_offset)
        {
            ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);
            var samp = (short) oscillator.Generate(phase, eg.duty, ref flip, __makeref(this.seed));
            ushort env_attenuation = (ushort) (envelope_attenuation(am_offset) << 2);

            const float SCALE = 1.0f / 8192;

            ushort logScale = (ushort)(Tables.attenuation_to_volume((ushort)(env_attenuation)));

            // var tl = 1 - (eg.tl*ONE_PER_THOU);
            // short result = (short) (samp * (logScale * SCALE) * tl);
            short result = (short) (samp * (logScale * SCALE) );

            return result;
        }

        public short ComputeVolume(ushort modulation, ushort am_offset)
        {
            // start with the upper 10 bits of the phase value plus modulation
            // the low 10 bits of this result represents a full 2*PI period over
            // the full sin wave
            ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);

            // get the absolute value of the sin, as attenuation, as a 4.8 fixed point value
            // ushort sin_attenuation = oscillator.Generate(phase, eg.duty, ref flip, __makeref(pg.hz));
            ushort sin_attenuation = oscillator.Generate(phase, eg.duty, ref flip, __makeref(pg.increment));

            // get the attenuation from the envelope generator as a 4.6 value, shifted up to 4.8
            ushort env_attenuation = (ushort) (envelope_attenuation(am_offset) << 2);
            // ushort env_attenuation = envelope_attenuation(am_offset) << 2;

            // combine into a 5.8 value, then convert from attenuation to 13-bit linear volume
            int result = Tables.attenuation_to_volume((ushort)(sin_attenuation + env_attenuation));

            // result = (int)(result * (1 - (eg.tl*Global.ONE_PER_THOU)));  //Floating point conversion.... expensive?

            // negate if in the negative part of the sin wave (sign bit gives 14 bits)
            return flip? (short)-result : (short)result;
        }


        /// Summary:  Calculates the self feedback of the given input with the given modulation amount.
        public short lBuf;  //Linear buffer?  FIXME / DEBUG
        public short ComputeFeedback(ushort modulation, ushort am_offset)
        {
            // if (eg.feedback == 0) 
            // {
            //     // return (ComputeVolume(modulation, am_offset));
            //     //Linear interpolation buffer;  Is this worth it?
            //     lBuf = (short)((ComputeVolume(modulation, am_offset) + lBuf) >> 1);
            //     return lBuf;
            // }
            var avg = (fbBuf[0] + fbBuf[1]) >> (10 - eg.feedback);
            var output = ComputeVolume(unchecked((ushort)(avg+modulation)), am_offset);
            fbBuf[1] = fbBuf[0];
            fbBuf[0] = output;

            return output;
        }


//////////////////// ENVELOPE /////////////////////////
        public void EGClock(uint env_counter)
        {

            if (egStatus == EGStatus.INACTIVE) return;
            ushort target;

            switch (egStatus)
            {
            case EGStatus.DELAY:
                if (env_counter >> 2 < eg.delay) return;
                //Why return here?  Other cases set the target level.  Consider setting it here too.  FIXME
                else {egStatus = EGStatus.ATTACK;  ResetPhase(); return;}  

            case EGStatus.ATTACK:
                target = eg.levels[(int)EGStatus.ATTACK];
                if (egAttenuation <= target)  
                {
                    egStatus = EGStatus.HOLD;
                    return;
                }
                break;

            case EGStatus.HOLD:
                if ((env_hold_counter >> 2) >= eg.hold)
                {
                    egStatus = EGStatus.DECAY;
                    // target = eg.levels[(int)EGStatus.DECAY];
                } else {
                    env_hold_counter++; 
                    return;
                }
                break;

            case EGStatus.DECAY:
                target = eg.levels[(int)EGStatus.DECAY];
                if ( ((egAttenuation >= target) && !eg.rising[(int)EGStatus.DECAY]) | (eg.rising[(int)EGStatus.DECAY] && (egAttenuation <= target)))  egStatus ++;
                break;
            case EGStatus.SUSTAINED:
                target = eg.levels[(int)EGStatus.SUSTAINED];
                if ( ((egAttenuation >= target) && !eg.rising[(int)EGStatus.SUSTAINED]) | (eg.rising[(int)EGStatus.SUSTAINED] && (egAttenuation <= target)))  
                {   //We're at the target sustain level.  Check if we can early exit to inactive state.

                    // // FIXME:  If release levels != L_MAX become supported, use the more complicated check commented out below and remove the simple check.
                    // var releaseTarget = eg.levels[(int)EGStatus.RELEASED];
                    // if(target == releaseTarget && releaseTarget == Envelope.L_MAX)  egStatus = EGStatus.INACTIVE;

                    if(target == Envelope.L_MAX)  egStatus = EGStatus.INACTIVE;
                    return;
                }
                break;
            case EGStatus.RELEASED:
                target = Envelope.L_MAX;  //Max attenuation until a different release level is supported (which may be never)
                if ( ((egAttenuation >= target) && !eg.rising[(int)EGStatus.RELEASED]) | (eg.rising[(int)EGStatus.RELEASED] && (egAttenuation <= target)))  
                {
                    egStatus = EGStatus.INACTIVE;
                    return;
                }

                break;
            }

            // determine our raw 5-bit rate value
            // byte rate = effective_rate(m_regs.adsr_rate(m_env_state), keycode);
            byte rate = eg.rates[(byte) egStatus];

            // compute the rate shift value; this is the shift needed to
            // apply to the env_counter such that it becomes a 5.11 fixed
            // point number
            byte rate_shift = (byte)(rate >> 2);
            env_counter <<= rate_shift;

            // see if the fractional part is 0; if not, it's not time to clock
            if (Tools.BIT(env_counter, 0, 11) != 0)
                return;


            // determine the increment based on the non-fractional part of env_counter
            byte increment = Tables.attenuation_increment(rate, (byte) Tools.BIT(env_counter, 11, 3));


            // attack is the only one that increases
            // if (egStatus == EGStatus.ATTACK || eg.rising[(int)egStatus])
            if (egStatus == EGStatus.ATTACK)
            {
                egAttenuation += (ushort) ((~egAttenuation * increment) >> 4);

            } else if (eg.rising[(int)egStatus]) {  //Decrement.
                egAttenuation = (ushort) Math.Max(egAttenuation-increment, 0);
            } else {  //Most envelope states simply increase the attenuation by the increment previously determined
                egAttenuation += increment;
            }


            // clamp the final attenuation
            if (egAttenuation >= 0x400)
                egAttenuation = 0x3ff;

        }


        //-------------------------------------------------
        //  envelope_attenuation - return the effective
        //  attenuation of the envelope
        //-------------------------------------------------

        protected ushort envelope_attenuation(ushort am_offset)
        {
            ushort result = egAttenuation;

            // // add in LFO AM modulation
            // if (m_regs.lfo_am_enabled())
            //     result += am_offset;
        	// result += pg.lastClockedAttenuation;

            // // add in total level
            result += eg.tl;

            if (eg.ams > 0)
                result += LFO.ApplyAMS(am_offset, eg.ams);


            // clamp to max and return
            return (result < 0x400) ? result : (ushort)0x3ff;
        }


    }

    public class BitwiseOperator : Operator
    {
        public delegate short OpFunc(short modulation, short oscOutput); //Function of the operator.
        OpFunc BitwiseOp;
        public static readonly OpFunc[] operations = {OP_AND, OP_OR, OP_XOR, OP_RINGMOD};
        public byte OpFuncType {get => eg.aux_func;  set { if (value<operations.Length)  {BitwiseOp = operations[value]; eg.aux_func=value;} }}

        public BitwiseOperator() {intent=Intents.BITWISE; BitwiseOp=OP_OR;}

        public override short RequestSample(ushort modulation = 0, ushort am_offset = 0)
        {
            return BitwiseOp(operatorOutputSample(0, am_offset), (short)modulation);  //Modulation sent to us is the sample value of previous operator.
        }

        //Bitwise Funcs.   TODO:  Implement ROR/ROL?  
        public static short OP_AND(short modulation, short input) {return (short)(input & modulation);}
        public static short OP_OR(short modulation, short input) {return (short)(input | modulation);}
        public static short OP_XOR(short modulation, short input) {return (short)(input ^ modulation);}
        public static short OP_RINGMOD(short modulation, short input) {return (short)(input * modulation >> 13);}

    }

}
