using Ncx.Config;
using Ncx.Core.Expander;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Plugins;

namespace Ncx.Cli;

/// <summary>
/// The stages that ncx check, trace, annotate and analyze share, in the order of virtual machine 1 and architecture 10:
/// read the file and the machine file, load the machine, parse, expand, run STATIC (or INTERPRETED for trace
/// --interpreted and analyze), collect the diagnostics. The commands differ only in the listeners they subscribe and
/// in what they write of the run.
/// </summary>
internal static class Pipeline
{
    // An NCX file, and the vars file of part-123.ncx, part-123.vars.toml (machine-config 8, 10).
    private const string NcxExtension = ".ncx";
    private const string VarsExtension = ".vars.toml";

    /// <summary>
    /// Runs the stages for one NCX file.
    /// </summary>
    /// <param name="settings">The file, the machine and the shared options of the command line.</param>
    /// <param name="listeners">The listeners of the run, which trace and annotate subscribe; none for check.</param>
    public static PipelineRun Run(RunSettings settings, IReadOnlyList<IVmListener> listeners)
    {
        return Run(settings, (_, _) => listeners);
    }

    /// <summary>
    /// Runs the stages for one NCX file with listeners made for the machine of the run, which the analytics of ncx
    /// analyze read (architecture 9; machine-config 4, 5).
    /// </summary>
    /// <param name="settings">The file, the machine and the shared options of the command line.</param>
    /// <param name="listenersFor">Makes the listeners of the run once the machine is loaded, from the machine and the
    /// name of its file, null for the built-in default machine; not called when the run does not start.</param>
    public static PipelineRun Run(RunSettings settings,
        Func<MachineConfig, string?, IReadOnlyList<IVmListener>> listenersFor)
    {
        var diagnostics = new Diagnostics(settings.File);

        // Code 2 is decided before the run starts and takes precedence: the NCX file, ncx.toml, the machine file and
        // its cycle catalog are read first, and one that cannot be found or read stops everything (D97, architecture
        // 10; P1-07, P2-04). All of them are read, so that all are reported.
        string? text = InputFile.Read(settings.File, DiagnosticCodes.InputUnreadable, "The file",
            "language 3, Encoding", diagnostics);
        RunMachine runMachine = RunMachine.Select(settings, diagnostics);

        // An INTERPRETED run starts its variables from the vars file that --vars names, or from <file>.vars.toml next
        // to the file when there is one (virtual machine 2.7, 3.6; machine-config 8, 10). It is an input like the
        // others: one that cannot be read decides exit code 2 (D97).
        string? varsFile = settings.Interpreted ? VarsFileOf(settings) : null;
        string? varsText = null;
        if (varsFile is not null)
        {
            varsText = InputFile.Read(varsFile, DiagnosticCodes.VarsFileUnreadable, "The vars file",
                "virtual machine 2.7, 3.6; machine-config 8", diagnostics);
        }

        if (text is null || !runMachine.InputsRead || (varsFile is not null && varsText is null))
        {
            return new PipelineRun { Diagnostics = diagnostics, InputsRead = false };
        }

        // ncx.toml, a machine file or a cycle catalog that reads but loads with an ERROR stops the run before it
        // starts, with exit code 1 like any other ERROR (D97; P1-07). So does a vars file with an ERROR (P4-01).
        if (runMachine.Machine is not MachineConfig machine)
        {
            return new PipelineRun { Diagnostics = diagnostics, InputsRead = true };
        }

        IReadOnlyDictionary<string, Value>? startValues = null;
        if (varsFile is not null && varsText is not null)
        {
            startValues = LoadVars(varsFile, varsText, diagnostics);
            if (startValues is null)
            {
                return new PipelineRun { Diagnostics = diagnostics, InputsRead = true };
            }
        }

        // The plugins of the working directory, whose rewriters join the expander and whose listeners join the virtual
        // machine (virtual machine 7; architecture 9, 10). What they report goes into the diagnostics of the run, and
        // a plugin that fails is left out while the run goes on (implementation 17, P7-01).
        PluginSet plugins = RunPlugins.Load(settings, runMachine.Project, diagnostics);

        // Parse. A byte order mark is no part of the text the parser reads; annotate puts it back in front of its copy,
        // as ncx format does (wave-1 question #80).
        string byteOrderMark = text.StartsWith(InputFile.ByteOrderMark, StringComparison.Ordinal)
            ? InputFile.ByteOrderMark
            : "";
        NcxProgram program = Parser.Parse(text.Substring(byteOrderMark.Length), settings.File, new ParserOptions());

        // Expand: the expansion rules of the machine and the program rewriters of the plugins turn into generated NCX
        // blocks around the blocks that trigger them, so that the virtual machine executes what the machine will
        // really do (virtual machine 1, architecture 5.5, D63). The expander returns the program unexpanded when the
        // parser reported an ERROR.
        NcxProgram expanded = Expander.Expand(program, machine, plugins.Rewriters);
        bool runnable = !expanded.Diagnostics.HasErrors;

        // Run STATIC, the mode of check (virtual machine 1, D91), or INTERPRETED under trace --interpreted and for
        // analyze (virtual machine 1, 3.6), with the run options of the command line (D37, D53) over the options the
        // machine file gives (machine-config 7). An ERROR of the parser or the expander stops the run before its first
        // block (virtual machine 2.9). An INTERPRETED run loads the external programs its CALLs name from the working
        // directory (virtual machine 3.6).
        VmOptions options = VmOptions.ForMachine(machine) with
        {
            SkipBlocks = settings.SkipBlocks,
            ExpandCycles = settings.ExpandCycles,
        };
        ExecutionMode mode = settings.Interpreted ? ExecutionMode.Interpreted : ExecutionMode.Static;
        var vm = new VirtualMachine(machine, options, expanded.Diagnostics, mode, startValues)
        {
            ExternalPrograms = settings.Interpreted
                ? name => LoadExternalProgram(name, settings.WorkingDirectory, machine, plugins.Rewriters)
                : null,
        };
        foreach (IVmListener listener in listenersFor(machine, runMachine.FileName))
        {
            vm.Subscribe(listener);
        }

        // The listeners of the plugins read every event after the listeners of the command (virtual machine 7).
        foreach (IVmListener listener in plugins.Listeners)
        {
            vm.Subscribe(listener);
        }

        vm.Run(expanded);

        // Collect: what the parser, the expander and the virtual machine reported, in that order, after the machine
        // file (D98).
        foreach (Diagnostic diagnostic in expanded.Diagnostics.Items)
        {
            diagnostics.Add(diagnostic);
        }

        return new PipelineRun
        {
            Diagnostics = diagnostics,
            InputsRead = true,
            Program = runnable ? expanded : null,
            ByteOrderMark = byteOrderMark,
        };
    }

