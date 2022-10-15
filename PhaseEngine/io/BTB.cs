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
        const double NATIVE_HZ_RATE = 55466.0;
        static double ClockMult = NATIVE_HZ_RATE/Global.MixRate * Global.ClockMult;  //Ratio needed to translate one OPNA clock to a PhaseEngine clock at current mixrate



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
