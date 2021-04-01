using System;
using gdsFM;

namespace gdsFM 
{
    public class Operator
    {
        public ulong phase;  //Phase accumulator
        public uint counter;  //Total clock count.  
        public float clocksNeeded;  //Clock accumulator. Consider moving this to a chip/cpu class so multiple operators can share the clock. Consider tying noise gens to chip
        public ulong noteIncrement;  //Frequency multiplier for note base hz.

        public short[] fbBuf = new short[2];  //feedback buffer
        public byte feedback = 0;

        public ushort duty = 32767;

        public Oscillator oscillator = new Oscillator(Oscillator.Sine);

        public delegate short sampleOutputFunc(); //Primary function of the operator
        public sampleOutputFunc operatorOutputSample;

        //thought:  separate into carrier and modulator funcs, carrier output being a float and modulator being double

        public Operator(){ operatorOutputSample=OperatorType_ComputeLogOuput; }

        public void NoteOn()
        {
            phase=0;
            env_counter = 0;
            m_env_attenuation = 0;
            
        }
        public void NoteOff()
        {}

        public void Clock()
        {
            // phase = (ulong)unchecked((long)phase + oscillator.Generate(phase, duty));
            phase += noteIncrement;   //FIXME:  CHANGE TO TOTAL INCREMENT
            counter++;


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

        public short RequestSample()
        {
            return operatorOutputSample();
            // return oscillator.Generate(unchecked(phase >> Global.FRAC_PRECISION_BITS), duty, ref flip);
        }

        //Operator meta functions;  delegate points to either the FM operation or some other operation, like filtering, waveshape, FM+Feedback.
        //At the top of a chain, use RequestSample() instead of this.  Special op functions can be set to passthru or silence.
        //TODO:  Consider whether there should be separate delegates for feedback-producing operators. 
        public short Modulate(ushort input) {return 0;}
        public short Modulate(short input) {return 0;}


        //Sets up the operator to act as an oscillator for FM output.
        public void SetOperatorType(Oscillator.waveFunc waveFunc)
        {
            oscillator.SetWaveform(waveFunc);
            switch(waveFunc.Method.Name)
            {
                case "Brown":
                case "White":
                case "Pink":
                case "Noise2":
                {
                    //Set the operator's sample output function to work in the linear domain.
                    operatorOutputSample = OperatorType_Noise;
                    return;
                }
            }

            operatorOutputSample = OperatorType_ComputeLogOuput;
        }  //TODO:  Operator types for filters and sample-based outputs

        //Oscillator output types.  Either standard waveform (log domain), noise, or sample.
        public short OperatorType_ComputeLogOuput()
        {return compute_volume(0,0);}

        public short OperatorType_Noise()
        {return unchecked((short) oscillator.Generate(phase, duty, ref flip));}


        // public float LinearVolume(short samp)
        // {
        //     var output = Tables.linVol[samp + Tables.SIGNED_TO_INDEX];
        //     // if (Tools.BIT(phase >> Global.FRAC_PRECISION_BITS, Tables.SINE_HALFWAY_BIT).ToBool())   output = -output;
        //     // if (Tools.BIT(samp, 14).ToBool())   output = -output;
        //     return output;
        // }


        bool flip=false;
        public short compute_volume(ushort modulation, ushort am_offset)
        {
            // start with the upper 10 bits of the phase value plus modulation
            // the low 10 bits of this result represents a full 2*PI period over
            // the full sin wave
            ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);

            // get the absolute value of the sin, as attenuation, as a 4.8 fixed point value
            // ushort sin_attenuation = Tables.abs_sin_attenuation(phase);
            ushort sin_attenuation = oscillator.Generate(phase, duty, ref flip);

            // get the attenuation from the evelope generator as a 4.6 value, shifted up to 4.8
            ushort env_attenuation = (ushort) (envelope_attenuation() << 2);
            // ushort env_attenuation = envelope_attenuation(am_offset) << 2;
            // ushort env_attenuation = 0;

            // combine into a 5.8 value, then convert from attenuation to 13-bit linear volume
            short result = (short) Tables.attenuation_to_volume((ushort)(sin_attenuation + env_attenuation));

            // negate if in the negative part of the sin wave (sign bit gives 14 bits)
            return flip ? (short)-result : result;
            // return Tools.BIT(phase, 9).ToBool() ? (short)-result : result;
        }


        /// Summary:  Calculates the self feedback of the given input with the given modulation amount.
        public short compute_fb(ushort modulation)
        {    
    		var avg = (fbBuf[0] + fbBuf[1]) >> (10 - feedback);
            var output = compute_volume(unchecked((ushort)(avg+modulation)),0);
            fbBuf[1] = fbBuf[0];
            fbBuf[0] = output;

            return output;
        }

        /// summary:  Given a MIDI note value 0-127,  produce an increment appropriate to oscillate at the tone of the note.
        public void NoteSelect(byte n)
        {
            const int NOTE_A4=69;
            // noteIncrement = IncOfFreq(440.0 * Math.Pow(2, (n-NOTE_A4)/12.0));
            var whole = IncOfFreq(Global.BASE_HZ * Math.Pow(2, (n-NOTE_A4)/12.0));
            var frac = whole - Math.Truncate(whole);
        
            noteIncrement = (uint)(frac * Global.FRAC_SIZE) | ((uint)(whole) << Global.FRAC_PRECISION_BITS);
            
        }
        /// summary:  Given a hz rate, produce an increment appropriate to tune the oscillator to this rate.
        public void FreqSelect(double freq)
        {
            // noteIncrement = IncOfFreq(freq);
            var whole = IncOfFreq(freq);
            var frac = whole - Math.Truncate(whole);
        
            noteIncrement = (ulong)(frac * Global.FRAC_SIZE) | ((ulong)(whole) << Global.FRAC_PRECISION_BITS);
            // noteIncrement &= Int32.MaxValue;
        }


        public static double FRatio { get =>  (1<<Tables.SINE_TABLE_BITS) / Global.MixRate; } // The increment of a frequency of 1 at the current mixing rate.
        public static double IncOfFreq(double freq)  //Get the increment of a given frequency.
        {
            return FRatio * freq;
        }


//////////////////// ENVELOPE /////////////////////////

    public ushort m_env_attenuation;

    public byte env_state=20;
    public uint env_counter;

    public void EGClock(uint env_counter)
    {
	    // determine our raw 5-bit rate value
        // byte rate = effective_rate(m_regs.adsr_rate(m_env_state), keycode);
        byte rate = env_state;

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

        // // attack is the only one that increases
        // if (env_state == ENV_ATTACK)
        // {
        //     // glitch means that attack rates of 62/63 don't increment if
        //     // changed after the initial key on (where they are handled
        //     // specially)

        //     // QUESTION: this check affects one of the operators on the gng credit sound
        //     //   is it correct?
        //     // QUESTION: does this apply only to YM2612?
        //     // if (rate < 62)
        //         m_env_attenuation += (~m_env_attenuation * increment) >> 4;
        // }

        // // all other cases are similar
        // else
        // {
            // non-SSG-EG cases just apply the increment
            // if (!m_regs.ssg_eg_enabled())
                m_env_attenuation += increment;


            // clamp the final attenuation
            if (m_env_attenuation >= 0x400)
                m_env_attenuation = 0x3ff;
        // }
    }


    //-------------------------------------------------
    //  envelope_attenuation - return the effective
    //  attenuation of the envelope
    //-------------------------------------------------

    ushort envelope_attenuation()//byte am_offset)
    {
        ushort result = m_env_attenuation;

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
