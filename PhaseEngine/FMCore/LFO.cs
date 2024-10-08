using System;
using PhaseEngine;
using PE_Json;

namespace PhaseEngine 
{
    public class LFO : OpBase
    {
        public delegate int scaleFunc(int input = 0); //Used to process attenuation to volume scaling in PM

        const int divider_max_count=4; //How often the LFO clocks.  The clock counter counts up to this number before recalculating the increment.
        byte cycle_counter;
        long delay_counter;  //Clocks up from sync point to determine when to start LFO

        
        short lastClockedVolume = 0;
        public ushort lastAMVolume = 0;


        //User set values.
        public ushort duty=32767;
        int delay = 0; int knee = 0;  //How long before starting to employ and fully employ LFO.
        byte speed = 0;  //Used for serialization and reference when loading an LFO
        public bool invert;  //User-specified flipping of the waveform.
        public float pmd;  //User specified pitch modulation depth.
        short amd;  public short AMD {get => (short)(Envelope.L_MAX-amd); set => amd=(short)(Envelope.L_MAX-value);}  //User specified amplitude modulation depth.
        public bool osc_sync;  //Resets the phase of the LFO when NoteOn occurs.
        public bool delay_sync;  //Resets the phase of the LFO after the delay elapses.
        public bool release_sync;  //Resets the phase of the LFO after NoteOff.
        public byte wavetable_bank;

        //Sets and returns the delay time in millisecs.
        public int Delay {get => (int)(delay / Global.MixRate * 1000); set  {delay=(int)(value * Global.MixRate / 1000); 
                if(!speedType.HasFlag(SpeedTypes.ManualKnee))  SetKnee(value);} }  
        public int Knee {get => (int)(knee / Global.MixRate * 1000); set => knee=(int)(value * Global.MixRate / 1000);}
        public byte Speed{get => speed;  set=> SetSpeed(value);}
        public double Frequency{get => pg.BaseFrequency;  set=> SetFrequency(value);}


        //Used to specify how frequency and knee is described during deserialization.
        [Flags] public enum SpeedTypes {Preset, FromFrequency, ManualKnee, ManualKneeAndFrequency}
        public SpeedTypes speedType = SpeedTypes.Preset;

        //Used to specify how the oscillator syncs to the last key pressed for serialization.
        internal int SyncType { get=> release_sync? 3: delay_sync? 2: osc_sync? 1:0; 
                set{ {osc_sync=value>0;  delay_sync=value>1;  release_sync=value>2;}} }


        // public LFO()  {Init();}
        public LFO(byte speed=19) => Init(speed);
        public LFO(double frequency) => Init(frequency);

        void Init(byte speed=19)  //Speed of LFO defaults to ~1.25s
        {
            intent = Intents.LFO;
            operatorOutputSample = OperatorType_LogOutput;
            SetSpeed(speed);
        }
        void Init(double frequency)
        {
            intent = Intents.LFO;
            operatorOutputSample = OperatorType_LogOutput;
            SetFrequency(frequency);
        }

        public void SetFrequency(double hz) { //Manually sets the frequency from a given Hz rate.
            pg = Increments.FromFreq(hz);
            pg.Recalc(); 
        }
        public void SetKnee(float delayMillisecs) { //Automatically sets the knee based on the delay value.
            var input = 34.2471 * Math.Log(5.407685402494024 * (0.123569 + delayMillisecs / 1000.0));
            Knee = (int)(Math.Min(0.020562 + 0.0280046 * Math.Pow(1.04673, input-0.5), 5) * 1000);
        }
        public void SetSpeed(byte speed) //Automatically sets the frequency based off the LFO speed table.
        {
            this.speed = speed;
            pg = Increments.FromFreq(Tables.LFOSpeed[speed]);  
            pg.Recalc(); 
        }

        public override void NoteOn()
        {
            egStatus = EGStatus.HOLD;
            delay_counter = 0;
            if (osc_sync) phase = 0;
        }
        public override void NoteOff()
        {
            egStatus = EGStatus.RELEASED;
             if (release_sync) {phase=0; delay_counter=0;} 
        }


        public bool ClockOK {get=> cycle_counter == 0;} //Returns true if the clock event just fired last tick.
        public override void Clock()
        {

            phase += pg.increment;
            cycle_counter++;

            if (delay_sync && delay_counter == delay)  
            {  //The following routine sets up the buffer to realign the LFO in the middle of the phase to mitigate pops and clicks.
               //TODO:  Consider leaving this up to the osc_sync setting.
                phase = -256 << Global.FRAC_PRECISION_BITS;  //Reset phase on delay ending
                cycle_counter = 0;
                lastClockedVolume = operatorOutputSample();
                lBuf = -lastClockedVolume << 3;
                lastAMVolume = RequestAM();
            }

            if(!release_sync || egStatus==EGStatus.RELEASED)  
                delay_counter++;


            if (cycle_counter == divider_max_count)
            {
                cycle_counter = 0;
                UpdateOscillator();
            }
        }
        public void UpdateOscillator()  //Updates the status of the oscillator output.
            { lastClockedVolume = operatorOutputSample(); lastAMVolume = RequestAM();} 


 
        //From "Musical Applications of Microprocessors" by Hal Chamberlin, page 438:
        //Reference:  https://stackoverflow.com/questions/38918530/simple-low-pass-filter-in-fixed-point/38927630#38927630
        int lBuf;  //Low pass filter buffer
        short Filter(int input)  //Filters input based on the status of our filter buffer.
        {
            const byte k = 3;  //Amount of lowpass
            var ou = lBuf >> k;
            lBuf = lBuf - ou + input;
            return (short)ou;
        }

