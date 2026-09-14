using Ncx.Config;

namespace Ncx.Cli;

/// <summary>
/// Where ncx looks for a machine file and a cycle catalog: a machine file at the path given or by its name in the
/// machine folder that ncx.toml names, then in machines/ of the working directory, then in machines/ of the tool's own
/// folder, which holds the shipped files; a catalog in the cycle folders alone, in the same order (implementation 12,
/// P2-04; machine-config 6, 10; architecture 10).
/// </summary>
internal sealed class ProjectFolders
{
    // Machine files and cycle catalogs are TOML files; a name without the extension gets it (--machine nakamura-ntjx).
    private const string TomlExtension = ".toml";

    private readonly List<string> _machineFolders = [];
    private readonly List<string> _cycleFolders = [];

    /// <summary>
    /// The folders of one run.
    /// </summary>
    /// <param name="workingDirectory">The working directory, where ncx.toml is read and relative paths start.</param>
    /// <param name="toolFolder">The tool's own folder, whose machines/ and cycles/ hold the shipped files.</param>
    /// <param name="settings">ncx.toml of the working directory; null without one.</param>
    public ProjectFolders(string workingDirectory, string toolFolder, ProjectSettings? settings)
    {
        WorkingDirectory = Path.GetFullPath(workingDirectory);

        // --machine <name> is resolved in machines/ of the folder of ncx.toml, then of the working directory, then of
        // the tool's own folder (implementation 12, P2-04). ncx.toml lies in the working directory and names its
        // machine and cycle folders (architecture 10); the folders it leaves out are those of the project layout
        // (machine-config 10). A catalog is looked for in the cycle folders in the same order.
        if (settings is not null)
        {
            AddFolder(_machineFolders, Path.Combine(WorkingDirectory, settings.Machines));
            AddFolder(_cycleFolders, Path.Combine(WorkingDirectory, settings.Cycles));
        }

        AddFolder(_machineFolders, Path.Combine(WorkingDirectory, ProjectSettings.MachinesFolder));
        AddFolder(_machineFolders, Path.Combine(toolFolder, ProjectSettings.MachinesFolder));
        AddFolder(_cycleFolders, Path.Combine(WorkingDirectory, ProjectSettings.CyclesFolder));
        AddFolder(_cycleFolders, Path.Combine(toolFolder, ProjectSettings.CyclesFolder));
    }

    /// <summary>
    /// The working directory as a full path.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// The machine folders in the order they are searched.
    /// </summary>
    public IReadOnlyList<string> MachineFolders => _machineFolders;

    /// <summary>
    /// The cycle folders in the order they are searched.
    /// </summary>
    public IReadOnlyList<string> CycleFolders => _cycleFolders;

    /// <summary>
    /// The machine file that --machine or the machine key of ncx.toml names: a file at that path, or a machine of that
    /// name in the machine folders (implementation 12, P2-04).
    /// </summary>
    /// <param name="value">The value as written: "nakamura-ntjx", "nakamura-ntjx.toml", "shop/dmu50.toml".</param>
    /// <returns>The file; null when the value is a name that no machine folder holds.</returns>
    public FoundFile? FindMachine(string value)
    {
        // A file at the path given is that file, a relative path counted from the working directory, and a value with
        // a folder in it is always a path, which P1-07 read as the only form of --machine (architecture 10). Any other
        // value is a name, looked up in the machine folders (implementation 12, P2-04).
        // TODO(question): the phase file resolves --machine "in machines/ ... or by path" without saying which wins
        // when there is a file at the path given and a machine of that name as well; the file at the path wins, which
        // keeps every --machine that P1-07 read as a path.
        string atPath = Path.Combine(WorkingDirectory, value);
        if (File.Exists(atPath) || IsPath(value))
        {
            return new FoundFile { FullPath = atPath, Name = value };
        }

        return FindInFolders(value, _machineFolders);
    }

    /// <summary>
    /// The catalog file that [cycles] catalog of a machine names, looked for in the cycle folders alone
    /// (machine-config 6, 10).
    /// </summary>
    /// <param name="value">The value as the machine file writes it: "fanuc.toml".</param>
    /// <returns>The file; null when no cycle folder holds it.</returns>
    public FoundFile? FindCatalog(string value)
    {
        // The catalog is a file of a cycle folder (machine-config 6, 10), and the path rules of --machine are none of
        // its rules: a file of that name in the working directory, the machine file itself among them, is never the
        // catalog, and a value with a folder in it is a path from each cycle folder in turn (the TODO(question) of
        // RunMachine.WithCatalog).
        return FindInFolders(value, _cycleFolders);
    }

    /// <summary>
    /// A value as the name of a file with the extension of a machine file: "nakamura-ntjx" is "nakamura-ntjx.toml".
    /// </summary>
    public static string WithExtension(string value)
    {
        return value.EndsWith(TomlExtension, StringComparison.Ordinal) ? value : value + TomlExtension;
    }

    /// <summary>
    /// The folders as a message lists them, each by the name the user knows.
    /// </summary>
    public string Names(IReadOnlyList<string> folders)
    {
        var names = new List<string>();
        foreach (string folder in folders)
        {
            names.Add(NameOf(folder));
        }

        return string.Join(", ", names);
    }

    // A name is looked up in the folders in order, and the first folder that holds the file wins (implementation 12,
    // P2-04). The file lies in the folder: a value that leaves it, a full path or one that climbs out with "..", names
    // no file of that folder.
    private FoundFile? FindInFolders(string value, IReadOnlyList<string> folders)
    {
        foreach (string folder in folders)
        {
            string candidate = Path.Combine(folder, WithExtension(value));
            if (File.Exists(candidate) && LiesBelow(folder, candidate))
            {
                string fullPath = Path.GetFullPath(candidate);
                return new FoundFile { FullPath = fullPath, Name = NameOf(fullPath) };
            }
        }

        return null;
    }

    // A value with a folder in it, or a full path, names a file by its path; a rule of --machine alone (P1-07).
    private static bool IsPath(string value)
    {
        return value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || Path.IsPathRooted(value);
    }

    // A folder is searched once, at its first place in the order.
    private static void AddFolder(List<string> folders, string folder)
    {
        string fullPath = Path.GetFullPath(folder);
        if (!folders.Contains(fullPath))
        {
            folders.Add(fullPath);
        }
    }

    // A file found by name is named as the user finds it: by its path from the working directory when it lies below
    // it, machines/nakamura-ntjx.toml, else by its full path (D98).
    private string NameOf(string path)
    {
        return LiesBelow(WorkingDirectory, path) ? Path.GetRelativePath(WorkingDirectory, path) : path;
    }

    // A path lies below a folder when the way from the folder to it never climbs out of the folder.
    private static bool LiesBelow(string folder, string path)
    {
        string relative = Path.GetRelativePath(folder, path);
        return relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }
}
