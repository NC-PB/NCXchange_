using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine.State;

/// <summary>
/// The variables of virtual machine 2.7 and 3.6: start values, Get and Set with UNKNOWN as a value, the locals of a
/// call, the SYS_ names, and a snapshot that keeps the values it was taken with.
/// </summary>
public sealed class VariableStoreTests
{
    // VM 2.7: vars start empty without a vars file.
    [Fact]
    public void Vars_AtStart_AreEmpty()
    {
        Assert.Empty(new ChannelState(StateMachines.MillTurn()).Snapshot().Vars);
    }

    // VM 2.7, 3.6: variables start from <file>.vars.toml when there is one.
    [Fact]
    public void Vars_WithStartValues_HoldTheValuesOfTheVarsFile()
    {
        var startValues = new Dictionary<string, Value>
        {
            ["Q1"] = new IntegerValue(10, "10"),
            ["Q2"] = new DecimalValue(5.5m, "5.5"),
            ["QS1"] = new StringValue("TEXT"),
        };

        VariableStore vars = new ChannelState(StateMachines.MillTurn(), startValues: startValues).Vars;

        Assert.Equal(new IntegerValue(10, "10"), vars.Get("Q1")?.Value);
        Assert.Equal(new DecimalValue(5.5m, "5.5"), vars.Get("Q2")?.Value);
        Assert.Equal(new StringValue("TEXT"), vars.Get("QS1")?.Value);
    }

    // VM 2.7: a SYS_ name is never assigned; its start value does not become a variable of the program.
    [Fact]
    public void Vars_StartValueOfASystemName_IsNotAVariableOfTheProgram()
    {
        var startValues = new Dictionary<string, Value> { ["SYS_WEAR_Z"] = new DecimalValue(0.02m, "0.02") };

        var state = new ChannelState(StateMachines.MillTurn(), startValues: startValues);

        Assert.Empty(state.Snapshot().Vars);
        Assert.True(state.Vars.GetSystem("SYS_WEAR_Z", 99).IsUnknown);
    }

    // VM 2.7, 3.6, wave-1 question #89: the vars file gives a SYS_ name its start value by the name as the program
    // writes it, with its index for a register; INTERPRETED mode reads it where the state gives none.
    [Fact]
    public void GetSystemStartValue_NameWithAndWithoutAnIndex_ReadsTheKeyOfTheVarsFile()
    {
        var startValues = new Dictionary<string, Value>
        {
            ["SYS_WEAR_Z[99]"] = new DecimalValue(0.012m, "0.012"),
            ["SYS_PART_MAIN"] = new IntegerValue(1, "1"),
        };

        var state = new ChannelState(StateMachines.MillTurn(), startValues: startValues);

        Assert.Equal(new DecimalValue(0.012m, "0.012"), state.Vars.GetSystemStartValue("SYS_WEAR_Z", 99)?.Value);
        Assert.Null(state.Vars.GetSystemStartValue("SYS_WEAR_Z", 98));
        Assert.Equal(new IntegerValue(1, "1"), state.Vars.GetSystemStartValue("SYS_PART_MAIN", null)?.Value);
        Assert.Empty(state.Snapshot().Vars);
    }

    // Language 4.12, VM 2.7, 3.4: $SYS_POS_X reads the position of X in the workpiece frame and $SYS_MPOS_Z the
    // machine position of Z, through the mapping of the configuration; a name it does not map is UNKNOWN.
    [Fact]
    public void GetSystem_PositionsOfKnownAxes_ReadTheirCoordinateInTheFrame()
    {
        MachineConfig machine = StateMachines.MillTurn() with
        {
            SystemVariables = new SystemVariables
            {
                Entries = new Dictionary<string, string> { ["SYS_POS_X"] = "$AA_IW[X]", ["SYS_MPOS_Z"] = "$AA_IM[Z]" },
            },
        };
        var state = new ChannelState(machine);
        state.Motion.Position["X"] = new AxisPosition(12.5m, PositionFrame.Workpiece, Known: true);

        Assert.Equal(new DecimalValue(12.5m, "12.5"), state.Vars.GetSystem("SYS_POS_X", null).Value);
        Assert.Equal(new IntegerValue(450, "450"), state.Vars.GetSystem("SYS_MPOS_Z", null).Value);
        Assert.True(state.Vars.GetSystem("SYS_POS_Z", null).IsUnknown);
        Assert.True(state.Vars.GetSystem("SYS_POS_X", 2).IsUnknown);
    }

