using Ncx.Cli.Commands;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// The line ncx plugin build adds to ncx.toml, the plugin assemblies of machine-config 10, plugins =
/// ["MyShop.NcxPlugins.dll"] (implementation 17, P7-02). ncx.toml cannot hold that list beside the
/// [plugins.&lt;name&gt;] sections of D80; which form it takes is D238, and a file in the other form stays as it is.
/// </summary>
public sealed class NcxTomlPluginsTests
{
    // Machine-config 10: without ncx.toml the file is the one line of the plugin assemblies.
    [Fact]
    public void Add_NoNcxToml_IsThePluginsLineOfMachineConfig10()
    {
        Assert.Equal("plugins = [\"CoolantClutch.dll\"]\n", NcxTomlPlugins.Add(null, "CoolantClutch.dll"));
    }

    // Machine-config 10: the line joins the keys of an ncx.toml that has none for the plugins.
    [Fact]
    public void Add_NcxTomlWithoutPlugins_AddsTheLineAfterItsKeys()
    {
        string text = "machine = \"dmu50\"\nout = \"nc\"\n";

        Assert.Equal("machine = \"dmu50\"\nout = \"nc\"\nplugins = [\"CoolantClutch.dll\"]\n",
            NcxTomlPlugins.Add(text, "CoolantClutch.dll"));
    }

    // A file whose last line has no line ending gets one before the new line.
    [Fact]
    public void Add_LastLineWithoutLineEnding_EndsItFirst()
    {
        Assert.Equal("machine = \"dmu50\"\nplugins = [\"CoolantClutch.dll\"]\n",
            NcxTomlPlugins.Add("machine = \"dmu50\"", "CoolantClutch.dll"));
    }

    // Machine-config 10: a list on one line gets the assembly at its end, in the form the list has.
    [Fact]
    public void Add_ListOnOneLine_AddsTheAssemblyAtItsEnd()
    {
        string text = "machine = \"dmu50\"\nplugins = [\"MyShop.NcxPlugins.dll\"]   # the shop\n";

        Assert.Equal("machine = \"dmu50\"\nplugins = [\"MyShop.NcxPlugins.dll\", \"CoolantClutch.dll\"]   # the shop\n",
            NcxTomlPlugins.Add(text, "CoolantClutch.dll"));
    }

    // An empty list gets the assembly as its only entry.
    [Fact]
    public void Add_EmptyList_GetsTheAssembly()
    {
        Assert.Equal("plugins = [\"CoolantClutch.dll\"]\n", NcxTomlPlugins.Add("plugins = []\n", "CoolantClutch.dll"));
    }

    // D238: the sections of D80 and the list cannot stand side by side in one ncx.toml, so a file with a section stays
    // as it is.
    [Fact]
    public void Add_SectionOfD80_LeavesNcxTomlAsItIs()
    {
        string text = "machine = \"dmu50\"\n\n[plugins.CoolantClutch]\nreason = \"clutch\"\n";

        Assert.Null(NcxTomlPlugins.Add(text, "CoolantClutch.dll"));
    }

    // A list over several lines is left as its author wrote it.
    [Fact]
    public void Add_ListOverSeveralLines_LeavesNcxTomlAsItIs()
    {
        string text = "plugins = [\n  \"MyShop.NcxPlugins.dll\",\n]\n";

        Assert.Null(NcxTomlPlugins.Add(text, "CoolantClutch.dll"));
    }

    // The new line ends with the line ending of the file.
    [Fact]
    public void Add_FileWithCrLf_KeepsCrLf()
    {
        Assert.Equal("machine = \"dmu50\"\r\nplugins = [\"CoolantClutch.dll\"]\r\n",
            NcxTomlPlugins.Add("machine = \"dmu50\"\r\n", "CoolantClutch.dll"));
    }

    // Machine-config 10: an assembly the list names, by its name or by its path in plugins/, is listed.
    [Theory]
    [InlineData("plugins = [\"CoolantClutch.dll\"]\n")]
    [InlineData("plugins = [\"plugins/CoolantClutch.dll\"]\n")]
    public void Lists_AssemblyTheListNames_IsListed(string text)
    {
        Assert.True(NcxTomlPlugins.Lists(text, "CoolantClutch.dll"));
    }

    // An assembly the list does not name is not listed, nor in a file without a list.
    [Theory]
    [InlineData("plugins = [\"MyShop.NcxPlugins.dll\"]\n")]
    [InlineData("machine = \"dmu50\"\n")]
    public void Lists_AssemblyTheListDoesNotName_IsNotListed(string text)
    {
        Assert.False(NcxTomlPlugins.Lists(text, "CoolantClutch.dll"));
    }
}
