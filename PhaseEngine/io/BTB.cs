using System;
using PhaseEngine;
using System.IO;
using System.Collections.Generic;
using PE_Json;

namespace PhaseEngine
{
    public class ImportBTB : VoiceBankImporter
    {
        enum InstrumentType {FM, SSG, ADPCM, Drumkit, UNKNOWN=0xFF};
        enum InstProps {FMEnvelope, LFO, ALSequence, FBSequence, 
                        FMArpeggioSequence=0x28,FMPitchSequence, FMPanningSequence,
                        SSGWaveformSequence=0x30, SSGToneSequence, SSGEnvelopeSequence, SSGArpeggioSequence, SSGPitchSequence,
                        ADPCMSample=0x40, ADPCMEnvelopeSequence,ADPCMArpeggioSequence, ADPCMPitchSequence, ADPCMPanningSequence
         }
        public ImportBTB(){fileFormat="btb"; description="BambooTracker Bank";}

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
                if (file.Length > 1024*2048)  throw new PE_ImportException(IOErrorFlags.TooLarge);  //BTB shouldn't be over 2mb, don't try to load it
                using (BinaryReader br = new BinaryReader(file.OpenRead()))
                {
                    
                    //Check header for validity.
                    var header = System.Text.Encoding.ASCII.GetString(br.ReadBytes(16));
                    var eof = br.ReadUInt32();
                    var version = br.ReadUInt32();
                    if (header != "BambooTrackerBnk") { throw new PE_ImportException(IOErrorFlags.UnrecognizedFormat, "Not a recognized BTB!"); }


                    header = System.Text.Encoding.ASCII.GetString(br.ReadBytes(8));  //Read the instrument section header.
                    if (header != "INSTRMNT") { throw new PE_ImportException(IOErrorFlags.UnrecognizedFormat, $"Expected INSTRMNT header, received {header} instead"); }
                    var toEndOfSubheader = br.BaseStream.Position + br.ReadUInt32();  //Relative offset to end of instrument subheader section.

                    //Prepare bank.
                    var numInstruments = br.ReadByte();
                    var names = new Dictionary<byte, string>();  //Dictionary of (instrument number): (name, envelope number)
                    var envelopes = new Dictionary<byte, byte>();
                    Voice[] v = new Voice[256];  

                    for (int i=0; i < numInstruments; i++)
                    {
                        // Read Instrument section properties
                        var idx = br.ReadByte();
                        var toEndOfInstDetails = br.BaseStream.Position + br.ReadUInt32();
                        var nameLen = br.ReadUInt32();
                        names[idx] = System.Text.Encoding.ASCII.GetString(br.ReadBytes((int)nameLen));  //Get name and envelope number.
                        InstrumentType instrumentType = (InstrumentType)br.ReadByte();

                        // // The only instrument type we support for FM import right now is FM. Throw an error if it's any other type.
                        if (instrumentType != InstrumentType.FM)
                        {
                            names.Remove(idx);  //Discard the instrument.
                            br.BaseStream.Position = toEndOfInstDetails;
                            continue;
                        }

                        //If we reached this far, we know we're dealing with an FM instrument.  Determine which envelope index it uses.
                        //Then, initialize a Voice for that bank to represent that envelope.
                        envelopes[idx] = br.ReadByte();
                        v[envelopes[idx]] = new Voice(4);

                        br.BaseStream.Position = toEndOfInstDetails;
                    }
                    br.BaseStream.Position = toEndOfSubheader;

                    //Prepare to process instrument properties.
                    var bankSize=1;
                    foreach(byte i in names.Keys)
                        bankSize = Math.Max(i, bankSize);  //Find highest bank number.

                    //Initialize bank.
                    bank = new string[bankSize+1];

                    err |= ImportBTI.LoadInstProperties(br, v, true);  //Load the rest of the properties into each voice.
                    foreach(byte i in names.Keys)
                    {
                        v[envelopes[i]].name = names[i];  //Assign the correct name for this slot in the bank.
                        bank[i] = v[envelopes[i]].ToJSONString();  //Load the correct voice into the bank.
                    }

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
