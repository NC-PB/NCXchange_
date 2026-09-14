using System.Globalization;
using System.Text;
using Ncx.Config;
using Ncx.Core.Model;
using Ncx.Plugins;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx plugin build [&lt;folder&gt;]: runs dotnet build on a plugin against the Ncx assemblies of the ncx that runs,
/// copies its DLL into plugins/ of the working directory and adds its line to ncx.toml, so that the user copies
/// nothing (code-guidelines 11, step 5; implementation 17, P7-02). The DLL and the line of ncx.toml are named on the
/// standard output. Exit code 0; 1 when the build fails or its result cannot be written; 2 when there is no plugin to
/// build or ncx.toml cannot be read (D97).
/// </summary>
internal static class PluginBuild
{
    // A DLL is named after its project: MyShopRules.dll.
    private const string DllExtension = ".dll";

    // The symbols of a DLL, which the plugin loader reads with it when they lie next to it (implementation 17, P7-01).
    private const string SymbolsExtension = ".pdb";

    // A diagnostic about a whole file stands on its first line.
    private const int FileLine = 1;

    // ncx plugin build builds into a folder of the plugin's own below bin/, which a project leaves out of its files.
    private static readonly string s_buildFolder = Path.Combine("bin", "ncx");

    // ncx.toml stays UTF-8 text, with its byte order mark only when it had one (language 3, Encoding).
    private static readonly UTF8Encoding s_utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Builds a plugin into plugins/ and registers it in ncx.toml.
    /// </summary>
    /// <param name="folder">The folder of the plugin; null for the one PluginFolder finds.</param>
    /// <param name="settings">The working directory, the folder of ncx, dotnet and --strict.</param>
    /// <param name="output">The standard output, for the DLL and the line of ncx.toml.</param>
    /// <param name="error">The standard error, for what a failed build wrote and the diagnostics (D98).</param>
    /// <returns>The exit code (D97).</returns>
    internal static int Run(string? folder, PluginCommandSettings settings, TextWriter output, TextWriter error)
    {
        var diagnostics = new Diagnostics(Program.ToolName);
        PluginProject? project = PluginFolder.Find(folder, settings.WorkingDirectory, "build", diagnostics);
        if (project is null)
        {
            error.Write(diagnostics.ToText());
            return ExitCodes.NotStarted;
        }

        // ncx.toml is an input like the others and is read before the build: one that cannot be read decides exit
        // code 2, one that loads with an ERROR stops the run (D97; architecture 10), so that the line can join it
        // afterwards.
        string ncxTomlPath = Path.Combine(settings.WorkingDirectory, ProjectSettings.FileName);
        string? ncxToml = null;
        if (File.Exists(ncxTomlPath))
        {
            ncxToml = InputFile.Read(ncxTomlPath, ProjectSettings.FileName, DiagnosticCodes.ProjectFileUnreadable,
                "ncx.toml of the working directory", "architecture 10, D97", diagnostics);
            if (ncxToml is null)
            {
                error.Write(diagnostics.ToText());
                return ExitCodes.NotStarted;
            }

            if (!Loads(WithoutByteOrderMark(ncxToml), diagnostics))
            {
                error.Write(diagnostics.ToText());
                return ExitCodes.Error;
            }
        }

        // dotnet build of the plugin against the Ncx assemblies of this ncx, which a run shares with the plugin (D106;
        // implementation 17, P7-02, risks), into a folder of its own; no build server of dotnet stays behind.
        string buildFolder = Path.Combine(project.Folder, s_buildFolder);
        DotnetResult build = DotnetProcess.Run(settings.Dotnet,
            ["build", project.File, "--nologo", "--disable-build-servers", "--output", buildFolder,
                "-p:NcxFolder=" + settings.NcxFolder],
            settings.WorkingDirectory);
        string dll = Path.Combine(buildFolder, project.Name + DllExtension);
        if (!Built(build, project, dll, diagnostics, error))
        {
            error.Write(diagnostics.ToText());
            return ExitCodes.Error;
        }

        // Code-guidelines 11, step 5: the command puts the DLL into plugins/ of the working directory, where every run
        // loads it (implementation 17, P7-01), and adds its line to ncx.toml.
        string plugin = Path.Combine(PluginLoader.PluginsFolder, project.Name + DllExtension);
        if (CopyIntoPlugins(dll, Path.Combine(settings.WorkingDirectory, plugin), plugin, diagnostics))
        {
            output.Write(plugin + "\n");
            AddLine(ncxToml, project.Name + DllExtension, ncxTomlPath, output, diagnostics);
        }

        error.Write(diagnostics.ToText());
        return ExitCodes.OfRun(diagnostics, settings.Strict);
    }

