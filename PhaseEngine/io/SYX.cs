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
                            v.name = sysex.voices[i].name.Trim();
                            v.SetPresetAlgorithm(sysex.voices[i].algorithm);
                            
                            //// LFO ////
                            v.lfo.AMD = (short) Map(p.lfoAMD, Envelope.L_MAX);
                            v.lfo.Delay = p.lfoDelay;  //FIXME: Produce a delay table for DX7 and map it to our engine
                            v.lfo.SetSpeed(p.lfoSpeed); //FIXME
                            v.lfo.SyncType = p.LFOKeySync? 2: 0; //Sync to end of delay period
                            v.lfo.pmd = Tools.Remap(p.lfoPMD, 0, 99, 0, 1.0f) * PMS_MAP[p.LFO_PMS];

                            if (p.LFOWaveform == LFOWaves.SawDown) v.lfo.invert = true;
                            v.lfo.SetOscillatorType(oscTypes[p.LFOWaveform]);


                            //// Operators ////
                            for (int j=0; j<p.ops.Length; j++)
                            {
                                var idx = presetMap[p.algorithm][j]; //Target operator index on the PhaseEngine side
                                var op = p.ops[j];
                                var eg = v.egs[idx];
                                var pg = Increments.Prototype();

                                //// Envelope ////
                                eg.osc_sync = p.OscKeySync; //DX7 osc sync is global, but ours is per-operator
                                eg.ams = (byte)(op.AMS << 1); //DX7 AMS is 0-3. Map it to values closer to ours
                                if(feedbackOperatorForPreset[p.algorithm] == j)  eg.feedback=p.Feedback;
                                
                                //Levels
                                eg.tl = LvMap(op.outputLevel);
                                eg.al = LvMap(op.EG_L1);
                                eg.dl = LvMap(op.EG_L2);
                                eg.sl = LvMap(op.EG_L3);
                                eg.rl = LvMap(op.EG_L4);
                                //Rates
                                eg.ar = (byte)Map(op.EG_R1, 32);
                                eg.dr = (byte)Map(op.EG_R2, 32);
                                eg.sr = (byte)Map(op.EG_R3, 32);
                                eg.rr = (byte)Map(op.EG_R4, Envelope.R_MAX);

                                //rTables
                                eg.velocity = new VelocityTable(); eg.velocity.ceiling = Tools.Remap(op.VelocitySensitivity, 0,7,0,100);
                                //TODO:  RATE CURVE SCALING

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
                                    // FIXME:  op.CoarseFrequency goes up to 32; check if we should change our mult vals
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

                                v.pgs[idx].Configure(pg);                                
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
        public static ushort LvMap(int input) => (ushort)Tools.Remap(input, 0, 99, Envelope.L_MAX, 0);
        
        public static int Map(int input, int outMax) => (int)Math.Round(Tools.Remap(input, 0, 99, 0, outMax));

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
                err = "Did not find bulk size 4096";
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
        const int NOTE_C3 = 0x27; //39. This is the default breakpoint for DX7. Notes go down to A-0; We need to translate breakpoints to MIDI note numbers.
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
            public static PackedVoice Prototype() 
            {
                var o= new PackedVoice();
                o.ops = new PackedOperator[6];
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
            public byte algorithm;       //110    0   0 |        ALG        | ALGORITHM     0-31
            public byte KEYSYNC_FB;      //111    0   0   0 |OKS|    FB     | OSC KEY SYNC  0-1    FEEDBACK      0-7

            readonly public bool OscKeySync {get => ((KEYSYNC_FB>>3) & 1) == 1; }
            readonly public byte Feedback {get => (byte)(KEYSYNC_FB & 0x7); }

            public byte lfoSpeed;
            public byte lfoDelay;
            public byte lfoPMD;
            public byte lfoAMD;
            public byte lfoPackedOpts;   //116  |  LPMS |      LFW      |LKS| LF PT MOD SNS 0-7   WAVE 0-5,  SYNC 0-1

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
