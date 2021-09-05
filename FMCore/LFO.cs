using System;
using gdsFM;

namespace gdsFM 
{
    public class LFO : OpBase
    {
        public delegate int scaleFunc(int input = 0); //Used to process attenuation to volume scaling in PM
        public scaleFunc ScaleProcess;  //FIXME:  Is this necessary?  We could consolidate this into operatorOutputSample maybe.....


        byte cycle_counter;
        long delay_counter;

        const int divider_max_count=6; //How often the LFO clocks.  The clock counter counts up to this number before recalculating the increment.
        
        short lastClockedVolume = 0;
        public ushort lastAMVolume = 0;


        //User set values.
        public ushort duty=32767;
        int delay = 0;
        byte speed = 0;  //Used for serialization and reference when loading an LFO
        public bool invert;  //User-specified flipping of the waveform.
        public float pmd;  //User specified pitch modulation depth.
        short amd;  public short AMD {get => (short)(Envelope.L_MAX-amd); set => amd=(short)(Envelope.L_MAX-value);}  //User specified amplitude modulation depth.
        public bool osc_sync;  //Resets the phase of the LFO when NoteOn occurs.
        public bool delay_sync;  //Resets the phase of the LFO after the delay elapses.


        //Sets and returns the delay time in millisecs.
        public int Delay {get => (int)(delay / Global.MixRate * 1000); set => delay=(int)(value * Global.MixRate / 1000);}  
        public byte Speed{get => speed;  set=> SetSpeed(value);}


        public LFO()  {Init();}
        public LFO(byte speed)  {Init(speed);}

        void Init(byte speed=19)  //Speed of LFO defaults to ~1.25s
        {
            ScaleProcess=PMScaleLog;
            operatorOutputSample = OperatorType_LogOutput;
            SetSpeed(speed);
        }


        public void SetSpeed(byte speed)
        {
            pg = Increments.FromFreq(Tables.LFOSpeed[speed]);  
            pg.Recalc(); 
        }

        public void NoteOn()
        {
            delay_counter = 0;
            if (osc_sync) phase = 0;
        }

        public bool ClockOK {get=> cycle_counter == 0;} //Returns true if the clock event just fired last tick.
        public override void Clock()
        {
            if (delay_sync && delay_counter == delay)  phase = 0;  //Reset phase on delay ending

            phase += pg.increment;

            cycle_counter++;
            delay_counter++;


            if (cycle_counter == divider_max_count)
            {
                cycle_counter = 0;

                UpdateOscillator();

                //TODO:  Clock logic here updating increment of the input frequency (not the same as the LFO's frequency)
                //      Map the output of the LFO osc to the channel's hz range between half the input increment and double.
            }
        }
        public void UpdateOscillator()  //Updates the status of the oscillator output.
            { lastClockedVolume = (short)ScaleProcess(operatorOutputSample()); lastAMVolume = RequestAM();} 



 
        //From "Musical Applications of Microprocessors" by Hal Chamberlin, page 438:
        //Reference:  https://stackoverflow.com/questions/38918530/simple-low-pass-filter-in-fixed-point/38927630#38927630
        int lBuf;  //Low pass filter buffer
        short Filter(int input)  //Filters input based on the status of our filter buffer.
        {
            const byte k = 5;  //Amount of lowpass
            var ou = lBuf >> k;
            lBuf = lBuf - ou + input;
            return (short)ou;
        }

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

            //TODO:  Recalc increments of the input based on the current state of our oscillator.  Output is mapped to short values;
            //      Needs to be mapped to a multiplier, 0.5 to 2.0 depending on status of flip bit, then applied to input frequency.
            //      Figure out where to apply this so end users can still modify hz themselves and not clobber the LFO state.

            //Grab a sample volume from the oscillator, then grab the float from the float table.  This value can be 0 to 8192 (technically 8168 from exp table).
            int volume = lastClockedVolume; 
            var ratio = flip ^ invert?  Tables.vol2pitchUp[volume] : Tables.vol2pitchDown[volume];

            //Apply ratio to the input.
            input.lfoMult = Tools.Lerp(1.0f, ratio, pmd);
            input.Recalc();
            return true;
        }


        // ===========================================  Operator meta functions  =============================================================

        //Sets up the operator to act as an oscillator for FM output.
        public override void SetOscillatorType(Oscillator.waveFunc waveFunc)
        {
            oscillator.SetWaveform(waveFunc);
            switch(waveFunc.Method.Name)
            {
                case "Brown":
                case "White":
                case "Pink":
                case "Noise1":
                case "Noise2":
                case "Sine3":
                {
                    seed = 1;  //Reset the seed.
                    //Set the operator's sample output function to work in the linear domain.
                    operatorOutputSample = OperatorType_Noise;
                    ScaleProcess = PMScaleNoise;
                    return;
                }
            }

            operatorOutputSample = OperatorType_LogOutput;
            ScaleProcess = PMScaleLog;
        }

        int PMScaleLog(int input) { return Tables.attenuation_to_volume((ushort) input); }
        int PMScaleNoise(int input) 
        {
            var output=Tables.attenuation_to_volume((ushort) (input>>3));  //Scale the input from 0-8192 like a normal osc before feeding in.
            // var output=Tables.vol2attenuation[(ushort) input >>3];
            flip = !invert && Tools.BIT((short)input, 9).ToBool();  //Make the LFO more melodic with invert OFF.  Invert ON holds the state open.
            // flip = false;
            return (int)output;
        }

        public override void SetOscillatorType(byte waveform_index)
        {
            try{
                SetOscillatorType(Oscillator.waveFuncs[waveform_index]);
            } catch(IndexOutOfRangeException e) {
                System.Diagnostics.Debug.Print(String.Format("Waveform {0} not implemented: {1}", waveform_index, e.ToString()));
            }
        }


        //Noise generators produce asymmetrical data.  Values must be translated to/from the log domain.
        public short OperatorType_Noise(ushort modulation, ushort auxdata=0)
        {
            ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);
            var samp = (short) oscillator.Generate(phase, duty, ref flip, __makeref(this.seed));
            // ushort env_attenuation = (ushort) (envelope_attenuation() << 2);

            // const float SCALE = 1.0f / 8192;

            // ushort logScale = (ushort)(Tables.attenuation_to_volume(0)); //TODO:  Add AMS here?

            // short result = (short) (samp * (logScale * SCALE) );

            short result = (short) (samp >> 1);  //Half vol

            return result;
        }

        public short OperatorType_LogOutput(ushort modulation, ushort auxdata=0)   //LFO VERSION RETURNS ATTENUATION, NOT VOLUME  (up to L_MAX units)
        {
            // start with the upper 10 bits of the phase value plus modulation
            // the low 10 bits of this result represents a full 2*PI period over
            // the full sin wave
            ushort phase = (ushort)((this.phase >> Global.FRAC_PRECISION_BITS) + modulation);

            // get the absolute value of the sin, as attenuation, as a 4.8 fixed point value
            // ushort sin_attenuation = Tables.abs_sin_attenuation(phase);
            ushort sin_attenuation = oscillator.Generate(phase, duty, ref flip, __makeref(pg.hz));

            return (short)sin_attenuation;

            // // combine into a 5.8 value, then convert from attenuation to 13-bit linear volume
            // int result = Tables.attenuation_to_volume((ushort)(sin_attenuation ));  //TODO:  Add AMS here?

            // // negate if in the negative part of the sin wave (sign bit gives 14 bits)
            // return flip? (short)-result : (short)result;
        }

        

    }

}
