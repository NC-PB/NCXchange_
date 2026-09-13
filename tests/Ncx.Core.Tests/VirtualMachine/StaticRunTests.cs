using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// STATIC mode (virtual machine 1, 3.9, D99): every program of the file once, every CALL of a subprogram followed with
/// the caller's state, TIMES=n in n passes, the call depth, the external CALL, and a subprogram nothing calls from the
/// default entry state; the run option skip_blocks (D53) and the ERROR that stops the run (2.9).
/// </summary>
public sealed class StaticRunTests
{
    // VM 1: one pass over every program of the file; each starts from the state of a channel at the start.
    [Fact]
    public void Run_TwoPrograms_WalksEachOnceFromTheStartState()
    {
        string text = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="FIRST"
            TOOL=4
            PRELOAD=4
            PROGRAM=END
            PROGRAM=BEGIN NAME="SECOND"
            TOOL=5
            PRELOAD=5
            PROGRAM=END
            FILE=END
            """;

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        Assert.Equal(2, vm.Count(DiagnosticCodes.ToolAlreadyInSpindle));
        Assert.Equal("SECOND", vm.State.Program.Name);
        Assert.Equal(new ToolRef(5), vm.State.Holders["H1"].SpindleTool);
        Assert.False(vm.Result!.Stopped);
    }

    // D99: at every CALL the subprogram is walked with the caller's state at that point.
    [Fact]
    public void Call_SubOfTheFile_IsWalkedWithTheCallersState()
    {
        string text = VmHarness.File(
            "TOOL=1", "PRELOAD=2", "CALL=10", "PROGRAM=END", "SUB=BEGIN NAME=10", "TOOL", "SUB=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        vm.AssertNoDiagnostics();
        Assert.Equal(new ToolRef(2), vm.State.Holders["H1"].SpindleTool);
    }

    // D99: a CALL with TIMES=n is walked n times in sequence, each pass from the state the previous one left.
    [Fact]
    public void CallWithTimes_WalksTheSubNTimesInSequence()
    {
        string text = VmHarness.File("CALL=10 TIMES=4", "PROGRAM=END", "SUB=BEGIN NAME=10", "SHIFT X=1", "SUB=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        Assert.Equal(4, vm.State.Frame.Chain.Count);
    }

    // D99: the second pass starts where the first ended: the preload the first consumed is gone.
    [Fact]
    public void CallWithTimes_SecondPass_StartsFromTheStateTheFirstLeft()
    {
        string text = VmHarness.File(
            "TOOL=1", "PRELOAD=2", "CALL=10 TIMES=2", "PROGRAM=END", "SUB=BEGIN NAME=10", "TOOL", "SUB=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        Assert.Equal([DiagnosticCodes.NothingPreloaded], vm.Codes());
        Assert.True(vm.Result!.Stopped);
    }

    // VM 3.9, D99: a CALL beyond the configured call depth (default 8) is the ERROR "call depth exceeded" and the
    // subprogram is not entered.
    [Fact]
    public void CallDepth_ExceededByARecursiveCall_IsAnErrorAndTheSubIsNotEntered()
    {
        string text = VmHarness.File("CALL=10", "PROGRAM=END", "SUB=BEGIN NAME=10", "SHIFT X=1", "CALL=10", "SUB=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        Assert.Equal([DiagnosticCodes.CallDepthExceeded], vm.Codes());
        Assert.Equal(8, vm.State.Frame.Chain.Count);
    }

    // VM 3.6: the depth is the configured one.
    [Fact]
    public void CallDepth_FromTheOptions_LimitsTheNesting()
    {
        string text = VmHarness.File(
            "CALL=A", "PROGRAM=END",
            "SUB=BEGIN NAME=A", "SHIFT X=1", "CALL=B", "SUB=END",
            "SUB=BEGIN NAME=B", "SHIFT X=1", "CALL=C", "SUB=END",
            "SUB=BEGIN NAME=C", "SHIFT X=1", "SUB=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.Default(), new VmOptions { CallDepth = 2 });

        Assert.Equal([DiagnosticCodes.CallDepthExceeded], vm.Codes());
        Assert.Equal(2, vm.State.Frame.Chain.Count);
    }

    // VM 1, D99: a CALL of an external program is recorded, not followed; the state after it is the state before it,
    // and the position becomes unknown.
    [Fact]
    public void ExternalCall_IsRecordedNotFollowed_AndThePositionBecomesUnknown()
    {
        string text = VmHarness.File("TOOL=3", "CALL=\"O9010\"", "PROGRAM=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.MillTurn());

        vm.AssertNoDiagnostics();
        Assert.Equal(new ToolRef(3), vm.State.Holders["H1"].SpindleTool);
        Assert.All(vm.State.Motion.Position.Values, position => Assert.Equal(AxisPosition.Unknown, position));
    }

    // VM 1, D99, D101: after an external CALL the position is unknown in every frame, also on an axis whose setpos
    // shift was recorded against the machine position; that position is gone with the record of it.
    [Fact]
    public void ExternalCall_AfterSetposAgainstTheMachinePosition_LeavesTheAxisUnknownWithoutTheRecord()
    {
        string text = VmHarness.File("HOME X", "SETPOS X=100", "CALL=\"O9010\"", "PROGRAM=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.MillTurn());

        vm.AssertNoDiagnostics();
        Assert.Equal(AxisPosition.Unknown, vm.Position("X"));
        Assert.Empty(vm.State.Frame.SetposAgainstMachine);
    }

    // VM 3.6: a CALL that names a program instead of a subprogram is an ERROR.
    [Fact]
    public void Call_NamingAProgram_IsAnError()
    {
        string text = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="FIRST"
            CALL=SECOND
            PROGRAM=END
            PROGRAM=BEGIN NAME="SECOND"
            PROGRAM=END
            FILE=END
            """;

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        Assert.Equal([DiagnosticCodes.CallOfProgram], vm.Codes());
    }

