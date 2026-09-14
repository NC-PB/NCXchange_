namespace Ncx.Acceptance.Plugins;

/// <summary>
/// ncx plugin new &lt;name&gt; copies the plugin template next to ncx into ./&lt;name&gt;/ and renames it
/// (implementation 17, P7-02; code-guidelines 11; architecture 10). These tests run no dotnet.
/// </summary>
public sealed class PluginNewTests : IDisposable
{
    private readonly PluginWorkspace _workspace = new();

    public void Dispose()
    {
        _workspace.Dispose();
    }

    // Implementation 17, P7-02: the template is copied into a folder of the name given, its files named after the
    // plugin, and each file written is named on the standard output.
    [Fact]
    public void PluginNew_Name_CopiesTheTemplateIntoAFolderOfThatName()
    {
        int exitCode = _workspace.New("Sample");

        Assert.True(exitCode == 0, _workspace.Error);
        string[] files =
        [
            Path.Combine("Sample", "CoolantClutchRule.cs"),
            Path.Combine("Sample", "Sample.Tests", "CoolantClutchRuleTests.cs"),
            Path.Combine("Sample", "Sample.Tests", "Sample.Tests.csproj"),
            Path.Combine("Sample", "Sample.csproj"),
            Path.Combine("Sample", "README.md"),
            Path.Combine("Sample", "ZOnItsOwnLine.cs"),
        ];
        Assert.Equal(string.Join('\n', files) + "\n", _workspace.Output);
        Assert.All(files, file => Assert.True(File.Exists(_workspace.PathOf(file)), file));
        Assert.Empty(_workspace.Error);
    }

    // Implementation 17, P7-02: "copies and renames": the namespace, the project and its tests carry the name of the
    // plugin, and MyShopRules is left nowhere.
    [Fact]
    public void PluginNew_Name_RenamesMyShopRulesInEveryFile()
    {
        _workspace.New("Sample");

        foreach (string file in Directory.GetFiles(_workspace.PathOf("Sample"), "*", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain("MyShopRules", File.ReadAllText(file), StringComparison.Ordinal);
        }

        Assert.StartsWith("namespace Sample;", File.ReadAllText(_workspace.PathOf("Sample", "CoolantClutchRule.cs")),
            StringComparison.Ordinal);
        string tests = File.ReadAllText(_workspace.PathOf("Sample", "Sample.Tests", "Sample.Tests.csproj"));
        Assert.Contains("<ProjectReference Include=\"../Sample.csproj\" />", tests, StringComparison.Ordinal);
    }

    // The plugin and its tests are built against the Ncx assemblies of the ncx that made them, whose folder both
    // project files name, so that the template builds unchanged (implementation 17, P7-02; D106).
    [Fact]
    public void PluginNew_Name_WritesTheFolderOfNcxIntoBothProjectFiles()
    {
        _workspace.New("Sample");

        string folderLine = "<NcxFolder Condition=\"'$(NcxFolder)' == ''\">"
            + Path.TrimEndingDirectorySeparator(Path.GetFullPath(_workspace.ToolFolder)) + "</NcxFolder>";
        foreach (string project in new[] { "Sample.csproj", Path.Combine("Sample.Tests", "Sample.Tests.csproj") })
        {
            string text = File.ReadAllText(_workspace.PathOf("Sample", project));
            Assert.Contains(folderLine, text, StringComparison.Ordinal);
            Assert.DoesNotContain("NCX_FOLDER", text, StringComparison.Ordinal);
        }
    }

    // Machine-config 10 names a plugin assembly MyShop.NcxPlugins.dll: a name of parts joined by dots is a name.
    [Fact]
    public void PluginNew_NameWithDots_IsANameOfAPlugin()
    {
        int exitCode = _workspace.New("MyShop.NcxPlugins");

        Assert.True(exitCode == 0, _workspace.Error);
        Assert.True(File.Exists(_workspace.PathOf("MyShop.NcxPlugins", "MyShop.NcxPlugins.csproj")));
    }

    // The name becomes the namespace, the project and the DLL, so it is letters, digits and underscores, parts joined
    // by dots, no part starting with a digit; anything else is a usage error that decides exit code 2 (D97) and makes
    // nothing.
    [Theory]
    [InlineData("1Shop")]
    [InlineData("My Shop")]
    [InlineData("../Shop")]
    [InlineData("Shop.")]
    [InlineData("")]
    public void PluginNew_NameThatIsNoName_IsAUsageErrorAndMakesNothing(string name)
    {
        int exitCode = _workspace.New(name);

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx(1): ERROR CLI550: ", _workspace.Error, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(_workspace.WorkingDirectory));
    }

    // ncx plugin new makes a new folder and changes none: a folder of the name that is there already is a usage
    // error, and its files stay as they are (D97).
    [Fact]
    public void PluginNew_FolderThatIsThereAlready_IsAUsageErrorAndStaysAsItIs()
    {
        string mine = _workspace.Write(Path.Combine("Sample", "Mine.cs"), "// mine\n");

        int exitCode = _workspace.New("Sample");

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx(1): ERROR CLI551: ", _workspace.Error, StringComparison.Ordinal);
        Assert.Equal([mine], Directory.GetFiles(_workspace.PathOf("Sample")));
    }

    // The template lies in templates/ncx-plugin/ of the folder of ncx; without it nothing can be copied, and the run
    // does not start (D97).
    [Fact]
    public void PluginNew_ToolFolderWithoutTheTemplate_IsReportedAndMakesNothing()
    {
        using var workspace = new PluginWorkspace(_workspace.WorkingDirectory);

        int exitCode = workspace.New("Sample");

        Assert.Equal(2, exitCode);
        Assert.Contains("ERROR CLI552: ", workspace.Error, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(workspace.WorkingDirectory));
    }
}
