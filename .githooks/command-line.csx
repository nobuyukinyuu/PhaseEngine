#load "logger.csx"
public class CommandLine
{
    public static string Execute(string command)
    {
        // according to: https://stackoverflow.com/a/15262019/637142
        // thans to this we will pass everything as one command
        command = command.Replace("\"", "\"\"");
        var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c \"" + command + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };
        proc.Start();
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            Logger.LogError(proc.StandardOutput.ReadToEnd());
            return proc.ExitCode.ToString();
        }
        return proc.StandardOutput.ReadToEnd();
    }
}