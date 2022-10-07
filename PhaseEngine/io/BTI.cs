using System;
using PhaseEngine;
using System.IO;
using System.Collections.Generic;
using PE_Json;

namespace PhaseEngine
{
    public class ImportBTI : VoiceBankImporter
    {
        enum OpNames {M1=0, C1=1, M2=2, C2=3}
        enum InstrumentType {FM, SSG, ADPCM, Drumkit, UNKNOWN=0xFF};
        enum InstProps {FMEnvelope, LFO, ALSequence, FBSequence, 
                        FMArpeggioSequence=0x28,FMPitchSequence, FMPanningSequence,
                        SSGWaveformSequence=0x30, SSGToneSequence, SSGEnvelopeSequence, SSGArpeggioSequence, SSGPitchSequence,
                        ADPCMSample=0x40, ADPCMEnvelopeSequence,ADPCMArpeggioSequence, ADPCMPitchSequence, ADPCMPanningSequence
         }
        public ImportBTI(){fileFormat="bti"; description="BambooTracker Instrument";}


        //Ratios used to convert an OPN detune value to a PhaseEngine one.
        readonly static float[] dt1_ratios = { 0, 0.25f, 0.667f, 1.0f, 0, -0.25f, -0.667f, -1.0f };

        const double NATIVE_HZ_RATE = 55466.0;
        static double ClockMult = NATIVE_HZ_RATE/Global.MixRate * Global.ClockMult;  //Ratio needed to translate one OPNA clock to a PhaseEngine clock at current mixrate

        //The below values are used to calculate ratios to translate values from the chips' defaults to PhaseEngine's level of precision.
        const int TL_MAX = 127;
        const int DL_MAX= 15;
        const int AR_MAX= 31;
        const int DR_MAX= 31;
        const int SR_MAX= 31;
        const int RR_MAX= 15;


        // const float RATIO_TL = (Envelope.L_MAX) / (float)(TL_MAX+0);  //127<<3 = 1016; 1016/128 = ratio
        const float RATIO_TL = 9.1f; 
        // const float RATIO_DL = (Envelope.L_MAX) / (float)(DL_MAX+0);
        const float RATIO_DL = RATIO_TL * 8.0f;


        // const float RATIO_AR = (Envelope.R_MAX+1) / (float)(AR_MAX+1);
        // const float RATIO_DR = Envelope.R_MAX / (float)DR_MAX;
        // const float RATIO_SR = Envelope.R_MAX / (float)SR_MAX;
        static double RATIO_RR = (Envelope.R_MAX+1) / (float)(RR_MAX+1) * ClockMult;

        static double RATIO_AR = ClockMult;
        static double RATIO_DR = ClockMult;
        static double RATIO_SR = ClockMult;
        // const float RATIO_RR = 1.0f;


