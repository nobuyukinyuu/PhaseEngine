using System;
using gdsFM;

namespace gdsFM 
{
    public class Operator
    {
        public long phase;  //Phase accumulator
        public uint env_counter;  //Envelope counter
        public uint env_hold_counter=0;  //Counter during the hold phase of an envelope
        public ulong noteIncrement;  //Frequency multiplier for note base hz.



        //Parameters specific to Operator
        public short[] fbBuf = new short[2];  //feedback buffer

        public byte feedback = 0;
        public ushort duty = 32767;



        public Envelope eg = new Envelope();
        public Increments pg = Increments.Prototype();

        public Oscillator oscillator = new Oscillator(Oscillator.Sine);

        public delegate short sampleOutputFunc(ushort modulation); //Primary function of the operator
        public sampleOutputFunc operatorOutputSample;


        public Operator(){ operatorOutputSample=OperatorType_ComputeLogOuput; }


        public void NoteOn(Increments increments){ pg = increments;  NoteOn(); }
        public void NoteOn()
        {
            phase=0;
            env_counter = 0;
            env_hold_counter = 0;
            eg.attenuation = 1023;
            
            eg.status = EGStatus.DELAY;
        }
        public void NoteOff()
        {
            eg.status = EGStatus.RELEASED;
        }

        public void Clock()
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

        public short RequestSample(ushort modulation = 0)
        {
            return operatorOutputSample(modulation);
            // return oscillator.Generate(unchecked(phase >> Global.FRAC_PRECISION_BITS), duty, ref flip);
        }

        //Operator meta functions;  delegate points to either the FM operation or some other operation, like filtering, waveshape, FM+Feedback.
        //Special op functions can be set to passthru or silence.
        //TODO:  Consider whether there should be separate delegates for feedback-producing operators. 


        //Sets up the operator to act as an oscillator for FM output.
        public void SetOperatorType(Oscillator.waveFunc waveFunc)
        {
            oscillator.SetWaveform(waveFunc);
            switch(waveFunc.Method.Name)
            {
                case "Brown":
                case "White":
                case "Pink":
                case "Noise1":
                case "Noise2":
                {
                    //Set the operator's sample output function to work in the linear domain.
                    operatorOutputSample = OperatorType_Noise;
                    return;
                }
            }

            operatorOutputSample = OperatorType_ComputeLogOuput;
        }  //TODO:  Operator types for filters and sample-based outputs

        public void SetOperatorType(byte waveform_index)
        {
            try{
                SetOperatorType(Oscillator.waveFuncs[waveform_index]);
            } catch(IndexOutOfRangeException e) {
                System.Diagnostics.Debug.Print(String.Format("Waveform {0} not implemented: {1}", waveform_index, e.ToString()));
            }
        }


        //Oscillator output types.  Either standard waveform (log domain), noise, or sample.
        public short OperatorType_ComputeLogOuput(ushort modulation)
        {return ComputeFeedback(modulation);}


        //Noise generators produce asymmetrical data.  Values must be translated to/from the log domain.
        public short OperatorType_Noise(ushort modulation)
        {
            ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);
            var samp = (short) oscillator.Generate(phase, eg.duty, ref flip);
            ushort env_attenuation = (ushort) (envelope_attenuation() << 2);

            const float SCALE = 1.0f / 8192;

            ushort logScale = (ushort)(Tables.attenuation_to_volume(env_attenuation));
            short result = (short) (samp * (logScale * SCALE));

            return result;
        }



        bool flip=false;  // Used by the oscillator to flip the waveform's values.  TODO:  User-specified waveform inversion
        public short ComputeVolume(ushort modulation, ushort am_offset)
        {
            // start with the upper 10 bits of the phase value plus modulation
            // the low 10 bits of this result represents a full 2*PI period over
            // the full sin wave
            ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);

            // get the absolute value of the sin, as attenuation, as a 4.8 fixed point value
            // ushort sin_attenuation = Tables.abs_sin_attenuation(phase);
            ushort sin_attenuation = oscillator.Generate(phase, eg.duty, ref flip);

            // get the attenuation from the evelope generator as a 4.6 value, shifted up to 4.8
            ushort env_attenuation = (ushort) (envelope_attenuation() << 2);
            // ushort env_attenuation = envelope_attenuation(am_offset) << 2;

