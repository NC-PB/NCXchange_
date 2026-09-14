using System.CommandLine;
using Ncx.Core.Expander;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;
using Ncx.Core.Writing;
using Ncx.Readers;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx convert &lt;file&gt; --machine &lt;toml&gt; [--output &lt;file&gt;] [--strict]: reads a controller program into
/// NCX with the reader that the controller of the machine file chooses, writes it with the canonical writer and ends
/// with a STATIC check over the produced program, whose diagnostics are reported together with the reader's
/// (architecture 7, 10). convert always needs a machine file, from --machine or from ncx.toml, never the built-in
/// default machine (D77, D103). The NCX text goes to the standard output or into the file of --output, the diagnostics
/// to the standard error (D98). Exit code 0 without an ERROR, 1 with one or with a WARNING under --strict, 2 when the
/// program or the machine file cannot be read or no machine file is named (D97).
/// </summary>
internal static class ConvertCommand
{
    // A diagnostic about a whole file stands on its first line, as the loaders of Ncx.Config report one.
    private const int FileLine = 1;

    /// <summary>
    /// The convert command of the ncx root command (architecture 10).
    /// </summary>
    /// <param name="readers">The readers by controller family, registered in Program.cs.</param>
    /// <param name="output">The standard output, for the NCX text.</param>
    /// <param name="error">The standard error, for the diagnostics.</param>
    public static Command Create(ReaderRegistry readers, TextWriter output, TextWriter error)
    {
        var fileArgument = new Argument<string>("file")
        {
            Description = "The controller program: a Fanuc or ISO program, a Heidenhain Klartext program.",
        };
        var machineOption = new Option<string>("--machine")
        {
            Description = "The machine the program was written for, by name in machines/ or by path; its controller "
                + "chooses the reader. Without it: the machine that ncx.toml names. convert needs one of the two.",
            HelpName = "toml",
        };
        var outputOption = new Option<string>("--output")
        {
            Description = "Write the NCX text into this file instead of the standard output.",
            HelpName = "file",
        };

        // --strict, which every command accepts (architecture 10, D97).
        var strictOption = RunOptions.StrictOption();
        var command = new Command(
            "convert",
            "Read a controller program into canonical NCX with the reader of the machine's controller, and check the "
            + "result STATIC. Needs the machine file, by --machine or by ncx.toml.")
        {
            fileArgument,
            machineOption,
            outputOption,
            strictOption,
        };

        command.SetAction(parseResult => Run(
            new RunSettings
            {
                File = parseResult.GetRequiredValue(fileArgument),
                MachineFile = parseResult.GetValue(machineOption),
                Strict = parseResult.GetValue(strictOption),
            },
            parseResult.GetValue(outputOption),
            readers,
            output,
            error));
        return command;
    }

