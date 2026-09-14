namespace Ncx.Cli;

// The codes of ncx plugin new, build, check and test, CLI550-CLI599 (P7-02): the name and the folder of a new plugin,
// the template next to the tool, the plugin project that a build or a test works on, dotnet of the .NET SDK, the line
// of ncx.toml and the tests of a plugin.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CLI550: the name that ncx plugin new takes is no name of a plugin: letters, digits and underscores, parts joined
    /// by dots, no part beginning with a digit, since the name becomes the folder, the namespace and the DLL of the
    /// plugin; a usage error, exit code 2 (code-guidelines 11; D97).
    /// </summary>
    public const string PluginNameInvalid = "CLI550";

    /// <summary>
    /// CLI551: the working directory holds a folder or a file of the name that ncx plugin new would make, which stays
    /// as it is; a usage error, exit code 2 (implementation 17, P7-02; D97).
    /// </summary>
    public const string PluginFolderExists = "CLI551";

    /// <summary>
    /// CLI552: the plugin template is not in templates/ncx-plugin/ of the folder of ncx, so ncx plugin new has nothing
    /// to copy; decided before the run starts, exit code 2 (code-guidelines 11; D97).
    /// </summary>
    public const string PluginTemplateMissing = "CLI552";

    /// <summary>
    /// CLI553: ncx plugin build or ncx plugin test finds no plugin project: the folder given holds none, or without a
    /// folder neither the working directory holds one nor exactly one of its folders a plugin that ncx plugin new made;
    /// decided before the run starts, exit code 2 (implementation 17, P7-02; D97).
    /// </summary>
    public const string PluginProjectNotFound = "CLI553";

    /// <summary>
    /// CLI554: dotnet of the .NET SDK, which ncx plugin build and ncx plugin test run, cannot be started; exit code 1
    /// (implementation 17, P7-02, risks; D97).
    /// </summary>
    public const string DotnetNotStarted = "CLI554";

    /// <summary>
    /// CLI555: dotnet build of the plugin failed; what it wrote stands before the diagnostic, and nothing reaches
    /// plugins/; exit code 1 (implementation 17, P7-02; D97).
    /// </summary>
    public const string PluginBuildFailed = "CLI555";

    /// <summary>
    /// CLI556: dotnet build of the plugin succeeded and left no DLL named after the project, so nothing reaches
    /// plugins/; exit code 1 (implementation 17, P7-02; D97).
    /// </summary>
    public const string PluginNotBuilt = "CLI556";

    /// <summary>
    /// CLI557, an INFO: ncx.toml holds its plugins in a form the line of ncx plugin build cannot join, and stays as it
    /// is; the DLL in plugins/ loads without the line (machine-config 10; implementation 17, P7-01; D238).
    /// </summary>
    public const string PluginLineNotAdded = "CLI557";

    /// <summary>
    /// CLI558: the plugin has no test project &lt;name&gt;.Tests, which ncx plugin test runs; decided before the run
    /// starts, exit code 2 (code-guidelines 11; D97).
    /// </summary>
    public const string PluginTestProjectMissing = "CLI558";

    /// <summary>
    /// CLI559: dotnet test of the plugin failed: a test failed or did not build; what it wrote is on the standard
    /// output; exit code 1 (implementation 17, P7-02; D97).
    /// </summary>
    public const string PluginTestsFailed = "CLI559";
}
