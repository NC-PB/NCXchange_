using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Tests.Fixtures;

namespace Ncx.Config.Tests;

/// <summary>
/// The five example machine files of machine-config 11 (with millturn1.toml, D104) and the three mills of machines/
/// load; what they still report is listed in a committed file (phase 2, P2-01, P2-04).
/// </summary>
public sealed class ExampleMachinesTests
{
    // The committed list of the WARNINGs the machine files report; each names a gap in a file, which P2-04 fixes.
    private const string ExpectedWarningsResource = "Fixtures/expected-machine-warnings.txt";

    // The five example machines, as paths relative to docs/spec/examples (tests/README.md).
    private static readonly string[] s_exampleMachines =
    [
        "machines/nakamura-ntjx.toml",
        "machines/doosan-puma-2600sy.toml",
        "machines/mori-ntx1000-mapps.toml",
        "machines/dmg-ctx-840d.toml",
        "machines/millturn1.toml",
    ];

    // The plain mills of the machines/ folder of the repository (P2-04).
    private static readonly string[] s_millMachines =
    [
        "fanuc-mill-30i.toml",
        "heidenhain-itnc530.toml",
        "siemens-840dsl-mill.toml",
    ];

    /// <summary>
    /// The five example machines for the theories.
    /// </summary>
    public static TheoryData<string> ExampleMachines => new(s_exampleMachines);

    /// <summary>
    /// The three mills of machines/ for the theories.
    /// </summary>
    public static TheoryData<string> MillMachines => new(s_millMachines);

    /// <summary>
    /// The eight files of machines/: the five example machines, copied there by name, and the three mills (P2-04).
    /// </summary>
    public static TheoryData<string> ShippedMachines
    {
        get
        {
            var fileNames = new TheoryData<string>();
            foreach (string examplePath in s_exampleMachines)
            {
                fileNames.Add(Path.GetFileName(examplePath));
            }

            foreach (string fileName in s_millMachines)
            {
                fileNames.Add(fileName);
            }

            return fileNames;
        }
    }

    // P2-01 done when: the five example machine files load without ERROR.
    [Theory]
    [MemberData(nameof(ExampleMachines))]
    public void ExampleMachine_EveryFile_LoadsWithoutError(string examplePath)
    {
        var diagnostics = new Diagnostics("docs/spec/examples/" + examplePath);

        MachineConfig? machine = MachineConfigLoader.LoadText(Fixture.ReadText(examplePath), diagnostics);

        Assert.False(diagnostics.HasErrors, diagnostics.ToText());
        Assert.NotNull(machine);
    }

    // P2-04: the plain mills of machines/ load through the path, without ERROR.
    [Theory]
    [MemberData(nameof(MillMachines))]
    public void MillMachine_EveryFile_LoadsWithoutError(string fileName)
    {
        var diagnostics = new Diagnostics("machines/" + fileName);

        MachineConfig? machine = MachineConfigLoader.Load(ShippedPath(fileName), diagnostics);

        Assert.False(diagnostics.HasErrors, diagnostics.ToText());
        Assert.NotNull(machine);
    }

