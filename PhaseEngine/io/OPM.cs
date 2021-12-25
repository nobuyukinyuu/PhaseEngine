using System;
using PhaseEngine;
using System.IO;

namespace PhaseEngine
{
    public class ImportOPM : VoiceBankImporter
    {
        public ImportOPM()    {}

        public override IOErrorFlags Load(string path)
        {
            IOErrorFlags err = IOErrorFlags.OK;
            try
            {
                using (StreamReader sr = new StreamReader(path))
                {
                    string line;
                    line=sr.ReadLine();
                    //Check header for validity
                    if (!line.StartsWith("//MiOPMdrv")) { throw new PE_ImportException(IOErrorFlags.UnrecognizedFormat); }

                    //OPM files all have 128 banks.  Initialize bank.
                    bank = new string[128];

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