            // combine into a 5.8 value, then convert from attenuation to 13-bit linear volume
            int result = Tables.attenuation_to_volume((ushort)(sin_attenuation + env_attenuation));

            result = (int)(result * (1 - (eg.tl*Global.ONE_PER_THOU)));  //Floating point conversion.... expensive?

            // negate if in the negative part of the sin wave (sign bit gives 14 bits)
            return flip ? (short)-result : (short)result;
        }


        /// Summary:  Calculates the self feedback of the given input with the given modulation amount.
        public short ComputeFeedback(ushort modulation)
        {
            if (eg.feedback == 0) return ComputeVolume(modulation, 0);    
            var avg = (fbBuf[0] + fbBuf[1]) >> (10 - eg.feedback);
            var output = ComputeVolume(unchecked((ushort)(avg+modulation)),0);
            fbBuf[1] = fbBuf[0];
            fbBuf[0] = output;

            return output;
        }




//////////////////// ENVELOPE /////////////////////////
    public void EGClock(uint env_counter)
    {

        if (eg.status == EGStatus.INACTIVE) return;
        ushort target;

        switch (eg.status)
        {
        case EGStatus.DELAY:
            if (env_counter >> 2 < eg.delay) return;
            else {eg.status = EGStatus.ATTACK;  return;}  //Why return here?  Other cases set the target level.  Consider setting it here too.  FIXME

        case EGStatus.ATTACK:
            target = eg.levels[(int)EGStatus.ATTACK];
            if (eg.attenuation <= target)  
            {
                eg.status = EGStatus.HOLD;
                return;
            }
            break;

        case EGStatus.HOLD:
            if ((env_hold_counter >> 2) >= eg.hold)
            {
                eg.status = EGStatus.DECAY;
                // target = eg.levels[(int)EGStatus.DECAY];
            } else {
                env_hold_counter++; 
                return;
            }
            break;

        case EGStatus.DECAY:
            target = eg.levels[(int)EGStatus.DECAY];
            if ( ((eg.attenuation >= target) && !eg.rising[(int)EGStatus.DECAY]) | (eg.rising[(int)EGStatus.DECAY] && (eg.attenuation <= target)))  eg.status ++;
            break;
        case EGStatus.SUSTAINED:
            target = eg.levels[(int)EGStatus.SUSTAINED];
            if ( ((eg.attenuation >= target) && !eg.rising[(int)EGStatus.SUSTAINED]) | (eg.rising[(int)EGStatus.SUSTAINED] && (eg.attenuation <= target)))  return;
            // if (eg.attenuation >= target) return;
            //TODO:  Logic to keep sustain at target attenuation until NoteOff;  check NoteOff before iterating status to release

            break;
        }

        // determine our raw 5-bit rate value
        // byte rate = effective_rate(m_regs.adsr_rate(m_env_state), keycode);
        byte rate = eg.rates[(byte) eg.status];

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
        // if (eg.status == EGStatus.ATTACK || eg.rising[(int)eg.status])
        if (eg.status == EGStatus.ATTACK)
        {
            eg.attenuation += (ushort) ((~eg.attenuation * increment) >> 4);

        } else if (eg.rising[(int)eg.status]) {  //Decrement.
            eg.attenuation = (ushort) Math.Max(eg.attenuation-increment, 0);
        } else {  //Most envelope states simply increase the attenuation by the increment previously determined
            eg.attenuation += increment;
        }


        // clamp the final attenuation
        if (eg.attenuation >= 0x400)
            eg.attenuation = 0x3ff;

    }


    //-------------------------------------------------
    //  envelope_attenuation - return the effective
    //  attenuation of the envelope
    //-------------------------------------------------

    ushort envelope_attenuation()//byte am_offset)
    {
        ushort result = eg.attenuation;

        // // add in LFO AM modulation
        // if (m_regs.lfo_am_enabled())
        // 	result += am_offset;

        // // add in total level
        // result += m_regs.total_level() << 3;

        // clamp to max and return
        return (result < 0x400) ? result : (ushort)0x3ff;
    }


    }

}
