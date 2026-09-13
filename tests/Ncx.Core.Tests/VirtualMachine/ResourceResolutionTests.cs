using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// Resource and axis resolution (virtual machine 3.8 rules 1 to 5) against a machine file, and the WARNING path of the
/// built-in default machine without one (D103); the spindle, coolant and function words per resource (language 4.5,
/// 4.6, 4.11).
/// </summary>
public sealed class ResourceResolutionTests
{
    // Language 4.5, VM 2.4: SPINDLE:MAIN=CW RPM:MAIN=1500 applies per resource.
    [Fact]
    public void SpindleAndRpmWithARole_ApplyToThatSpindleOnly()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("SPINDLE:MAIN=CW RPM:MAIN=1500", "SPINDLE:SUB=CCW RPM:SUB=800");

        vm.AssertNoDiagnostics();
        Assert.Equal(SpindleDirection.Clockwise, vm.State.Spindles["S1"].Direction);
        Assert.Equal(1500m, vm.State.Spindles["S1"].Rpm);
        Assert.Equal(SpindleDirection.Counterclockwise, vm.State.Spindles["S2"].Direction);
        Assert.Equal(800m, vm.State.Spindles["S2"].Rpm);
        Assert.Equal(SpindleDirection.Off, vm.State.Spindles["S3"].Direction);
        Assert.Equal(0m, vm.State.Spindles["S3"].Rpm);
    }

    // VM 3.8 rule 2: SPINDLE and RPM without a role address target default_spindle.
    [Fact]
    public void SpindleWithoutARole_TargetsTheDefaultSpindle()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE=CW RPM=2000");

        Assert.Equal(SpindleDirection.Clockwise, vm.State.Spindles["S1"].Direction);
        Assert.Equal(2000m, vm.State.Spindles["S1"].Rpm);
    }

    // VM 3.8 rule 2: TOOL and PRELOAD without a role address target default_holder.
    [Fact]
    public void ToolAndPreloadWithoutARole_TargetTheDefaultHolder()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("PRELOAD=5 TOOL=4");

        Assert.Equal(new ToolRef(4), vm.State.Holders["H1"].SpindleTool);
        Assert.Equal(new ToolRef(5), vm.State.Holders["H1"].Preloaded);
    }

    // VM 3.8 rule 2: a word without a role address on a machine without a resource of its kind is an ERROR.
    [Fact]
    public void ToolAndOffset_MachineWithoutAHolder_AreErrors()
    {
        VmHarness vm = new VmHarness(VmMachines.WithoutHolder()).Execute("TOOL=1 OFFSET=1");

        Assert.Equal(2, vm.Count(DiagnosticCodes.NoDefaultResource));
    }

    // VM 3.8 rule 1: an unknown role with a machine file is an ERROR.
    [Fact]
    public void UnknownRole_WithAMachineFile_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE:TAILSTOCK=CW");

        Assert.Equal([DiagnosticCodes.UnknownRole], vm.Codes());
    }

    // D103: without a machine file an unknown role is the WARNING "not checked: no machine file", once per name.
    [Fact]
    public void UnknownRole_WithoutAMachineFile_WarnsNotCheckedOncePerName()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("SPINDLE:SUB=CW", "RPM:SUB=100");

        Assert.Equal([DiagnosticCodes.NotCheckedNoMachineFile], vm.Codes());
        Assert.Contains("not checked: no machine file", vm.Messages(DiagnosticCodes.NotCheckedNoMachineFile)[0],
            StringComparison.Ordinal);
    }

    // D103: the word runs against a resource created on the spot.
    [Fact]
    public void UnknownRole_WithoutAMachineFile_RunsAgainstAResourceCreatedOnTheSpot()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("SPINDLE:SUB=CW RPM:SUB=100", "TOOL:TURRET1=3");

        Assert.Equal(SpindleDirection.Clockwise, vm.State.Spindles["SUB"].Direction);
        Assert.Equal(100m, vm.State.Spindles["SUB"].Rpm);
        Assert.Equal(new ToolRef(3), vm.State.Holders["TURRET1"].SpindleTool);
    }

    // VM 3.8 rule 1: a role of the wrong resource type is an ERROR.
    [Fact]
    public void WrongResourceType_SpindleWordOnAHolderOrToolOnASpindle_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE:TURRET1=CW", "TOOL:MAIN=3");

        Assert.Equal([DiagnosticCodes.WrongResourceType, DiagnosticCodes.WrongResourceType], vm.Codes());
    }

    // Language 4.5, VM 3.8 rule 1: SPINDLE_MODE is a word of a work spindle.
    [Fact]
    public void SpindleMode_ToolSpindle_IsWrongType()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE_MODE:TOOL=AXIS");

        Assert.Equal([DiagnosticCodes.WrongResourceType], vm.Codes());
    }

    // D103: a work spindle created on the spot gets a rotary axis of its own, so C resolves to it while it is the
    // workpiece holder (rule 3).
    [Fact]
    public void CreatedWorkSpindle_HoldsTheWorkpiece_CResolvesToItsOwnRotaryAxis()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM SPINDLE_MODE:SUB=AXIS WORKPIECE=SUB", "HOME C", "SETPOS C=10");

        Assert.Equal(1, vm.Count(DiagnosticCodes.NotCheckedNoMachineFile));
        Assert.Equal(1, vm.Count(DiagnosticCodes.HomeWithoutReferencePoint));
        Assert.False(vm.Diagnostics.HasErrors);
        Assert.Equal(new AxisPosition(10m, PositionFrame.Workpiece, Known: true), vm.Position("C_SUB"));
        Assert.Equal(AxisPosition.Unknown, vm.Position("C"));
    }

    // VM 3.8 rule 3: C resolves to the rotary axis of the current workpiece holder.
    [Fact]
    public void CWord_WorkpieceHolderWithARotaryAxis_ResolvesToIt()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("UNITS=MM SPINDLE_MODE:SUB=AXIS WORKPIECE=SUB", "HOME C");

        vm.AssertNoDiagnostics();
        Assert.Equal(new AxisPosition(0m, PositionFrame.Machine, Known: true), vm.Position("C2"));
        Assert.Equal(new AxisPosition(90m, PositionFrame.Machine, Known: true), vm.Position("C"));
    }

    // VM 3.8 rule 3, D93: a machine axis the [[axis]] list does not declare is an ERROR with a machine file.
    [Fact]
    public void UnknownMachineAxis_WithAMachineFile_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "HOME W");

        Assert.Equal([DiagnosticCodes.UnknownMachineAxis], vm.Codes());
    }

    // D103: without a machine file a machine axis the default machine lacks is the WARNING, once per name, and the
    // word runs against an axis created on the spot.
    [Fact]
    public void UnknownMachineAxis_WithoutAMachineFile_WarnsOnceAndCreatesTheAxis()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM", "RAPID Z2=-58", "RAPID Z2=-5");

        Assert.Equal([DiagnosticCodes.NotCheckedNoMachineFile], vm.Codes());
        Assert.Equal(new AxisPosition(-5m, PositionFrame.Workpiece, Known: true), vm.Position("Z2"));
        Assert.Equal(0m, vm.State.Frame.SetposShift["Z2"]);
    }

    // VM 3.8 rule 4: SPINDLE on a spindle in AXIS mode is an ERROR.
    [Fact]
    public void Spindle_SpindleInAxisMode_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE_MODE:MAIN=AXIS", "SPINDLE:MAIN=CW");

        Assert.Equal([DiagnosticCodes.SpindleInAxisMode], vm.Codes());
    }

    // VM 3.8 rule 4: back in SPINDLE mode in the same block, SPINDLE is allowed; the words of a block do not depend
    // on their order.
    [Fact]
    public void Spindle_BackInSpindleModeInTheSameBlock_IsAllowed()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("SPINDLE_MODE:MAIN=AXIS", "SPINDLE:MAIN=CW SPINDLE_MODE:MAIN=SPINDLE");

        vm.AssertNoDiagnostics();
    }

    // VM 3.8 rule 4: in SPINDLE mode, C= on the spindle's axis is an ERROR.
    [Fact]
    public void CWord_SpindleInSpindleMode_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM", "RAPID C=10");

        Assert.Equal([DiagnosticCodes.AxisOfSpindleInSpindleMode], vm.Codes());
    }

    // VM 3.8 rule 4: in AXIS mode C= is allowed, also when the mode is set in the same block (language 5 rule 3).
    [Fact]
    public void CWord_SpindleInAxisModeFromTheSameBlock_IsAllowed()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("UNITS=MM", "RAPID C=10 SPINDLE_MODE:MAIN=AXIS");

        vm.AssertNoDiagnostics();
    }

    // VM 3.8 rule 4: HOME carries a bare C, no C=.
    [Fact]
    public void HomeC_SpindleInSpindleMode_IsNoCWord()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("UNITS=MM", "HOME C");

        vm.AssertNoDiagnostics();
    }

    // Language 4.5, VM 2.4: SPINDLE_SYNC=a,b, the second follows the first, with the offset of PHASE.
    [Fact]
    public void SpindleSync_TwoWorkSpindlesWithPhase_TheSecondFollowsTheFirst()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE_SYNC=MAIN,SUB PHASE=113.5");

        vm.AssertNoDiagnostics();
        Assert.Equal("S1", vm.State.Spindles["S2"].SyncPartner);
        Assert.Equal(113.5m, vm.State.Spindles["S2"].SyncPhase);
        Assert.Null(vm.State.Spindles["S1"].SyncPartner);
    }

    // VM 3.8 rule 5: SPINDLE_SYNC needs two work spindles in SPINDLE mode.
    [Fact]
    public void SpindleSync_SpindleInAxisMode_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE_MODE:SUB=AXIS", "SPINDLE_SYNC=MAIN,SUB");

        Assert.Equal([DiagnosticCodes.SyncSpindleNotInSpindleMode], vm.Codes());
    }

    // VM 3.8 rules 1 and 5: a tool spindle is no work spindle.
    [Fact]
    public void SpindleSync_ToolSpindle_IsWrongType()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE_SYNC=MAIN,TOOL");

        Assert.Equal([DiagnosticCodes.WrongResourceType], vm.Codes());
    }

    // Language 4.5: SPINDLE_SYNC takes two spindle roles.
    [Fact]
    public void SpindleSync_ThreeRoles_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE_SYNC=MAIN,SUB,TOOL");

        Assert.Equal([DiagnosticCodes.SyncNeedsTwoSpindles], vm.Codes());
    }

    // VM 3.8 rule 5: RPM:b and SPINDLE:b while synchronized are WARNINGs.
    [Fact]
    public void RpmAndSpindleOfTheFollowingSpindle_WhileSynchronized_Warn()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("SPINDLE_SYNC=MAIN,SUB", "RPM:SUB=100", "SPINDLE:SUB=CW");

        Assert.Equal(2, vm.Count(DiagnosticCodes.SynchronizedSpindleCommanded));
    }

    // VM 3.8 rule 5: the leading spindle is commanded as usual.
    [Fact]
    public void RpmOfTheLeadingSpindle_WhileSynchronized_DoesNotWarn()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE_SYNC=MAIN,SUB", "RPM:MAIN=100");

        vm.AssertNoDiagnostics();
    }

    // Language 4.5: SPINDLE_SYNC=OFF ends the synchronization.
    [Fact]
    public void SpindleSyncOff_EndsTheSynchronization()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn())
            .Execute("SPINDLE_SYNC=MAIN,SUB PHASE=90", "SPINDLE_SYNC=OFF", "RPM:SUB=100");

        vm.AssertNoDiagnostics();
        Assert.Null(vm.State.Spindles["S2"].SyncPartner);
        Assert.Null(vm.State.Spindles["S2"].SyncPhase);
    }

    // Language 4.5: ORIENT is an oriented spindle stop; the spindle is stopped afterwards.
    [Fact]
    public void Orient_RunningSpindle_SetsTheAngleAndStopsIt()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("SPINDLE:MAIN=CW", "ORIENT:MAIN=90");

        Assert.Equal(90m, vm.State.Spindles["S1"].Orientation);
        Assert.Equal(SpindleDirection.Off, vm.State.Spindles["S1"].Direction);
    }

    // Language 4.11: CSS, VC and RPM_MAX per spindle role.
    [Fact]
    public void CssVcRpmMax_WithARole_ApplyToThatSpindleOnly()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("CSS:SUB=ON VC:SUB=140 RPM_MAX:SUB=3000");

        SpindleState sub = vm.State.Spindles["S2"];
        Assert.True(sub.Css);
        Assert.Equal(140m, sub.Vc);
        Assert.Equal(3000m, sub.RpmMax);
        Assert.False(vm.State.Spindles["S1"].Css);
    }

    // VM 1: a state variable set from an expression becomes UNKNOWN in STATIC mode, until a known value replaces it.
    [Fact]
    public void Rpm_FromAnExpressionInStaticMode_IsUnknownUntilAKnownValue()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("RPM:MAIN=1500", "RPM:MAIN={$Q1 * 2}");
        Assert.Contains("RPM:S1", vm.State.Unknown);
        Assert.Contains("RPM:S1", vm.Vm.Snapshot().Unknown);

        vm.Execute("RPM:MAIN=800");

        Assert.DoesNotContain("RPM:S1", vm.State.Unknown);
        Assert.Equal(800m, vm.State.Spindles["S1"].Rpm);
    }

    // VM 2.5, machine-config 5: FUNC:name=state sets the state of a named function.
    [Fact]
    public void Function_KnownFunctionAndState_SetsTheState()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("FUNC:SUB_CHUCK=OPEN");

        vm.AssertNoDiagnostics();
        Assert.Equal("OPEN", vm.State.Functions["SUB_CHUCK"]);
    }

    // VM 5: an unknown function with a machine file is an ERROR.
    [Fact]
    public void Function_UnknownWithAMachineFile_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("FUNC:DOOR=OPEN");

        Assert.Equal([DiagnosticCodes.UnknownFunction], vm.Codes());
    }

    // Machine-config 5, VM 5: a state the function does not list is an unknown value, an ERROR.
    [Fact]
    public void Function_UnknownStateWithAMachineFile_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("FUNC:SUB_CHUCK=HALF");

        Assert.Equal([DiagnosticCodes.UnknownFunctionState], vm.Codes());
        Assert.Null(vm.State.Functions["SUB_CHUCK"]);
    }

    // D103: without a machine file an unknown function is the WARNING, once per name, and runs against a function
    // created on the spot.
    [Fact]
    public void Function_WithoutAMachineFile_WarnsOncePerNameAndSetsTheState()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("FUNC:SUB_CHUCK=OPEN", "FUNC:SUB_CHUCK=CLOSE");

        Assert.Equal([DiagnosticCodes.NotCheckedNoMachineFile], vm.Codes());
        Assert.Equal("CLOSE", vm.State.Functions["SUB_CHUCK"]);
    }

    // VM 2.5, F29: a bare COOLANT addresses the default channel STANDARD.
    [Fact]
    public void Coolant_WithoutAnAddress_AddressesTheStandardChannel()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("COOLANT=ON");

        vm.AssertNoDiagnostics();
        Assert.True(vm.State.Coolant["STANDARD"]);
    }

    // Language 4.6: a named channel of the machine configuration.
    [Fact]
    public void Coolant_NamedChannel_SetsThatChannelOnly()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("COOLANT:THROUGH=ON");

        Assert.True(vm.State.Coolant["THROUGH"]);
        Assert.False(vm.State.Coolant["STANDARD"]);
    }

    // Language 4.6: a channel the machine file does not name is an ERROR.
    [Fact]
    public void Coolant_UnknownChannelWithAMachineFile_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("COOLANT:AIR=ON");

        Assert.Equal([DiagnosticCodes.UnknownCoolantChannel], vm.Codes());
    }

    // D103: without a machine file an unknown channel is the WARNING and runs against a channel created on the spot.
    [Fact]
    public void Coolant_UnknownChannelWithoutAMachineFile_WarnsAndCreatesIt()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("COOLANT:THROUGH=ON");

        Assert.Equal([DiagnosticCodes.NotCheckedNoMachineFile], vm.Codes());
        Assert.True(vm.State.Coolant["THROUGH"]);
    }

    // D103: WORKPIECE with a role the default machine lacks runs against a work spindle created on the spot.
    [Fact]
    public void Workpiece_UnknownRoleWithoutAMachineFile_SelectsAWorkSpindleCreatedOnTheSpot()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("WORKPIECE=SUB");

        Assert.Equal("SUB", vm.State.Frame.WorkpieceHolder);
        Assert.True(vm.State.Spindles.ContainsKey("SUB"));
    }

    // Language 4.10, VM 3.8 rule 1: the workpiece holder is a work spindle or a table.
    [Fact]
    public void Workpiece_ToolHolderRole_IsWrongType()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("WORKPIECE=TURRET1");

        Assert.Equal([DiagnosticCodes.WrongResourceType], vm.Codes());
        Assert.Equal("S1", vm.State.Frame.WorkpieceHolder);
    }
}
