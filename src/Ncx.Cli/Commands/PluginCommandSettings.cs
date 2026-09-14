namespace Ncx.Cli.Commands;

/// <summary>
/// What the ncx plugin commands work with: the working directory, where a new plugin is made and whose plugins/ and
/// ncx.toml a build fills, the folder of ncx with the template and the Ncx assemblies a plugin is built against, the
/// command of the .NET SDK, and --strict (architecture 10; code-guidelines 11; implementation 17, P7-02; D97).
/// </summary>
internal sealed record PluginCommandSettings
{
    /// <summary>
    /// The command of the .NET SDK, found on the path (implementation 17, P7-02, risks).
    /// </summary>
    public const string DotnetCommand = "dotnet";

    /// <summary>
    /// The working directory, the folder of ncx.toml and plugins/ (architecture 10, machine-config 10); the working
    /// directory of the process by default.
    /// </summary>
    public string WorkingDirectory { get; init; } = Directory.GetCurrentDirectory();

    /// <summary>
    /// The folder of ncx, which holds templates/ncx-plugin/ and the Ncx assemblies (code-guidelines 11; D106); the
    /// folder of ncx by default.
    /// </summary>
    public string ToolFolder { get; init; } = AppContext.BaseDirectory;

    /// <summary>
    /// The command that starts dotnet; dotnet from the path by default.
    /// </summary>
    public string Dotnet { get; init; } = DotnetCommand;

    /// <summary>
    /// --strict: a WARNING sets the exit code 1 (D97).
    /// </summary>
    public bool Strict { get; init; }

    /// <summary>
    /// The folder of ncx as a full path without a separator at its end, as the project files of a plugin name it.
    /// </summary>
    public string NcxFolder => Path.TrimEndingDirectorySeparator(Path.GetFullPath(ToolFolder));
}
