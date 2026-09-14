using Ncx.Core.Model;

namespace Ncx.Cli.Commands;

/// <summary>
/// Finds the plugin project that ncx plugin build and ncx plugin test work on: the project of the folder given, else
/// the one of the working directory, else the one that ncx plugin new made in a folder of the working directory
/// (implementation 17, P7-02). None found is a missing input, which decides exit code 2 (D97).
/// </summary>
internal static class PluginFolder
{
    // A plugin is a C# project (code-guidelines 11).
    private const string ProjectPattern = "*.csproj";
    private const string ProjectExtension = ".csproj";

    /// <summary>
    /// The plugin project of a command.
    /// </summary>
    /// <param name="folder">The folder the command line names; null for none.</param>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="command">build or test, which the diagnostics name.</param>
    /// <param name="diagnostics">Where a project that is not found is reported.</param>
    /// <returns>The project; null when none is found.</returns>
    public static PluginProject? Find(string? folder, string workingDirectory, string command, Diagnostics diagnostics)
    {
        // A folder the command line names is the folder of the plugin and holds its one project file.
        if (folder is not null)
        {
            if (OnlyProjectIn(Path.Combine(workingDirectory, folder)) is string named)
            {
                return PluginProject.Of(named, workingDirectory);
            }

            diagnostics.Error(Program.CommandLine, DiagnosticCodes.PluginProjectNotFound,
                $"The folder {folder} holds no project file of a plugin, or more than one; ncx plugin {command} takes "
                + "the folder that ncx plugin new made (architecture 10).");
            return null;
        }

        // TODO(question): the six steps of code-guidelines 11 run ncx plugin build after ncx plugin new, in the same
        // folder and without naming the plugin, and no document says which plugin build and test take without a
        // folder. The working directory is the folder of the plugin when it holds a project file; else the one folder
        // of the working directory that ncx plugin new made, which holds a project file of its own name. Several of
        // them, or none, are reported, until that is answered.
        if (OnlyProjectIn(workingDirectory) is string own)
        {
            return PluginProject.Of(own, workingDirectory);
        }

        List<string> made = MadeByPluginNew(workingDirectory);
        if (made.Count == 1)
        {
            return PluginProject.Of(made[0], workingDirectory);
        }

        var names = new List<string>();
        foreach (string project in made)
        {
            names.Add(Path.GetFileNameWithoutExtension(project));
        }

        string message = names.Count == 0
            ? "The working directory holds no plugin project, and none of its folders a plugin that ncx plugin new "
                + $"made; make one with ncx plugin new <name>, or name its folder: ncx plugin {command} <folder> "
                + "(architecture 10)."
            : $"The working directory holds the plugins {string.Join(", ", names)}, which ncx plugin new made; name "
                + $"the one to {command}: ncx plugin {command} {names[0]} (architecture 10).";
        diagnostics.Error(Program.CommandLine, DiagnosticCodes.PluginProjectNotFound, message);
        return null;
    }

    // The one project file of a folder; null when the folder is not there or holds none or several.
    private static string? OnlyProjectIn(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return null;
        }

        string[] projects = Directory.GetFiles(folder, ProjectPattern);
        return projects.Length == 1 ? projects[0] : null;
    }

    // The project files of the folders of the working directory that ncx plugin new made: MyShopRules/
    // MyShopRules.csproj, in the ordinal order of their names.
    private static List<string> MadeByPluginNew(string workingDirectory)
    {
        var projects = new List<string>();
        foreach (string folder in Directory.GetDirectories(workingDirectory))
        {
            string project = Path.Combine(folder, Path.GetFileName(folder) + ProjectExtension);
            if (File.Exists(project))
            {
                projects.Add(project);
            }
        }

        projects.Sort(StringComparer.Ordinal);
        return projects;
    }
}
