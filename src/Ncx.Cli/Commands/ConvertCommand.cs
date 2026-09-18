using System.CommandLine;
using Ncx.Core.Expander;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.Writing;
using Ncx.Plugins;
using Ncx.Readers;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx convert &lt;file&gt; --machine &lt;toml&gt; [--output &lt;file&gt;] [--strict]: reads a controller program into
/// NCX with the reader that the controller of the machine file chooses, writes it with the canonical writer and ends
/// with a STATIC check over the produced program, whose diagnostics are reported together with the reader's
/// (architecture 7, 10). convert always needs a machine file, from --machine or from ncx.toml, never the built-in
/// default machine (D77, D103). The NCX text goes to the standard output or into the file of --output, the diagnostics
/// to the standard error (D98). Exit code 0 without an ERROR, 1 with one or with a WARNING under --strict, 2 when the
/// program or the machine file cannot be read or no machine file is named (D97). ncx convert --batch &lt;folder&gt;
/// --machine &lt;toml&gt; --report &lt;file&gt; converts every file of a folder instead (BatchCommand; implementation
/// 13, P3-07).
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
            Arity = ArgumentArity.ZeroOrOne,
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
        var batchOption = new Option<string>("--batch")
        {
            Description = "Convert every file of this folder and of its folders instead of one program, never stopping "
                + "on an error, and write the summary into the file of --report.",
            HelpName = "folder",
        };
        var reportOption = new Option<string>("--report")
        {
            Description = "With --batch: the file the summary goes into, per file and in total the blocks, the RAW "
                + "blocks per word and the diagnostics per code.",
            HelpName = "file",
        };

        // --strict, which every command accepts (architecture 10, D97).
        var strictOption = RunOptions.StrictOption();
        var command = new Command(
            "convert",
            "Read a controller program into canonical NCX with the reader of the machine's controller, and check the "
            + "result STATIC; with --batch every file of a folder, with a summary. Needs the machine file, by "
            + "--machine or by ncx.toml.")
        {
            fileArgument,
            machineOption,
            outputOption,
            batchOption,
            reportOption,
            strictOption,
        };

        // convert takes one program, or a folder with --batch, whose summary goes into the file of --report and which
        // writes no NCX text (implementation 13, P3-07; D209). A command line that mixes the two is a usage error, exit
        // code 2 (D97).
        command.Validators.Add(result =>
        {
            bool batch = result.GetValue(batchOption) is not null;
            if (!batch && result.GetValue(fileArgument) is null)
            {
                result.AddError("convert needs the controller program, or a folder with --batch");
            }

            if (batch && result.GetValue(fileArgument) is not null)
            {
                result.AddError("--batch converts the files of its folder, so it takes no controller program");
            }

            if (batch && result.GetValue(reportOption) is null)
            {
                result.AddError("--batch writes its summary into the file of --report, which is missing");
            }

            if (!batch && result.GetValue(reportOption) is not null)
            {
                result.AddError("--report is the summary of --batch, which is missing");
            }

            if (batch && result.GetValue(outputOption) is not null)
            {
                result.AddError("--batch writes no NCX text, so it takes no --output");
            }
        });

        command.SetAction(parseResult =>
        {
            string? folder = parseResult.GetValue(batchOption);
            var settings = new RunSettings
            {
                File = folder ?? parseResult.GetRequiredValue(fileArgument),
                MachineFile = parseResult.GetValue(machineOption),
                Strict = parseResult.GetValue(strictOption),
            };
            return folder is not null
                ? BatchCommand.Run(settings, parseResult.GetRequiredValue(reportOption), readers, error)
                : Run(settings, parseResult.GetValue(outputOption), readers, output, error);
        });
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
        // architecture 10). All of them are read, so that all are reported. The program is UTF-8 text, or Windows-1252
        // with a WARNING (ControllerProgram, D229).
        string? text = ControllerProgram.Read(settings.File, diagnostics);
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

        // The plugins of the working directory: their reader rules read what the tables of the machine leave
        // undecided, their rewriters and listeners join the check (virtual machine 7; architecture 9; D40, D66). What
        // they report goes into the diagnostics of the run, and a plugin that fails is left out while the run goes on
        // (implementation 17, P7-01).
        PluginSet plugins = RunPlugins.Load(settings, runMachine.Project, diagnostics);

        // The reader and the STATIC check over what it produced, then the canonical writer (architecture 7, 10; D5).
        NcxProgram program = Convert(new SourceFile(settings.File, text), machine, reader, plugins, diagnostics);
        string canonical = NcxWriter.Write(program);

        // The NCX text goes to the standard output, or into the file of --output (architecture 10).
        // TODO(question): architecture 10 gives convert the output ".ncx in the working directory" without naming the
        // file, while the phase plan (P3-02) compares what the command writes with the example. The text goes to the
        // standard output or into the file of --output, as ncx format writes it, and nothing is written into the
        // working directory that the command line does not name, until D209 is answered.
        // TODO(question): architecture 7 ends convert with a STATIC pass that reports on the produced program, and
        // code-guidelines 5 and 6 stop a run on ERROR; neither says whether convert writes the NCX text when the reader
        // or the check reports an ERROR. The reader always produces a whole program, what it cannot read kept as RAW
        // (D5), so the text is written and the ERROR sets the exit code 1, until D205 is answered.
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

    /// <summary>
    /// Converts the text of one controller program: the reader of the machine's controller, then the STATIC check over
    /// the program it produced, whose diagnostics are added after the reader's (architecture 7, 10). ncx convert writes
    /// the program with the canonical writer; ncx convert --batch counts its blocks.
    /// </summary>
    /// <param name="source">The program, named as the diagnostics name it.</param>
    /// <param name="machine">The machine the program was written for.</param>
    /// <param name="reader">The reader of the machine's controller.</param>
    /// <param name="plugins">The plugins of the working directory; null for none.</param>
    /// <param name="diagnostics">Where the diagnostics of the reader and of the check go, in that order (D98).</param>
    /// <returns>The program the reader produced.</returns>
    internal static NcxProgram Convert(SourceFile source, MachineConfig machine, IReader reader, PluginSet? plugins,
        Diagnostics diagnostics)
    {
        // The reader turns the program into NCX, keeping as RAW what it cannot express (architecture 7; D5); the reader
        // rules of the plugins read what the tables of the machine leave undecided (D40, D66).
        NcxProgram program = reader.Read(source, machine, new ReadOptions { Rules = plugins?.SourceRules ?? [] });

        // convert ends with a STATIC pass over the produced program, expanded as ncx check expands it, and reports its
        // diagnostics together with the reader's, the reader's first (architecture 7, 10; virtual machine 1; D91, D98).
        // The blocks of the program carry the lines of the source blocks they were read from, so what the check finds
        // cites the controller program as the reader does (architecture 7, Begin(sourceLine); code-guidelines 6). An
        // ERROR of the reader stops the run before its first block, as an ERROR of the parser does (virtual machine
        // 2.9).
        NcxProgram expanded = Expander.Expand(program, machine, plugins?.Rewriters ?? []);
        var vm = new VirtualMachine(machine, VmOptions.ForMachine(machine), expanded.Diagnostics);
        foreach (IVmListener listener in plugins?.Listeners ?? [])
        {
            vm.Subscribe(listener);
        }

        vm.Run(expanded);
        foreach (Diagnostic diagnostic in expanded.Diagnostics.Items)
        {
            diagnostics.Add(diagnostic);
        }

        return program;
    }

    /// <summary>
    /// A machine file that neither --machine nor ncx.toml names is reported as a mistake of the command line: the tool
    /// as its file and line 1, the one line of the command, as a usage error is reported (wave-1 question #79; D98).
    /// </summary>
    internal static void ReportMachineRequired(Diagnostics diagnostics)
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

    /// <summary>
    /// The controller of the machine file chooses the reader, one per controller family, from the registry
    /// (architecture 7; machine-config 1; code-guidelines 5, Strategy and Registry). A family without a reader is an
    /// ERROR on the program, which is then not read.
    /// </summary>
    internal static IReader? ReaderOf(MachineConfig machine, ReaderRegistry readers, Diagnostics diagnostics)
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
}
