using Ncx.Config;
using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Cli;

/// <summary>
/// The machine a run of check, trace and annotate is checked against, with its cycle catalog: the machine of
/// --machine, else the one that ncx.toml of the working directory names, else the built-in default machine of D103
/// (architecture 10; machine-config 6, 10; implementation 12, P2-04).
/// </summary>
internal sealed record RunMachine
{
    /// <summary>
    /// The machine, with the catalog file of its controller family beneath its own [[cycle]] entries; null when
    /// ncx.toml, the machine file or its catalog could not be read or loads with an ERROR.
    /// </summary>
    public MachineConfig? Machine { get; init; }

    /// <summary>
    /// False when ncx.toml, the machine file or its cycle catalog cannot be found or read: the run does not start and
    /// the exit code is 2 (D97).
    /// </summary>
    public required bool InputsRead { get; init; }

    /// <summary>
    /// True when neither --machine nor ncx.toml names a machine file and the run gets the built-in default machine of
    /// D103, which convert and compile never run against (D77, architecture 10).
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// The machine file as the diagnostics name it, which the reports of ncx analyze name as well (implementation 14,
    /// risks); null for the built-in default machine.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// The machine file as it was found, its path and its name as the user knows it, next to which the tool table of
    /// D10 lies and after which out/&lt;machine&gt;/ is named (machine-config 1, 10); null for the built-in default
    /// machine and when no machine file could be read.
    /// </summary>
    public FoundFile? MachineFile { get; init; }

    /// <summary>
    /// ncx.toml of the working directory, which names the output folder of compile (architecture 10); null without
    /// one.
    /// </summary>
    public ProjectSettings? Project { get; init; }

    // A file that cannot be found or read: the run does not start, exit code 2 (D97).
    private static RunMachine NotRead => new() { InputsRead = false };

    // A file that reads but loads with an ERROR: the run stops before it starts, exit code 1 (D97, P1-07).
    private static RunMachine Stopped => new() { InputsRead = true };

    /// <summary>
    /// Reads and loads ncx.toml of the working directory, the machine file and its cycle catalog; every mistake is a
    /// diagnostic of its file (D98).
    /// </summary>
    /// <param name="settings">The file, the machine and the folders of the command line.</param>
    /// <param name="diagnostics">Where the diagnostics of the three files go, in the order they are read.</param>
    public static RunMachine Select(RunSettings settings, Diagnostics diagnostics)
    {
        // ncx.toml of the working directory names the machine file that --machine defaults to and the machine and
        // cycle folders (architecture 10, machine-config 10); without it the folders of the project layout apply. It
        // is an input like the others: one that cannot be read decides exit code 2, one that loads with an ERROR
        // stops the run (D97).
        ProjectSettings? project = null;
        string projectFile = Path.Combine(settings.WorkingDirectory, ProjectSettings.FileName);
        if (File.Exists(projectFile))
        {
            string? projectText = InputFile.Read(projectFile, ProjectSettings.FileName,
                DiagnosticCodes.ProjectFileUnreadable, "ncx.toml of the working directory", "architecture 10, D97",
                diagnostics);
            if (projectText is null)
            {
                return NotRead;
            }

            var projectDiagnostics = new Diagnostics(ProjectSettings.FileName);
            project = ProjectSettingsLoader.LoadText(WithoutByteOrderMark(projectText), projectDiagnostics);
            AddAll(diagnostics, projectDiagnostics);
            if (project is null)
            {
                return Stopped;
            }
        }

        // The machine of the run: --machine, else the machine that ncx.toml names, else the built-in default machine
        // of D103 (architecture 10, machine-config 10).
        var folders = new ProjectFolders(settings.WorkingDirectory, settings.ToolFolder, project);
        string? machineValue = settings.MachineFile ?? project?.Machine;
        if (machineValue is null)
        {
            return new RunMachine
            {
                Machine = DefaultMachine.Create(),
                InputsRead = true,
                IsDefault = true,
                Project = project,
            };
        }

        ProjectSettings? namedBy = settings.MachineFile is null ? project : null;
        FoundFile? machineFile = folders.FindMachine(machineValue);
        if (machineFile is null)
        {
            ReportMachineNotFound(machineValue, namedBy, folders, diagnostics);
            return NotRead;
        }

        string subject = namedBy is null ? "The machine file of --machine" : "The machine file that ncx.toml names";
        string? machineText = InputFile.Read(machineFile.FullPath, machineFile.Name,
            DiagnosticCodes.MachineFileUnreadable, subject, "D97, D103", diagnostics);
        if (machineText is null)
        {
            return NotRead;
        }

        // The mistakes of the machine file are diagnostics of the machine file on their lines, and a file with an ERROR
        // stops the run before it starts (P2-01, D97). A byte order mark is no part of the TOML text.
        var machineDiagnostics = new Diagnostics(machineFile.Name);
        MachineConfig? machine = MachineConfigLoader.LoadText(WithoutByteOrderMark(machineText), machineDiagnostics);
        AddAll(diagnostics, machineDiagnostics);
        if (machine is null)
        {
            return Stopped;
        }

        RunMachine withCatalog = WithCatalog(machine, machineFile, folders, diagnostics);
        return withCatalog with { FileName = machineFile.Name, MachineFile = machineFile, Project = project };
    }

