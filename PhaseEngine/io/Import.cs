using System;
using PhaseEngine;

namespace PhaseEngine 
{
    public abstract class IOVoiceBank
    {
        public string metadata;
        public string[] bank;

        public IOVoiceBank()    {}

        public abstract IOErrorFlags Load(string path);
    }

    [Flags]
    public enum IOErrorFlags {OK=0, Failed=1, UnrecognizedFormat=2, NotFound=4, AccessDenied=8, Corrupt=16, }

    //Stub
    public class PE_ImportException : Exception
    {
        public IOErrorFlags flags;
        public PE_ImportException(IOErrorFlags flags) {this.flags = flags;}
    }

    public class IOVoiceData
    {
        public IOVoiceData() {}

        
    }
}
