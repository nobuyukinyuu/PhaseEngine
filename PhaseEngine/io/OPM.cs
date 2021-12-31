using System;
using PhaseEngine;
using System.IO;
using System.Collections.Generic;
using GdsFMJson;

namespace PhaseEngine
{
    public class ImportOPM : VoiceBankImporter
    {
        enum OpNames {M1=0, C1=1, M2=2, C2=3}
        public ImportOPM(){fileFormat="opm";}

        public override IOErrorFlags Load(string path)
        {
            IOErrorFlags err = IOErrorFlags.OK;
            try
            {
                using (StreamReader sr = new StreamReader(path))
                {
                    string line;
                    line=sr.ReadLine();
                    //Check header for validity.  TODO:  Check to see if there exists other formats created in other drivers
                    if (!line.StartsWith("//MiOPMdrv")) { throw new PE_ImportException(IOErrorFlags.UnrecognizedFormat); }

                    //Move forward to the instrument blocks.
                    // while (!line.StartsWith("@:"))  line=sr.ReadLine();


                    //OPM files all have 128 banks.  Initialize bank.
                    bank = new string[128];


                    //At any point, some joker may have inserted a comment or empty line.  Use this local func to skip them.
                    string NextValidLine() { var ln=line; while(ln.StartsWith("//") || ln.Trim()=="") ln=sr.ReadLine(); if (ln==null) return null; return ln; }

                    //Prepare to process banks.
                    var egs = new Envelope[4]; egs.InitArray();  //Envelope used to generate partial JSON.                    
                    // for(int i=0; i<bank.Length; i++)
                    while(!sr.EndOfStream)
                    {
                        //Use our envelope proto as a way to store voice data as a json string.
                        //Assume the format of each block corresponds to the MiOPMdrv specification:
                        //@:[Num] [Name]
                        //LFO: FRQ AMD PMD WAV NFRQ   //Where NFRQ = Noise Frequency (Duty in PhaseEngine Noise1 or Noise2 mode)
                        //CH:  PAN	FB ALG AMS PMS SLOT NE  //Where FB = Feedback of M1 (first op) and NE = Noise override (Change all waveforms to Noise1)
                        //[OPname]: AR DR  SR  RR  DL   TL  KS MUL DT1 DT2 AMS-EN
                        //
                        // CH SLOT is the mute mask, where  M1=8, C1=16, M2=32, C2=64. It's currently unknown if flags 1, 2, 4 are used. Normal mask:  120
                        // AMS-EN is AMS enable, which only appears to have 0 and 128 as values.  Treat DT1 as normal detune and DT2 as coarse (Cents mult).

                        var p = new JSONObject();

                        //Get the first instrument.
                        var l=NextValidLine();
                        while (!l.StartsWith("@:")) l=NextValidLine();

                        ProcessNextVoice:
                        //Currently, l should be the instrument header line. Process every instrument in the bank now.
                        l = l.Substring(2).Trim();  //Prep for split.
                        var splitLine = l.Split(" ", StringSplitOptions.RemoveEmptyEntries);  //Should have a length of 2.
                        var slot = Convert.ToInt32(splitLine[0]);  //Will be used to assign the correct bank once we have built our voice proto.
                        p.AddPrim("name", splitLine[1]);

                        l=NextValidLine();  //Next line should be LFO.  However, order could be anything....
                        splitLine=l.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                        
                        ProcessNextLine:
                        switch(splitLine[0].Trim())
                        {
                            case "LFO:": //LFO configuration options.
                                break;
                            case "CH:":  //Voice configuration options.
                                break;
                            default:  //One of the 4 operators.  Translate from OpNames to their correct values to assign the correct envelope.
                                OpNames opName;
                                if (Enum.TryParse<OpNames>(splitLine[0].Substring(0, 2), true, out opName)) //Only executes on success
                                {
                                    var opNum = (int)opName;
                                    var e = egs[opNum];  //Select the envelope.
                                    //[OPname]: AR DR  SR  RR  DL   TL  KS MUL DT1 DT2 AMS-EN

                                    e.ar = Convert.ToByte(splitLine[1]);
                                    e.dr = Convert.ToByte(splitLine[2]);
                                    e.sr = Convert.ToByte(splitLine[3]);
                                    e.rr = Convert.ToByte(splitLine[4]);

                                    e.dl = Convert.ToUInt16(splitLine[5]);
                                    e.tl = Convert.ToUInt16(splitLine[6]);

                                    //Assign envelope RateTable to a default preset and scale the max application.

                                    //TODO:  Import and assign the other values

                                }
                            break;    
                        }

                        l=NextValidLine();
                        if (l==null) break;  //End of File
                        else if (l.StartsWith("@:"))
                        {
                            // bank[slot] = //TODO:  Convert voice to string here...  Or, change bank type to Voice[] from string[]....
                            goto ProcessNextVoice;
                        }
                        else goto ProcessNextLine;
                    }

                }
            }
            catch (FileNotFoundException) { err |= IOErrorFlags.NotFound; }
            catch (PE_ImportException e) { err |= e.flags; }
            catch //Anything else
            { err |= IOErrorFlags.Failed | IOErrorFlags.Corrupt; }

            return err;
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }
}