    /// <summary>
    /// Converts one controller program: the reader of the machine's controller, the canonical writer, the STATIC check
    /// (architecture 10).
    /// </summary>
    /// <param name="settings">The program, the machine, the folders and --strict; the diagnostics carry the name of the
    /// program as the command line gives it (D98).</param>
    /// <param name="outputFile">The file for the NCX text; null for the standard output.</param>
    /// <param name="readers">The readers by controller family.</param>
    /// <param name="output">The standard output.</param>
    /// <param name="error">The standard error.</param>
    /// <returns>The exit code (D97).</returns>
    internal static int Run(RunSettings settings, string? outputFile, ReaderRegistry readers, TextWriter output,
        TextWriter error)
    {
        var diagnostics = new Diagnostics(settings.File);

        // Code 2 is decided before the run starts and takes precedence: the controller program, ncx.toml, the machine
        // file and its cycle catalog are read first, and one that cannot be found or read stops everything (D97,
        // architecture 10). All of them are read, so that all are reported.
        // TODO(question): language 3 gives the encoding of an NCX file, UTF-8, and no document gives the encoding of a
        // controller program, which older controls and editors write in a code page of their own. It is read as UTF-8
        // like every input of ncx, and a program whose bytes are no UTF-8 is an input that cannot be read, exit code 2,
        // until that is answered.
        string? text = InputFile.Read(settings.File, DiagnosticCodes.InputUnreadable, "The controller program",
            "language 3, Encoding; D97", diagnostics);
        RunMachine runMachine = RunMachine.Select(settings, diagnostics);

        // convert always needs the machine file the program was written for, named by --machine or by the machine key
        // of ncx.toml, never the built-in default machine: its tables name the M codes, its X axis says whether the
        // program writes diameters (D77, D60, D103). A missing machine file where one is required decides exit code 2
        // before the run starts (D97, architecture 10).
        if (runMachine.IsDefault)
        {
            ReportMachineRequired(diagnostics);
        }

        if (text is null || !runMachine.InputsRead || runMachine.IsDefault)
        {
            error.Write(diagnostics.ToText());
            return ExitCodes.NotStarted;
        }

        // ncx.toml, a machine file or a cycle catalog that reads but loads with an ERROR stops the run before it
        // starts, with exit code 1 like any other ERROR (D97; P1-07). So does a controller that ncx has no reader for.
        if (runMachine.Machine is not MachineConfig machine
            || ReaderOf(machine, readers, diagnostics) is not IReader reader)
        {
            error.Write(diagnostics.ToText());
            return ExitCodes.Error;
        }

        // The reader turns the program into NCX, keeping as RAW what it cannot express, and the canonical writer writes
        // it (architecture 7; D5). A byte order mark is no part of the text the reader reads (wave-1 question #80).
        // TODO: the reader rules and the program rewriters of the plugins that ncx.toml names join here (P7-01, D106).
        var source = new SourceFile(settings.File, WithoutByteOrderMark(text));
        NcxProgram program = reader.Read(source, machine, new ReadOptions());
        string canonical = NcxWriter.Write(program);

        // convert ends with a STATIC pass over the produced program, expanded as ncx check expands it, and reports its
        // diagnostics together with the reader's, the reader's first (architecture 7, 10; virtual machine 1; D91, D98).
        // The blocks of the program carry the lines of the source blocks they were read from, so what the check finds
        // cites the controller program as the reader does (architecture 7, Begin(sourceLine); code-guidelines 6). An
        // ERROR of the reader stops the run before its first block, as an ERROR of the parser does (virtual machine
        // 2.9).
        NcxProgram expanded = Expander.Expand(program, machine, []);
        new VirtualMachine(machine, VmOptions.ForMachine(machine), expanded.Diagnostics).Run(expanded);
        foreach (Diagnostic diagnostic in expanded.Diagnostics.Items)
        {
            diagnostics.Add(diagnostic);
        }

        // The NCX text goes to the standard output, or into the file of --output (architecture 10).
        // TODO(question): architecture 10 gives convert the output ".ncx in the working directory" without naming the
        // file, while the phase plan (P3-02) compares what the command writes with the example. The text goes to the
        // standard output or into the file of --output, as ncx format writes it, and nothing is written into the
        // working directory that the command line does not name, until that is answered.
        // TODO(question): architecture 7 ends convert with a STATIC pass that reports on the produced program, and
        // code-guidelines 5 and 6 stop a run on ERROR; neither says whether convert writes the NCX text when the reader
        // or the check reports an ERROR. The reader always produces a whole program, what it cannot read kept as RAW
        // (D5), so the text is written and the ERROR sets the exit code 1, until that is answered.
        if (outputFile is not null)
        {
            OutputFile.Write(outputFile, canonical, diagnostics);
        }
        else
        {
            output.Write(canonical);
        }

        error.Write(diagnostics.ToText());
        return ExitCodes.OfRun(diagnostics, settings.Strict);
    }

    // A machine file that neither --machine nor ncx.toml names is reported as a mistake of the command line: the tool
    // as its file and line 1, the one line of the command, as a usage error is reported (wave-1 question #79; D98).
    private static void ReportMachineRequired(Diagnostics diagnostics)
    {
        diagnostics.Add(new Diagnostic
        {
            Severity = Severity.Error,
            File = Program.ToolName,
            Line = Program.CommandLine,
            Code = DiagnosticCodes.MachineRequired,
            Message = "convert needs the machine file the program was written for: name it with --machine or with the "
                + "machine key of ncx.toml; convert never runs against the built-in default machine (D77, D103; "
                + "architecture 10).",
        });
    }

    // The controller of the machine file chooses the reader, one per controller family, from the registry
    // (architecture 7; machine-config 1; code-guidelines 5, Strategy and Registry). A family without a reader is an
    // ERROR on the program, which is then not read.
    private static IReader? ReaderOf(MachineConfig machine, ReaderRegistry readers, Diagnostics diagnostics)
    {
        if (machine.Machine.Controller is Controller controller && readers.Create(controller) is IReader reader)
        {
            return reader;
        }

        string family = machine.Machine.Controller is Controller named ? $"the controller {named}" : "no controller";
        diagnostics.Error(FileLine, DiagnosticCodes.NoReaderForController,
            $"The machine \"{machine.Machine.Name}\" has {family}, and ncx has no reader for its programs, so the "
            + "program is not converted (architecture 7; machine-config 1).");
        return null;
    }

    // A byte order mark is no part of the text a reader reads, as it is none of the text the parser reads (wave-1
    // question #80).
    private static string WithoutByteOrderMark(string text)
    {
        return text.StartsWith(InputFile.ByteOrderMark, StringComparison.Ordinal)
            ? text.Substring(InputFile.ByteOrderMark.Length)
            : text;
    }
}