        public override short RequestSample(ushort input, ushort am_offset){return (short)RequestAM();} //Not currently used. Channels will never call this
        public ushort RequestAM()  //Returns an attenuation value from 0-TL.
        {
            if (amd==Envelope.L_MAX) return 0;
            if (delay_counter < delay) return 0;
            if (cycle_counter !=0) return lastAMVolume;
 
            short output = lastClockedVolume;  //Up to 0x1FE8 (8168), volume output
            if (flip^invert) output = (short)-output;
            
            output = Filter(output);  //Apply lowpass filter to the output to mitigate pops and clicks.  

            // var ratio = AMD/1023.0f;
            // short pushupValue = (short)(0x1FFF*(ratio));


            if (delay_counter < delay + knee) //FIXME
            {
                var amt = (delay_counter-delay)/(float)knee;
                output = (short)Tools.Lerp(-0x1FE8, output, amt);
                // output = (short)(output*amt);
            }

            var ratio = Tables.amdScaleRatio[amd];
            short pushupValue = Tables.amdPushupRatio[amd];

            output = (short)(output*ratio);  //Grab the 0-1 value from the reciprocal table and apply it to the output to scale it down.
            output += pushupValue;  //Waveform must always be above 0. Scale the result up to be between 0-0x3FFF.

                
            output >>= 4;  //Scale down to 0-1023.

            // output = (short)Tools.Clamp(output, 0, Envelope.TL_MAX);  //TODO:  Figure out if this can be made more efficient

            return (ushort) output; 
        }

        //Takes input from LFO and scales it.  Output never exceeds a 10-bit value;  at max AMS the attenuation is not culled. Lower AMS values cull output more.
        public static ushort ApplyAMS (ushort input, byte ams) { return (ushort)(input >> (10-ams)); }


        public bool ApplyPM(ref Increments input)
        {
            if (pmd==0) return false;
            if (cycle_counter!=0) return false;  //Don't bother recalculating the pitch if we're not clock-ready.  Lowers recalc cost.
            if (delay_counter < delay) return false;

            var knee_vol = 1.0f;
            if (delay_counter < delay + knee) 
                knee_vol = (delay_counter-delay)/(float)knee ;

            //Grab a sample volume from the oscillator, then grab the float from the float table.  This value can be 0 to 8192 (technically 8168 from exp table).
            int volume = (int)(lastClockedVolume * pmd * knee_vol); 
            var ratio = flip ^ invert?  Tables.vol2pitchUp[volume] : Tables.vol2pitchDown[volume];

            //Apply ratio to the input.
            input.lfoMult = ratio;
            input.Recalc();
            return true;
        }