    // A build that dotnet ran to its end and that left the DLL; else what went wrong is reported, and what dotnet
    // wrote goes first, the errors of the compiler among it (implementation 17, P7-02).
    private static bool Built(DotnetResult build, PluginProject project, string dll, Diagnostics diagnostics,
        TextWriter error)
    {
        if (build.Failure is string failure)
        {
            DotnetProcess.ReportNotStarted(failure, diagnostics);
            return false;
        }

        if (build.ExitCode != 0)
        {
            error.Write(build.Output);
            string exitCode = build.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "";
            Report(diagnostics, Severity.Error, project.DisplayFile, DiagnosticCodes.PluginBuildFailed,
                $"dotnet build ended with exit code {exitCode}, so the plugin is not built; what it wrote stands "
                + "above (implementation 17, P7-02).");
            return false;
        }

        if (!File.Exists(dll))
        {
            Report(diagnostics, Severity.Error, project.DisplayFile, DiagnosticCodes.PluginNotBuilt,
                $"dotnet build left no {Path.GetFileName(dll)}, so there is nothing to put into plugins/; the DLL of a "
                + "plugin is named after its project (implementation 17, P7-02).");
            return false;
        }

        return true;
    }

    // The DLL, and its symbols when the build left them, into plugins/; a file that cannot be written is an ERROR of
    // the run, which has started (D97).
    // TODO(question): no document says what ncx plugin build does with the assemblies a plugin brings, a package it
    // references among them (D106: the load context isolates everything else the plugin brings). Only the DLL of the
    // plugin goes into plugins/, where every DLL is a plugin (implementation 17, P7-01), until that is answered.
    private static bool CopyIntoPlugins(string dll, string target, string plugin, Diagnostics diagnostics)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? PluginLoader.PluginsFolder);
            File.Copy(dll, target, overwrite: true);
            string symbols = Path.ChangeExtension(dll, SymbolsExtension);
            if (File.Exists(symbols))
            {
                File.Copy(symbols, Path.ChangeExtension(target, SymbolsExtension), overwrite: true);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Report(diagnostics, Severity.Error, plugin, DiagnosticCodes.OutputUnwritable,
                $"The DLL of the plugin cannot be written into plugins/: {exception.Message.TrimEnd('.')} "
                + "(architecture 10).");
            return false;
        }
    }

    // Code-guidelines 11, step 5: the command adds the line of the plugin to ncx.toml, the list of the plugin
    // assemblies of machine-config 10 (NcxTomlPlugins, and its TODO(question) of D238). A file the line cannot join
    // stays as it is, with an INFO, since the DLL in plugins/ loads without it (implementation 17, P7-01).
    private static void AddLine(string? ncxToml, string dll, string path, TextWriter output, Diagnostics diagnostics)
    {
        bool hasByteOrderMark = ncxToml is not null
            && ncxToml.StartsWith(InputFile.ByteOrderMark, StringComparison.Ordinal);
        string byteOrderMark = hasByteOrderMark ? InputFile.ByteOrderMark : "";
        string? text = ncxToml?.Substring(byteOrderMark.Length);
        if (text is not null && NcxTomlPlugins.Lists(text, dll))
        {
            output.Write(ProjectSettings.FileName + ": " + NcxTomlPlugins.KeyLine(text) + "\n");
            return;
        }

        string? changed = NcxTomlPlugins.Add(text, dll);
        if (changed is null)
        {
            Report(diagnostics, Severity.Info, ProjectSettings.FileName, DiagnosticCodes.PluginLineNotAdded,
                $"ncx.toml holds its plugins in a form the line of {dll} cannot join, so it stays as it is; the plugin "
                + "loads from plugins/ without the line (machine-config 10; D238).");
            return;
        }

        try
        {
            File.WriteAllText(path, byteOrderMark + changed, s_utf8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Report(diagnostics, Severity.Error, ProjectSettings.FileName, DiagnosticCodes.OutputUnwritable,
                $"ncx.toml cannot be written: {exception.Message.TrimEnd('.')} (architecture 10).");
            return;
        }

        output.Write(ProjectSettings.FileName + ": " + NcxTomlPlugins.KeyLine(changed) + "\n");
    }

    // ncx.toml as every command reads it: its mistakes are diagnostics of ncx.toml on their lines, and an ERROR stops
    // the run (P2-01, D97).
    private static bool Loads(string text, Diagnostics diagnostics)
    {
        var projectDiagnostics = new Diagnostics(ProjectSettings.FileName);
        ProjectSettings? settings = ProjectSettingsLoader.LoadText(text, projectDiagnostics);
        foreach (Diagnostic diagnostic in projectDiagnostics.Items)
        {
            diagnostics.Add(diagnostic);
        }

        return settings is not null;
    }

    // A diagnostic about a whole file stands on its first line (D98).
    private static void Report(Diagnostics diagnostics, Severity severity, string file, string code, string message)
    {
        diagnostics.Add(new Diagnostic
        {
            Severity = severity,
            File = file,
            Line = FileLine,
            Code = code,
            Message = message,
        });
    }

    // A byte order mark is no part of the text the TOML loader reads (wave-1 question #80).
    private static string WithoutByteOrderMark(string text)
    {
        return text.StartsWith(InputFile.ByteOrderMark, StringComparison.Ordinal)
            ? text.Substring(InputFile.ByteOrderMark.Length)
            : text;
    }
}