    // P2-04: the eight files of machines/, the five example machines copied there and the three mills, load without
    // ERROR and without WARNING; every gap of P2-01's committed list is fixed in the files.
    [Theory]
    [MemberData(nameof(ShippedMachines))]
    public void ShippedMachine_EveryFileOfMachines_LoadsWithoutErrorAndWithoutWarning(string fileName)
    {
        var diagnostics = new Diagnostics("machines/" + fileName);

        MachineConfig? machine = MachineConfigLoader.Load(ShippedPath(fileName), diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(machine);
    }

    // P2-01: the WARNINGs of the eight files are listed in a committed file, each one a gap in a file.
    [Fact]
    public void MachineFiles_Warnings_AreTheCommittedList()
    {
        var reported = new List<string>();
        foreach (string examplePath in s_exampleMachines)
        {
            var diagnostics = new Diagnostics("docs/spec/examples/" + examplePath);
            MachineConfigLoader.LoadText(Fixture.ReadText(examplePath), diagnostics);
            AddLines(reported, diagnostics.ToText());
        }

        foreach (string fileName in s_millMachines)
        {
            var diagnostics = new Diagnostics("machines/" + fileName);
            MachineConfigLoader.Load(ShippedPath(fileName), diagnostics);
            AddLines(reported, diagnostics.ToText());
        }

        Assert.Equal(string.Join('\n', ExpectedWarnings()), string.Join('\n', reported));
    }

    // F23: the sub spindle of the Nakamura names the axis C2, which the file declares; C resolves to it while the sub
    // spindle holds the part (VM 3.8 rule 3).
    [Fact]
    public void NakamuraNtjx_SubSpindle_OwnsTheDeclaredAxisC2()
    {
        MachineConfig machine = LoadExample("machines/nakamura-ntjx.toml");

        Assert.Equal("C2", machine.ResolveRole("SUB")?.Axis);
        Assert.Equal("C2", machine.ResolveAxis("C", "S2")?.Id);
        Assert.Equal("C1", machine.ResolveAxis("C", "S1")?.Id);
        Assert.Equal(-120m, machine.Positions.Find("tool_change")?["Z"]);
        Assert.Contains("G411", machine.Raw?.Known ?? []);
        Assert.Equal(2, machine.SpindleSync?.Channel);
    }

    // D105: the Doosan writes its codes with leading zeros; they load normalized, the P suffix kept.
    [Fact]
    public void DoosanPuma_CodesWithLeadingZeros_LoadNormalized()
    {
        MachineConfig machine = LoadExample("machines/doosan-puma-2600sy.toml");

        Assert.Equal("M3 P11", machine.SpindleTables["MAIN"].States["CW"]);
        Assert.Equal("M5 P12", machine.SpindleTables["TOOL"].States["OFF"]);
        Assert.Equal("M8", machine.Coolant["STANDARD"].States["ON"]);
        Assert.Equal("M7", machine.Coolant["HIGH_PRESSURE"].States["ON"]);
        Assert.Equal("workpiece_transfer_to = SUB", machine.FuncMeta.Find("SUB_CHUCK", "CLOSE"));
    }

    // Machine-config 3: the Mori Seiki unloads over two lines and binds the synchronization to every channel (D56).
    [Fact]
    public void MoriNtx_UnloadAndSpindleSync_Load()
    {
        MachineConfig machine = LoadExample("machines/mori-ntx1000-mapps.toml");

        Assert.Equal("T0\nG361", machine.ToolChange?.Unload);
        Assert.Equal("1.", machine.ToolChange?.KindMap["TURNING"]);
        Assert.Equal(PreloadPosition.BeforeFirstMotion, machine.ToolChange?.PreloadPosition);
        Assert.True(machine.SpindleSync?.AllChannels);
    }

    // Machine-config 5a: the DMG runs the tool change point cycle before every change.
    [Fact]
    public void DmgCtx_ToolChange_CarriesItsRuleAndKindMap()
    {
        MachineConfig machine = LoadExample("machines/dmg-ctx-840d.toml");

        Assert.Equal(["HOME X Y Z"], machine.ToolChange?.Rule?.Pre);
        Assert.Equal("1", machine.ToolChange?.KindMap["ROTARY"]);
        Assert.Equal("M2=3", machine.SpindleTables["TOOL2"].States["CW"]);
        Assert.Equal("L707({angle})", machine.SpindleModeTables["MAIN"].States["AXIS"]);
        Assert.Equal(Programming.Switchable, machine.ResolveAxis("X")?.Programming);
    }

    // D104: the machine of MILLTURN_TRANSFER, as the decision describes it.
    [Fact]
    public void Millturn1_MachineOfD104_HasItsRolesAxesAndFunctions()
    {
        MachineConfig machine = LoadExample("machines/millturn1.toml");

        Assert.Equal("S1", machine.ResolveRole("MAIN")?.Id);
        Assert.Equal("C1", machine.ResolveRole("MAIN")?.Axis);
        Assert.Equal("C2", machine.ResolveRole("SUB")?.Axis);
        Assert.Equal(ResourceType.ToolSpindle, machine.ResolveRole("TOOL")?.Type);
        Assert.Equal(ResourceType.ToolHolder, machine.ResolveRole("TURRET1")?.Type);
        var ncxNames = new List<string>();
        foreach (AxisDef axis in machine.Axes)
        {
            ncxNames.Add(axis.NcxName);
        }

        Assert.Equal(["X", "Y", "Z", "C", "Z2", "C2"], ncxNames);
        Assert.Equal("M68", machine.FindFunction("SUB_CHUCK", "OPEN"));
        Assert.Equal("M67", machine.FindFunction("MAIN_CHUCK", "CLOSE"));
        Assert.Equal("T{tool} D{offset}", machine.ToolChange?.Change);
        Assert.Equal("COUPON(S2,S1)", machine.SpindleSync?.States["ON"]);
        Assert.Equal(WorkpieceFrame.Datum, machine.Workpiece?.Frames["SUB"]);
    }

    // Loads an example machine that loads without ERROR.
    private static MachineConfig LoadExample(string examplePath)
    {
        var diagnostics = new Diagnostics("docs/spec/examples/" + examplePath);
        MachineConfig? machine = MachineConfigLoader.LoadText(Fixture.ReadText(examplePath), diagnostics);
        Assert.NotNull(machine);
        return machine;
    }

    // A file of machines/ of the repository, the mills and the copies of the examples, found from the test assembly
    // (tests/README.md).
    private static string ShippedPath(string fileName)
    {
        return Path.Combine(Fixture.RepositoryRoot(), "machines", fileName);
    }

    // One line per diagnostic, as the user reads it.
    private static void AddLines(List<string> lines, string text)
    {
        foreach (string line in text.Split('\n'))
        {
            if (line.Length > 0)
            {
                lines.Add(line);
            }
        }
    }

    // The committed list without its comment lines and blank lines.
    private static List<string> ExpectedWarnings()
    {
        using Stream? stream =
            typeof(ExampleMachinesTests).Assembly.GetManifestResourceStream(ExpectedWarningsResource);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        foreach (string line in reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.Length > 0 && !line.StartsWith('#'))
            {
                lines.Add(line);
            }
        }

        return lines;
    }
}
