using System;
using System.Collections.Generic;
using PhaseEngine;

namespace PhaseEngine 
{
    
    public abstract class OpBase : IBindableDataConsumer  //Base for operator, LFO and filter classes
    {
        public enum Intents { LFO=-1, NONE, FM_OP, FM_HQ, FILTER, BITWISE, WAVEFOLDER, LINEAR };
        public Intents intent = Intents.NONE;


        protected Oscillator oscillator = new Oscillator(Oscillator.Sine2);
        public Oscillator.oscTypes OscType{ get => oscillator.CurrentWaveform; }
        public SortedList<string, CachedEnvelope> BindStates { get; set; } = new SortedList<string, CachedEnvelope>();

        protected delegate short SampleOutputFunc(ushort modulation = 0, ushort am_offset=0); //Primary function of the oscillator
        protected SampleOutputFunc operatorOutputSample;


        protected long phase;  //Phase accumulator
        protected bool flip=false;  // Used by the oscillator to flip the waveform's values.  TODO:  User-specified waveform inversion
        protected int seed = Global.DEFAULT_SEED;  //LFSR state sent ByRef to oscillators which produce noise

        readonly public Envelope eg = new Envelope();  //Singleton for the lifetime of the operator. Use Envelope.configure() to replicate from a prototype
        public EGStatus egStatus = EGStatus.INACTIVE;
        public ushort egAttenuation = Envelope.L_MAX;  //5-bit value
        public Increments pg = Increments.Prototype();
 
        public WaveTableData wavetable;
        public PE_Json.IJSONSerializable auxdata;  //Used by future operators to store extra data such as extended feedback, etc. Not covered by eg.aux_func
        //TODO:  Add a func to be implemented by derivative classes validating whether their copy of auxdata is the correct derived type

        // public abstract void SetOscillatorType(Oscillator.waveFunc waveFunc);
        public abstract void SetOscillatorType(byte waveform_index);
        // public abstract short RequestSample(ushort modulation = 0);
        public abstract void Clock();

