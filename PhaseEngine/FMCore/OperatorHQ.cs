using System;
using System.Collections.Generic;
using PhaseEngine;

namespace PhaseEngine 
{
    public class OperatorHQ : Operator
    {
        // public long phase;  //Phase accumulator
        // bool flip=false;  // Used by the oscillator to flip the waveform's values.  TODO:  User-specified waveform inversion
        // public uint env_counter;  //Envelope counter
        // public uint env_hold_counter=0;  //Counter during the hold phase of an envelope


        //Parameters specific to Operator
        // public short[] fbBuf = new short[2];  //feedback buffer

        public OperatorHQ(){operatorOutputSample=ComputeVolume; intent=Intents.FM_HQ; }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)] 
            void ResetPhase() {if (eg.osc_sync) {phase=0; seed=Global.DEFAULT_SEED;};  phase += Increments.PhaseOffsetOf(in pg, eg.phase_offset);}

        public override void NoteOn()
        {
            base.NoteOn();
            egAttenuation2 = (Envelope.L_MAX<<5) << EG_LEVEL_PRECISION;
            // ResetPhase();
            
            // env_counter = 0;
            // env_hold_counter = 0;

            // egAttenuation = Envelope.L_MAX;
            // egStatus = EGStatus.DELAY;
        }
        public override void NoteOff()
        {
            base.NoteOff();
            egStatus = EGStatus.RELEASED;
        }

        public override void Clock()
        {
            phase += pg.increment;  

            //Clock the cached envelopes.
            //If it's time to update call BindManager.Update().
            // BindManager.Update(this, eg, BindManager.NO_ACTION);

            

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
            // ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);
            ushort phase = (ushort)( (this.phase >> (Global.FRAC_PRECISION_BITS-0)) + (modulation<<5) );
            
            //FIXME:  LFO calculations need to be translated to HQ attenuation values.  This func needs a rewrite since it depends on 10-bit egAttenuation.
            // ushort env_attenuation = (ushort) (envelope_attenuation(am_offset) << 2);  

            // // SineHQ's table spits out up to 15 bit precision values, which we convert to attenuation values to add the EG and osc attenuation values together.
            // int result = Tables.vol2attenuationHQ[ Oscillator.SineHQ(phase, eg.duty, ref flip, __makeref(this.phase)) ];

            // //Add in the attenuation from the EG and TL here, then convert to volume and reduce to 14-bit to maintain compatibility with other operators.
            // result = Tables.attenuationHQ2vol[(int)Math.Min(ushort.MaxValue, (egAttenuation2>>EG_LEVEL_PRECISION) + result + (eg.tl<<5))] >> 2;
            // return flip? (short)-result : (short)result;



            //FIXME:  The result sample is in 16 bit which means modulation of phase can only have 16-bit fidelity! Consider 32-bit fidelity for smoother FM?
            //      This would require having a ref param specific to this operator which could be fed back in as fixed point decimal bits and mixed down
            //      in Channel.ProcessNextSample; it would mean another add operation and cache for every operator. When generating phase plus modulation
            //      in THIS method, the bit shift amount would be more complex, first reducing by (FRAC_PRECISION_BITS - MODULATION_PRECISION_BITS)
            //      before adding in the decimal bits, then shifting the rest of the way to add the original modulation.  This should provide a higher
            //      fidelity phase adjustment as if the previous sample was 32 bits, and ignore any decimal bits from operators which don't support HQ FM.
            //      

            //To get the floating point approximation of volume from the addition of sinF table and attenuation of EG, multiply against the result of
            // dutyRatio[the converted volume output of egAttenuation2].  We can then convert this to fixed point with our specified bit precision.


            //First, determine the max volume the EG will allow us to go.  This should give us a float from 0-1.
            //TODO:  Make sure the attenuation value accounts for the LFO AM offset
            float env_vol = -Tables.short2float[ 32767 - 
                            Tables.attenuationHQ2vol[ (int)Math.Min(ushort.MaxValue, (egAttenuation2>>(EG_LEVEL_PRECISION)) + (eg.tl<<5)) ] 
                            
                            ];

            //Now, get the oscillator output, which should also be in float, and multiply together.
            env_vol *= OscillatorHQ.Sine((ulong)phase, eg.duty, ref flip, __makeref(modulation));

            //Now scale up to the 14-bit volume the other operators expect for backwards compatibility. The extra precision bits will go elsewhere
            //So that they can be mixed into other HQ operators by the chip's channel mixer.
            if (flip) env_vol = -env_vol;
            env_vol *= 0x2000;
            return (short)env_vol;

            // return oscillator.Generate(unchecked(phase >> Global.FRAC_PRECISION_BITS), duty, ref flip);
        }

        //Sets up the operator to act as an oscillator for FM output.
        public override void SetOscillatorType(Oscillator.oscTypes type)
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
                    //Set the operator's sample output function to work in the linear domain.
                    operatorOutputSample = OperatorType_Noise;
                    return;
                }

                case "Wave":
                {
                    if (eg.feedback>0) operatorOutputSample = ComputeWavetableFeedback; else operatorOutputSample = ComputeWavetable;
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
                #if GODOT
                Godot.GD.Print($"Oscillator {waveform_index} not implemented: {e.ToString()}");
                #else
                System.Diagnostics.Debug.Print($"Oscillator {waveform_index} not implemented: {e.ToSTring()}");
                #endif
            }
        }


        //=============Oscillator output types.  Either standard waveform (log domain), noise, or wavetable sample.=========================

