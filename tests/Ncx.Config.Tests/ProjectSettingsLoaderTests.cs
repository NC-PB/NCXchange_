using Ncx.Core.Model;

namespace Ncx.Config.Tests;

/// <summary>
/// ncx.toml, the settings of the working directory: the machine file that --machine defaults to, the machine and cycle
/// folders, the output folder and the plugin assemblies (machine-config 10, architecture 10; implementation 12, P2-04).
/// </summary>
public sealed class ProjectSettingsLoaderTests
{
    // Machine-config 10, architecture 10: ncx.toml names the machine, the machine and cycle folders, the output folder
    // and the plugin assemblies.
    [Fact]
    public void NcxToml_EveryKeyOfMachineConfig10_Loads()
    {
        var diagnostics = new Diagnostics(ProjectSettings.FileName);

        ProjectSettings? settings = ProjectSettingsLoader.LoadText("""
            machine = "dmu50"
            machines = "shop/machines"
            cycles = "shop/cycles"
            out = "nc"
            plugins = ["MyShop.NcxPlugins.dll"]
            """, diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(settings);
        Assert.Equal("dmu50", settings.Machine);
        Assert.Equal(1, settings.MachineLine);
        Assert.Equal("shop/machines", settings.Machines);
        Assert.Equal("shop/cycles", settings.Cycles);
        Assert.Equal("nc", settings.Out);
        Assert.Equal(["MyShop.NcxPlugins.dll"], settings.Plugins);
    }

    // Machine-config 10: the project layout puts the machine files in machines/, the catalogs in cycles/ and the NC
    // files in out/; an ncx.toml that names none of them keeps those folders and names no machine, so the built-in
    // default machine of D103 applies.
    [Fact]
    public void NcxToml_WithoutKeys_KeepsTheFoldersOfTheProjectLayout()
    {
        var diagnostics = new Diagnostics(ProjectSettings.FileName);

        ProjectSettings? settings = ProjectSettingsLoader.LoadText("# the settings of my shop\n", diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(settings);
        Assert.Null(settings.Machine);
        Assert.Equal("machines", settings.Machines);
        Assert.Equal("cycles", settings.Cycles);
        Assert.Equal("out", settings.Out);
        Assert.Empty(settings.Plugins);
        Assert.Empty(settings.PluginSettings);
    }

    // The line of the machine key, which a machine that cannot be found is reported on (D98).
    [Fact]
    public void NcxToml_MachineKey_KeepsItsLine()
    {
        var diagnostics = new Diagnostics(ProjectSettings.FileName);

        ProjectSettings? settings = ProjectSettingsLoader.LoadText("""
            # the settings of my shop
            out = "nc"
            machine = "dmu50"
            """, diagnostics);

        Assert.Equal(3, settings?.MachineLine);
    }

    // P2-01 applies to ncx.toml as to the machine file: an unknown key is a WARNING on its line that names the nearest
    // known key.
    [Fact]
    public void NcxToml_UnknownKey_WarnsOnItsLineWithTheNearestKey()
    {
        var diagnostics = new Diagnostics(ProjectSettings.FileName);

        ProjectSettings? settings = ProjectSettingsLoader.LoadText("""
            machine = "dmu50"
            mashines = "shop"
            """, diagnostics);

        Diagnostic warning = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal(DiagnosticCodes.UnknownKey, warning.Code);
        Assert.Equal(2, warning.Line);
        Assert.Equal("Unknown key mashines in ncx.toml; the nearest known key is machines (machine-config 10).",
            warning.Message);
        Assert.NotNull(settings);
    }

    // P2-01: a wrong type is an ERROR on its line, and a file with an ERROR gives nothing, because the run stops.
    [Fact]
    public void NcxToml_MachineAsANumber_IsWrongTypeErrorAndGivesNoSettings()
    {
        var diagnostics = new Diagnostics(ProjectSettings.FileName);

        ProjectSettings? settings = ProjectSettingsLoader.LoadText("""
            machines = "shop"
            machine = 50
            """, diagnostics);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(DiagnosticCodes.WrongType, error.Code);
        Assert.Equal(2, error.Line);
        Assert.Null(settings);
    }

    // A file that is not TOML reports its line and loads nothing (P2-01).
    [Fact]
    public void NcxToml_NotToml_IsSyntaxErrorAndGivesNoSettings()
    {
        var diagnostics = new Diagnostics(ProjectSettings.FileName);

        ProjectSettings? settings = ProjectSettingsLoader.LoadText("machine = \n", diagnostics);

        Assert.Equal(DiagnosticCodes.TomlSyntax, Assert.Single(diagnostics.Items).Code);
        Assert.Null(settings);
    }

    // D80: a plugin reads its own [plugins.<name>] section of ncx.toml, a string dictionary; the sections load per
    // plugin, and name no assembly.
    [Fact]
    public void NcxToml_PluginSections_LoadAsTheSettingsOfEachPlugin()
    {
        var diagnostics = new Diagnostics(ProjectSettings.FileName);

        ProjectSettings? settings = ProjectSettingsLoader.LoadText("""
            machine = "dmu50"

            [plugins.MyShopRules]
            rpm_limit = "8000"
            coolant = "THROUGH"
            """, diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(settings);
        Assert.Empty(settings.Plugins);
        Assert.Equal("8000", settings.PluginSettings["MyShopRules"]["rpm_limit"]);
        Assert.Equal("THROUGH", settings.PluginSettings["MyShopRules"]["coolant"]);
    }

    // D80: the settings of a plugin are strings; a number is the wrong type, an ERROR on its line.
    [Fact]
    public void NcxToml_PluginSettingAsANumber_IsWrongTypeError()
    {
        var diagnostics = new Diagnostics(ProjectSettings.FileName);

        ProjectSettings? settings = ProjectSettingsLoader.LoadText("""
            [plugins.MyShopRules]
            rpm_limit = 8000
            """, diagnostics);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.WrongType, error.Code);
        Assert.Equal(2, error.Line);
        Assert.Null(settings);
    }
}
