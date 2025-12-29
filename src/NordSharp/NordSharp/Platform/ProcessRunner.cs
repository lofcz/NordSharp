using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NordSharp.Platform;

/// <summary>
/// Helper for running external processes.
/// </summary>
internal static class ProcessRunner
{
    /// <summary>
    /// Runs a command and returns the output.
    /// </summary>
    public static (int exitCode, string output, string error) Run(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        int timeoutMs = 60000)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? string.Empty,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.Start();

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(); } catch { }
            return (-1, output, "Process timed out");
        }

        return (process.ExitCode, output, error);
    }

    /// <summary>
    /// Runs a command with arguments array and returns the output.
    /// </summary>
    public static (int exitCode, string output, string error) Run(
        string fileName,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        int timeoutMs = 60000)
    {
        var args = string.Join(" ", arguments.Select(EscapeArgument));
        return Run(fileName, args, workingDirectory, timeoutMs);
    }

    /// <summary>
    /// Starts a process without waiting (fire and forget).
    /// </summary>
    public static void StartDetached(string fileName, string arguments, string? workingDirectory = null)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? string.Empty,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.Start();
    }

    /// <summary>
    /// Checks if a process with the given name is running.
    /// </summary>
    public static bool IsProcessRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        var isRunning = processes.Length > 0;
        foreach (var p in processes)
        {
            p.Dispose();
        }
        return isRunning;
    }

    private static string EscapeArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return "\"\"";

        if (!arg.Contains(' ') && !arg.Contains('"') && !arg.Contains('\\'))
            return arg;

        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
