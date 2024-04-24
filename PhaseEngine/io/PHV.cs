using System;
using PhaseEngine;
using System.IO;
using System.Collections.Generic;
using PE_Json;
using Godot;

namespace PhaseEngine
{
    public class ImportPHV : VoiceBankImporter
    {
        enum InstrumentType {FM, SSG, ADPCM, Drumkit, UNKNOWN=0xFF};
        enum InstProps {FMEnvelope, LFO, ALSequence, FBSequence, 
                        FMArpeggioSequence=0x28,FMPitchSequence, FMPanningSequence,
                        SSGWaveformSequence=0x30, SSGToneSequence, SSGEnvelopeSequence, SSGArpeggioSequence, SSGPitchSequence,
                        ADPCMSample=0x40, ADPCMEnvelopeSequence,ADPCMArpeggioSequence, ADPCMPitchSequence, ADPCMPanningSequence
         }
        public ImportPHV(){fileFormat="phv"; description="PhaseEngine Voice";}


 

        public override IOErrorFlags Load(string path)
        {
            IOErrorFlags err = IOErrorFlags.OK;
            try
            {
                var file = new FileInfo(path);
                //In theory, PHV currently can only handle 8 operators. There are a limit to the number of samples useful
                //to a particular voice until wavetable morphing operators are implemented.  Therefore, we'll base the max
                //filesize on a reasonable number of embedded samples (16) plus 8kb to account for JSON data.
                const int MAX_SAMPLE_BYTES = 0x10000; 
                if (file.Length > 8192 + MAX_SAMPLE_BYTES*16)  throw new PE_ImportException(IOErrorFlags.TooLarge); //~1mb


                using (StreamReader sr = new StreamReader(file.OpenRead()))
                {
                    //PHV files contain one voice per file.  Initialize bank.
                    bank = new string[1];

                    //See if this is a valid PhaseEngine voice
                    // var firstchar = System.Text.Encoding.ASCII.GetString(new byte[]{(byte)sr.Read()});
                    // if (firstchar != "{") throw new PE_ImportException(IOErrorFlags.UnrecognizedFormat, "Not a JSON object");

                    // sr.BaseStream.Seek(0,SeekOrigin.Begin);
                    var json = JSONData.ReadJSON(sr.ReadToEnd());

                    //Check if the JSON data was malformed in some way.
                    if (json is JSONnull)
                        throw new PE_ImportException(IOErrorFlags.Failed, "JSON contains no data or is malformed");
                    else if (json is JSONDataError)
                        throw new PE_ImportException(IOErrorFlags.Failed, json.ToString());
                    else if (!(json is JSONObject))
                        throw new PE_ImportException(IOErrorFlags.Unsupported, "JSON data member found but unrecognized format");

                    //At this point, we should be confident at least that we have a JSON file. Check format version.
                    //Only valid format version currently is 10; future versions may support better sample compression
                    //which may not be specified in the file itself, so don't try to support newer file versions.
                    //Moreover, don't attempt to load any PHV files which don't have format version data.
                    var o = (JSONObject)json;
                    var version = o.GetItem("FORMAT", -1);
                    if (version==-1) throw new PE_ImportException(IOErrorFlags.Unsupported, "PHV: No format version info found");
                    if (version != 10)  throw new PE_ImportException(IOErrorFlags.Unsupported, 
                            $"The version is incorrect. Expecting {Global.FORMAT_VERSION}; Got {version}");
                    //TODO:  Look into some forward-compatibility options

                    //We should be reasonably confident now that unless this is a maliciously-formed PHV that we can import it.
                    //But just in case, let's try to convert it to a voice first in a catchable way.
                    Voice v;
                    try{ v = new Voice(o); }
                    catch (Exception e) {
                        throw new PE_ImportException(IOErrorFlags.Corrupt, e.Message);
                    }

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
