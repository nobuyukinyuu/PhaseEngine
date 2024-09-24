using System;
using PhaseEngine;
using System.IO;
using System.Collections.Generic;
using PE_Json;

namespace PhaseEngine
{
    public class ImportDX7Sysex : VoiceBankImporter
    {
        public ImportDX7Sysex(){fileFormat="syx"; description="DX7 Sysex";}

        static readonly Dictionary<LFOWaves, Oscillator.oscTypes> oscTypes = new Dictionary<LFOWaves, Oscillator.oscTypes>
            {
                {LFOWaves.Triangle, Oscillator.oscTypes.Triangle},
                {LFOWaves.Sine, Oscillator.oscTypes.Sine},
                {LFOWaves.SawDown, Oscillator.oscTypes.Saw},
                {LFOWaves.SawUp, Oscillator.oscTypes.Saw},
                {LFOWaves.Square, Oscillator.oscTypes.Pulse},
                {LFOWaves.SAndHold, Oscillator.oscTypes.Noise2},
            };
        static readonly byte[] feedbackOperatorForPreset = new byte[] // 0-indexed 
        {// Which operator for a given dx7 algorithm takes the feedback value? Multi-op loops consider the bottom of the stack.
            5,1,5,3, 5,4,5,3,
            1,2,5,1, 5,5,1,5,
            1,2,5,2, 2,5,5,5,
            5,5,2,4, 5,4,5,5,
        };

        static readonly byte[][] presetMap = new byte[][] 
        {   //Operator preset map to arrange DX7 algorithms to fit PhaseEngine 6op preset order. Index=dx7op, value=pe_op
            Algo(5,3,6,4,2,1), Algo(5,3,6,4,2,1), Algo(5,3,1,6,4,2), Algo(5,3,1,6,4,2), //0-3
            Algo(4,1,5,2,6,3), Algo(4,1,5,2,6,3), Algo(5,2,6,3,4,1), Algo(5,2,6,3,4,1), //4-7
            Algo(5,2,6,3,4,1), Algo(6,4,1,5,2,3), Algo(6,4,1,5,2,3), Algo(6,4,5,1,2,3), //8-11
            Algo(6,4,5,1,2,3), Algo(5,3,6,4,1,2), Algo(5,3,6,4,1,2), Algo(6,3,4,1,5,2), //12-15
            Algo(6,3,4,1,5,2), Algo(6,3,4,5,2,1), Algo(4,2,1,5,6,3), Algo(4,5,1,6,2,3), //16-19
            Algo(3,4,1,5,6,2), Algo(3,1,4,5,6,2), Algo(3,4,1,5,6,2), Algo(2,3,4,5,6,1), //20-23
            Algo(2,3,4,5,6,1), Algo(4,5,1,6,3,2), Algo(4,5,1,6,3,2), Algo(4,2,5,3,1,6), //24-27
            Algo(3,4,5,2,6,1), Algo(3,4,5,2,1,6), Algo(2,3,4,5,6,1), Algo(1,2,3,4,5,6), //28-31
        }; 
        static byte[] Algo(params byte[] input) { //Takes a 1-indexed array of bytes and makes it a 0-indexed array 
            for(int i=0; i<input.Length; i++)
                input[i] -=1;
            return input;
        }


