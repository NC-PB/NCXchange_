namespace Ncx.Acceptance.Plugins;

/// <summary>
/// The plugin that ncx plugin build and ncx plugin test work on, and what they report when there is none or dotnet is
/// missing (implementation 17, P7-02, and its risks: the SDK must be on the path, and the command says so when it is
/// not). These tests run no dotnet.
/// </summary>
public sealed class PluginFolderTests : IDisposable
{
    private readonly PluginWorkspace _workspace = new();

    public void Dispose()
    {
        _workspace.Dispose();
    }

    // A folder given that holds no plugin project is a missing input, which decides exit code 2 (D97).
    [Fact]
    public void PluginBuild_FolderWithoutProject_IsReportedAndNothingIsBuilt()
    {
        Directory.CreateDirectory(_workspace.PathOf("Empty"));

        int exitCode = _workspace.Build("Empty");

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx(1): ERROR CLI553: ", _workspace.Error, StringComparison.Ordinal);
        Assert.Contains("Empty", _workspace.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(_workspace.PathOf("plugins")));
    }

    // The TODO(question) of PluginFolder: without a folder, a working directory with no plugin that ncx plugin new
    // made has nothing to build.
    [Fact]
    public void PluginBuild_NoFolderAndNoPlugin_IsReported()
    {
        int exitCode = _workspace.Build(null);

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx(1): ERROR CLI553: ", _workspace.Error, StringComparison.Ordinal);
    }

    // The TODO(question) of PluginFolder: without a folder, two plugins that ncx plugin new made in the working
    // directory are named, and the user chooses one.
    [Fact]
    public void PluginBuild_NoFolderAndTwoPlugins_NamesBothAndBuildsNeither()
    {
        _workspace.New("First");
        _workspace.New("Second");

        int exitCode = _workspace.Build(null);

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx(1): ERROR CLI553: ", _workspace.Error, StringComparison.Ordinal);
        Assert.Contains("First", _workspace.Error, StringComparison.Ordinal);
        Assert.Contains("Second", _workspace.Error, StringComparison.Ordinal);
    }

    // Implementation 17, risks: ncx plugin build shells out to dotnet, and says so when dotnet cannot be started.
    [Fact]
    public void PluginBuild_DotnetThatCannotBeStarted_SaysTheSdkIsNeeded()
    {
        using var workspace = new PluginWorkspace { Dotnet = Path.Combine(_workspace.WorkingDirectory, "no-dotnet") };
        workspace.New("Sample");

        int exitCode = workspace.Build(null);

        Assert.Equal(1, exitCode);
        Assert.StartsWith("ncx(1): ERROR CLI554: ", workspace.Error, StringComparison.Ordinal);
        Assert.Contains(".NET SDK", workspace.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(workspace.PathOf("plugins")));
    }

    // ncx plugin test runs the test project &lt;name&gt;.Tests of the plugin; a plugin without one is reported (D97).
    [Fact]
    public void PluginTest_PluginWithoutTestProject_IsReported()
    {
        _workspace.New("Sample");
        Directory.Delete(_workspace.PathOf("Sample", "Sample.Tests"), recursive: true);

        int exitCode = _workspace.Test("Sample");

        Assert.Equal(2, exitCode);
        Assert.StartsWith(Path.Combine("Sample", "Sample.Tests", "Sample.Tests.csproj") + "(1): ERROR CLI558: ",
            _workspace.Error, StringComparison.Ordinal);
    }
}