//         public short ComputeWavetableFeedback(ushort modulation, ushort am_offset)
//         {
//             var avg = (fbBuf[0] + fbBuf[1]) >> (10 - eg.feedback);
//             var output = ComputeWavetable(unchecked((ushort)(avg+modulation)), am_offset);
//             fbBuf[1] = fbBuf[0];
//             fbBuf[0] = output;

//             return output;            
//         }
//         public short ComputeWavetable(ushort modulation, ushort am_offset)
//         {
//             ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);

//             //TODO:  Consider using this.CurrentTable to reduce fetch calls.
//             //  This would also allow a Linear intent to create morphed tables during Clock() at a rate we can specify as a separate envelope
//             var tbl = this.wavetable.GetTable(eg.wavetable_bank);  
//             // var samp = (short) oscillator.Generate(phase, eg.duty, ref flip, __makeref(tbl));
//             var samp = (short) Oscillator.Wave2(phase, ref flip, tbl);
   
//             // get the attenuation from the envelope generator as a 4.6 value, shifted up to 4.8
//             ushort env_attenuation = (ushort) (envelope_attenuation(am_offset) << 2);

//             // combine into a 5.8 value, then convert from attenuation to 13-bit linear volume
//             int result = Tables.attenuation_to_volume((ushort)(samp + env_attenuation));

//             return flip? (short)-result : (short)result;
 
//         }

//         //Noise generators produce asymmetrical data.  Values must be translated to/from the log domain.
//         public short OperatorType_Noise(ushort modulation, ushort am_offset)
//         {
//             ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);
//             var seedRef = __makeref(this.seed);
//             var samp = (short) oscillator.Generate(phase, eg.duty, ref flip, seedRef);
//             // seed = __refvalue(seedRef, int);
//             ushort env_attenuation = (ushort) (envelope_attenuation(am_offset) << 2);

//             const float SCALE = 1.0f / 8192;

//             ushort logScale = (ushort)(Tables.attenuation_to_volume((ushort)(env_attenuation)));

//             // var tl = 1 - (eg.tl*ONE_PER_THOU);
//             // short result = (short) (samp * (logScale * SCALE) * tl);
//             short result = (short) (samp * (logScale * SCALE) );

//             return result;
//         }

//         public short ComputeVolume(ushort modulation, ushort am_offset)
//         {
//             // start with the upper 10 bits of the phase value plus modulation
//             // the low 10 bits of this result represents a full 2*PI period over
//             // the full sin wave
//             ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);

//             // get the absolute value of the sin, as attenuation, as a 4.8 fixed point value
//             // ushort sin_attenuation = oscillator.Generate(phase, eg.duty, ref flip, __makeref(pg.hz));
//             ushort sin_attenuation = oscillator.Generate(phase, eg.duty, ref flip, __makeref(pg.increment));

//             // get the attenuation from the envelope generator as a 4.6 value, shifted up to 4.8
//             ushort env_attenuation = (ushort) (envelope_attenuation(am_offset) << 2);

//             // combine into a 5.8 value, then convert from attenuation to 13-bit linear volume
//             int result = Tables.attenuation_to_volume((ushort)(sin_attenuation + env_attenuation));

//             // result = (int)(result * (1 - (eg.tl*Global.ONE_PER_THOU)));  //Floating point conversion.... expensive?

//             // negate if in the negative part of the sin wave (sign bit gives 14 bits)
//             return flip? (short)-result : (short)result;
//         }


