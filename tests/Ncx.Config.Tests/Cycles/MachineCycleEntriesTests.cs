using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Tests.Fixtures;

namespace Ncx.Config.Tests.Cycles;

/// <summary>
/// The cycle catalog of a machine: the built-in drilling family of its controller, the catalog file of [cycles] and
/// the [[cycle]] entries of the machine file over both (machine-config 6, 5a).
/// </summary>
public sealed class MachineCycleEntriesTests
{
    // A Fanuc lathe that selects the peck behaviour with a mode function before G83 (machine-config 5a, the Doosan
    // of language 4.7.1).
    private const string PeckModeLathe = """
        [machine]
        name = "Test lathe"
        controller = "fanuc"

        [cycles]
        catalog = "fanuc.toml"

        [[cycle]]
        name = "PECK"
        native = "G83"
        pre = ["FUNC:PECK_MODE=RETRACT"]
        """;

    // Machine-config 6: without [[cycle]] a machine has the built-in family of its controller.
    [Fact]
    public void MachineFile_WithoutCycleEntries_HasTheDrillingFamilyOfItsController()
    {
        MachineConfig machine = LoadMachine("""
            [machine]
            name = "Test mill"
            controller = "siemens"
            """);

        Assert.Empty(machine.CycleEntries);
        Assert.Equal(Controller.Siemens, machine.CycleCatalog.Controller);
        Assert.Equal("CYCLE81", machine.CycleCatalog.Find("DRILL")?.Native);
    }

    // P2-03: a catalog entry with a pre rule loads into an ExpansionRule; the entry of the machine file overrides the
    // family per machine and keeps the address words of G83 (machine-config 6, 5a).
    [Fact]
    public void MachineFile_CycleEntryWithPre_OverridesTheFamilyAndCarriesTheRule()
    {
        MachineConfig machine = LoadMachine(PeckModeLathe);

        CycleEntry written = Assert.Single(machine.CycleEntries);
        Assert.Equal(["name", "native", "pre"], written.WrittenKeys);
        CycleEntry? peck = machine.CycleCatalog.Find("PECK");
        Assert.NotNull(peck);
        Assert.Equal(["FUNC:PECK_MODE=RETRACT"], peck.Rule?.Pre);
        Assert.Equal("Q", peck.NativeOf("PECK"));
        Assert.True(peck.Modal);
    }

    // Machine-config 6: the catalog file of [cycles] stands between the family and the entries of the machine file;
    // its turning cycles arrive and the machine's PECK stays on top.
    [Fact]
    public void WithCatalog_ShippedFanucCatalog_StandsBetweenTheFamilyAndTheMachine()
    {
        MachineConfig machine = LoadMachine(PeckModeLathe);
        CycleCatalog catalog = LoadShippedCatalog(machine);

        MachineConfig withCatalog = CycleCatalogLoader.WithCatalog(machine, catalog);

        Assert.True(withCatalog.CycleCatalog.Find("TURN_OD")?.Modal);
        Assert.Equal(["P", "Q"], withCatalog.CycleCatalog.Find("ROUGH_TURN")?.Contour);
        Assert.Equal(["FUNC:PECK_MODE=RETRACT"], withCatalog.CycleCatalog.Find("PECK")?.Rule?.Pre);
        Assert.Equal("Q", withCatalog.CycleCatalog.Find("PECK")?.NativeOf("PECK"));
        Assert.Same(machine.CycleEntries, withCatalog.CycleEntries);
    }

    // The catalog of another controller family cannot serve a machine; that is a mistake of the caller, not of a file
    // (code-guidelines 6).
    [Fact]
    public void WithCatalog_CatalogOfAnotherFamily_Throws()
    {
        MachineConfig machine = LoadMachine(PeckModeLathe);

        Assert.Throws<InvalidOperationException>(
            () => CycleCatalogLoader.WithCatalog(machine, DrillingFamily.Catalog(Controller.Heidenhain)));
    }

    // P2-01: a [[cycle]] of a machine file with a wrong type is an ERROR on its line, and the machine does not load.
    [Fact]
    public void MachineFile_CycleEntryWithWrongType_ReportsItsLineAndGivesNoMachine()
    {
        var diagnostics = new Diagnostics("test.toml");

        MachineConfig? machine = MachineConfigLoader.LoadText("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [[cycle]]
            name = "PECK"
            native = "G83"
            modal = "yes"
            """, diagnostics);

        Assert.Null(machine);
        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.WrongType, error.Code);
        Assert.Equal(8, error.Line);
    }

    // Machine-config 6: the three mills of machines/ name the shipped catalog of their family, which loads for the
    // machine's controller and serves it.
    [Theory]
    [InlineData("fanuc-mill-30i.toml", "G81")]
    [InlineData("heidenhain-itnc530.toml", "200")]
    [InlineData("siemens-840dsl-mill.toml", "CYCLE81")]
    public void MillMachine_NamedCatalog_LoadsForItsController(string machineFile, string drillNative)
    {
        var machineDiagnostics = new Diagnostics("machines/" + machineFile);
        MachineConfig? machine = MachineConfigLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "machines", machineFile), machineDiagnostics);
        Assert.NotNull(machine);

        CycleCatalog catalog = LoadShippedCatalog(machine);
        MachineConfig withCatalog = CycleCatalogLoader.WithCatalog(machine, catalog);

        Assert.Equal("DRILL", withCatalog.CycleCatalog.FindNative(drillNative)?.Name);
    }

    // D104: millturn1.toml names the Siemens catalog, which loads for it.
    [Fact]
    public void Millturn1_NamedCatalog_LoadsForItsController()
    {
        var diagnostics = new Diagnostics("docs/spec/examples/machines/millturn1.toml");
        MachineConfig? machine =
            MachineConfigLoader.LoadText(Fixture.ReadText("machines/millturn1.toml"), diagnostics);
        Assert.NotNull(machine);

        MachineConfig withCatalog = CycleCatalogLoader.WithCatalog(machine, LoadShippedCatalog(machine));

        Assert.Equal("_AXN", withCatalog.CycleCatalog.Find("PECK")?.Axis);
    }

    // Loads a machine file that is expected to be clean; a failure prints the diagnostics.
    private static MachineConfig LoadMachine(string toml)
    {
        var diagnostics = new Diagnostics("test.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText(toml, diagnostics);
        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(machine);
        return machine;
    }

    // Loads the shipped catalog that [cycles] catalog of the machine names, from cycles/ of the repository, for the
    // controller of the machine (machine-config 6, 10).
    private static CycleCatalog LoadShippedCatalog(MachineConfig machine)
    {
        string? catalogFile = machine.Cycles?.CatalogFile;
        Controller? controller = machine.Machine.Controller;
        Assert.NotNull(catalogFile);
        Assert.NotNull(controller);

        var diagnostics = new Diagnostics("cycles/" + catalogFile);
        CycleCatalog? catalog = CycleCatalogLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "cycles", catalogFile), controller.Value, diagnostics);
        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(catalog);
        return catalog;
    }
}
