using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        protected new OscillatorHQ oscillator = new OscillatorHQ(OscillatorHQ.Sine);

        public OperatorHQ(){operatorOutputSample=ComputeVolume; intent=Intents.FM_HQ; }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)] 
            void ResetPhase() {if (eg.osc_sync) {phase=0; seed=Global.DEFAULT_SEED;};  phase += Increments.PhaseOffsetOf(in pg, eg.phase_offset);}

        public override void NoteOn()
        {
            base.NoteOn();
            egAttenuation2 = (Envelope.L_MAX<<5) << EG_LEVEL_PRECISION;
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



        //Sets up the operator to act as an oscillator for FM output.
        public override void SetOscillatorType(Oscillator.oscTypes type)
        {
            oscillator.CurrentWaveform = (OscillatorHQ.oscTypes)type;
            switch(type.ToString())
            {
                case "Brown":
                case "White":
                case "Pink":
                case "Noise1":
                case "Noise2":
                {   //Set the operator's sample output function to work in the linear domain.
                    operatorOutputSample = ComputeNoise;
                    return;
                }

                case "Wave":
                {
                    if (eg.feedback>0) operatorOutputSample = ComputeWavetableFeedback; 
                    else operatorOutputSample = ComputeWavetable;
                    return;
                }
            }

            //Feedback causes amplitude oscillation issues when applied to sounds, so we don't use this function if the feedback's off.
            if (eg.feedback>0) operatorOutputSample = ComputeFeedback; else operatorOutputSample = ComputeVolume;
        }

        //=============Oscillator output types.  Either standard waveform (log domain), noise, or wavetable sample.=========================

        public new short ComputeWavetableFeedback(ushort modulation, ushort am_offset)
        {
            var avg = (fbBuf[0] + fbBuf[1]) * Tables.fbRatio[eg.feedback];
            var output = ComputeWavetable(unchecked((ushort)(avg+modulation)), am_offset);
            fbBuf[1] = fbBuf[0];
            fbBuf[0] = output;

            return output;            
        }
        public new short ComputeWavetable(ushort modulation, ushort am_offset)
        {
            ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + (modulation<<5));

            //TODO:  Consider using this.CurrentTable to reduce fetch calls.
            //  This would also allow a Linear intent to create morphed tables during Clock() at a rate we can specify as a separate envelope
            var tbl = this.wavetable.GetTable(eg.wavetable_bank);  
            var samp = OscillatorHQ.Wave2(phase, ref flip, tbl);
   
            //First, determine the max volume the EG will allow us to go.  This should give us a float from 0-1.
            float env_vol = -Tables.short2float[ 32767 - Tables.attenuationHQ2vol[envelope_attenuation(am_offset)] ];


            //Now scale up to the 14-bit volume the other operators expect for backwards compatibility. The extra precision bits will go elsewhere
            //So that they can be mixed into other HQ operators by the chip's channel mixer.
            if (flip) env_vol = -env_vol;
            env_vol *= samp;
            env_vol *= 0x2000;
            return (short)env_vol;
 
        }

        public new short ComputeVolume(ushort modulation = 0, ushort am_offset = 0)
        {
            // ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);
            ushort phase = (ushort)( (this.phase >> (Global.FRAC_PRECISION_BITS)) + (modulation<<5) );
            
            // // SineHQ's table spits out up to 15 bit precision values, which we convert to attenuation values to add the EG and osc attenuation values together.
            // int result = Tables.vol2attenuationHQ[ Oscillator.SineHQ(phase, eg.duty, ref flip, __makeref(this.phase)) ];

            // //Add in the attenuation from the EG and TL here, then convert to volume and reduce to 14-bit to maintain compatibility with other operators.
            // result = Tables.attenuationHQ2vol[(int)Math.Min(ushort.MaxValue, (egAttenuation2>>EG_LEVEL_PRECISION) + result + (eg.tl<<5))] >> 2;
            // return flip? (short)-result : (short)result;


            //First, determine the max volume the EG will allow us to go.  This should give us a float from 0-1.
            float env_vol = -Tables.short2float[ 32767 - Tables.attenuationHQ2vol[envelope_attenuation(am_offset)] ];


            //Now, get the oscillator output, which should also be in float, and multiply together.
            // env_vol *= OscillatorHQ.Sine((ulong)phase, eg.duty, ref flip, __makeref(modulation));
            env_vol *= oscillator.Generate(phase, eg.duty, ref flip, __makeref(pg.increment));

            //Now scale up to the 14-bit volume the other operators expect for backwards compatibility. The extra precision bits will go elsewhere
            //So that they can be mixed into other HQ operators by the chip's channel mixer.
            if (flip) env_vol = -env_vol;
            env_vol *= 0x2000;  //8192
            return (short)env_vol;  //Lops off the decimal component

        }

        //Produces a value taking into account the LFO state and TL.
        protected new ushort envelope_attenuation(ushort am_offset)
        {
            long result = egAttenuation2 >> EG_LEVEL_PRECISION;
            
            am_offset = (ushort)Filter(am_offset);  //Filter result to prevent pops and clicks.  TODO:  Determine how slow this is

            // // add in total level
            result += eg.tl<<5;

            if (eg.ams > 0)  //Apply AMS attenuation
                result += LFO.ApplyAMS(am_offset, eg.ams) << 5;


            // clamp to max and return
            return (result <= ushort.MaxValue) ? (ushort)result : (ushort)0xFFFF;
        }



        /// Summary:  Calculates the self feedback of the given input with the given modulation amount.
        public new short ComputeFeedback(ushort modulation, ushort am_offset)
        {
            var avg = (fbBuf[0] + fbBuf[1]) * Tables.fbRatio[eg.feedback];
            var output = ComputeVolume(unchecked((ushort)(avg+modulation)), am_offset);
            fbBuf[1] = fbBuf[0];
            fbBuf[0] = output;

            return output;
        }

        //Noise generators produce asymmetrical data.  Values must be translated to/from the log domain.
        public short ComputeNoise(ushort modulation, ushort am_offset)
        {
            ushort phase = (ushort)( ((this.phase >> Global.FRAC_PRECISION_BITS)) + (modulation<<5) );
            var seedRef = __makeref(this.seed);
            var samp = (short) oscillator.Generate(phase, eg.duty, ref flip, seedRef);
            // seed = __refvalue(seedRef, int);
            float env_vol = -Tables.short2float[ 32767 - Tables.attenuationHQ2vol[envelope_attenuation(am_offset)] ];

            // ushort logScale = (ushort)(Tables.attenuation_to_volume((ushort)(env_attenuation)));

            short result = (short) (samp * (env_vol));

            return result;
        }


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
                    // egStatus = eg.rates[(int)EGStatus.DECAY] == Envelope.R_MAX? EGStatus.SUSTAINED : EGStatus.DECAY;
                    egStatus = EGStatus.DECAY;
                    return;

                    // target = eg.levels[(int)EGStatus.DECAY];
                } else {
                    env_hold_counter++; 
                    return;
                }
                // break;

            case EGStatus.DECAY:
                target = eg.levels[(int)EGStatus.DECAY];
                if ( ((egAttenuation >= target) && !eg.rising[(int)EGStatus.DECAY]) | (eg.rising[(int)EGStatus.DECAY] && (egAttenuation <= target)))  {
                    egStatus ++;
                    return;
                }
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
                // target = Envelope.L_MAX;  //Max attenuation until a different release level is supported (which may be never)
                target = eg.levels[(int)EGStatus.RELEASED];  //Max attenuation until a different release level is supported (which may be never)
                if ( ((egAttenuation >= target) && !eg.rising[(int)EGStatus.RELEASED]) | (eg.rising[(int)EGStatus.RELEASED] && (egAttenuation <= target)))  
                {
                    egStatus = EGStatus.INACTIVE;
                    return;
                }

                break;
            }

            // determine our rate value.  Extra precision is pulled from 8 bits of the aux_func value.
            float rate = eg.rates[(byte) egStatus] + ((eg.aux_func>>((byte)egStatus*8)) & 255) / (egStatus == EGStatus.RELEASED? 256.0f: 128.0f);
            // float rate = eg.rates[(byte) egStatus];

            // determine the increment based on the non-fractional part of env_counter
            uint increment = get_eg_increment(rate, egStatus);


            const int MAX_ATTENUATION = 0x7FE0 << EG_LEVEL_PRECISION;
            // attack is the only one that increases
            if (egStatus == EGStatus.ATTACK)
            {
                // var amt = ((~egAttenuation * increment) >> EG_LEVEL_PRECISION) << 8;

                //FIXME????? WEIRD AND HACKY ALTERNATIVE DONE TO MAKE TIME OUTPUTS MATCH NORMAL OPERATOR
                var amt = (~egAttenuation2 * increment) >> 28;
                // amt = (long)(amt * 0.43349223970429271698464778124127);
                amt = (long)(amt * 0.65);
                
                egAttenuation2 = Tools.Clamp(egAttenuation2+amt, 0, MAX_ATTENUATION);
                
                

            } else if (eg.rising[(int)egStatus]) {  //Decrement.
                egAttenuation2 = (uint) Math.Max(egAttenuation2-increment, 0);
            } else {  //Most envelope states simply increase the attenuation by the increment previously determined
                egAttenuation2 += increment;

                if (egAttenuation2 >= MAX_ATTENUATION) 
                    egAttenuation2 = MAX_ATTENUATION;
            }

            // clamp the final attenuation.  TODO:  Consider if the value 32767 is necessary or if we can go full 16-bit unsigned
            egAttenuation = (ushort)(egAttenuation2 >> EG_LEVEL_PRECISION >> 5);
            if (egAttenuation >= 0x400)
                egAttenuation = 0x3FF;

        }

        

        //Consider the following:  Case switches are converted at compile time to constant hash jump tables.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static double BaseSecs(EGStatus status) => status switch {
            //outputs the number of seconds we expect the longest finite envelope state to take.  Used to calculate HQ EG increments.
            EGStatus.ATTACK => 18.56, //Tested on PhaseEngine
            EGStatus.DECAY => 260,
            EGStatus.SUSTAINED => 261.9, //Tested on PhaseEngine
            EGStatus.RELEASED => 260,
            _ => 240
        };
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static double Flatness(EGStatus status) => status switch {
            //outputs the number of seconds we expect the longest finite envelope state to take.  Used to calculate HQ EG increments.
            EGStatus.ATTACK => 1, 
            EGStatus.DECAY => 0.26,
            EGStatus.SUSTAINED => 0.268,
            EGStatus.RELEASED => 0.26,
            _ => 1
        };

        public static readonly double[] _ARs = new double[] { //AR lengths, in seconds, from normal PhaseEngine operators, converted to increments.
            240.0, 18.56, 9.28, 6.207, 4.64, 3.103, 2.319, 1.551, 
            1.159, 0.775, 0.579, 0.387, 0.289, 0.194, 0.144, 0.0965,
            3479/48000.0, 2326/48000.0, 1735/48000.0, 1162/48000.0, 864/48000.0, 580/48000.0, 438/48000.0, 290/48000.0, 
            220/48000.0, 153/48000.0, 124/48000.0, 84/48000.0, 66/48000.0, 55/48000.0, 48/48000.0, 42/48000.0,
            24/48000.0, 1/48000.0 //Value 32-33 is safety for lerps
        };

        static uint ToIncrement(double secs) => (uint)Tools.ToFixedPoint((float)(1.0/(secs * (Global.MixRate/3.0)) * 0x7FFF), EG_LEVEL_PRECISION);

        public const int EG_LEVEL_PRECISION = 16;  //Fixed point decimal precision bits for the state of the EG attenuation status
        //FIXME:  GENERATE THESE CURVES IN TABLES.cs AND RETREIVE THEM THAT WAY BASED ON CURRENT EGSTATUS RATE
        static uint get_eg_increment(float rate, EGStatus egStatus)
        {
            if (rate==0) return 0;
            // double rate = adsr_rate;
            //Retrieve the base seconds of the envelope state based on the rate and convert to number of samples..
            // var base_samples = (BaseSecs(egStatus) / Math.Pow(1.2, rate-1)) * Global.MixRate/3;  //The 3 is for the number of clock cycles it takes to run the EGClock

            switch(egStatus)
            {
                case EGStatus.ATTACK:
                    Debug.Assert(rate>=0);  //We're not going to get the absolute value of rate in order to split it into whole/frac
                    var n = rate/2.0;
                    var whole = (int)Math.Truncate(n);
                    var frac = n - whole;
                    var increment = ToIncrement(Tools.Lerp(_ARs[whole], _ARs[whole+1], frac));
                    return increment;
                default:
                    //Dividing by 3 is for the number of clock cycles it takes to run the EGClock
                    var base_samples = BaseSecs(egStatus) / Math.Pow(2, Flatness(egStatus) * (rate-1)) * Global.MixRate/3;

                    //Now get the increment as a reciprocal plus our number of fixed point decimal places.....
                    base_samples = 1.0 / base_samples * 0x7FFF;  
                    return (uint)Tools.ToFixedPoint((float)base_samples, EG_LEVEL_PRECISION);
            }            
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
