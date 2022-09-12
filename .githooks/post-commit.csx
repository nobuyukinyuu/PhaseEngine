#!/usr/bin/env dotnet-script
//If calling this script from a symlink in .git/hooks then the below commands wouldn't work without moving back to our proper working directory in the REPL.
#load "../../.githooks/logger.csx"
#load "../../.githooks/command-line.csx"
#load "../../.githooks/git-commands.csx"


    string commitVersion = GitCommands.CommitHash();
    const string templateLine = $"        public const string BUILD_VERSION";
    string replaceLine = $"        public const string BUILD_VERSION = \"{commitVersion.TrimEnd()}\"; //Generated at: {comment}";
    string comment = DateTime.Now.ToLocalTime().ToString("d MMM yyyy, hh:mm tt");
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