    // VM 2.7, language 4.9: VAR assigns a variable; reading it gives its value.
    [Fact]
    public void Get_AssignedVariable_ReadsItsValue()
    {
        VariableStore vars = Vars();
        vars.Set("Q1", VariableValue.Of(new IntegerValue(10, "10")));

        Assert.Equal(VariableValue.Of(new IntegerValue(10, "10")), vars.Get("Q1"));
    }

    // VM 3.6, D38: reading an unassigned variable is an ERROR; the store has no value and the caller reports it.
    [Fact]
    public void Get_UnassignedVariable_HasNoValue()
    {
        Assert.Null(Vars().Get("Q9"));
    }

    // VM 3.6, D38: under unassigned = 0 an unassigned variable reads 0.
    [Fact]
    public void Get_UnassignedVariableUnderUnassignedZero_ReadsZero()
    {
        MachineConfig machine = StateMachines.MillTurn() with
        {
            Variables = new VariablesConfig { Unassigned = UnassignedVariable.Zero },
        };

        VariableValue? value = new ChannelState(machine).Vars.Get("Q9");

        Assert.Equal(new IntegerValue(0, "0"), value?.Value);
    }

    // VM 1: in STATIC mode a variable set from an expression holds UNKNOWN, and reads back UNKNOWN.
    [Fact]
    public void Set_Unknown_ReadsBackUnknown()
    {
        VariableStore vars = Vars();
        vars.Set("Q1", VariableValue.Unknown);

        VariableValue? value = vars.Get("Q1");

        Assert.NotNull(value);
        Assert.True(value.IsUnknown);
        Assert.Null(value.Value);
    }

    // VM 2.7: a new VAR replaces the value.
    [Fact]
    public void Set_Again_ReplacesTheValue()
    {
        VariableStore vars = Vars();
        vars.Set("Q1", VariableValue.Of(new IntegerValue(10, "10")));

        vars.Set("Q1", VariableValue.Of(new IntegerValue(30, "30")));

        Assert.Equal(new IntegerValue(30, "30"), vars.Get("Q1")?.Value);
    }