        public static readonly float[] PMS_MAP = new float[]{0, 0.0264f, 0.0534f, 0.0889f, 0.1612f, 0.2769f, 0.4967f, 1}; 
        const ushort FILE_SIZE = 4104;  //Sysex size, in bytes
        public override IOErrorFlags Load(string path)
        {
            IOErrorFlags err = IOErrorFlags.OK;
            try
            {
                var file = new FileInfo(path);

                using (var stream = File.OpenRead(path))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        if (file.Length > FILE_SIZE)    //SYX shouldn't be over 4k, don't try to load it
                            throw new PE_ImportException(IOErrorFlags.TooLarge, $"File specified not correct size! (Expecting {FILE_SIZE}, got {stream.Length})");
                        var sysex = Parse(reader);

                        //Parsed successfully.  Validate data.
                        string errorMessage;
                        err |= Validate(sysex, out errorMessage);
                        if (err != IOErrorFlags.OK) throw new PE_ImportException(err, errorMessage);  //Failed validation

                        //Validation passed.  Begin import banks.
                        bank = new string[32];
                        for (int i=0; i<32; i++)
                        {
                            //// General ////
                            var p = sysex.voices[i];
                            Voice v = new Voice(6);
                            v.name = sysex.voices[i].name/*.Trim()*/;
                            v.SetPresetAlgorithm(sysex.voices[i].algorithm);

                            v.Gain = 2/3f * (7 - v.alg.NumberOfConnectionsToOutput) + 1/3f; //Volume compensation
                            
                            //// LFO ////
                            v.lfo.AMD = (short) Map(p.lfoAMD, Envelope.L_MAX);

                            //Based on lfo.cc from music-synthesizer-for-android; ©2013 Google Inc. Apache License 2.0.
                            double lfoRate(int rate){ //Get the oscillator length in seconds from a DX7 rate [0..99]
                                int sr = rate == 0 ? 1 : (165 * rate) >> 6;
                                sr *= sr < 160 ? 11 : (11 + ((sr - 160) >> 4));
                                return 170.5/(double)sr;
                            }

                            if (p.lfoDelay > 0)
                            {
                                v.lfo.Knee = (int) (0.020562 + 0.0280046 * Math.Pow(1.04673, p.lfoDelay) * 1000); //Regression curve fit of DX7 knee value
                                v.lfo.Delay = (int) (-0.124569 + 0.184922 * Math.Pow(1.02963, p.lfoDelay) * 1000) - v.lfo.Knee; //Fit of DX7 delay value
                            }
                            v.lfo.Frequency = 1.0 / lfoRate(p.lfoSpeed);
                            v.lfo.speedType = LFO.SpeedTypes.ManualKneeAndFrequency;

                            v.lfo.SyncType = p.LFOKeySync? 2: 0; //Sync to end of delay period if DX LFO Sync is enabled.
                            v.lfo.pmd = Tools.Remap(p.lfoPMD, 0, 99, 0, 1.0f) * PMS_MAP[p.LFO_PMS];

                            if (p.LFOWaveform == LFOWaves.SawUp) v.lfo.invert = true;
                            v.lfo.SetOscillatorType(oscTypes[p.LFOWaveform]);


                            //// Operators ////
                            for (int j=0; j<p.ops.Length; j++)
                            {
                                var pe_opIndex = presetMap[p.algorithm][j]; //Target operator index on the PhaseEngine side
                                v.SetIntent(pe_opIndex, OpBase.Intents.FM_HQ);  //We need the higher fidelity for EG rate emulation
                                var op = p.ops[j];
                                var eg = v.egs[pe_opIndex];
                                var pg = Increments.Prototype();

                                //// Envelope ////
                                eg.osc_sync = p.OscKeySync; //DX7 osc sync is global, but ours is per-operator
                                eg.ams = (byte)(op.AMS * 2); //DX7 AMS is 0-3. Map it to values closer to ours. (For Dexed, *3, for dx7, *2)
                                //Feedback is set outside of this loop after the intents are set.  See below.
                            
                                
                                //Levels
                                eg.tl = LvMap(op.outputLevel);
                                eg.al = LvMap(op.EG_L1);
                                eg.dl = LvMap(op.EG_L2);
                                eg.sl = LvMap(op.EG_L3);
                                eg.rl = LvMap(op.EG_L4, Envelope.L_MAX);  //Adjust on full scale to prevent stuck notes

                                // float Ease(float t) {return t;} //Linear
                                // float Ease(float t) {var sq=t*t; return sq / (2.0f * (sq - t) + 1.0f);}  //Parametric
                                // float Ease(float t) {return t * t * (3.0f - 2.0f * t);}  //Cubic Bezier
                                // float Ease(float t) {return t < 0.5f? 2.0f * t*t : -1.0f + (4.0f - 2.0f * t) * t;} //Quadratic
                                float Ease(float t) {return (float)Math.Pow(t, 1.3); } //Custom

                                //Rates
                                v.SetRateExtension(pe_opIndex, "ar", Tools.Remap(Ease(op.EG_R1/99.0f), 0, 1, 0, 31));
                                v.SetRateExtension(pe_opIndex, "dr", Map(op.EG_R2, 32));
                                v.SetRateExtension(pe_opIndex, "sr", Map(op.EG_R3, 32));
                                v.SetRateExtension(pe_opIndex, "rr", Map(op.EG_R4, Envelope.R_MAX));

                                //rTables
                                eg.velocity = new VelocityTable(); eg.velocity.ceiling = Tools.Remap(op.VelocitySensitivity, 0,7,0,100);
                                eg.ksr = new RateTable(); eg.ksr.ceiling = Tools.Remap(op.RateScale, 0,7,0, 100); //FIXME: Check for accuracy
                                eg.ksl = new LevelTable();
                               
                                //Since rTables can only ADD to attenuation, to deal with positive level scaling values, we have to determine
                                //The largest value we'd have to scale up a note by and adjust the TL accordingly. Usually this will mean a TL
                                // will become 0 and the KSL table scaled to account for the difference.  DX7 only scales in key groups of
                                // 3 semitones each, but we'll interpolate these values based on the curve type to try to enhance the sound.
                                const double LN_RATIO = 256/12.0; //256 units of PhaseEngine attenuation == -24dB per octave. 
                                const double EX_RATIO = 1024/96.0; //Corresponds to a 50% volume decrease (-6dB) every 2 octaves on our log2 scale.
                                const byte LN_MAX = 48;  //Number of notes until the linear attenuator maxes out its ability to attenuate. 
                                const byte EX_MAX = 79;  //Number of notes until the exp attenuator maxes out its ability to attenuate. (maybe 72?)
                                static ushort lnScale(int x) => (ushort)Math.Round(x*LN_RATIO);
                                static ushort exScale(int x) => (ushort)Math.Min((Math.Pow(2, (x+10)/12)-1) * EX_RATIO, Envelope.L_MAX); //Rough fit to measurements

                                int UnscaledValue(CurveScaleType curve, int pos) => curve switch {
                                    CurveScaleType.LinMinus => lnScale(pos),
                                    CurveScaleType.ExpMinus => exScale(pos),
                                    CurveScaleType.LinPlus => -lnScale(pos),
                                    CurveScaleType.ExpPlus => -exScale(pos),
                                    _ => throw new Exception($"SYX.cs:  Unknown CurveScaleType! Op{j+1}: Right side, Voice {i}: {v.name})"),
                                };
                                static ushort CurveMax(CurveScaleType curve) => curve switch {
                                    CurveScaleType.LinMinus =>  LN_MAX,     CurveScaleType.LinPlus =>  LN_MAX,
                                    CurveScaleType.ExpMinus => EX_MAX,      CurveScaleType.ExpPlus => EX_MAX,
                                _ => 0 };

                                const byte TRANSPOSE = 48 - NOTE_C3; //Amount needed to add to sysex note to create the equivalent midi_note.
                                var note_num = op.levelScalingBreakPoint + TRANSPOSE; //The highest DX7 can go is C-8 (99). So the higest we go is 108.

                                bool commonScale = op.scaleLeftDepth == op.scaleRightDepth;  //We can skip the lerp step if the scales are the same.
                                if (commonScale) eg.ksl.ceiling = (float)Math.Round(op.scaleLeftDepth * 1.01);
                                else  eg.ksl.ceiling = 100;

                                //If we encounter Plus curves (adds volume, subtracts attenuation), to convert to rTables we must lift the entire curve
                                //by a specific amount. That amount is the lowest attenuation value achieved by the curve. Then, we subtract the lift
                                //from the operator's total attenuation level (TL).
                                int lowest=0, highest=0;  //Indices of the highest and lowest value found.
                                double[] intermediate = new double[128]; //Create Intermediate values to transform later.
                                for(int c_idx=0; c_idx<128; c_idx++)
                                {
                                    var dist = Math.Abs(c_idx-note_num); //Distance from the breakpoint note.
                                    double val = 0;
                                    if (c_idx<note_num) //Left Curve
                                    {
                                        val = UnscaledValue(op.CurveScaleLeft, dist);
                                        if (!commonScale) //Scale the value.
                                            val *= op.scaleLeftDepth / 99.0;

                                    } else {            //Right Curve
                                       val = UnscaledValue(op.CurveScaleRight, dist);
                                        if (!commonScale) //Scale the value.
                                            val *= op.scaleRightDepth / 99.0;
                                    }

                                    intermediate[c_idx] = (int)Math.Truncate(val);
                                    if(intermediate[c_idx] > intermediate[highest])     highest = c_idx;
                                    if(intermediate[c_idx] < intermediate[lowest])      lowest  = c_idx;
                                }

                                //Now that we have the highest and lowest values, determine if we need to lift, and have room for the full lift amount.
                                //If we don't need to lift and the scaling value is common, then we can use rTables' built-in scaling. Otherwise, we have
                                //we have to get as close to possible as matching the values around the breakpoint note by using TL as our lift value.
                                var lift = 0;


                                // // FIXME:  BROKEN IMPLEMENTATION relies on prescaled values. Un-prescaled this lift won't work.
                                // //         Consider premultiplying lift by 1/100 the ceiling amount.
                                if(intermediate[lowest]<0 && eg.ksl.ceiling == 100)
                                {
                                    lift = (intermediate[highest] - intermediate[lowest] > eg.tl)?  eg.tl : (int)-intermediate[lowest];
                                    eg.tl = (ushort) Math.Clamp(eg.tl - lift, 0, Envelope.L_MAX);
                                }
                                    for(int c_idx=0; c_idx<128; c_idx++) 
                                        eg.ksl[c_idx] = (ushort)Math.Clamp(intermediate[c_idx] + lift, 0, Envelope.L_MAX);


                                //// Increments ////
                                if (op.FrequencyMode == OscModes.Fixed)
                                {
                                    pg.fixedFreq = true;

                                    //Determine frequency from OP values. The multiplier from coarse determines 1-1000hz, repeating
                                    var freq = Math.Pow(10, op.CoarseFrequency % 4);
                                    //The fine freq was calculated using a model regression from an emulator, may not be accurate...
                                    freq *= Math.Pow(2, op.FineFrequency * 0.0332193);

                                    pg.FreqSelect(freq);
                                } else {  //Ratio mode
                                    pg.mult = op.CoarseFrequency==0? 0.5f : op.CoarseFrequency ; 

                                    // FineFrequency is a 0-99 value multiplier from 1x-2x of our mult.
                                    // We should probably set mult to an integer value, then determine
                                    // the closest coarse/fine freq to the integral and fractional representations
                                    // the dx7 fine frequency maps to. So eg. dx7 FINE 99 = coarse +12, fine -1.

                                    var trans = GetTranspose(op.FineFrequency); //Returns a value from 0-1299.
                                    pg.coarse = trans / 100; //0-12
                                    pg.fine = trans % 100; //0-99
                                }
                                pg.Detune = Tools.Remap(op.Detune, -7, 7, -1.0f, 1.0f);

                                v.pgs[pe_opIndex].Configure(pg);                                
                            } //End Operator loop
                            
                            //// Feedback ////
                            var fb = p.Feedback * (v.alg.intent[feedbackOperatorForPreset[p.algorithm]]==OpBase.Intents.FM_HQ? 25.5 : 1.0);
                            switch(sysex.voices[i].algorithm){ //Algorithms 4 and 6 have special feedback stacks which we'll handle here.
                                case 3:  //3-op stack, we'll split the feedback into 3rds and apply a bit to each operator.
                                    // eg.feedback = (byte)(fb / 3.0);
                                    v.egs[1].feedback = (byte)(fb / 3.0);
                                    v.egs[3].feedback = (byte)(fb / 3.0);
                                    v.egs[5].feedback = (byte)(fb / 3.0);
                                    break;
                                case 5: //2-op stack with feedback, we'll split the feedback in half and apply it to each operator.
                                    v.egs[2].feedback = (byte)(fb / 2.0);
                                    v.egs[5].feedback = (byte)(fb / 2.0);
                                    break;
                                default:
                                    v.egs[presetMap[p.algorithm][feedbackOperatorForPreset[p.algorithm]]].feedback = (byte)fb;
                                    break;
                            }                            


                            //TODO:  IMPORT AND CONVERT EVERY OTHER PROPERTY
                            bank[i] = v.ToJSONString();
                        }
                    }

                }
            }
            catch (FileNotFoundException) { err |= IOErrorFlags.NotFound; }
            catch (PE_ImportException e) { err |= e.flags; System.Diagnostics.Debug.Print(e.Message); importDetails = e.Message;}
            // catch //Anything else
            // { err |= IOErrorFlags.Failed | IOErrorFlags.Corrupt; }
            return err;
        }

        // public static ushort LvMap(int input) => (ushort)(((int)(Tools.Remap(input, 0, 99, Envelope.L_MAX, 0)) << 1) & Envelope.L_MAX);
        public static ushort LvMap(int input, int outmin=820) => (ushort)Tools.Remap(LvEase(input,1.0), 0, 99, outmin, 16);
        public static double LvEase(int dx_lvl, double amt) => Math.Pow(dx_lvl/100.0, amt)*100.0; //Eases the DX level curve slightly
        
        public static float Map(float input, float outMax) => Tools.Remap(input, 0, 99, 0, outMax);

        public override string ToString()
        {
            return base.ToString();
        }

   /////////////////////////////////////////////// GLUE /////////////////////////////////////////////// 
        private static Dictionary<int, int> transpose_cache = new Dictionary<int, int>(100);
        protected static int GetTranspose(int input, bool shortcircuit=true)
        { //Gets a dx7 fine value and returns the corresponding PhaseEngine coarse/fine transpose table index.
            if (transpose_cache.ContainsKey(input)) return transpose_cache[input];
            if(input < 0 || input > 99) 
                if(input==127) //This has been confirmed to happen in Dexed_01.syx.....
                    input=99;  //Fix it by clamping the value to 99
                else
                    throw new PE_ImportException(IOErrorFlags.Corrupt, $"Transpose value {input} out of 0-99 range");

            var target = input/100.0f + 1; //Convert to multiplier value 1.0-2.0, same as transpose table.
            var closest = -1;
            var minDifference = double.MaxValue;
            for(int i=0; i<Tables.transpose.Length; i++)
            {
                var element = Tables.transpose[i];
                var difference = Math.Abs(element - target);
                if (difference < minDifference)
                {
                    minDifference = difference;
                    closest = i;
                } 
                else if (shortcircuit)  break;
            }
            if (closest>=0)  transpose_cache[input] = closest;
            return closest;
        }

   //////////////////////////////////////////////// IO //////////////////////////////////////////////// 
   
        private static IOErrorFlags Validate(DX7Sysex sysex, out string err)
        {
            // Verify Header and Footer            
            if (sysex.sysexBegin != 0xF0)
            {
                err = "Did not find sysex start byte 0xF0..";   
                return IOErrorFlags.UnrecognizedFormat;
            }
            if (sysex.vendorID != 0x43)
            {
                err = $"Did not find Vendor ID.. Expected  Yamaha (0x43), got {sysex.vendorID}";
                return IOErrorFlags.Unsupported;
            }
            if (sysex.subStatusAndChannel != 0)
            {
                err = "Did not find substatus 0 and channel 1..";
                return IOErrorFlags.UnrecognizedFormat;
            }
            if (sysex.format != 0x09)
            {
                err = "Did not find format 9 (32 voices)..";
                return IOErrorFlags.Unsupported;  //TODO:  Consider supporting single voice format in the future!!
            }
            if (sysex.sizeMSB != 0x20  ||  sysex.sizeLSB != 0)
            {
                err = "Did not find bulk VMEM size 4096";
                // return IOErrorFlags.UnrecognizedFormat;
            }
            if (sysex.sysexEnd != 0xF7)
            {
                err = "Did not find sysex end byte 0xF7..";
                return IOErrorFlags.UnrecognizedFormat;
            }


            // **** checksum ****
            // Start of 4096 byte data block.
            byte sum = 0;
            var p= new byte[4096];
            p = sysex.rawdata;
            // for(int i=0; i<32; i++)
            // {
            //     Array.Copy(getBytes(sysex.voices[i]), 0, p, i*128, 128);
            // }

            for (int i=0; i<4096; i++)
            {
                sum += (byte)(p[i] & 0x7F);
            }
            // Two's complement: Flip the bits and add 1
            sum = (byte)((~sum) + 1);
            // Mask to 7 bits
            sum &= 0x7F;
            if (sum != sysex.checksum)
            {
                err = $"CHECKSUM FAILED: Produced {sum} from the raw data, but expected {sysex.checksum}";
                return IOErrorFlags.Corrupt;
            }
            
            err = "Success?";
            return IOErrorFlags.OK;

        }
        private static DX7Sysex Parse(BinaryReader reader)
        {
            var o = DX7Sysex.Prototype();
            o.sysexBegin = reader.ReadByte();
            o.vendorID = reader.ReadByte();
            o.subStatusAndChannel = reader.ReadByte();
            o.format = reader.ReadByte();
            o.sizeMSB = reader.ReadByte();
            o.sizeLSB = reader.ReadByte();

            //Do voices
            for(int v=0; v<o.voices.Length; v++)
            {
                PackedVoice voice = PackedVoice.Prototype();
                
                //Do operators.  Sysex format packs it starting with Op6(!) and going in reverse back to Op1.
                for(int i=0; i<voice.ops.Length; i++)
                {
                    var op= voice.ops[voice.ops.Length-i-1];
                    op.EG_R1 = reader.ReadByte();
                    op.EG_R2 = reader.ReadByte();
                    op.EG_R3 = reader.ReadByte();
                    op.EG_R4 = reader.ReadByte();

                    //Levels
                    op.EG_L1 = reader.ReadByte();
                    op.EG_L2 = reader.ReadByte();
                    op.EG_L3 = reader.ReadByte();
                    op.EG_L4 = reader.ReadByte();
                    op.levelScalingBreakPoint = reader.ReadByte();
                    op.scaleLeftDepth = reader.ReadByte();
                    op.scaleRightDepth = reader.ReadByte();

                    op.scaleCurve = reader.ReadByte();
                    op.DT_RS = reader.ReadByte();
                    op.VEL_AMS = reader.ReadByte();
                    op.outputLevel = reader.ReadByte();
                    
                    op.FC_M = reader.ReadByte();
                    op.frequencyFine = reader.ReadByte();

                    voice.ops[voice.ops.Length-i-1] = op;
                }

                voice.pitchEGR1 = reader.ReadByte();
                voice.pitchEGR2 = reader.ReadByte();
                voice.pitchEGR3 = reader.ReadByte();
                voice.pitchEGR4 = reader.ReadByte();
                voice.pitchEGL1 = reader.ReadByte();
                voice.pitchEGL2 = reader.ReadByte();
                voice.pitchEGL3 = reader.ReadByte();
                voice.pitchEGL4 = reader.ReadByte();
            
                voice.algorithm = reader.ReadByte();
                voice.KEYSYNC_FB = reader.ReadByte();

                voice.lfoSpeed = reader.ReadByte();
                voice.lfoDelay = reader.ReadByte();
                voice.lfoPMD = reader.ReadByte();
                voice.lfoAMD = reader.ReadByte();
                voice.lfoPackedOpts = reader.ReadByte();

                voice.transpose = reader.ReadByte();
                
                voice.name = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(10));

                o.voices[v] = voice;
            }
            
            o.checksum = reader.ReadByte();  // 0xF0
            o.sysexEnd = reader.ReadByte();  // 0xF7
            
            reader.BaseStream.Seek(6,SeekOrigin.Begin);
            o.rawdata = reader.ReadBytes(4096);
 
            return o;
        }




   //////////////////////////////////////////////// STRUCTS //////////////////////////////////////////////// 
        public const int NOTE_C3 = 0x27; //39. This is the default breakpoint for DX7. Notes go down to A-0; We need to translate breakpoints to MIDI note numbers.
        struct PackedOperator
        {
            //Rates
            public byte EG_R1;
            public byte EG_R2;
            public byte EG_R3;
            public byte EG_R4;

            //Levels
            public byte EG_L1;
            public byte EG_L2;
            public byte EG_L3;
            public byte EG_L4;
            public byte levelScalingBreakPoint;
            // public string LevelScalingBreakPoint {get=> Program.NoteName(levelScalingBreakPoint);}
            public byte scaleLeftDepth;
            public byte scaleRightDepth;

                                        //public byte             bit #
                                        // #     6   5   4   3   2   1   0   param A       range  param B       range
                                        //----  --- --- --- --- --- --- ---  ------------  -----  ------------  -----
            public byte scaleCurve;    // 11    0   0   0 |  RC   |   LC  | SCL LEFT CURVE 0-3   SCL RGHT CURVE 0-3
            public byte DT_RS;         // 12  |      DET      |     RS    | OSC DETUNE     0-14  OSC RATE SCALE 0-7
            public byte VEL_AMS;       // 13    0   0 |    KVS    |  AMS  | KEY VEL SENS   0-7   AMP MOD SENS   0-3
            public byte outputLevel;
            
            public byte FC_M;          // 15    0 |         FC        | M | FREQ COARSE    0-31  OSC MODE       0-1
            public byte frequencyFine;


            readonly public CurveScaleType CurveScaleLeft { get=> (CurveScaleType)(scaleCurve & 3); }
            readonly public CurveScaleType CurveScaleRight { get=> (CurveScaleType)((scaleCurve >> 2) & 3); }

            readonly public int Detune { get=> ((DT_RS>>3) & 0xF) -7; }
            readonly public int RateScale { get=> DT_RS & 0x7; }

            readonly public int VelocitySensitivity { get=> (VEL_AMS >> 2) & 0x7; }
            readonly public byte AMS  { get=> (byte)(VEL_AMS & 0x3); }


            readonly public OscModes FrequencyMode {get=> (OscModes)(FC_M & 1);}
            readonly public int CoarseFrequency {get=> (FC_M >> 1) & 31;}
            readonly public int FineFrequency {get=> frequencyFine;}
        }

        enum OscModes {Ratio, Fixed}
        enum CurveScaleType {LinMinus, ExpMinus, ExpPlus, LinPlus}
        
        struct PackedVoice
        {
            public static PackedVoice Prototype() //Defaults
            {
                var o= new PackedVoice();
                o.ops = new PackedOperator[6];

                // Defaults from https://usa.yamaha.com/files/download/other_assets/0/319440/plg150dx_1.pdf pg.54 (VCED format)
                o.name = "INIT VOICE";

                o.pitchEGR1 = 99;
                o.pitchEGR2 = 99;
                o.pitchEGR3 = 99;
                o.pitchEGR4 = 99;
                o.pitchEGL1 = 50;
                o.pitchEGL2 = 50;
                o.pitchEGL3 = 50;
                o.pitchEGL4 = 50;
                o.KEYSYNC_FB = 1 << 3;  //OPI=1, FB=0
                o.lfoSpeed = 0x23;
                o.lfoPackedOpts = (03 << 4) | 1;  //PMS=3, LFO KS=1

                for(int i=0; i<o.ops.Length; i++)
                {
                    o.ops[i].EG_R1=99;
                    o.ops[i].EG_R2=99;
                    o.ops[i].EG_R3=99;
                    o.ops[i].EG_R4=99;
                    o.ops[i].EG_L1=99;
                    o.ops[i].EG_L2=99;
                    o.ops[i].EG_L3=99;
                    o.ops[i].EG_L4=00;
                    o.ops[i].levelScalingBreakPoint=NOTE_C3;
                    o.ops[i].FC_M= 1 << 1;  //Coarse=01,  Mode=0 (Ratio)
                    o.ops[i].DT_RS = 7 << 3; //Detune=+0, RateScale=0
                }
                    o.ops[0].outputLevel=99;

                return o;
            }
            public PackedOperator[] ops;

            public string Name {get=> name;}

            public byte pitchEGR1;
            public byte pitchEGR2;
            public byte pitchEGR3;
            public byte pitchEGR4;
            public byte pitchEGL1;
            public byte pitchEGL2;
            public byte pitchEGL3;
            public byte pitchEGL4;
        

                                        //public byte             bit #
                                        // #     6   5   4   3   2   1   0   param A       range  param B       range
                                        //----  --- --- --- --- --- --- ---  ------------  -----  ------------  -----
            public byte algorithm;      //110    0   0 |        ALG        | ALGORITHM     0-31
            public byte KEYSYNC_FB;     //111    0   0   0 |OKS|    FB     | OSC KEY SYNC  0-1    FEEDBACK      0-7

            readonly public bool OscKeySync {get => ((KEYSYNC_FB>>3) & 1) == 1; }
            readonly public byte Feedback {get => (byte)(KEYSYNC_FB & 0x7); }

            public byte lfoSpeed;
            public byte lfoDelay;
            public byte lfoPMD;
            public byte lfoAMD;
            public byte lfoPackedOpts;   //116  |  LPMS |      LFW      |LKS| LFO PITCH MOD SENS 0-7,   WAVE 0-5,  SYNC 0-1

            readonly public bool LFOKeySync {get=> (lfoPackedOpts & 1) == 1;}
            readonly public LFOWaves LFOWaveform {get=>  (LFOWaves)((lfoPackedOpts >> 1) & 0x7);}
            readonly public int LFO_PMS {get=> (lfoPackedOpts >> 4);}

            public byte transpose;
            public string name;
        }

        enum LFOWaves {Triangle, SawDown, SawUp, Square, Sine, SAndHold}

        struct DX7Sysex
        {
            public byte sysexBegin;
            public byte vendorID;
            public byte subStatusAndChannel;
            public byte format;
            public byte sizeMSB;  // 7 bits
            public byte sizeLSB;  // 7 bits

            public PackedVoice[] voices;

            public byte checksum;
            public byte sysexEnd;  // 0xF7

            public byte[] rawdata;

            public static DX7Sysex Prototype()
            {
                var o = new DX7Sysex();
                o.sysexBegin = 0xF0;
                o.vendorID = 0x43;
                o.subStatusAndChannel=0;
                o.format=9;
                o.sizeMSB=0x20;
                o.sizeLSB=0x00;

                o.voices = new PackedVoice[32];

                o.checksum=0;
                o.sysexEnd=0xF7;  // 0xF7

                o.rawdata=new byte[4096];

                return o;
            }
        };


    }
}
