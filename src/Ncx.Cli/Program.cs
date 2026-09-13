using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;
using Ncx.Cli.Commands;
using Ncx.Core.Model;

namespace Ncx.Cli;

/// <summary>
/// The composition root of ncx (code-guidelines 5): the root command with its commands, built by hand, run once. The
/// commands of architecture 10 join as their tasks arrive: ncx format (phase 0, P0-06), then check, trace and annotate
/// (phase 1, P1-07).
/// </summary>
internal static class Program
{
    // The name of the tool, which a diagnostic about the command line names as its file (D98).
    private const string ToolName = "ncx";

    // The command line is one line.
    private const int CommandLine = 1;

    public static int Main(string[] args)
    {
        // The canonical text goes to the standard output as UTF-8 without a byte order mark and with the line endings
        // it carries, whatever the encoding of the console (language 3, Encoding).
        using var standardOutput = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
        int exitCode = Run(args, standardOutput, Console.Error);
        standardOutput.Flush();
        return exitCode;
    }

    /// <summary>
    /// Runs one command line and returns its exit code (D97).
    /// </summary>
    /// <param name="args">The command line without the name of the tool: "format", "file.ncx", "--check".</param>
    /// <param name="output">The standard output: the canonical text, the help.</param>
    /// <param name="error">The standard error: the diagnostics (D98).</param>
    internal static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        var root = new RootCommand("ncx reads, checks, formats and compiles NCX programs (architecture 10).")
        {
            FormatCommand.Create(output, error),
            CheckCommand.Create(error),
            TraceCommand.Create(output, error),
            AnnotateCommand.Create(output, error),
        };

        // A usage error is reported as a diagnostic and decides exit code 2 before the run starts (D97, D98;
        // architecture 10).
        // TODO(question): D98 renders a diagnostic as file(line), and a usage error is about the command line, not a
        // file. It names the tool as its file and line 1, the one line of the command, until that is answered.
        ParseResult parseResult = root.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            var diagnostics = new Diagnostics(ToolName);
            foreach (ParseError parseError in parseResult.Errors)
            {
                diagnostics.Error(CommandLine, DiagnosticCodes.UsageError,
                    $"{parseError.Message.TrimEnd('.')} (ncx --help; architecture 10).");
            }

            error.Write(diagnostics.ToText());
            return ExitCodes.NotStarted;
        }

        return parseResult.Invoke(new InvocationConfiguration { Output = output, Error = error });
    }
}