    // VM 3.6: a missing call target is an ERROR.
    [Fact]
    public void Call_MissingSub_IsAnError()
    {
        VmHarness vm = VmHarness.Run(VmHarness.File("CALL=99", "PROGRAM=END"), VmMachines.Default());

        Assert.Equal([DiagnosticCodes.CallTargetMissing], vm.Codes());
    }

    // D99: the default entry state of a subprogram nothing calls: units, workplane, diameter and feed mode as at the
    // first verb of the file's first program, everything else initial, position unknown.
    [Fact]
    public void UncalledSub_EntryState_TakesUnitsWorkplaneDiameterFeedModeFromTheFirstVerb()
    {
        string text = VmHarness.File(
            "FEED_MODE=PER_REV UNITS=INCH WORKPLANE=ZX DIAMETER=ON TOOL=4",
            "RAPID X=1",
            "UNITS=MM WORKPLANE=XY",
            "PROGRAM=END",
            "SUB=BEGIN NAME=10",
            "SUB=END");
        VmHarness vm = VmHarness.Run(text, VmMachines.MillTurn());

        ChannelState entry = vm.Vm.EntryStateOfUncalledSub(1);

        Assert.Equal(Units.Inch, entry.Frame.Units);
        Assert.Equal(Workplane.ZX, entry.Frame.Workplane);
        Assert.True(entry.Frame.Diameter);
        Assert.Equal(FeedMode.PerRev, entry.Motion.FeedMode);
        Assert.Equal("Y", entry.Cycle.Axis);
        Assert.Equal(new ToolRef(0), entry.Holders["H1"].SpindleTool);
        Assert.All(entry.Motion.Position.Values, position => Assert.Equal(AxisPosition.Unknown, position));
    }

