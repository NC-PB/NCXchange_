using System.CommandLine;
using System.Text;
using Ncx.Compilers;
using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx compile &lt;file.ncx&gt; --machine &lt;toml&gt; [--output &lt;folder&gt;] [--strict]: compiles an NCX file
/// for one machine with the compiler that the controller of the machine file chooses, and writes the output files
/// into out/&lt;machine&gt;/ of the working directory, or into the folder of --output (architecture 8, 10;
/// machine-config 10). compile always needs a machine file, from --machine or from ncx.toml, never the built-in
/// default machine (D77,
/// D103). The diagnostics go to the standard error (D98). Exit code 0 without an ERROR, 1 with one or with a WARNING
/// under --strict, 2 when the file, the machine file or its tool table cannot be read or no machine file is named
/// (D97).
/// </summary>
internal static class CompileCommand
{
    // A diagnostic about a whole file stands on its first line, as the loaders of Ncx.Config report one.
    private const int FileLine = 1;

    // The output files are UTF-8 text without a byte order mark, as every file ncx writes (language 3, Encoding).
    private static readonly UTF8Encoding s_utf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The compile command of the ncx root command (architecture 10).
    /// </summary>
    /// <param name="compilers">The compilers by controller family, registered in Program.cs.</param>
    /// <param name="error">The standard error, for the diagnostics.</param>
    public static Command Create(CompilerRegistry compilers, TextWriter error)
    {
        var fileArgument = new Argument<string>("file") { Description = "The NCX file." };
        var machineOption = new Option<string>("--machine")
        {
            Description = "The machine to compile for, by name in machines/ or by path; its controller chooses the "
                + "compiler. Without it: the machine that ncx.toml names. compile needs one of the two.",
            HelpName = "toml",
        };
        var outputOption = new Option<string>("--output")
        {
            Description = "Write the output files into this folder instead of out/<machine>/ of the working "
                + "directory.",
            HelpName = "folder",
        };

        // --strict, which every command accepts (architecture 10, D97).
        var strictOption = RunOptions.StrictOption();
        var command = new Command(
            "compile",
            "Compile an NCX file for one machine with the compiler of its controller, into out/<machine>/. Needs the "
            + "machine file, by --machine or by ncx.toml.")
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
            compilers,
            error));
        return command;
    }

    /// <summary>
    /// Compiles one NCX file: the parser, then the compiler of the machine's controller with its expander, its STATIC
    /// run and its writing (architecture 8, 10).
    /// </summary>
    /// <param name="settings">The file, the machine, the folders and --strict; the diagnostics carry the name of the
    /// file as the command line gives it (D98).</param>
    /// <param name="outputFolder">The folder of --output; null for out/&lt;machine&gt;/.</param>
    /// <param name="compilers">The compilers by controller family.</param>
    /// <param name="error">The standard error.</param>
    /// <returns>The exit code (D97).</returns>
    internal static int Run(RunSettings settings, string? outputFolder, CompilerRegistry compilers, TextWriter error)
    {
        var diagnostics = new Diagnostics(settings.File);

        // Code 2 is decided before the run starts and takes precedence: the NCX file, ncx.toml, the machine file and
        // its cycle catalog are read first, and one that cannot be found or read stops everything (D97, architecture
        // 10). All of them are read, so that all are reported.
        string? text = InputFile.Read(settings.File, DiagnosticCodes.InputUnreadable, "The file",
            "language 3, Encoding; D97", diagnostics);
        RunMachine runMachine = RunMachine.Select(settings, diagnostics);

        // compile always needs the machine file it compiles for, named by --machine or by the machine key of
        // ncx.toml, never the built-in default machine (D77, D103; architecture 10); a missing machine file where one
        // is required decides exit code 2 before the run starts (D97).
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
        // starts, with exit code 1 like any other ERROR (D97). So does a controller that ncx has no compiler for.
        if (runMachine.Machine is not MachineConfig machine
            || CompilerOf(machine, compilers, diagnostics) is not ICompiler compiler)
        {
            error.Write(diagnostics.ToText());
            return ExitCodes.Error;
        }

        // The tool table is an input like the others: one that is there and cannot be read decides exit code 2, one
        // that loads with an ERROR stops the run (D97); one that is not there is no error (D10).
        CompileOptions? options = OptionsWithToolTable(machine, runMachine.MachineFile, diagnostics);
        if (options is null)
        {
            error.Write(diagnostics.ToText());
            return ExitCodes.NotStarted;
        }

        if (diagnostics.HasErrors)
        {
            error.Write(diagnostics.ToText());
            return ExitCodes.Error;
        }

        // Parse, then compile: the compiler expands, runs the virtual machine STATIC and writes (architecture 8). A
        // byte order mark is no part of the text the parser reads (wave-1 question #80).
        // TODO: the program rewriters and block writers of the plugins that ncx.toml names join the options here
        // (P7-01, D106).
        NcxProgram program = Parser.Parse(WithoutByteOrderMark(text), settings.File, new ParserOptions());
        CompileResult result = compiler.Compile(program, machine, options);
        foreach (Diagnostic diagnostic in result.Diagnostics.Items)
        {
            diagnostics.Add(diagnostic);
        }

        // The files go into out/<machine>/ of the working directory, or into the folder of --output (architecture 10,
        // machine-config 10); a compile that an ERROR stopped has none (virtual machine 2.9).
        // TODO(question): architecture 10 writes the NC file "under out/<machine>/" and names no --output for compile,
        // while format and convert take --output as a file; a compile under file_per_program writes several files. The
        // --output of compile names the folder the files go into, until that is answered.
        string folder = outputFolder is not null
            ? Path.Combine(settings.WorkingDirectory, outputFolder)
            : Path.Combine(settings.WorkingDirectory, runMachine.Project?.Out ?? ProjectSettings.OutFolder,
                MachineFolderOf(runMachine.MachineFile));
        WriteFiles(result.Files, folder, diagnostics);
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
            Code = DiagnosticCodes.CompileMachineRequired,
            Message = "compile needs the machine file of the machine it compiles for: name it with --machine or with "
                + "the machine key of ncx.toml; compile never runs against the built-in default machine (D77, D103; "
                + "architecture 10).",
        });
    }

    // The controller of the machine file chooses the compiler, one per controller family, from the registry
    // (architecture 8; machine-config 1; code-guidelines 5, Strategy and Registry). A family without a compiler is an
    // ERROR on the file, which is then not compiled.
    private static ICompiler? CompilerOf(MachineConfig machine, CompilerRegistry compilers, Diagnostics diagnostics)
    {
        if (machine.Machine.Controller is Controller controller && compilers.Create(controller) is ICompiler compiler)
        {
            return compiler;
        }

        string family = machine.Machine.Controller is Controller named ? $"the controller {named}" : "no controller";
        diagnostics.Error(FileLine, DiagnosticCodes.NoCompilerForController,
            $"The machine \"{machine.Machine.Name}\" has {family}, and ncx has no compiler for it, so the file is not "
            + "compiled (architecture 8; machine-config 1).");
        return null;
    }

    // The tool table that [machine] tool_table names lies next to the machine file (D10), and the warning block of D10
    // names it as the user finds it. Null when it is there and cannot be read.
    private static CompileOptions? OptionsWithToolTable(MachineConfig machine, FoundFile? machineFile,
        Diagnostics diagnostics)
    {
        if (machine.ToolTable is not string toolTable || machineFile is null)
        {
            return new CompileOptions();
        }

        string path = Path.Combine(Path.GetDirectoryName(machineFile.FullPath) ?? "", toolTable);
        string name = Path.Combine(Path.GetDirectoryName(machineFile.Name) ?? "", toolTable);
        if (!File.Exists(path))
        {
            return new CompileOptions { ToolTableFile = name };
        }

        string? text = InputFile.Read(path, name, DiagnosticCodes.ToolTableUnreadable, "The tool table of the machine",
            "machine-config 1, D10, D97", diagnostics);
        if (text is null)
        {
            return null;
        }

        var tableDiagnostics = new Diagnostics(name);
        ToolTable? table = ToolTable.LoadText(WithoutByteOrderMark(text), tableDiagnostics);
        foreach (Diagnostic diagnostic in tableDiagnostics.Items)
        {
            diagnostics.Add(diagnostic);
        }

        return new CompileOptions { ToolTable = table, ToolTableFile = name };
    }

    // out/<machine>/ is named after the machine file: machines/dmu50.toml compiles into out/dmu50/ (machine-config 10).
    private static string MachineFolderOf(FoundFile? machineFile)
    {
        return machineFile is null ? "" : Path.GetFileNameWithoutExtension(machineFile.FullPath);
    }

    // Every output file into the folder, which is created when it is not there; a file that cannot be written is an
    // ERROR of the run, which has started, so the exit code is 1 (D97, architecture 10; code-guidelines 6).
    private static void WriteFiles(IReadOnlyList<CompiledFile> files, string folder, Diagnostics diagnostics)
    {
        foreach (CompiledFile file in files)
        {
            string path = Path.Combine(folder, file.Name);
            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(path, file.Text, s_utf8);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                or ArgumentException)
            {
                diagnostics.Add(new Diagnostic
                {
                    Severity = Severity.Error,
                    File = path,
                    Line = FileLine,
                    Code = DiagnosticCodes.OutputUnwritable,
                    Message = $"The compiled program cannot be written into the file: "
                        + $"{exception.Message.TrimEnd('.')} (architecture 10).",
                });
            }
        }
    }

    // A byte order mark is no part of the text the parser and the TOML loaders read (wave-1 question #80).
    private static string WithoutByteOrderMark(string text)
    {
        return text.StartsWith(InputFile.ByteOrderMark, StringComparison.Ordinal)
            ? text.Substring(InputFile.ByteOrderMark.Length)
            : text;
    }
}
