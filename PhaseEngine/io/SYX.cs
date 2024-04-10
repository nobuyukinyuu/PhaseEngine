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
                            Voice v = new Voice();
                            v.name = sysex.voices[i].name.Trim();
                            v.alg = Algorithm.FromPreset(sysex.voices[i].algorithm, Algorithm.PresetType.DX);
                            bank[i] = v.ToJSONString();

                            //TODO:  IMPORT AND CONVERT EVERY OTHER PROPERTY
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


        public override string ToString()
        {
            return base.ToString();
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
                return IOErrorFlags.UnrecognizedFormat;
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
            readonly public int AMS  { get=> VEL_AMS & 0x3; }


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
