using System;
using PhaseEngine;
using System.Collections.Generic;
using System.Reflection;

namespace PhaseEngine 
{
    //summary:  Abstract class describing an import extension PhaseEngine can use to set up a voice for import.
    public abstract class VoiceBankImporter
    {
        public string metadata;
        public string[] bank;

        public string fileFormat;  //Associated format
        public string description="";

        public VoiceBankImporter()    {}

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



    public static class PE_ImportServer
    {
        public static Dictionary<string, VoiceBankImporter> loaders = new Dictionary<string, VoiceBankImporter>();

        static PE_ImportServer()
        {
            //Get a list of all available importers and create singleton instances of all of them.
            var importers = new List<VoiceBankImporter>();
            var importerTypes = new List<Type>();
            importerTypes = FindDerivedTypes<VoiceBankImporter>();

            foreach (Type t in importerTypes)
                importers.Add( (VoiceBankImporter)Convert.ChangeType(System.Activator.CreateInstance(t), t) );

            foreach(VoiceBankImporter importer in importers)
            {
                //for multiple formats, add to each key.  Note that only one class per format can be supported currently.
                //TODO:  make loaders a Dictionary<string, List<VoiceBankImporter>> instead, then if the first instance fails,
                //       go to the next instance and try loading the bank with it. If all fail, can return unknown format...
                var formats = importer.fileFormat.ToLowerInvariant().Trim().Split("|");
                foreach (string f in formats)
                    loaders.Add(f, importer);
            }

            System.Diagnostics.Debug.Print("Reached the end of the import server initializer.");
            Godot.GD.Print("ImportServer init");
        }

        public static IOErrorFlags TryLoad(string path, out VoiceBankImporter loader)
        {
            foreach(string format in loaders.Keys)
                if(path.EndsWith(format))  //Try loading the format
                {
                    loader = loaders[format];
                    return loader.Load(path);
                }

            loader = null;
            return IOErrorFlags.UnrecognizedFormat;
        }

        static List<Type> FindDerivedTypes<T>() { return FindDerivedTypes<T>(Assembly.GetAssembly(typeof(T))); }
        static List<Type> FindDerivedTypes<T>(Assembly assembly)
        {
            var derived = typeof(T);
            var output = new List<Type>();
            foreach(Type type in assembly.GetTypes())
            {
                if (type!= derived && derived.IsAssignableFrom(type)) output.Add(type);
            }
            return output;
        }

        #if GODOT
            public static Godot.Collections.Array GetSupportedFormats()
            {
                var output = new Godot.Collections.Array();

                foreach (VoiceBankImporter v in loaders.Values)
                    output.Add( $"*.{v.fileFormat}; {v.description}" );

                return output;
            }
        #endif

    }

}
