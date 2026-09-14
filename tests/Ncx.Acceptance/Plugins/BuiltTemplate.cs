namespace Ncx.Acceptance.Plugins;

/// <summary>
/// The six steps of code-guidelines 11 up to step five, once for the tests of a class: ncx plugin new CoolantClutch in
/// an empty working directory, nothing changed, then ncx plugin build without a folder, which runs dotnet build against
/// the Ncx assemblies of the test run (implementation 17, P7-02).
/// </summary>
public sealed class BuiltTemplate : IDisposable
{
    /// <summary>
    /// The name of the plugin, CoolantClutch, which its INFO line names (implementation 17, P7-02; M10).
    /// </summary>
    public const string Name = "CoolantClutch";

    public BuiltTemplate()
    {
        NewExitCode = Workspace.New(Name);
        NewError = Workspace.Error;
        BuildExitCode = Workspace.Build(null);
        BuildOutput = Workspace.Output;
        BuildError = Workspace.Error;
    }

    /// <summary>
    /// The working directory of the steps.
    /// </summary>
    internal PluginWorkspace Workspace { get; } = new();

    /// <summary>
    /// The exit code of ncx plugin new, and what it reported.
    /// </summary>
    public int NewExitCode { get; }

    public string NewError { get; }

    /// <summary>
    /// The exit code of ncx plugin build, and what it wrote to the standard output and the standard error.
    /// </summary>
    public int BuildExitCode { get; }

    public string BuildOutput { get; }

    public string BuildError { get; }

    public void Dispose()
    {
        Workspace.Dispose();
    }
}
