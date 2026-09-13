using Ncx.Cli;
using Ncx.Cli.Commands;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// A working directory and a tool folder of their own in a temporary folder, for the tests of the machine of a run:
/// ncx.toml, --machine by name or by path, the cycle catalog of the machine (implementation 12, P2-04). ncx check runs
/// through CheckCommand.Run with these folders, so that no test depends on the working directory of the test process
/// (code-guidelines 8).
/// </summary>
internal sealed class ProjectHarness : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("ncx-project-");

    /// <summary>
    /// A working directory and a tool folder, both empty.
    /// </summary>
    public ProjectHarness()
        : this(null)
    {
    }

    /// <summary>
    /// An empty working directory and the tool folder given; null for an empty one.
    /// </summary>
    public ProjectHarness(string? toolFolder)
    {
        WorkingDirectory = Directory.CreateDirectory(Path.Combine(_folder.FullName, "work")).FullName;
        ToolFolder = toolFolder ?? Directory.CreateDirectory(Path.Combine(_folder.FullName, "tool")).FullName;
    }

    /// <summary>
    /// The working directory of the runs.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// The tool's own folder of the runs.
    /// </summary>
    public string ToolFolder { get; }

    /// <summary>
    /// What the last check wrote to the standard error: the diagnostics (D98).
    /// </summary>
    public string Error { get; private set; } = "";

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    /// <summary>
    /// Writes a file of the working directory, its folders created: "machines/mill.toml".
    /// </summary>
    public string WriteInWorkingDirectory(string relativePath, string text)
    {
        return Write(WorkingDirectory, relativePath, text);
    }

    /// <summary>
    /// Writes a file of the tool folder, its folders created: "machines/mill.toml".
    /// </summary>
    public string WriteInToolFolder(string relativePath, string text)
    {
        return Write(ToolFolder, relativePath, text);
    }

    /// <summary>
    /// Writes a file of the working directory byte for byte.
    /// </summary>
    public string WriteBytesInWorkingDirectory(string relativePath, byte[] bytes)
    {
        string path = Path.Combine(WorkingDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? WorkingDirectory);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>
    /// Runs ncx check on a file with --machine as given, null for none, in the working directory and with the tool
    /// folder of this harness; <see cref="Error"/> holds what it reported.
    /// </summary>
    /// <returns>The exit code (D97).</returns>
    public int Check(string file, string? machine)
    {
        using var error = new StringWriter();
        var settings = new RunSettings
        {
            File = file,
            MachineFile = machine,
            WorkingDirectory = WorkingDirectory,
            ToolFolder = ToolFolder,
        };

        int exitCode = CheckCommand.Run(settings, error);
        Error = error.ToString();
        return exitCode;
    }

    private static string Write(string folder, string relativePath, string text)
    {
        string path = Path.Combine(folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? folder);
        File.WriteAllText(path, text);
        return path;
    }
}
