using System.CommandLine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.Writing;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx format &lt;file&gt; [--check] [--output &lt;file&gt;] [--strict]: reads an NCX file and writes it in canonical
/// form. It is the parser and the canonical writer and nothing else: no machine file, no virtual machine, no expander,
/// so a bare TOOL stays bare (D91; architecture 10). Exit code 0 without an ERROR, 1 with one, with a WARNING under
/// --strict or when --check finds a difference, 2 for a usage error or an input that cannot be read (D97); the
/// diagnostics go to the standard error (D98).
/// </summary>
internal static class FormatCommand
{
    /// <summary>
    /// The format command of the ncx root command (architecture 10). It has no --machine option, because format takes
    /// no machine file (D91).
    /// </summary>
    /// <param name="output">The standard output, for the canonical text.</param>
    /// <param name="error">The standard error, for the diagnostics.</param>
    public static Command Create(TextWriter output, TextWriter error)
    {
        var fileArgument = new Argument<string>("file") { Description = "The NCX file." };
        var checkOption = new Option<bool>("--check")
        {
            Description = "Write nothing; exit with 1 when the file is not in canonical form.",
        };
        var outputOption = new Option<string>("--output")
        {
            Description = "Write the canonical text into this file instead of the standard output.",
            HelpName = "file",
        };

        // --strict, which every command accepts (architecture 10, D97).
        var strictOption = RunOptions.StrictOption();
        var command = new Command(
            "format",
            "Write an NCX file in canonical form: the words in canonical order, the comment in column 57. No machine "
            + "file, no virtual machine.")
        {
            fileArgument,
            checkOption,
            outputOption,
            strictOption,
        };

        // --check writes no output, so it takes no --output (architecture 10).
        command.Validators.Add(result =>
        {
            if (result.GetValue(checkOption) && result.GetValue(outputOption) is not null)
            {
                result.AddError("--check writes nothing, so it takes no --output");
            }
        });

        command.SetAction(parseResult => Run(
            parseResult.GetRequiredValue(fileArgument),
            parseResult.GetValue(checkOption),
            parseResult.GetValue(outputOption),
            parseResult.GetValue(strictOption),
            output,
            error));
        return command;
    }

    /// <summary>
    /// Formats one file: parse, write, nothing else (D91).
    /// </summary>
    /// <param name="file">The NCX file, as the command line names it; the diagnostics carry this name (D98).</param>
    /// <param name="check">Write nothing and compare the canonical text with the file.</param>
    /// <param name="outputFile">The file for the canonical text; null for the standard output.</param>
    /// <param name="strict">--strict: a WARNING sets the exit code 1 (D97).</param>
    /// <param name="output">The standard output.</param>
    /// <param name="error">The standard error.</param>
    /// <returns>The exit code (D97).</returns>
    internal static int Run(string file, bool check, string? outputFile, bool strict, TextWriter output,
        TextWriter error)
    {
        var readDiagnostics = new Diagnostics(file);
        string? text = InputFile.Read(file, DiagnosticCodes.InputUnreadable, "The file", "language 3, Encoding",
            readDiagnostics);
        if (text is null)
        {
            error.Write(readDiagnostics.ToText());
            return ExitCodes.NotStarted;
        }

        // TODO(question): language 3 says UTF-8 and does not say whether a byte order mark belongs to an NCX file. It is
        // no part of the text the parser reads and goes back in front of the canonical text as it came, so that format
        // changes nothing it is not asked to, until that is answered.
        string byteOrderMark = text.StartsWith(InputFile.ByteOrderMark, StringComparison.Ordinal)
            ? InputFile.ByteOrderMark
            : "";
        NcxProgram program = Parser.Parse(text.Substring(byteOrderMark.Length), file, new ParserOptions());
        Diagnostics diagnostics = program.Diagnostics;

        // The run stops on ERROR: the diagnostics are printed, nothing is written, and the exit code is 1
        // (code-guidelines 5 and 6; D97).
        if (diagnostics.HasErrors)
        {
            error.Write(diagnostics.ToText());
            return ExitCodes.Error;
        }

        string canonical = byteOrderMark + NcxWriter.Write(program);
        bool differs = false;
        if (check)
        {
            differs = ReportDifference(text, canonical, diagnostics);
        }
        else if (outputFile is not null)
        {
            OutputFile.Write(outputFile, canonical, diagnostics);
        }
        else
        {
            output.Write(canonical);
        }

        // A WARNING is reported and the run continues; under --strict it sets the exit code 1 (D97).
        error.Write(diagnostics.ToText());
        return differs ? ExitCodes.Error : ExitCodes.OfRun(diagnostics, strict);
    }

    // --check writes nothing and exits with 1 when the canonical text differs from the file (D97, architecture 10). The
    // difference is no ERROR, since hand-written files are free (language 5 rule 6, D43), and no WARNING; a note names
    // the first line that differs, so that the user knows where to look.
    private static bool ReportDifference(string input, string canonical, Diagnostics diagnostics)
    {
        if (input == canonical)
        {
            return false;
        }

        diagnostics.Info(FirstDifferingLine(input, canonical), DiagnosticCodes.NotCanonical,
            "The file differs from its canonical form from this line on: ncx format writes the words in canonical order "
            + "one space apart, the comment in column 57 and every line with the line ending of the file (language 2 "
            + "rule 7, 5 rules 6 and 7).");
        return true;
    }

    // The 1-based line of the input on which the first character differs from the canonical text.
    private static int FirstDifferingLine(string input, string canonical)
    {
        int line = 1;
        int length = Math.Min(input.Length, canonical.Length);
        for (int index = 0; index < length && input[index] == canonical[index]; index++)
        {
            if (input[index] == '\n')
            {
                line++;
            }
        }

        return line;
    }
}
