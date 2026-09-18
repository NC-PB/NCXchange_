using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;
using Ncx.Analytics;
using Ncx.Analytics.Runtime;
using Ncx.Analytics.Segments;
using Ncx.Analytics.ToolList;
using Ncx.Analytics.ToolVectors;
using Ncx.Cli.Commands;
using Ncx.Compilers;
using Ncx.Compilers.Heidenhain;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Readers;
using Ncx.Readers.Fanuc;
using Ncx.Readers.Heidenhain;
using Ncx.Readers.Siemens;

namespace Ncx.Cli;

/// <summary>
/// The composition root of ncx (code-guidelines 5): the root command with its commands, built by hand, run once. The
/// commands of architecture 10 join as their tasks arrive: ncx format (phase 0, P0-06), then check, trace and annotate
/// (phase 1, P1-07), then convert (phase 3, P3-02), then analyze (phase 4, P4-02).
/// </summary>
internal static class Program
{
    /// <summary>
    /// The name of the tool, which a diagnostic about the command line names as its file (D98).
    /// </summary>
    internal const string ToolName = "ncx";

    /// <summary>
    /// The command line is one line.
    /// </summary>
    internal const int CommandLine = 1;

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
            ConvertCommand.Create(Readers(), output, error),
            AnalyzeCommand.Create(Analytics(), output, error),
            CompileCommand.Create(Compilers(), error),
            PluginCommand.Create(output, error),
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

    /// <summary>
    /// The readers by controller family, one registration line each, so that the controller of the machine file
    /// chooses the reader of ncx convert (architecture 7; code-guidelines 5, Strategy and Registry).
    /// </summary>
    internal static ReaderRegistry Readers()
    {
        var readers = new ReaderRegistry();
        readers.Register(Controller.Fanuc, () => new FanucReader());
        readers.Register(Controller.Heidenhain, () => new HeidenhainReader());
        readers.Register(Controller.Siemens, () => new SiemensReader());
        return readers;
    }

    /// <summary>
    /// The analytics by the name ncx analyze --analytic takes, one registration line each (architecture 9; D67;
    /// code-guidelines 5, Registry).
    /// </summary>
    internal static AnalyticsRegistry Analytics()
    {
        var analytics = new AnalyticsRegistry();
        analytics.Register("tools", options => new ToolListAnalytic(options));
        analytics.Register("runtime", options => new RuntimeAnalytic(options));
        analytics.Register("segments", options => new SegmentAnalytic(options));
        analytics.Register("vectors", options => new ToolVectorAnalytic(options));
        return analytics;
    }

    /// <summary>
    /// The compilers by controller family, one registration line each, so that the controller of the machine file
    /// chooses the compiler of ncx compile (architecture 8; code-guidelines 5, Strategy and Registry). A family
    /// registers its compiler here with one line: compilers.Register(Controller.Heidenhain, () => new
    /// HeidenhainCompiler()).
    /// </summary>
    internal static CompilerRegistry Compilers()
    {
        var compilers = new CompilerRegistry();
        compilers.Register(Controller.Heidenhain, () => new HeidenhainCompiler());
        return compilers;
    }
}