        // ===========================================  Operator meta functions  =============================================================

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
                    seed = Global.DEFAULT_SEED;  //Reset the seed.
                    //Set the operator's sample output function to work in the linear domain.
                    operatorOutputSample = OperatorType_Noise;
                    return;
                }
                case "Wave":
                {
                    operatorOutputSample = ComputeWavetable;
                    return;
                }
            }

            operatorOutputSample = OperatorType_LogOutput;
        }

        int PMScaleLog(int input) => Tables.attenuation_to_volume((ushort) input);
        int PMScaleNoise(int input) 
        {
            var output=Tables.attenuation_to_volume((ushort) (input>>3));  //Scale the input from 0-8192 like a normal osc before feeding in.
            // var output=Tables.vol2attenuation[(ushort) input >>3];
            flip = !invert && Tools.BIT((short)input, 9).ToBool();  //Make the LFO more melodic with invert OFF.  Invert ON holds the state open.
            return (int)output;
        }

        public override void SetOscillatorType(byte waveform_index)
        {
            try{
                SetOscillatorType( (Oscillator.oscTypes)waveform_index );
            } catch(IndexOutOfRangeException e) {
                System.Diagnostics.Debug.Print($"Waveform {waveform_index} not implemented: {e}");
            }
        }


        public short ComputeWavetable(ushort modulation, ushort am_offset)
        {
            ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);
            //TODO:  Consider using this.CurrentTable to reduce fetch calls.
            //  This would also allow a Linear intent to create morphed tables during Clock() at a rate we can specify as a separate envelope
            var tbl = this.wavetable.GetTable(wavetable_bank);  
            var samp = (short) Oscillator.Wave2(phase, ref flip, tbl);
   
            // combine into a 5.8 value, then convert from attenuation to 13-bit linear volume
            var result = Tables.attenuation_to_volume((ushort)samp);

            return (short)result;
        }

        //Noise generators produce asymmetrical data.  Values must be translated to/from the log domain.
        public short OperatorType_Noise2(ushort modulation, ushort auxdata=0)
        {
            ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);
            var samp = (short) oscillator.Generate(phase, duty, ref flip, __makeref(this.seed));
            // ushort env_attenuation = (ushort) (envelope_attenuation() << 2);

            // short result = (short) (samp >> 1);  //Half vol

            var result=Tables.attenuation_to_volume((ushort) (samp>>3));  //Scale the input from 0-8192 like a normal osc before feeding in.
            // var output=Tables.vol2attenuation[(ushort) input >>3];
            flip = !invert && Tools.BIT((short)samp, 9).ToBool();  //Make the LFO more melodic with invert OFF.  Invert ON holds the state open.

            return (short)result;
        }

        public short OperatorType_Noise(ushort modulation, ushort auxdata=0)
        {
            ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);
            var seedRef = __makeref(this.seed);
            var samp = oscillator.Generate(phase, duty, ref flip, seedRef);

            short result = (short)(samp>>3);
                        
            flip = !invert && Tools.BIT((short)samp, 9).ToBool();  //Make the LFO more melodic with invert OFF.  Invert ON holds the state open.

            return result;
        }

        public short OperatorType_LogOutput(ushort modulation, ushort auxdata=0)
        {
            // start with the upper 10 bits of the phase value plus modulation
            // the low 10 bits of this result represents a full 2*PI period over
            // the full sin wave
            ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);

            // get the absolute value of the sin, as attenuation, as a 4.8 fixed point value
            ushort sin_attenuation = oscillator.Generate(phase, duty, ref flip, __makeref(pg.increment));

            // // combine into a 5.8 value, then convert from attenuation to 13-bit linear volume
            ushort result = Tables.attenuation_to_volume((ushort)sin_attenuation);

            // // negate if in the negative part of the sin wave (sign bit gives 14 bits)
            // return flip? (short)-result : (short)result;
            return (short)result;  //We don't need to flip the result because this is handled in the respective ApplyPM / ApplyAM?
        }

#region io /////////////////////////////
        public string ToJSONString() => ToJSONObject().ToJSONString();
        public JSONObject ToJSONObject()
        {
            var o = new JSONObject();

            o.AddPrim("oscillator", oscillator.CurrentWaveform);
           
            if (duty!=32767) o.AddPrim("duty", duty);
            if (wavetable_bank>0) o.AddPrim("wavetable_bank", wavetable_bank);
            o.AddPrim("delay", Delay);  //Delay is tied to MixRate so we should get the independent value.

            if (speedType.HasFlag(SpeedTypes.FromFrequency))  o.AddPrim("freq", pg.BaseFrequency);
            else o.AddPrim("speed", Speed);
           
            if (speedType.HasFlag(SpeedTypes.ManualKnee)) //Manual knee specified.
                    o.AddPrim("knee", Knee);  //Knee is tied to MixRate so we should get the independent value.


            o.AddPrim("invert", invert);
            o.AddPrim("pmd", pmd);
            o.AddPrim("amd", AMD);  //Amp depth is specified in terms easier to use a LUT with.  Grab the user-friendly value instead.
            o.AddPrim("syncType", SyncType);  //Condense sync types into one since delay_sync implies osc_sync
            // o.AddPrim("", );

            return o;
        }
        public void FromJSON(JSONObject j)
        {
            Oscillator.oscTypes osc = Oscillator.oscTypes.Sine;
            if (j.Assign("oscillator", ref osc))  SetOscillatorType(osc);
            else System.Diagnostics.Debug.Fail("LFO.FromJSON: Can't parse osc");
           
            if (j.HasItem("duty")) j.Assign("duty", ref duty); else duty=32767;
            wavetable_bank = (byte) j.GetItem("wavetable_bank", 0); //Reset if the tag doesn't exist so a channel doesn't reuse a previous value.

            Delay = j.GetItem("delay", Delay);  //Convert delay and speed values to reflect our mix rate.

            speedType = SpeedTypes.Preset; //Default to speed preset. We'll update this as we pull info from the JSON.
            if (j.HasItem("speed"))  Speed = (byte) j.GetItem("speed", Speed);
            if (j.HasItem("freq"))  
            {
                speedType |= SpeedTypes.FromFrequency;
                Frequency = j.GetItem("freq", (float)Frequency);
            }

            if (j.HasItem("knee")) {
                speedType |= SpeedTypes.ManualKnee;
                Knee = j.GetItem("knee", Knee);
            } else { //Set the knee automatically based on the delay value.
                SetKnee(Delay);
            }


            j.Assign("invert", ref invert);
            j.Assign("pmd", ref pmd);

            AMD = (short) j.GetItem("amd", AMD);
            SyncType = j.GetItem("syncType", SyncType);
        }


    }
#endregion

}