        public abstract short RequestSample(ushort input, ushort am_offset);
        public virtual void NoteOn() => IBindableDataConsumer.NoteOn(this);
        public virtual void NoteOff() => IBindableDataConsumer.NoteOff(this);
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

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)] 
            void ResetPhase() {if (eg.osc_sync) {phase=0; seed=Global.DEFAULT_SEED;};  phase += Increments.PhaseOffsetOf(in pg, eg.phase_offset);}

        public void NoteOn(Increments increments){ pg = increments;  NoteOn(); }
        public override void NoteOn()
        {
            base.NoteOn();
            ResetPhase();
            
            env_counter = 0;
            env_hold_counter = 0;

            egAttenuation = Envelope.L_MAX;
            egStatus = EGStatus.DELAY;
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

        public override short RequestSample(ushort modulation = 0, ushort am_offset = 0) => operatorOutputSample(modulation, am_offset);


        //Sets up the operator to act as an oscillator for FM output.
        public virtual void SetOscillatorType(Oscillator.oscTypes type)
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

        public short ComputeWavetableFeedback(ushort modulation, ushort am_offset)
        {
            var avg = (fbBuf[0] + fbBuf[1]) >> (10 - eg.feedback);
            var output = ComputeWavetable(unchecked((ushort)(avg+modulation)), am_offset);
            fbBuf[1] = fbBuf[0];
            fbBuf[0] = output;

            return output;            
        }
        public short ComputeWavetable(ushort modulation, ushort am_offset)
        {
            ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + (modulation<<5));

            //TODO:  Consider using this.CurrentTable to reduce fetch calls.
            //  This would also allow a Linear intent to create morphed tables during Clock() at a rate we can specify as a separate envelope
            var tbl = this.wavetable.GetTable(eg.wavetable_bank);  
            // var samp = (short) oscillator.Generate(phase, eg.duty, ref flip, __makeref(tbl));
            var samp = (short) Oscillator.Wave2(phase, ref flip, tbl);
   
            // get the attenuation from the envelope generator as a 4.6 value, shifted up to 4.8
            ushort env_attenuation = (ushort) (envelope_attenuation(am_offset) << 2);

            // combine into a 5.8 value, then convert from attenuation to 13-bit linear volume
            int result = Tables.attenuation_to_volume((ushort)(samp + env_attenuation));

            return flip? (short)-result : (short)result;
 
        }

        //Noise generators produce asymmetrical data.  Values must be translated to/from the log domain.
        public short OperatorType_Noise(ushort modulation, ushort am_offset)
        {
            ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);
            var seedRef = __makeref(this.seed);
            var samp = (short) oscillator.Generate(phase, eg.duty, ref flip, seedRef);
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
            ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + (modulation<<5));
            // ushort phase = (ushort)((this.phase + (modulation<<6) >> Global.FRAC_PRECISION_BITS);

            // get the absolute value of the sin, as attenuation, as a 4.8 fixed point value
            // ushort sin_attenuation = oscillator.Generate(phase, eg.duty, ref flip, __makeref(pg.hz));
            ushort sin_attenuation = oscillator.Generate(phase, eg.duty, ref flip, __makeref(pg.increment));

            // get the attenuation from the envelope generator as a 4.6 value, shifted up to 4.8
            ushort env_attenuation = (ushort) (envelope_attenuation(am_offset) << 2);

            // combine into a 5.8 value, then convert from attenuation to 13-bit linear volume
            int result = Tables.attenuation_to_volume((ushort)(sin_attenuation + env_attenuation));

            // result = (int)(result * (1 - (eg.tl*Global.ONE_PER_THOU)));  //Floating point conversion.... expensive?

            // negate if in the negative part of the sin wave (sign bit gives 14 bits)
            return flip? (short)-result : (short)result;
        }


        /// Summary:  Calculates the self feedback of the given input with the given modulation amount.
        public short ComputeFeedback(ushort modulation, ushort am_offset)
        {
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
                    target = eg.levels[(int)EGStatus.SUSTAINED];
                    //Skip the decay phase if we've already gone past the sustain level.
                    //Deals with situations where the decay is set to 0 but we want to move on.
                    // if ( ((egAttenuation >= target) && !eg.rising[(int)EGStatus.DECAY]) | (eg.rising[(int)EGStatus.DECAY] && (egAttenuation <= target)))
                    //     egStatus = EGStatus.SUSTAINED;
                    // else egStatus = EGStatus.DECAY;
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
            // byte increment = Tables.attenuation_increment(rate, (byte) Tools.BIT(env_counter, 11, 3));
            byte increment = Tables.attenuation_increment(rate, (byte)Tools.BIT(env_counter, (rate_shift <= 11) ? (byte)11 : rate_shift, 3));


            // attack is the only one that increases
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

        int amBuf = 0;
        short Filter(int input)  //Filters input based on the status of our filter buffer.
        {
            const byte k = 3;  //Amount of lowpass
            var ou = amBuf >> k;
            amBuf = amBuf - ou + input;
            return (short)ou;
        }

        protected ushort envelope_attenuation(ushort am_offset)
        {
            ushort result = egAttenuation;
            am_offset = (ushort)Filter(am_offset);  //Filter result to prevent pops and clicks.  TODO:  Determine how slow this is

            // // add in total level
            result += eg.tl;

            if (eg.ams > 0)  //Apply AMS attenuation
                result += LFO.ApplyAMS(am_offset, eg.ams);


            // clamp to max and return
            return (result < 0x400) ? result : (ushort)0x3ff;
        }


    }

    public class BitwiseOperator : Operator
    {
        public delegate short OpFunc(short modulation, short oscOutput); //Function of the operator.
        OpFunc BitwiseOp;
        public static readonly OpFunc[] operations = {OP_AND, OP_OR, OP_XOR, OP_RINGMOD, OP_MOD, OP_INVMOD, OP_ROL, OP_ROR};
        public byte OpFuncType {get => (byte)eg.aux_func;  set { if (value<operations.Length)  {BitwiseOp = operations[value]; eg.aux_func=value;} }}

        public BitwiseOperator():base() {intent=Intents.BITWISE; BitwiseOp=OP_AND;}

        public override short RequestSample(ushort modulation = 0, ushort am_offset = 0)
        {
            return BitwiseOp(operatorOutputSample(0, am_offset), (short)modulation);  //Modulation sent to us is the sample value of previous operator.
        }

        //Bitwise Funcs.   TODO:  Implement ROR/ROL?  
        public static short OP_AND(short modulation, short input) {return (short)(input & modulation);}
        public static short OP_OR(short modulation, short input) {return (short)(input | modulation);}
        public static short OP_XOR(short modulation, short input) {return (short)(input ^ modulation);}

        public static short OP_RINGMOD(short modulation, short input) {return (short)(input * modulation >> 13);}


#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
        public static short OP_MOD(short modulation, short input) => (short)(input % (modulation|(modulation==0).ToByte()));
        public static short OP_INVMOD(short modulation, short input) {
            var operand = Tools.Sign(modulation) * (0x1FFF - Tools.Abs(modulation));
            return (short)(input % (operand|(operand==0).ToByte()));}
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand

        public static short OP_ROR(short modulation, short input) {return (short)Rotate(input, modulation, ROR16);}
        public static short OP_ROL(short modulation, short input) {return (short)Rotate(modulation, input, ROL16);}
 
        static int ROR16(int x, int amt) => (x >> amt | (x << (16 - amt))) & 0xFFFF;
        static int ROL16(int x, int amt) => (x << amt | (x >> (16 - amt))) & 0xFFFF;
        static short Rotate(int input, int modulation, Func<int,int,int> ROTF)
        {

            const byte WINDOW = 4;  //The number of bits to evaluate in a chunk.
            const ushort WINDOW_MASK = (1<<WINDOW) -1; //A bitmask of all 1s the width of the window
            const ushort MSB = 12;  //The most significant amount of bits to apply the operation to. All bits above this remain untouched.

            var output=0;
            for (short i=0; i<MSB; i+=WINDOW) //Split the modulation into bite sized chunks to determine how much to rotate the carrier input.
            {
                var amt = (modulation>>i) & WINDOW_MASK;  //Rotation value 0 to WINDOW_MASK
                var val = ROTF(input, (amt + i)%16 ) & WINDOW_MASK;  //rotate the input window n bits we're interested in
                output |= val<<i;  //Apply rotated window to the output mask.
            }

            //Knock out the LSBs from the input and replace them with the output mask.
            input &= ~((1<<MSB)-1);
            output |= input; 

            return (short)(output);
        }

    }

}