//         /// Summary:  Calculates the self feedback of the given input with the given modulation amount.
//         public short ComputeFeedback(ushort modulation, ushort am_offset)
//         {
//             var avg = (fbBuf[0] + fbBuf[1]) >> (10 - eg.feedback);
//             var output = ComputeVolume(unchecked((ushort)(avg+modulation)), am_offset);
//             fbBuf[1] = fbBuf[0];
//             fbBuf[0] = output;

//             return output;
//         }


// //////////////////// ENVELOPE /////////////////////////
        public long egAttenuation2 = (Envelope.L_MAX<<5) << EG_LEVEL_PRECISION;  //High res fixed point EG level counter, 16.16
        public new void EGClock(uint env_counter)
        {

            if (egStatus == EGStatus.INACTIVE) return;
            int target;  //The target attenuation value here is increased to 32768 (actually closer to 32736) for finer increments by shifting left 5.

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
                    target = eg.levels[(int)EGStatus.SUSTAINED];
                    //Skip the decay phase if we've already gone past the sustain level.
                    //Deals with situations where the decay is set to 0 but we want to move on.
                    // if ( ((egAttenuation >= target) && !eg.rising[(int)EGStatus.DECAY]) | (eg.rising[(int)EGStatus.DECAY] && (egAttenuation <= target)))
                    //     egStatus = EGStatus.SUSTAINED;
                    // else egStatus = EGStatus.DECAY;
                    egStatus = eg.rates[(int)EGStatus.DECAY] == Envelope.R_MAX? EGStatus.SUSTAINED : EGStatus.DECAY;

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
            byte rate = eg.rates[(byte) egStatus];

            // determine the increment based on the non-fractional part of env_counter
            uint increment = get_eg_increment(rate, egStatus);


            // attack is the only one that increases
            // if (egStatus == EGStatus.ATTACK || eg.rising[(int)egStatus])
            if (egStatus == EGStatus.ATTACK)
            {
                var amt = ((~egAttenuation * increment) >> EG_LEVEL_PRECISION) << 4;
                egAttenuation2 += amt;
                // egAttenuation2 = (uint) Math.Max(egAttenuation2-increment, 0);

            } else if (eg.rising[(int)egStatus]) {  //Decrement.
                egAttenuation2 = (uint) Math.Max(egAttenuation2-increment, 0);
            } else {  //Most envelope states simply increase the attenuation by the increment previously determined
                egAttenuation2 += increment;
            }

            // clamp the final attenuation.  TODO:  Consider if the value 32767 is necessary or if we can go full 16-bit unsigned
            const int MAX_ATTENUATION = 0x7FE0 << EG_LEVEL_PRECISION;
            if (egAttenuation2 >= MAX_ATTENUATION) 
                egAttenuation2 = MAX_ATTENUATION;

            egAttenuation = (ushort)(egAttenuation2 >> EG_LEVEL_PRECISION >> 5);
            if (egAttenuation >= 0x400)
                egAttenuation = 0x3FF;
        }

        static int BaseSecs(EGStatus status) => status switch 
        {  //outputs the number of seconds we expect the longest finite envelope state to take.  Used to calculate HQ EG increments.
            EGStatus.ATTACK => 30,
            EGStatus.DECAY => 240,
            EGStatus.SUSTAINED => 240,
            EGStatus.RELEASED => 240,
            _ => 240
        };

        public const int EG_LEVEL_PRECISION = 16;  //Fixed point decimal precision bits for the state of the EG attenuation status
        static uint get_eg_increment(int rate, EGStatus egStatus)
        {
            if (rate==0) return 0;
            //Retrieve the base seconds of the envelope state based on the rate and convert to number of samples..
            var base_samples = (BaseSecs(egStatus) / Math.Pow(1.2, rate-1)) * Global.MixRate/3;  //The 3 is for the number of clock cycles it takes to run the EGClock
            //Now get the increment as a reciprocal plus our number of fixed point decimal places.....
            base_samples = 1.0 / base_samples * 0x7FFF;  
            return (uint)Tools.ToFixedPoint((float)base_samples, EG_LEVEL_PRECISION);
            
        }

        //-------------------------------------------------
        //  envelope_attenuation - return the effective
        //  attenuation of the envelope
        //-------------------------------------------------

        int amBuf = 0;
        short Filter(int input)  //Filters input based on the status of our filter buffer.
        {
            const byte k = 3;  //Amount of lowpass
            var ou = amBuf >> k;
            amBuf = amBuf - ou + input;
            return (short)ou;
        }



    }


}