    // VM 2.7, 5: assignment to a SYS_ variable is an ERROR the VM reports first; the store refuses it.
    [Fact]
    public void Set_SystemVariable_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Vars().Set("SYS_POS_X", VariableValue.Unknown));
    }

    // VM 2.7: the snapshot keeps the variables while the live store changes, UNKNOWN included.
    [Fact]
    public void Vars_ChangedAfterASnapshot_SnapshotKeepsTheValues()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Vars.Set("Q1", VariableValue.Of(new IntegerValue(10, "10")));
        state.Vars.Set("Q3", VariableValue.Unknown);

        ChannelSnapshot snapshot = state.Snapshot();
        state.Vars.Set("Q1", VariableValue.Of(new IntegerValue(30, "30")));
        state.Vars.Set("Q2", VariableValue.Of(new IntegerValue(5, "5")));

        Assert.Equal(new IntegerValue(10, "10"), snapshot.Vars["Q1"].Value);
        Assert.True(snapshot.Vars["Q3"].IsUnknown);
        Assert.False(snapshot.Vars.ContainsKey("Q2"));
    }

    // VM 3.6: V1 to V33 are the locals of a call; V100 and Q1 are not.
    [Theory]
    [InlineData("V1", true)]
    [InlineData("V33", true)]
    [InlineData("V34", false)]
    [InlineData("V0", false)]
    [InlineData("V01", false)]
    [InlineData("V100", false)]
    [InlineData("Q1", false)]
    [InlineData("V", false)]
    public void IsLocal_Name_IsTrueForV1ToV33(string name, bool local)
    {
        Assert.Equal(local, VariableStore.IsLocal(name));
    }

    // VM 3.6: CALL pushes the locals V1 to V33; the callee does not see the caller's.
    [Fact]
    public void PushLocals_CallerLocals_AreNotSeenByTheCallee()
    {
        VariableStore vars = Vars();
        vars.Set("V1", VariableValue.Of(new IntegerValue(7, "7")));

        vars.PushLocals();

        Assert.Null(vars.Get("V1"));
    }

    // VM 3.6: SUB=END and RETURN restore the caller's locals.
    [Fact]
    public void PopLocals_AfterTheCall_RestoresTheCallerLocals()
    {
        VariableStore vars = Vars();
        vars.Set("V1", VariableValue.Of(new IntegerValue(7, "7")));
        vars.PushLocals();
        vars.Set("V1", VariableValue.Of(new IntegerValue(1, "1")));
        vars.Set("V2", VariableValue.Of(new IntegerValue(2, "2")));

        vars.PopLocals();

        Assert.Equal(new IntegerValue(7, "7"), vars.Get("V1")?.Value);
        Assert.Null(vars.Get("V2"));
    }

    // VM 4: variables live for the program; only V1 to V33 are per call, so Q1 and V100 set in the callee stay.
    [Fact]
    public void PushLocals_ProgramVariables_StayVisibleAcrossTheCall()
    {
        VariableStore vars = Vars();
        vars.Set("Q1", VariableValue.Of(new IntegerValue(10, "10")));
        vars.PushLocals();
        vars.Set("V100", VariableValue.Of(new IntegerValue(3, "3")));

        vars.PopLocals();

        Assert.Equal(new IntegerValue(10, "10"), vars.Get("Q1")?.Value);
        Assert.Equal(new IntegerValue(3, "3"), vars.Get("V100")?.Value);
    }

    // VM 2.7: the snapshot shows the locals of the call that runs.
    [Fact]
    public void Snapshot_InsideACall_ShowsTheCalleeLocals()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Vars.Set("V1", VariableValue.Of(new IntegerValue(7, "7")));
        state.Vars.PushLocals();
        state.Vars.Set("V2", VariableValue.Of(new IntegerValue(2, "2")));

        ChannelSnapshot snapshot = state.Snapshot();

        Assert.False(snapshot.Vars.ContainsKey("V1"));
        Assert.Equal(new IntegerValue(2, "2"), snapshot.Vars["V2"].Value);
    }

    // SUB=END without a CALL is a mistake of the VM, not of the program (VM 3.6: RETURN in a program is JUMP=END).
    [Fact]
    public void PopLocals_WithoutAPush_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Vars().PopLocals());
    }

    // VM 2.7, 3.6: a SYS_ name the configuration does not map is UNKNOWN.
    [Fact]
    public void GetSystem_NameTheConfigurationDoesNotMap_IsUnknown()
    {
        Assert.True(Vars().GetSystem("SYS_MPOS_B", null).IsUnknown);
    }

    // Machine-config 7: an empty template names a value the control cannot read: UNKNOWN.
    [Fact]
    public void GetSystem_EmptyTemplate_IsUnknown()
    {
        Assert.True(Vars().GetSystem("SYS_PART_MAIN", null).IsUnknown);
    }

    // VM 2.7: a wear register is a state the VM does not hold, UNKNOWN whatever the index.
    [Fact]
    public void GetSystem_WearRegister_IsUnknown()
    {
        Assert.True(Vars().GetSystem("SYS_WEAR_Z", 99).IsUnknown);
    }

    // Language 4.12, VM 3.6: SYS_TOOL is the tool in the spindle of the holder called last.
    [Fact]
    public void GetSystem_ActiveTool_IsTheToolInTheSpindleOfTheLastHolder()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Holders["H1"].SpindleTool = new ToolRef(4);
        state.Holders["H2"].SpindleTool = new ToolRef(12);
        state.LastHolder = "H2";

        Assert.Equal(new IntegerValue(12, "12"), state.Vars.GetSystem("SYS_TOOL", null).Value);
    }

    // Language 4.4: a tool by name reads as its name, a string.
    [Fact]
    public void GetSystem_ActiveToolByName_IsTheNameAsAString()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Holders["H1"].SpindleTool = new ToolRef("DRILL_D8");

        Assert.Equal(new StringValue("DRILL_D8"), state.Vars.GetSystem("SYS_TOOL", null).Value);
    }

    // VM 2.7: reading a SYS_ name reads the state through GetSystem.
    [Fact]
    public void Get_SystemName_ReadsTheStateOfTheChannel()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Holders["H1"].SpindleTool = new ToolRef(4);

        Assert.Equal(new IntegerValue(4, "4"), state.Vars.Get("SYS_TOOL")?.Value);
        Assert.True(state.Vars.Get("SYS_POS_X")?.IsUnknown);
    }

    private static VariableStore Vars()
    {
        return new ChannelState(StateMachines.MillTurn()).Vars;
    }
}