    // A name that no machine folder holds and that is no file either is a missing machine file where one is named,
    // which decides exit code 2 (D97); it is reported on the value of --machine, or on the line of the machine key of
    // ncx.toml (D98).
    private static void ReportMachineNotFound(
        string value, ProjectSettings? namedBy, ProjectFolders folders, Diagnostics diagnostics)
    {
        string named = namedBy is null ? "--machine " + value : "The machine " + value + " of ncx.toml";
        diagnostics.Add(new Diagnostic
        {
            Severity = Severity.Error,
            File = namedBy is null ? value : ProjectSettings.FileName,
            Line = namedBy?.MachineLine ?? 1,
            Code = DiagnosticCodes.MachineNotFound,
            Message = $"{named} names no file at that path and no machine file "
                + $"{ProjectFolders.WithExtension(value)} in the machine folders "
                + $"{folders.Names(folders.MachineFolders)} (architecture 10, machine-config 10; D97).",
        });
    }

    // The catalog file that [cycles] catalog names goes beneath the [[cycle]] entries of the machine file
    // (machine-config 6). It is looked for in the cycle folders alone, and read and loaded like the machine file: one
    // that cannot be found or read decides exit code 2, one with an ERROR stops the run (machine-config 10, D97).
    // TODO(question): machine-config 6 and 10 name the catalog file of a machine and the cycle folder of the project,
    // and architecture 10 lets ncx.toml name the cycle folder, but no document says where else ncx looks for the
    // catalog, nor what a catalog that cannot be found means. It is looked for in the cycle folder of ncx.toml, then in
    // cycles/ of the working directory, then in cycles/ of the tool's own folder, the order of the machine folders, and
    // nowhere else: the path rules of --machine do not apply, so a file of that name in the working directory is never
    // the catalog, a value with a folder in it is a path from each cycle folder in turn, and a full path or a value
    // that climbs out with ".." names no file of them. One that is in none of them is an input that cannot be read
    // (D97).
    private static RunMachine WithCatalog(
        MachineConfig machine, FoundFile machineFile, ProjectFolders folders, Diagnostics diagnostics)
    {
        if (machine.Cycles?.CatalogFile is not string catalogValue
            || machine.Machine.Controller is not Controller controller)
        {
            return new RunMachine { Machine = machine, InputsRead = true };
        }

        FoundFile? catalogFile = folders.FindCatalog(catalogValue);
        if (catalogFile is null)
        {
            diagnostics.Add(new Diagnostic
            {
                Severity = Severity.Error,
                File = catalogValue,
                Line = 1,
                Code = DiagnosticCodes.CycleCatalogUnreadable,
                Message = $"The cycle catalog {ProjectFolders.WithExtension(catalogValue)} that [cycles] catalog of "
                    + $"{machineFile.Name} names is in none of the cycle folders "
                    + $"{folders.Names(folders.CycleFolders)} (machine-config 6, 10; D97).",
            });
            return NotRead;
        }

        string? catalogText = InputFile.Read(catalogFile.FullPath, catalogFile.Name,
            DiagnosticCodes.CycleCatalogUnreadable, "The cycle catalog of " + machineFile.Name,
            "machine-config 6, D97", diagnostics);
        if (catalogText is null)
        {
            return NotRead;
        }

        var catalogDiagnostics = new Diagnostics(catalogFile.Name);
        CycleCatalog? catalog =
            CycleCatalogLoader.LoadText(WithoutByteOrderMark(catalogText), controller, catalogDiagnostics);
        AddAll(diagnostics, catalogDiagnostics);
        if (catalog is null)
        {
            return Stopped;
        }

        return new RunMachine { Machine = CycleCatalogLoader.WithCatalog(machine, catalog), InputsRead = true };
    }

    // The diagnostics of one file after those reported before it (D98).
    private static void AddAll(Diagnostics diagnostics, Diagnostics fileDiagnostics)
    {
        foreach (Diagnostic diagnostic in fileDiagnostics.Items)
        {
            diagnostics.Add(diagnostic);
        }
    }

    // A byte order mark is no part of a TOML text, as File.ReadAllText, which the loaders use, drops it.
    private static string WithoutByteOrderMark(string text)
    {
        return text.StartsWith(InputFile.ByteOrderMark, StringComparison.Ordinal)
            ? text.Substring(InputFile.ByteOrderMark.Length)
            : text;
    }
}
