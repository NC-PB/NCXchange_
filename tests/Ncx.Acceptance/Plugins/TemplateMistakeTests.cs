namespace Ncx.Acceptance.Plugins;

/// <summary>
/// A plugin with a mistake in its words: ncx plugin build passes on what the compiler says and puts nothing into
/// plugins/ (implementation 17, P7-02). This test runs dotnet build of the .NET SDK.
/// </summary>
public sealed class TemplateMistakeTests : IDisposable
{
    private readonly PluginWorkspace _workspace = new();

    public void Dispose()
    {
        _workspace.Dispose();
    }

    // Code-guidelines 11, step 3 gone wrong: a semicolon left out is reported as the compiler reports it, with the
    // ERROR of the build after it, exit code 1 (D97); no DLL reaches plugins/ and ncx.toml is not made.
    [Fact]
    public void PluginBuild_RuleWithAMistake_PassesOnTheCompilerErrorAndBuildsNothing()
    {
        _workspace.New("Mistake");
        string rule = _workspace.PathOf("Mistake", "CoolantClutchRule.cs");
        File.WriteAllText(rule, File.ReadAllText(rule).Replace("return RewriteResult.Unchanged;",
            "return RewriteResult.Unchanged", StringComparison.Ordinal));

        int exitCode = _workspace.Build(null);

        Assert.Equal(1, exitCode);
        Assert.Contains("error CS1002", _workspace.Error, StringComparison.Ordinal);
        Assert.Contains(Path.Combine("Mistake", "Mistake.csproj") + "(1): ERROR CLI555: ", _workspace.Error,
            StringComparison.Ordinal);
        Assert.False(Directory.Exists(_workspace.PathOf("plugins")));
        Assert.False(File.Exists(_workspace.PathOf("ncx.toml")));
        Assert.Empty(_workspace.Output);
    }
}
