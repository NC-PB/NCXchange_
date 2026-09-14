namespace Ncx.Cli.Commands;

/// <summary>
/// The project of a plugin that ncx plugin build and ncx plugin test work on: its name, which is the name of its DLL
/// and of the plugin (implementation 17, P7-01), its folder and its project file (implementation 17, P7-02).
/// </summary>
internal sealed record PluginProject
{
    /// <summary>
    /// The name of the project file without .csproj: MyShopRules.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The folder of the plugin, a full path.
    /// </summary>
    public required string Folder { get; init; }

    /// <summary>
    /// The project file, a full path.
    /// </summary>
    public required string File { get; init; }

    /// <summary>
    /// The project file as the user finds it from the working directory, MyShopRules/MyShopRules.csproj, which the
    /// diagnostics name (D98).
    /// </summary>
    public required string DisplayFile { get; init; }

    /// <summary>
    /// The plugin of a project file.
    /// </summary>
    /// <param name="projectFile">The project file.</param>
    /// <param name="workingDirectory">The working directory, from which the diagnostics name it.</param>
    public static PluginProject Of(string projectFile, string workingDirectory)
    {
        string fullPath = Path.GetFullPath(projectFile);
        string relative = Path.GetRelativePath(Path.GetFullPath(workingDirectory), fullPath);
        return new PluginProject
        {
            Name = Path.GetFileNameWithoutExtension(fullPath),
            Folder = Path.GetDirectoryName(fullPath) ?? fullPath,
            File = fullPath,
            DisplayFile = relative.StartsWith("..", StringComparison.Ordinal) ? fullPath : relative,
        };
    }
}