        public override IOErrorFlags Load(string path)
        {
            //Update the clock multiplier, in case sample rate changed....
            ClockMult = NATIVE_HZ_RATE/Global.MixRate * Global.ClockMult;

            IOErrorFlags err = IOErrorFlags.OK;
            try
            {
                var file = new FileInfo(path);
                if (file.Length > 1024*1024)  throw new PE_ImportException(IOErrorFlags.TooLarge);  //BTI shouldn't be over 1mb, don't try to load it
                using (BinaryReader br = new BinaryReader(file.OpenRead()))
                {
                    
                    //Check header for validity.
                    var header = System.Text.Encoding.ASCII.GetString(br.ReadBytes(16));
                    var eof = br.ReadUInt32();
                    var version = br.ReadUInt32();
                    if (header != "BambooTrackerIst") { throw new PE_ImportException(IOErrorFlags.UnrecognizedFormat, "Not a recognized BTI!"); }

                    //BTI files contain one voice per file.  Initialize bank.
                    bank = new string[1];



                    //Prepare to process instrument.
                    // var egs = new Envelope[4]; egs.InitArray();  //Envelope used to generate partial JSON.
                    // var pgs = new Increments[4]; for(int i=0; i<pgs.Length; i++) pgs[i] = Increments.Prototype();
                    var v = new Voice(4);

                    header = System.Text.Encoding.ASCII.GetString(br.ReadBytes(8));  //Read the instrument section header.
                    if (header != "INSTRMNT") { throw new PE_ImportException(IOErrorFlags.UnrecognizedFormat, $"Expected INSTRMNT header, received {header} instead"); }
                    var toEndOfSubheader = br.BaseStream.Position + br.ReadUInt32();  //Relative offset to end of instrument subheader section.
                    var nameLen = br.ReadUInt32();
                    if (nameLen>0) v.name = System.Text.Encoding.ASCII.GetString(br.ReadBytes((int)nameLen));

                    // Read Instrument section properties
                    // InstrumentType instrumentType = (InstrumentType)br.ReadByte();
                    // if (instrumentType != InstrumentType.FM)  //The only instrument type we support for FM import right now is FM. Throw an error if it's any other type.
                    //     throw new PE_ImportException(IOErrorFlags.Unsupported, 
                    //             $"Only FM Instruments are supported at this time. Received {instrumentType.ToString()} instead");

                    // //If we reached this far, we should be at the start of the FM block.
                    // //The next set of bytes represent sequence data for the MML interpreter. 
                    // // We don't need these unless we want to convert them to bind envelopes in the future, so skip them for now.
                    // br.ReadBytes(11);

                    //Begin reading properties
                    //First, skip the instrument section property subheader entirely.  Comment this out and uncomment the above if we need it later.
                    br.BaseStream.Position = toEndOfSubheader;

                    header = System.Text.Encoding.ASCII.GetString(br.ReadBytes(8));  //Read the instrument properties section header.
                    if (header != "INSTPROP") { throw new PE_ImportException(IOErrorFlags.UnrecognizedFormat, $"Expected INSTPROP header, received {header} instead"); }
                    var endPos = br.BaseStream.Position + br.ReadUInt32();  //End of instrument property section.

                    // for(int i=0; i<bank.Length; i++)
                    while(br.BaseStream.Position < endPos)
                    {
                        // byte offset=0;
                        switch((InstProps) br.ReadByte())  //Get the subsection identifier.
                        {
                            case InstProps.FMEnvelope:
                                uint offset = br.ReadByte();
                                var algorithm = br.ReadByte();  //Packed into the high nibble is the algorithm. First let's get the feedback in the low nibble.
                                var feedback = (byte)(algorithm & 0xF);  algorithm >>= 4;
                                v.alg =  Algorithm.FromPreset(algorithm, Algorithm.PresetType.OPN);
                                v.egs[0].feedback = feedback;

                                for (int i=0; i<4; i++)  //Get each operator's properties.
                                {
                                    var e = v.egs[i];
                                    var buf = br.ReadByte();  //First byte read packs AR in low nibble and enable flag in bit 5.
                                    e.ar = (byte)((buf & 0x1F) * RATIO_AR);  //Get first 5 bits (AR up to 32)
                                    if (Tools.BIT(buf, 5) == 0)  v.egs[i].mute = true;

                                    buf = br.ReadByte();  //KS/DR.  Bits 0-4 are DR.  Bits 5 and 6 are KSR.
                                    e.dr = (byte)((buf & 0x1F) * RATIO_DR);  //Get first 5 bits (DR up to 32)
                                    //Assign envelope RateTable to a default preset and scale the max application.
                                    e.ksr = new RateTable();  e.ksr.ceiling = (float)(Convert.ToUInt16(buf>>4) * 25 / ClockMult); //FIXME:  Check accuracy

                                    buf = br.ReadByte();  //DT/SR.  Bits 0-4 are SR.  Bits 5-7 are detune.
                                    e.sr = (byte)((buf & 0x1F) * RATIO_SR);  //Get first 5 bits (SR up to 32)
                                    v.pgs[i].Detune = dt1_ratios[ (buf>>4) & 0x7 ];  //DT1 lookup

                                    buf = br.ReadByte();  //SL/RR.  Low nibble:  Release, High nibble:  Sustain rate
                                    e.rr = (byte)(((buf & 0xF)<<1) * RATIO_RR);  buf >>= 4;
                                    e.dl = (byte)((buf & 0xF) * RATIO_DL);
                                    e.sl = e.sr>0? Envelope.L_MAX: e.dl;

                                    e.tl = (ushort)(br.ReadByte() << 3);  //Total level

                                    buf = br.ReadByte();  //SSGEG/ML.  Low nibble:  Mult; High nibble:  SSGEG type (0x8 if disabled)
                                    v.pgs[i].mult = buf & 0xF;  //Read only the Mult.  We don't use SSG for anything right now.
                                }
                                break;
                            case InstProps.LFO:
                                offset = br.ReadByte();
                                //TODO:  Implement proper PMS lookup table to reflect correct pitch adjustments
                                br.BaseStream.Position += offset -1;  //Skip to next block
                                break;
                            case InstProps.ADPCMSample:
                                offset = br.ReadUInt32();
                                br.BaseStream.Position += offset -4;  //Skip to next block
                                break;
                            default:   //Probably a Sequence type
                                offset = br.ReadUInt16();
                                br.BaseStream.Position += offset -2;  //Skip to next block
                               break;
                        }
                    }  //EOF
                    bank[0] = v.ToJSONString();
                }  //Close file handle                
            }
            catch (FileNotFoundException) { err |= IOErrorFlags.NotFound; }
            catch (PE_ImportException e) { err |= e.flags; System.Diagnostics.Debug.Print(e.Message); }
            // catch //Anything else
            // { err |= IOErrorFlags.Failed | IOErrorFlags.Corrupt; }

            return err;
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }
}
