using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Ncx.Core.Model;

namespace Ncx.Cli.Commands;

/// <summary>
/// Runs dotnet of the .NET SDK for ncx plugin build and ncx plugin test and keeps what it writes (implementation 17,
/// P7-02, and its risks: the SDK must be on the path, and the command says so when it is not).
/// </summary>
internal static class DotnetProcess
{
    /// <summary>
    /// Runs dotnet with its arguments and waits for it.
    /// </summary>
    /// <param name="dotnet">The command that starts dotnet: dotnet from the path.</param>
    /// <param name="arguments">The arguments: build, the project file, the options.</param>
    /// <param name="workingDirectory">The folder it runs in.</param>
    /// <returns>Its exit code and what it wrote, or why it could not be started.</returns>
    public static DotnetResult Run(string dotnet, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(dotnet)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // What dotnet writes to its standard output and to its standard error, kept line by line as it comes, so that
        // the user reads the errors of the compiler in their place.
        var written = new StringBuilder();
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, line) => Keep(written, line.Data);
        process.ErrorDataReceived += (_, line) => Keep(written, line.Data);

        // A dotnet that is not on the path cannot be started: an error of the environment, reported with its reason
        // (code-guidelines 6).
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            return new DotnetResult { Failure = exception.Message };
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        return new DotnetResult { ExitCode = process.ExitCode, Output = written.ToString() };
    }

    /// <summary>
    /// Reports a dotnet that could not be started, and what the user needs (implementation 17, P7-02, risks).
    /// </summary>
    /// <param name="failure">Why it could not be started.</param>
    /// <param name="diagnostics">The diagnostics of the command.</param>
    public static void ReportNotStarted(string failure, Diagnostics diagnostics)
    {
        diagnostics.Error(Program.CommandLine, DiagnosticCodes.DotnetNotStarted,
            $"dotnet cannot be started: {failure.ReplaceLineEndings(" ").TrimEnd('.')}; ncx plugin build and "
            + "ncx plugin test run dotnet of the .NET SDK, which must be installed and on the path (step 1 of the "
            + "README of the plugin, code-guidelines 11).");
    }

    // One line of what dotnet wrote; the end of a stream gives none. The two streams come on two threads.
    private static void Keep(StringBuilder written, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (written)
        {
            written.Append(line).Append('\n');
        }
    }
}