    // D99: a subprogram nothing calls is walked once from the default entry state, with the position unknown there,
    // even on an axis whose home makes it known at the start of a program; the rules that stay report there.
    [Fact]
    public void UncalledSub_IsWalkedFromTheDefaultEntryState()
    {
        string text = VmHarness.File("PROGRAM=END", "SUB=BEGIN NAME=10", "SETPOS X=0", "SUB=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.MillTurn());

        Assert.Equal([DiagnosticCodes.SetposAxisUnknown], vm.Codes());
    }

    // VM 3.9, 5, D99: inside a subprogram nothing calls the tool rules are suppressed.
    [Fact]
    public void UncalledSub_ToolRules_AreSuppressed()
    {
        string text = VmHarness.File("PROGRAM=END", "SUB=BEGIN NAME=10", "TOOL", "PRELOAD=0", "SUB=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        vm.AssertNoDiagnostics();
        Assert.False(vm.Result!.Stopped);
    }

    // VM 3.9, D99: at a CALL the subprogram runs with the caller's state and every rule applies.
    [Fact]
    public void CalledSub_ToolRules_Apply()
    {
        string text = VmHarness.File("CALL=10", "PROGRAM=END", "SUB=BEGIN NAME=10", "TOOL", "SUB=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        Assert.Equal([DiagnosticCodes.NothingPreloaded], vm.Codes());
    }

    // VM 2.9: an ERROR stops the run; what follows it is not executed.
    [Fact]
    public void Error_InABlock_StopsTheRun()
    {
        string text = VmHarness.File("TOOL", "HOME Y", "PROGRAM=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.MillTurn());

        Assert.Equal([DiagnosticCodes.NothingPreloaded], vm.Codes());
        Assert.True(vm.Result!.Stopped);
    }

    // VM 2.9, 3.6: an ERROR the parser reported stops the run before its first block.
    [Fact]
    public void ParserError_TheRunDoesNotStart()
    {
        string text = VmHarness.File("HOME Y", "UNKNOWN_WORD=1", "PROGRAM=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.MillTurn());

        Assert.True(vm.Result!.Stopped);
        vm.AssertNoDiagnostics();
    }

    // VM 2.1, 2.7: the pre-pass collects the labels of every section.
    [Fact]
    public void Run_LabelsOfTheFile_AreCollectedPerSection()
    {
        string text = VmHarness.File("LABEL=1", "JUMP=1", "PROGRAM=END");

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        Section program = vm.State.Flow.Programs[0];
        Assert.Equal(2, vm.State.Flow.Labels[program]["1"]);
    }

    // VM 2.1: PROGRAM=BEGIN makes the program active with its name and number.
    [Fact]
    public void ProgramBegin_MakesTheProgramActiveWithItsNameAndNumber()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("PROGRAM=BEGIN NAME=\"SHAFT\" NUMBER=7");

        Assert.True(vm.State.Program.Active);
        Assert.Equal("SHAFT", vm.State.Program.Name);
        Assert.Equal(7, vm.State.Program.Number);
    }

    // VM 2.1, 2.8: PROGRAM=END ends the program and the channel is finished.
    [Fact]
    public void ProgramEnd_EndsTheProgramAndFinishesTheChannel()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("PROGRAM=BEGIN NAME=\"SHAFT\"", "PROGRAM=END");

        Assert.True(vm.State.Program.Ended);
        Assert.False(vm.State.Program.Active);
        Assert.True(vm.State.Finished);
    }

    // D53: SKIP blocks are executed by default.
    [Fact]
    public void SkipBlocks_None_ExecutesSkipBlocks()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("SKIP SPINDLE=CW");

        Assert.Equal(SpindleDirection.Clockwise, vm.State.Spindles["S1"].Direction);
    }

    // D53: skip_blocks all skips every SKIP block; a skipped block changes nothing.
    [Fact]
    public void SkipBlocks_All_SkipsEverySkipBlock()
    {
        var options = new VmOptions { SkipBlocks = SkipBlocks.Every };
        VmHarness vm = new VmHarness(VmMachines.MillTurn(), options).Execute("SKIP SPINDLE=CW", "SKIP=3 RPM=100");

        Assert.Equal(SpindleDirection.Off, vm.State.Spindles["S1"].Direction);
        Assert.Equal(0m, vm.State.Spindles["S1"].Rpm);
    }

    // VM 3.6: skip_blocks = [1, 3] skips the blocks of the numbered switches that are on.
    [Fact]
    public void SkipBlocks_Switches_SkipTheirNumberedBlocksOnly()
    {
        var options = new VmOptions { SkipBlocks = SkipBlocks.OnSwitches([1, 3]) };
        VmHarness vm = new VmHarness(VmMachines.MillTurn(), options)
            .Execute("SKIP=3 SPINDLE=CW", "SKIP=2 RPM=100", "SKIP COOLANT=ON");

        Assert.Equal(SpindleDirection.Off, vm.State.Spindles["S1"].Direction);
        Assert.Equal(100m, vm.State.Spindles["S1"].Rpm);
        Assert.True(vm.State.Coolant["STANDARD"]);
    }

    // Architecture 5: the options a machine file gives, the call depth, the block cap and unassigned of [variables].
    [Fact]
    public void VmOptions_ForAMachine_TakesTheVariablesTable()
    {
        MachineConfig machine = VmMachines.Default() with
        {
            Variables = new VariablesConfig
            {
                CallDepth = 3,
                BlockCap = 500,
                Unassigned = UnassignedVariable.Zero,
            },
        };

        VmOptions options = VmOptions.ForMachine(machine);

        Assert.Equal(3, options.CallDepth);
        Assert.Equal(500, options.BlockCap);
        Assert.Equal(UnassignedVariable.Zero, options.Unassigned);
        Assert.Null(options.ArcTolerance);
        Assert.False(options.ExpandCycles);
    }
}