    // The vars file of an INTERPRETED run: the one --vars names, or the file's own, <file>.vars.toml next to it, when
    // there is one (virtual machine 3.6: "when present"; machine-config 10: part-123.ncx and part-123.vars.toml).
    private static string? VarsFileOf(RunSettings settings)
    {
        if (settings.VarsFile is string varsFile)
        {
            return varsFile;
        }

        return OwnVarsFile(settings.File);
    }

    /// <summary>
    /// The vars file of an NCX file, &lt;file&gt;.vars.toml next to it, when there is one (virtual machine 3.6;
    /// machine-config 10); the files of a job have one each (JobPipeline).
    /// </summary>
    internal static string? OwnVarsFile(string file)
    {
        string ownVarsFile = Path.ChangeExtension(file, VarsExtension);
        return File.Exists(ownVarsFile) ? ownVarsFile : null;
    }

    // The start values of the variables: numbers and strings by variable name (machine-config 8), whose mistakes are
    // diagnostics of the vars file on their lines (P2-01). Null when the vars file has an ERROR.
    internal static IReadOnlyDictionary<string, Value>? LoadVars(string varsFile, string varsText,
        Diagnostics diagnostics)
    {
        var varsDiagnostics = new Diagnostics(varsFile);
        VarsFile? vars = VarsFile.LoadText(WithoutByteOrderMark(varsText), varsDiagnostics);
        foreach (Diagnostic diagnostic in varsDiagnostics.Items)
        {
            diagnostics.Add(diagnostic);
        }

        return vars?.Variables;
    }

    // The external program of CALL="name" in an INTERPRETED run, searched in the working directory (virtual machine
    // 3.6), parsed and expanded like the file itself (virtual machine 1), with what reading it found under its own file
    // name (2.9). Null when the working directory holds none that can be read as UTF-8 text, which the virtual machine
    // reports on the CALL.
    // TODO(question): language 4.9 calls an external program by its file name (CALL="O9010") and virtual machine 3.6
    // searches the working directory, and neither says whether the name carries the extension of the NCX file; the name
    // is taken as written, and with .ncx when the working directory holds no file of that name, until that is answered.
    // TODO: the program rewriters of the plugins expand the external program like the file itself, but what a plugin
    // reports about a block of it names the file of the run, whose diagnostics the plugins report into.
    internal static NcxProgram? LoadExternalProgram(string name, string workingDirectory, MachineConfig machine,
        IReadOnlyList<IProgramRewriter> rewriters)
    {
        string path = Path.Combine(workingDirectory, name);
        if (!File.Exists(path) && File.Exists(path + NcxExtension))
        {
            path += NcxExtension;
        }

        string fileName = Path.GetFileName(path);
        var reading = new Diagnostics(fileName);
        if (!File.Exists(path)
            || InputFile.Read(path, DiagnosticCodes.InputUnreadable, "The external program", "virtual machine 3.6",
                reading) is not string text)
        {
            return null;
        }

        NcxProgram program = Parser.Parse(WithoutByteOrderMark(text), fileName, new ParserOptions());
        return Expander.Expand(program, machine, rewriters);
    }

    // A byte order mark is no part of the text the parser and the TOML loaders read (wave-1 question #80).
    internal static string WithoutByteOrderMark(string text)
    {
        return text.StartsWith(InputFile.ByteOrderMark, StringComparison.Ordinal)
            ? text.Substring(InputFile.ByteOrderMark.Length)
            : text;
    }
}
