using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config.Tests;

/// <summary>
/// The machine file loaded table by table: every table of machine-config 1 to 9, a mistake reported with its line
/// (phase 2, P2-01).
/// </summary>
public sealed class MachineConfigLoaderTests
{
    // P2-01: an unknown key is a WARNING on its line that names the nearest known key; [formt] suggests [format].
    [Fact]
    public void UnknownKey_TableFormt_WarnsOnItsLineAndSuggestsFormat()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test mill"
            controller = "fanuc"

            [formt]
            decimals = { X = 3 }
            """);

        Diagnostic warning = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal(DiagnosticCodes.UnknownKey, warning.Code);
        Assert.Equal(5, warning.Line);
        Assert.Contains("[formt]", warning.Message, StringComparison.Ordinal);
        Assert.Contains("[format]", warning.Message, StringComparison.Ordinal);
    }

    // P2-01: a typo in a key of a table names the nearest known key of that table.
    [Fact]
    public void UnknownKey_TypoInASpindleTable_WarnsWithTheNearestKey()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [spindle.MAIN]
            CW = "M3"
            rpm_mx = 5000
            """);

        Diagnostic warning = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.UnknownKey, warning.Code);
        Assert.Equal(7, warning.Line);
        Assert.Contains("rpm_mx", warning.Message, StringComparison.Ordinal);
        Assert.Contains("rpm_max", warning.Message, StringComparison.Ordinal);
    }

    // P2-01: a wrong type is an ERROR on its line; decimals is a table of whole numbers, not a string.
    [Fact]
    public void WrongType_DecimalsAsString_ReportsAnErrorOnItsLine()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test mill"
            controller = "fanuc"

            [format]
            decimals = "3"
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(DiagnosticCodes.WrongType, error.Code);
        Assert.Equal(6, error.Line);
        Assert.Contains("decimals", error.Message, StringComparison.Ordinal);
    }

    // P2-01: a file with an ERROR gives no machine, because the run stops (virtual machine 2.9).
    [Fact]
    public void WrongType_FlagAsString_GivesNoMachine()
    {
        var diagnostics = new Diagnostics("test.toml");

        MachineConfig? machine = MachineConfigLoader.LoadText("""
            [machine]
            name = "Test mill"
            controller = "fanuc"

            [format]
            trailing_zeros = "no"
            """, diagnostics);

        Assert.Null(machine);
        Assert.Equal(DiagnosticCodes.WrongType, Assert.Single(diagnostics.Items).Code);
    }

    // P2-01: a missing required key is an ERROR that names the table.
    [Fact]
    public void MissingKey_ResourceWithoutType_NamesTheTable()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test mill"
            controller = "fanuc"

            [[resource]]
            id = "S1"
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.MissingKey, error.Code);
        Assert.Equal(5, error.Line);
        Assert.Contains("[[resource]]", error.Message, StringComparison.Ordinal);
        Assert.Contains("type", error.Message, StringComparison.Ordinal);
    }

    // Machine-config 1: a machine file without [machine] has no identity.
    [Fact]
    public void MissingKey_FileWithoutMachineTable_ReportsAnError()
    {
        Diagnostics diagnostics = Report("""
            [format]
            decimals = { X = 3 }
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.MissingKey, error.Code);
        Assert.Contains("[machine]", error.Message, StringComparison.Ordinal);
    }

    // Machine-config 1: controller is fanuc, heidenhain or siemens.
    [Fact]
    public void ValueNotAllowed_UnknownController_ReportsAnError()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test mill"
            controller = "fanox"
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.ValueNotAllowed, error.Code);
        Assert.Equal(3, error.Line);
        Assert.Contains("fanox", error.Message, StringComparison.Ordinal);
    }

    // VM 3.8 rule 2: more than one resource of a kind without an explicit default is an ERROR when the machine is
    // loaded; two work spindles without default_spindle.
    [Fact]
    public void DefaultSpindle_TwoWorkSpindlesWithoutDefault_ReportsAnError()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [[resource]]
            id = "S1"
            type = "work_spindle"

            [[resource]]
            id = "S2"
            type = "work_spindle"
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(DiagnosticCodes.SeveralWithoutDefault, error.Code);
        Assert.Contains("default_spindle", error.Message, StringComparison.Ordinal);
    }

    // VM 3.8 rule 2: with default_spindle the two work spindles load.
    [Fact]
    public void DefaultSpindle_TwoWorkSpindlesWithDefault_Loads()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"
            default_spindle = "S1"

            [[resource]]
            id = "S1"
            type = "work_spindle"

            [[resource]]
            id = "S2"
            type = "work_spindle"
            """);

        Assert.Equal(2, machine.Resources.Count);
        Assert.Equal("S1", machine.ResolveDefaultSpindle()?.Id);
    }

    // VM 3.8 rule 2: two tool holders without default_holder.
    [Fact]
    public void DefaultHolder_TwoHoldersWithoutDefault_ReportsAnError()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [[resource]]
            id = "T1"
            type = "tool_holder"

            [[resource]]
            id = "T2"
            type = "tool_holder"
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.SeveralWithoutDefault, error.Code);
        Assert.Contains("default_holder", error.Message, StringComparison.Ordinal);
    }

    // F23: a resource that names an axis the file does not declare is an ERROR on the line of that key.
    [Fact]
    public void UndeclaredAxis_ResourceNamesAnAxisTheFileLacks_ReportsAnError()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [[resource]]
            id = "S1"
            type = "work_spindle"
            axis = "C1"
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.UndeclaredAxis, error.Code);
        Assert.Equal(8, error.Line);
        Assert.Contains("C1", error.Message, StringComparison.Ordinal);
    }

    // D105: a bare integer means M and the number, and 8, 08, M8 and M08 all load as M8.
    [Fact]
    public void FunctionValue_BareIntegerDigitsAndPaddedCode_AllLoadAsM8()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [coolant]
            STANDARD = { ON = 8, OFF = "M09" }
            AIR = { ON = "08", OFF = "9" }
            THROUGH = { ON = "M08", OFF = "M9" }
            """);

        foreach (string channel in new[] { "STANDARD", "AIR", "THROUGH" })
        {
            Assert.Equal("M8", machine.Coolant[channel].States["ON"]);
            Assert.Equal("M9", machine.Coolant[channel].States["OFF"]);
        }
    }

    // D105, machine-config 5: every M or G code of a multi-word value is normalized, the other text kept as written.
    [Fact]
    public void FunctionValue_CodeWithParameters_NormalizesOnlyTheCodes()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [spindle.MAIN]
            CW = "M03 P11"

            [coolant]
            HIGH_PRESSURE = { ON = "H07={value} M07", OFF = "M09" }
            """);

        Assert.Equal("M3 P11", machine.SpindleTables["MAIN"].States["CW"]);
        Assert.Equal("H07={value} M7", machine.Coolant["HIGH_PRESSURE"].States["ON"]);
    }

    // Machine-config 5, D105: the templates of [format], [tool_change], [sync] and [transform] are normalized too;
    // NCX text in pre and post carries no native codes and stays as written.
    [Fact]
    public void Templates_EveryTemplateWithACode_IsNormalizedButNotNcxText()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [format]
            program_end = "M030"
            sub_end = "M099"

            [tool_change]
            change = "T{tool} M06"
            pre = ["RAW=\"M06\""]

            [sync]
            wait = "M{mark} P{paths}"

            [transform]
            TILT_ON = "G068.2 X{x} Y{y} Z{z} I{a} J{b} K{c}"
            """);

        Assert.Equal("M30", machine.Format?.ProgramEnd);
        Assert.Equal("M99", machine.Format?.SubEnd);
        Assert.Equal("T{tool} M6", machine.ToolChange?.Change);
        Assert.Equal(["RAW=\"M06\""], machine.ToolChange?.Rule?.Pre);
        Assert.Equal("M{mark} P{paths}", machine.Sync?.Wait);
        Assert.Equal("G68.2 X{x} Y{y} Z{z} I{a} J{b} K{c}", machine.Transform?.TiltOn);
    }

    // D105: a bare integer is the one exception to the wrong-type ERROR, and only one that can be an M number.
    [Fact]
    public void FunctionValue_NegativeInteger_ReportsWrongType()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [func]
            DOOR = { OPEN = -8 }
            """);

        Assert.Equal(DiagnosticCodes.WrongType, Assert.Single(diagnostics.Items).Code);
    }

    // D105: function values are strings; a table is a wrong type.
    [Fact]
    public void FunctionValue_TableAsState_ReportsWrongType()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [func]
            DOOR = { OPEN = { X = 1 } }
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.WrongType, error.Code);
        Assert.Equal(6, error.Line);
    }

    // TOML itself forbids a leading zero in an integer: ON = 08 is a syntax ERROR on its line, "08" a string.
    [Fact]
    public void TomlSyntax_LeadingZeroInteger_ReportsAnErrorOnItsLine()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [coolant]
            STANDARD = { ON = 08, OFF = 9 }
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(DiagnosticCodes.TomlSyntax, error.Code);
        Assert.Equal(6, error.Line);
    }

    // TOML forbids a key twice in a table; the second one is the ERROR.
    [Fact]
    public void TomlSyntax_KeyTwiceInATable_ReportsAnErrorOnTheSecond()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test mill"
            name = "Other mill"
            controller = "fanuc"
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.TomlSyntax, error.Code);
        Assert.Equal(3, error.Line);
    }

    // Machine-config 1: every key of [machine].
    [Fact]
    public void Machine_EveryKey_LoadsIntoTheIdentity()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "DMU 50"
            controller = "heidenhain"
            dialect = "iTNC530"
            builder = "dmg"
            gcode_system = "A"
            s_binds_to_spindle_word = true
            limits = "clamp"
            channels = [1, 2]
            units_default = "MM"
            default_spindle = "S1"
            default_holder = "H1"
            default_workpiece = "TABLE1"
            tool_table = "dmu50.tools.toml"
            kinematics = "dmu50.kin.toml"
            """);

        MachineIdentity identity = machine.Machine;
        Assert.Equal("DMU 50", identity.Name);
        Assert.Equal(Controller.Heidenhain, identity.Controller);
        Assert.Equal("iTNC530", identity.Dialect);
        Assert.Equal("dmg", identity.Builder);
        Assert.Equal(GcodeSystem.A, identity.GcodeSystem);
        Assert.True(identity.SBindsToSpindleWord);
        Assert.Equal(LimitPolicy.Clamp, machine.Limits);
        Assert.Equal([1, 2], identity.Channels);
        Assert.Equal("MM", identity.UnitsDefault);
        Assert.Equal("S1", identity.DefaultSpindle);
        Assert.Equal("H1", identity.DefaultHolder);
        Assert.Equal("TABLE1", identity.DefaultWorkpiece);
        Assert.Equal("dmu50.tools.toml", machine.ToolTable);
        Assert.Equal("dmu50.kin.toml", machine.Kinematics?.File);
    }

    // D64: machine limits warn when the file does not say otherwise; VM 3.6: the variables defaults.
    [Fact]
    public void Defaults_KeysLeftOut_TakeTheSpecifiedDefaults()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test mill"
            controller = "siemens"
            """);

        Assert.Equal(LimitPolicy.Warn, machine.Limits);
        Assert.Equal(UnassignedVariable.Error, machine.Variables.Unassigned);
        Assert.Equal(1_000_000, machine.Variables.BlockCap);
        Assert.Equal(8, machine.Variables.CallDepth);
        Assert.Null(machine.Format);
        Assert.Empty(machine.Functions);
    }

    // Machine-config 5a: the coolant clutch rule, a function with pre and post, and the retract before the change.
    [Fact]
    public void ExpansionRule_CoolantClutchPalletChangeAndToolChange_Load()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test mill"
            controller = "fanuc"

            [coolant]
            THROUGH = { ON = "M51", OFF = "M9", requires = { SPINDLE = "OFF" }, restore = ["SPINDLE"] }

            [func]
            PALLET_CHANGE = { RUN = "M60", pre = ["HOME Z", "HOME X Y"], post = ["ORIGIN=1"] }

            [tool_change]
            pre = ["RAPID {position:tool_change} FRAME=MACHINE"]
            """);

        FunctionTable through = machine.Coolant["THROUGH"];
        Assert.Equal(["ON", "OFF"], through.States.Keys);
        Assert.Equal("OFF", through.Rule?.Requires["SPINDLE"]);
        Assert.Equal(["SPINDLE"], through.Rule?.Restore);
        FunctionTable pallet = machine.Functions["PALLET_CHANGE"];
        Assert.Equal(["HOME Z", "HOME X Y"], pallet.Rule?.Pre);
        Assert.Equal(["ORIGIN=1"], pallet.Rule?.Post);
        Assert.Equal(["RAPID {position:tool_change} FRAME=MACHINE"], machine.ToolChange?.Rule?.Pre);
    }

    // D56: a table bound to one channel, and a function bound to one channel.
    [Fact]
    public void ChannelBinding_SpindleSyncAndDoor_LoadTheirChannel()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [spindle_sync]
            ON = "M96"
            OFF = "M97"
            PHASE = "M92"
            channel = 2

            [func]
            DOOR = { OPEN = "M68", CLOSE = "M69", channel = 1 }
            """);

        Assert.Equal(2, machine.SpindleSync?.Channel);
        Assert.False(machine.SpindleSync?.AllChannels);
        Assert.Equal(["ON", "OFF", "PHASE"], machine.SpindleSync?.States.Keys);
        Assert.Equal(1, machine.Functions["DOOR"].Channel);
        Assert.Equal(["OPEN", "CLOSE"], machine.Functions["DOOR"].States.Keys);
    }

    // D56: channels = "all" is the one form of that key.
    [Fact]
    public void ChannelBinding_ChannelsAllAndAnotherWord_LoadAndReport()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [spindle_sync]
            ON = "M35"
            channels = "all"
            """);
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [spindle_sync]
            ON = "M35"
            channels = "some"
            """);

        Assert.True(machine.SpindleSync?.AllChannels);
        Assert.Equal(DiagnosticCodes.ValueNotAllowed, Assert.Single(diagnostics.Items).Code);
    }

    // Machine-config 4, D100: home and limits in machine coordinates, the dynamics of the runtime estimate, the named
    // positions.
    [Fact]
    public void Axis_LimitsReferencePointsAndPositions_Load()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [[axis]]
            id = "X1"
            ncx = "X"
            letter = "X"
            incremental_letter = "U"
            kind = "linear"
            limits = [-10, 650.5]
            rapid = 30000
            max_feed = 10000
            acceleration = 3000
            programming = "diameter"
            home = 0
            home2 = -50
            clamp = { ON = "M10", OFF = "M11" }

            [positions]
            tool_change = { X = 0, Z = -120 }

            [dynamics]
            block_time = 0.001
            path_mode = "exact_stop"
            corner_speed = 2000
            """);

        AxisDef axis = Assert.Single(machine.Axes);
        Assert.Equal("X", axis.NcxName);
        Assert.Equal("U", axis.IncrementalLetter);
        Assert.Equal(AxisKind.Linear, axis.Kind);
        Assert.Equal(-10m, axis.Min);
        Assert.Equal(650.5m, axis.Max);
        Assert.Equal(30000m, axis.Rapid);
        Assert.Equal(10000m, axis.MaxFeed);
        Assert.Equal(3000m, axis.Acceleration);
        Assert.Equal(Programming.Diameter, axis.Programming);
        Assert.Equal(0m, axis.Home);
        Assert.Equal(-50m, axis.Home2);
        Assert.Equal("M10", axis.Clamp["ON"]);
        Assert.Equal(-120m, machine.Positions.Find("tool_change")?["Z"]);
        Assert.Equal(0.001m, machine.Dynamics?.BlockTime);
        Assert.Equal(PathMode.ExactStop, machine.Dynamics?.PathMode);
    }

    // Machine-config 4: limits are a minimum and a maximum.
    [Fact]
    public void Axis_LimitsWithOneNumber_ReportsWrongType()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test mill"
            controller = "fanuc"

            [[axis]]
            id = "X1"
            ncx = "X"
            kind = "linear"
            limits = [650]
            """);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.WrongType, error.Code);
        Assert.Equal(9, error.Line);
    }

    // A number that is not finite is not a number of the machine.
    [Fact]
    public void Number_Infinity_ReportsWrongType()
    {
        Diagnostics diagnostics = Report("""
            [machine]
            name = "Test mill"
            controller = "fanuc"

            [dynamics]
            block_time = inf
            """);

        Assert.Equal(DiagnosticCodes.WrongType, Assert.Single(diagnostics.Items).Code);
    }

    // Machine-config 5, D57: the selection per role, the frame and the mirror cycle of the sub spindle side.
    [Fact]
    public void Workpiece_SelectionFrameAndMirror_Load()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [workpiece]
            MAIN = "G54 M428"
            SUB = "G59 M427"
            SUB_frame = "mirror"
            SUB_mirror = { ON = "G360", OFF = "G361" }
            """);

        WorkpieceConfig? workpiece = machine.Workpiece;
        Assert.NotNull(workpiece);
        Assert.Equal("G54 M428", workpiece.Templates["MAIN"]);
        Assert.Equal("G59 M427", workpiece.Templates["SUB"]);
        Assert.Equal(WorkpieceFrame.Mirror, workpiece.Frames["SUB"]);
        Assert.Equal("G361", workpiece.Mirrors["SUB"].States["OFF"]);
    }

    // Machine-config 5, D40: a dotted key of [func_meta] names the function state.
    [Fact]
    public void FuncMeta_DottedKey_LoadsTheRuleOfTheState()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [func_meta]
            SUB_CHUCK.CLOSE = "workpiece_transfer_to = SUB"
            """);

        Assert.Equal("workpiece_transfer_to = SUB", machine.FuncMeta.Find("SUB_CHUCK", "CLOSE"));
    }

    // Machine-config 5: the [sync] marks and the Nakamura wait groups.
    [Fact]
    public void Sync_MarksAndGroups_Load()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [sync]
            wait = "M{mark} P{paths}"
            paths = "bitmask"
            mark_range = [100, 199]
            start_mark = 199
            groups = [{ channels = [1, 2], range = [100, 199] }, { channels = [3, 4], range = [800, 849] }]
            """);

        SyncConfig? sync = machine.Sync;
        Assert.NotNull(sync);
        Assert.Equal(SyncPaths.Bitmask, sync.Paths);
        Assert.Equal(new MarkRange(100, 199), sync.MarkRange);
        Assert.Equal(199, sync.StartMark);
        Assert.Equal(2, sync.Groups.Count);
        Assert.Equal([3, 4], sync.Groups[1].Channels);
        Assert.Equal(new MarkRange(800, 849), sync.Groups[1].Range);
    }

    // Implementation 16, P6-02: [format] channel_files names how the job compiler names the file of each channel
    // (the TODO(question) of OutputFormat.ChannelFiles).
    [Theory]
    [InlineData("path_suffix", ChannelFiles.PathSuffix)]
    [InlineData("channel_suffix", ChannelFiles.ChannelSuffix)]
    [InlineData("program_name", ChannelFiles.ProgramName)]
    public void ChannelFiles_EachValue_LoadsItsNaming(string value, ChannelFiles expected)
    {
        MachineConfig machine = LoadClean($"""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [format]
            channel_files = "{value}"
            """);

        Assert.Equal(expected, machine.Format?.ChannelFiles);
    }

    // Machine-config 5: transform, retract and tolerance templates, with the value maps of {move} and {mode}.
    [Fact]
    public void TransformRetractTolerance_TemplatesAndValueMaps_Load()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test mill"
            controller = "heidenhain"

            [transform]
            TILT_AXIS_ON = ""
            ROTARY_PATH_SHORTEST = "M126"
            move = { TURN = "TURN FMAX", STAY = "STAY" }

            [retract]
            MAX = "M140 MB MAX"
            BY = "M140 MB{distance}"

            [tolerance]
            ON = "CYCL DEF 32.0 TOLERANZ\nCYCL DEF 32.1 T{tol}"
            mode = { FINISH = 0, ROUGH = 1 }
            """);

        Assert.Equal("", machine.Transform?.TiltAxisOn);
        Assert.Equal("M126", machine.Transform?.RotaryPathShortest);
        Assert.Equal("TURN FMAX", machine.Transform?.Move["TURN"]);
        Assert.Equal("M140 MB{distance}", machine.Retract?.By);
        Assert.Equal("CYCL DEF 32.0 TOLERANZ\nCYCL DEF 32.1 T{tol}", machine.Tolerance?.On);
        Assert.Equal("1", machine.Tolerance?.Mode["ROUGH"]);
    }

    // Machine-config 3, D52: the values of {kind} are text; the spec writes them as numbers and as text.
    [Fact]
    public void KindMap_NumbersAndText_LoadAsTheTextOfKind()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test lathe"
            controller = "siemens"

            [tool_change]
            change = "G361 B{b} D{kind}"
            kind_map = { ROTARY = 1, TURNING = "1." }
            """);

        Assert.Equal("1", machine.ToolChange?.KindMap["ROTARY"]);
        Assert.Equal("1.", machine.ToolChange?.KindMap["TURNING"]);
    }

    // F23: channel on a resource and the [raw] table are part of the schema.
    [Fact]
    public void ResourceChannelAndRaw_KeysOfF23_LoadWithoutWarning()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [[resource]]
            id = "T2"
            type = "tool_holder"
            magazine = false
            channel = 2

            [raw]
            known = ["G411", "G300"]
            """);

        Assert.Equal(2, Assert.Single(machine.Resources).Channel);
        Assert.Equal(["G411", "G300"], machine.Raw?.Known);
    }

    // Machine-config 7: the variables table and the system variables.
    [Fact]
    public void Variables_UnassignedZeroMapAndSystemVariables_Load()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test mill"
            controller = "fanuc"

            [variables]
            unassigned = 0
            map = { Q = "#1", QL = "#", QR = "#5" }
            block_cap = 5000
            call_depth = 4

            [system_variables]
            SYS_POS_X = "#5041"
            SYS_WEAR_Z = "#11{index:03}"
            """);

        Assert.Equal(UnassignedVariable.Zero, machine.Variables.Unassigned);
        Assert.Equal("#5", machine.Variables.Map["QR"]);
        Assert.Equal(5000, machine.Variables.BlockCap);
        Assert.Equal(4, machine.Variables.CallDepth);
        Assert.Equal("#11{index:03}", machine.SystemVariables.Find("SYS_WEAR_Z"));
    }

    // Machine-config 9: the kinematic tree is loaded and not interpreted.
    [Fact]
    public void Kinematics_Nodes_LoadAsWritten()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test mill"
            controller = "heidenhain"

            [[node]]
            id = "X1"
            parent = "BASE"
            type = "linear"
            direction = [1, 0, 0]

            [[node]]
            id = "TABLE1"
            parent = "X1"
            """);

        Assert.NotNull(machine.Kinematics);
        Assert.Equal(2, machine.Kinematics.Nodes.Count);
        Assert.Equal([1m, 0m, 0m], machine.Kinematics.Nodes[0].Direction);
        Assert.Null(machine.Kinematics.Nodes[1].Type);
    }

    // Machine-config 6: [cycles] names the catalog; a machine may override a catalog entry with [[cycle]].
    [Fact]
    public void Cycles_CatalogAndCycleEntry_LoadWithoutWarning()
    {
        MachineConfig machine = LoadClean("""
            [machine]
            name = "Test lathe"
            controller = "fanuc"

            [cycles]
            catalog = "fanuc.toml"

            [[cycle]]
            name = "PECK"
            native = "G83"
            pre = ["FUNC:PECK_MODE=RETRACT"]
            """);

        Assert.Equal("fanuc.toml", machine.Cycles?.CatalogFile);
    }

    // Loads a machine file that is expected to report something and returns what it reported.
    private static Diagnostics Report(string toml)
    {
        var diagnostics = new Diagnostics("test.toml");
        MachineConfigLoader.LoadText(toml, diagnostics);
        return diagnostics;
    }

    // Loads a machine file that is expected to be clean; a failure prints the diagnostics.
    private static MachineConfig LoadClean(string toml)
    {
        var diagnostics = new Diagnostics("test.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText(toml, diagnostics);
        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(machine);
        return machine;
    }
}
