using Ncx.Config;
using Ncx.Core.Expander;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Cli;

/// <summary>
/// The stages that ncx check, trace and annotate share, in the order of virtual machine 1 and architecture 10: read the
/// file and the machine file, load the machine, parse, expand, run STATIC, collect the diagnostics. The three commands
/// differ only in the listeners they subscribe and in what they write of the run.
/// </summary>
internal static class Pipeline
{
    /// <summary>
    /// Runs the stages for one NCX file.
    /// </summary>
    /// <param name="settings">The file, the machine and the shared options of the command line.</param>
    /// <param name="listeners">The listeners of the run, which trace and annotate subscribe; none for check.</param>
    public static PipelineRun Run(RunSettings settings, IReadOnlyList<IVmListener> listeners)
    {
        var diagnostics = new Diagnostics(settings.File);

        // Code 2 is decided before the run starts and takes precedence: the NCX file and the machine file of --machine
        // are read first, and one that cannot be found or read stops everything (D97, architecture 10; P1-07). Both are
        // read, so that both are reported.
        string? text = InputFile.Read(settings.File, DiagnosticCodes.InputUnreadable, "The file",
            "language 3, Encoding", diagnostics);
        string? machineText = null;
        if (settings.MachineFile is string machineFile)
        {
            machineText = InputFile.Read(machineFile, DiagnosticCodes.MachineFileUnreadable,
                "The machine file of --machine", "D97, D103", diagnostics);
        }

        if (text is null || (settings.MachineFile is not null && machineText is null))
        {
            return new PipelineRun { Diagnostics = diagnostics, InputsRead = false };
        }

        // A machine file that reads but loads with an ERROR stops the run before it starts, with exit code 1 like any
        // other ERROR (D97; P1-07).
        MachineConfig? machine = LoadMachine(settings.MachineFile, machineText, diagnostics);
        if (machine is null)
        {
            return new PipelineRun { Diagnostics = diagnostics, InputsRead = true };
        }

        // Parse. A byte order mark is no part of the text the parser reads; annotate puts it back in front of its copy,
        // as ncx format does (wave-1 question #80).
        string byteOrderMark = text.StartsWith(InputFile.ByteOrderMark, StringComparison.Ordinal)
            ? InputFile.ByteOrderMark
            : "";
        NcxProgram program = Parser.Parse(text.Substring(byteOrderMark.Length), settings.File, new ParserOptions());

        // Expand: the expansion rules of the machine turn into generated NCX blocks around the blocks that trigger
        // them, so that the virtual machine executes what the machine will really do (virtual machine 1, architecture
        // 5.5, D63). The expander returns the program unexpanded when the parser reported an ERROR.
        // TODO: the program rewriters of the plugins that ncx.toml names join the expander here (P7-01, D106).
        NcxProgram expanded = Expander.Expand(program, machine, []);
        bool runnable = !expanded.Diagnostics.HasErrors;

        // Run STATIC, the mode of check (virtual machine 1, D91), with the run options of the command line (D37, D53)
        // over the options the machine file gives (machine-config 7). An ERROR of the parser or the expander stops the
        // run before its first block (virtual machine 2.9).
        VmOptions options = VmOptions.ForMachine(machine) with
        {
            SkipBlocks = settings.SkipBlocks,
            ExpandCycles = settings.ExpandCycles,
        };
        var vm = new VirtualMachine(machine, options, expanded.Diagnostics);
        foreach (IVmListener listener in listeners)
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

    // The machine the run is checked against: without --machine the built-in default machine (D103, virtual machine
    // 3.8), with it the machine file at the path given, whose mistakes are diagnostics of the machine file on their
    // lines (P2-01). Null when the machine file has an ERROR.
    // TODO: P2-04 part two resolves --machine by name in machines/, takes the machine that ncx.toml names when
    // --machine is not given, and loads the catalog file that [cycles] catalog names from the cycles folder beneath
    // the [[cycle]] entries of the machine file (machine-config 6, 10; CycleCatalogLoader.WithCatalog).
    private static MachineConfig? LoadMachine(string? machineFile, string? machineText, Diagnostics diagnostics)
    {
        if (machineFile is null || machineText is null)
        {
            return DefaultMachine.Create();
        }

        // A byte order mark is no part of the TOML text, as File.ReadAllText, which MachineConfigLoader.Load uses,
        // drops it.
        string toml = machineText.StartsWith(InputFile.ByteOrderMark, StringComparison.Ordinal)
            ? machineText.Substring(InputFile.ByteOrderMark.Length)
            : machineText;
        var machineDiagnostics = new Diagnostics(machineFile);
        MachineConfig? machine = MachineConfigLoader.LoadText(toml, machineDiagnostics);
        foreach (Diagnostic diagnostic in machineDiagnostics.Items)
        {
            diagnostics.Add(diagnostic);
        }

        return machine;
    }
}
