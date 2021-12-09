using System;
using PhaseEngine;
using System.IO;

namespace PhaseEngine
{
    public class ImportOPM : IOVoiceBank
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
                    if (!line.StartsWith("@:")) { throw new PE_ImportException(IOErrorFlags.UnrecognizedFormat); }
                }
            }
            catch (FileNotFoundException e) { err |= IOErrorFlags.NotFound; }
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
