using Ncx.Cli.Commands;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// A working directory of its own in a temporary folder for the ncx plugin commands, with the folder of ncx of the
/// test run as the tool folder: Ncx.Cli.dll lies next to the test assembly, with the Ncx assemblies a plugin is built
/// against and templates/ncx-plugin/, which the build copies there (implementation 17, P7-02). The commands run
/// through their Run methods with these folders, so that no test depends on the working directory of the test process
/// (code-guidelines 8).
/// </summary>
internal sealed class PluginWorkspace : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("ncx-plugin-");

    /// <summary>
    /// An empty working directory, and the folder of ncx of the test run.
    /// </summary>
    public PluginWorkspace()
        : this(ToolFolderOfTheTests())
    {
    }

    /// <summary>
    /// An empty working directory, and the tool folder given.
    /// </summary>
    public PluginWorkspace(string toolFolder)
    {
        WorkingDirectory = Directory.CreateDirectory(Path.Combine(_folder.FullName, "work")).FullName;
        ToolFolder = toolFolder;
    }

    /// <summary>
    /// The working directory of the commands, the folder of ncx.toml and plugins/.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// The folder of ncx of the commands.
    /// </summary>
    public string ToolFolder { get; }

    /// <summary>
    /// The command that starts dotnet; "dotnet" from the path unless a test names another.
    /// </summary>
    public string Dotnet { get; init; } = PluginCommandSettings.DotnetCommand;

    /// <summary>
    /// What the last command wrote to the standard output.
    /// </summary>
    public string Output { get; private set; } = "";

    /// <summary>
    /// What the last command wrote to the standard error: the diagnostics (D98).
    /// </summary>
    public string Error { get; private set; } = "";

    /// <summary>
    /// The settings of the commands, with --strict as given.
    /// </summary>
    public PluginCommandSettings Settings(bool strict = false)
    {
        return new PluginCommandSettings
        {
            WorkingDirectory = WorkingDirectory,
            ToolFolder = ToolFolder,
            Dotnet = Dotnet,
            Strict = strict,
        };
    }

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    /// <summary>
    /// ncx plugin new &lt;name&gt;.
    /// </summary>
    public int New(string name)
    {
        return Run((output, error) => PluginNew.Run(name, Settings(), output, error));
    }

    /// <summary>
    /// ncx plugin build [&lt;folder&gt;].
    /// </summary>
    public int Build(string? folder)
    {
        return Run((output, error) => PluginBuild.Run(folder, Settings(), output, error));
    }

    /// <summary>
    /// ncx plugin check &lt;dll&gt; [--strict].
    /// </summary>
    public int Check(string dll, bool strict = false)
    {
        return Run((output, error) => PluginCheck.Run(dll, Settings(strict), output, error));
    }

    /// <summary>
    /// ncx plugin test [&lt;folder&gt;].
    /// </summary>
    public int Test(string? folder)
    {
        return Run((output, error) => PluginTest.Run(folder, Settings(), output, error));
    }

    /// <summary>
    /// A path in the working directory.
    /// </summary>
    public string PathOf(params string[] parts)
    {
        return Path.Combine([WorkingDirectory, .. parts]);
    }

    /// <summary>
    /// Writes a file of the working directory, its folders created.
    /// </summary>
    public string Write(string relativePath, string text)
    {
        string path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? WorkingDirectory);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>
    /// The folder of ncx in the test run: the folder of Ncx.Cli.dll, next to the test assembly.
    /// </summary>
    public static string ToolFolderOfTheTests()
    {
        return Path.GetDirectoryName(typeof(PluginNew).Assembly.Location)
            ?? throw new InvalidOperationException("Ncx.Cli.dll has no folder.");
    }

    private int Run(Func<TextWriter, TextWriter, int> command)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exitCode = command(output, error);
        Output = output.ToString();
        Error = error.ToString();
        return exitCode;
    }
}
