#load "logger.csx"
#load "command-line.csx"
public class GitCommands
{
    public static void StashChanges()
    {
        CommandLine.Execute("git stash -q --keep-index");
    }
    public static void UnstashChanges()
    {
        CommandLine.Execute("git stash pop -q");
    }
	
	public static string CommitHash(bool isShortHash=true)
	{
		var s = isShortHash? "--short" : "";
		return CommandLine.Execute($"git rev-parse {s} HEAD");
	}
}