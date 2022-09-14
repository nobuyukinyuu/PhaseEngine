#!/usr/bin/env dotnet-script
//If calling this script from a symlink in .git/hooks then the below commands wouldn't work without moving back to our proper working directory in the REPL.
#load "../../script_templates/.githooks/logger.csx"
#load "../../script_templates/.githooks/command-line.csx"
#load "../../script_templates/.githooks/git-commands.csx"


    string commitVersion = GitCommands.CommitHash();
    const string templateLine = $"        public const string BUILD_VERSION";
    string comment = DateTime.Now.ToLocalTime().ToString("d MMM yyyy, hh:mm tt");
    string replaceLine = $"        public const string BUILD_VERSION = \"{commitVersion.TrimEnd()}\"; //Generated on {comment}";
    const string fileName = "PhaseEngine/Constants.cs";

    string[] arrLine = File.ReadAllLines(fileName);

    Console.WriteLine(replaceLine);
    for(int i=0; i<arrLine.Length; i++)
    {
        if(arrLine[i].TrimStart().StartsWith(templateLine.TrimStart()))
        {
            arrLine[i] = replaceLine;
            Console.WriteLine("Line Replacement OK.");
            break;
        }
    }

     File.WriteAllLines(fileName, arrLine);
