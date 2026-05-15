using System.Diagnostics;

public static class GitHelper
{
    public static string GetCurrentGitCommitHash()
    {
        string gitCommand = "rev-parse HEAD";

        ProcessStartInfo processInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = gitCommand,
            WorkingDirectory = System.IO.Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = new Process())
        {
            process.StartInfo = processInfo;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Remove newlines and extra spaces from the output
            string commitHash = output.Trim().Replace("\r", "").Replace("\n", "");

            return commitHash;
        }
    }

    public static string GetGitCommitMessage()
    {
        string gitCommand = $"log -1 --pretty=format:%s HEAD";

        ProcessStartInfo processInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = gitCommand,
            WorkingDirectory = System.IO.Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = new Process())
        {
            process.StartInfo = processInfo;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Remove newlines and extra spaces from the output
            string commitMessage = output.Trim().Replace("\r", "").Replace("\n", "");

            return commitMessage;
        }
    }    
}