using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Cli.Commands;

/// <summary>
/// ncx plugin test [&lt;folder&gt;]: runs dotnet test on the tests of a plugin, the project &lt;name&gt;.Tests in its
/// folder, against the Ncx assemblies of the ncx that runs (code-guidelines 11, step 7; implementation 17, P7-02). What
/// dotnet test writes goes to the standard output. Exit code 0; 1 when a test fails or dotnet cannot be started; 2
/// when there is no plugin or no test project (D97).
/// </summary>
internal static class PluginTest
{
    // The tests of a plugin are the project <name>.Tests in its folder (code-guidelines 11).
    private const string TestsSuffix = ".Tests";
    private const string ProjectExtension = ".csproj";

    // A diagnostic about a whole file stands on its first line.
    private const int FileLine = 1;

    /// <summary>
    /// Runs the tests of a plugin.
    /// </summary>
    /// <param name="folder">The folder of the plugin; null for the one PluginFolder finds.</param>
    /// <param name="settings">The working directory, the folder of ncx, dotnet and --strict.</param>
    /// <param name="output">The standard output, for what dotnet test writes.</param>
    /// <param name="error">The standard error, for the diagnostics (D98).</param>
    /// <returns>The exit code (D97).</returns>
    internal static int Run(string? folder, PluginCommandSettings settings, TextWriter output, TextWriter error)
    {
        var diagnostics = new Diagnostics(Program.ToolName);
        PluginProject? project = PluginFolder.Find(folder, settings.WorkingDirectory, "test", diagnostics);
        if (project is null)
        {
            error.Write(diagnostics.ToText());
            return ExitCodes.NotStarted;
        }

        // A plugin without its test project has nothing to run, a missing input (D97).
        string testsName = project.Name + TestsSuffix;
        string tests = Path.Combine(project.Folder, testsName, testsName + ProjectExtension);
        string testsFile = Path.Combine(Path.GetDirectoryName(project.DisplayFile) ?? "", testsName,
            testsName + ProjectExtension);
        if (!File.Exists(tests))
        {
            Report(diagnostics, testsFile, DiagnosticCodes.PluginTestProjectMissing,
                $"The plugin {project.Name} has no test project, which ncx plugin test runs; the template keeps its "
                + "tests in this project (code-guidelines 11).");
            error.Write(diagnostics.ToText());
            return ExitCodes.NotStarted;
        }

        // dotnet test against the Ncx assemblies of this ncx (D106); no build server of dotnet stays behind.
        DotnetResult run = DotnetProcess.Run(settings.Dotnet,
            ["test", tests, "--nologo", "--disable-build-servers", "-p:NcxFolder=" + settings.NcxFolder],
            settings.WorkingDirectory);
        if (run.Failure is string failure)
        {
            DotnetProcess.ReportNotStarted(failure, diagnostics);
            error.Write(diagnostics.ToText());
            return ExitCodes.Error;
        }

        output.Write(run.Output);
        if (run.ExitCode != 0)
        {
            string exitCode = run.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "";
            Report(diagnostics, testsFile, DiagnosticCodes.PluginTestsFailed,
                $"dotnet test ended with exit code {exitCode}: a test of the plugin failed or did not build; what it "
                + "wrote is on the standard output (implementation 17, P7-02).");
        }

        error.Write(diagnostics.ToText());
        return ExitCodes.OfRun(diagnostics, settings.Strict);
    }

    // A diagnostic about a whole file stands on its first line (D98).
    private static void Report(Diagnostics diagnostics, string file, string code, string message)
    {
        diagnostics.Add(new Diagnostic
        {
            Severity = Severity.Error,
            File = file,
            Line = FileLine,
            Code = code,
            Message = message,
        });
    }
}
