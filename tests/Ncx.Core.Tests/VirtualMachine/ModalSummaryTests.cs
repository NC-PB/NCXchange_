using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// The modal summary of virtual machine 4, one test per row: what stays, and what resets it.
/// </summary>
public sealed class ModalSummaryTests
{
    // VM 4, row "units, workplane, origin, transform chain, diameter": modal, reset by an explicit word only; ORIGIN
    // empties the chain.
    [Fact]
    public void UnitsWorkplaneOriginChainDiameter_ModalUntilAnExplicitWord_OriginEmptiesTheChain()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute(
            "UNITS=INCH WORKPLANE=ZX ORIGIN=2 DIAMETER=ON",
            "SHIFT Z=-5",
            "TILT B=45",
            "COMMENT=\"NOTHING CHANGES HERE\"",
            "PROGRAM=END");
        FrameState frame = vm.State.Frame;

        Assert.Equal(Units.Inch, frame.Units);
        Assert.Equal(Workplane.ZX, frame.Workplane);
        Assert.Equal(2, frame.Origin);
        Assert.True(frame.Diameter);
        Assert.Equal(new[] { TransformKind.Shift, TransformKind.Tilt }, Kinds(frame.Chain));

        vm.Execute("ORIGIN=1");

        Assert.Equal(1, frame.Origin);
        Assert.Empty(frame.Chain);
    }

    // VM 4, row "setpos shift": modal, reset by SETPOS on the same axis and by ORIGIN.
    [Fact]
    public void SetposShift_ModalUntilSetposOnTheSameAxisOrOrigin()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("HOME X Z", "SETPOS X=100");
        Assert.Equal(200m, vm.State.Frame.SetposShift["X"]);

        vm.Execute("SETPOS Z=0", "PROGRAM=END");
        Assert.Equal(200m, vm.State.Frame.SetposShift["X"]);
        Assert.Equal(450m, vm.State.Frame.SetposShift["Z"]);

        vm.Execute("SETPOS X=50");
        Assert.Equal(250m, vm.State.Frame.SetposShift["X"]);

        vm.Execute("ORIGIN=1");
        Assert.Equal(0m, vm.State.Frame.SetposShift["X"]);
        Assert.Equal(0m, vm.State.Frame.SetposShift["Z"]);
    }

    // VM 4, row "verb, FRAME, IF, ARG, TIMES, WITH": block scoped, reset at the end of their block (VM 3 step 6).
    [Fact]
    public void VerbFrameIfArgTimesWith_BlockScoped_EndWithTheirBlock()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("RAPID Z=0 FRAME=MACHINE");

        Assert.Null(vm.State.Motion.BlockVerb);
        Assert.False(vm.State.Frame.MachineFrameBlock);

        vm.Execute("CALL=10 TIMES=2 ARG:V1=5", "SYNC=1 WITH=1,2", "JUMP=1 IF={$Q1 < 2}");

        Assert.Null(vm.State.Vars.Get("V1"));
        Assert.Null(vm.State.Motion.BlockVerb);
        vm.AssertNoDiagnostics();
    }

    // VM 4, row "feed value and mode, compensation": modal, reset by an explicit word and by PROGRAM=END.
    [Fact]
    public void FeedValueAndModeCompensation_ModalUntilAnExplicitWord_ProgramEndResetsThem()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("F=200 FEED_MODE=PER_REV COMP=LEFT", "COOLANT=ON");
        MotionState motion = vm.State.Motion;

        Assert.Equal(200m, motion.Feed);
        Assert.Equal(FeedMode.PerRev, motion.FeedMode);
        Assert.Equal(Compensation.Left, motion.Comp);

        vm.Execute("PROGRAM=END");

        Assert.Null(motion.Feed);
        Assert.Equal(FeedMode.PerMin, motion.FeedMode);
        Assert.Equal(Compensation.Off, motion.Comp);
    }

    // VM 4, row "tool in spindle, preloaded tool, offsets": modal; the preload is consumed by TOOL; PROGRAM=END keeps
    // the tool in the spindle.
    [Fact]
    public void ToolPreloadOffsets_ModalPreloadConsumedByTool_ProgramEndKeepsTheTool()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute(
            "TOOL=4 OFFSET:LEN=4 OFFSET:RAD=4", "PRELOAD=5", "COOLANT=ON");
        HolderState holder = vm.State.Holders["H1"];

        Assert.Equal(new ToolRef(4), holder.SpindleTool);
        Assert.Equal(new ToolRef(5), holder.Preloaded);

        vm.Execute("TOOL=5");
        Assert.Null(holder.Preloaded);

        vm.Execute("PRELOAD=6", "PROGRAM=END");
        Assert.Equal(new ToolRef(5), holder.SpindleTool);
        Assert.Equal(new ToolRef(6), holder.Preloaded);
        Assert.Equal(4, holder.OffsetLen);
        Assert.Equal(4, holder.OffsetRad);
    }

    // VM 4, row "spindle direction, rpm, vc, mode, sync and phase, css": modal; PROGRAM=END sets OFF, mode SPINDLE,
    // sync OFF.
    [Fact]
    public void SpindleDirectionRpmVcModeSyncPhaseCss_ModalUntilAnExplicitWord_ProgramEndSetsOffModeSpindleSyncOff()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute(
            "SPINDLE:MAIN=CW RPM:MAIN=1500 CSS:MAIN=ON VC:MAIN=140",
            "SPINDLE_SYNC=MAIN,SUB PHASE=113.5",
            "SPINDLE_MODE:MAIN=AXIS",
            "COOLANT=ON");
        SpindleState main = vm.State.Spindles["S1"];
        SpindleState sub = vm.State.Spindles["S2"];

        Assert.Equal(SpindleDirection.Clockwise, main.Direction);
        Assert.Equal(1500m, main.Rpm);
        Assert.True(main.Css);
        Assert.Equal(140m, main.Vc);
        Assert.Equal(SpindleMode.Axis, main.Mode);
        Assert.Equal("S1", sub.SyncPartner);
        Assert.Equal(113.5m, sub.SyncPhase);

        vm.Execute("PROGRAM=END");

        Assert.Equal(SpindleDirection.Off, main.Direction);
        Assert.Equal(SpindleMode.Spindle, main.Mode);
        Assert.Null(sub.SyncPartner);
        Assert.Null(sub.SyncPhase);
        Assert.Equal(1500m, main.Rpm);
        Assert.Equal(140m, main.Vc);
    }

    // VM 4, row "cylinder, polar, tcpm, rotary path and feed, tolerance": modal; PROGRAM=END sets OFF and the
    // defaults.
    [Fact]
    public void CylinderPolarTcpmRotaryPathAndFeedTolerance_ModalUntilAnExplicitWord_ProgramEndSetsOffAndDefaults()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute(
            "CYLINDER=30 POLAR=ON TCPM=ON ROTARY_PATH=SHORTEST ROTARY_FEED=MM_MIN",
            "TOLERANCE=0.02 TOLERANCE:ROTARY=0.05 TOLERANCE_MODE=ROUGH",
            "COOLANT=ON");
        FrameState frame = vm.State.Frame;

        Assert.Equal(30m, frame.Cylinder);
        Assert.True(frame.Polar);
        Assert.True(frame.Tcpm);
        Assert.Equal(RotaryPath.Shortest, frame.RotaryPath);
        Assert.Equal(RotaryFeed.MmMin, frame.RotaryFeed);
        Assert.Equal(new ToleranceState { Value = 0.02m, Rotary = 0.05m, Mode = ToleranceMode.Rough }, frame.Tolerance);

        vm.Execute("PROGRAM=END");

        Assert.Null(frame.Cylinder);
        Assert.False(frame.Polar);
        Assert.False(frame.Tcpm);
        Assert.Equal(RotaryPath.Full, frame.RotaryPath);
        Assert.Equal(RotaryFeed.DegMin, frame.RotaryFeed);
        Assert.Equal(new ToleranceState { Value = null, Rotary = null, Mode = ToleranceMode.Finish }, frame.Tolerance);
    }

    // VM 4, row "SKIP, PHASE, POINT": block scoped, end with their block.
    [Fact]
    public void SkipPhasePoint_BlockScoped_EndWithTheirBlock()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("SKIP SPINDLE=CW");

        Assert.False(vm.State.Motion.Skip);
        Assert.Equal(SpindleDirection.Clockwise, vm.State.Spindles["S1"].Direction);

        vm.Execute("SPINDLE_SYNC=MAIN,SUB PHASE=90", "SPINDLE_SYNC=OFF", "SPINDLE_SYNC=MAIN,SUB");
        Assert.Null(vm.State.Spindles["S2"].SyncPhase);

        vm.Execute("HOME X POINT=2", "HOME X");
        Assert.Equal(300m, vm.Position("X").Value);
    }

    // VM 4, row "coolant, named functions": modal; PROGRAM=END sets the coolant OFF and keeps the functions.
    [Fact]
    public void CoolantNamedFunctions_ModalUntilAnExplicitWord_ProgramEndSetsCoolantOff()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute(
            "COOLANT=ON COOLANT:THROUGH=ON FUNC:SUB_CHUCK=OPEN", "COMMENT=\"NOTHING CHANGES HERE\"");

        Assert.True(vm.State.Coolant["STANDARD"]);
        Assert.True(vm.State.Coolant["THROUGH"]);
        Assert.Equal("OPEN", vm.State.Functions["SUB_CHUCK"]);

        vm.Execute("PROGRAM=END");

        Assert.False(vm.State.Coolant["STANDARD"]);
        Assert.False(vm.State.Coolant["THROUGH"]);
        Assert.Equal("OPEN", vm.State.Functions["SUB_CHUCK"]);
    }

    // VM 4, row "cycle": modal, reset by CYCLE=OFF, the next CYCLE, TOOL (with a WARNING) and PROGRAM=END; a new CYCLE
    // replaces all parameters (VM 2.6).
    [Fact]
    public void Cycle_ModalUntilCycleOffNextCycleToolOrProgramEnd()
    {
        string[] drillParameters = ["SURFACE", "CLEARANCE", "DEPTH", "PECK"];
        string[] dwellParameters = ["CLEARANCE", "DEPTH", "CYCLE_DWELL"];
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute(
            "CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-10 PECK=2", "COOLANT=ON");
        Assert.Equal("DRILL", vm.State.Cycle.Name);
        Assert.Equal(drillParameters, Keys(vm.State.Cycle.Parameters));

        vm.Execute("CYCLE=DRILL_DWELL CLEARANCE=2 DEPTH=-5 CYCLE_DWELL=0.5");
        Assert.Equal("DRILL_DWELL", vm.State.Cycle.Name);
        Assert.Equal(dwellParameters, Keys(vm.State.Cycle.Parameters));

        vm.Execute("TOOL=2");
        Assert.Null(vm.State.Cycle.Name);
        Assert.Equal(1, vm.Count(DiagnosticCodes.ToolChangeWhileCycleActive));

        vm.Execute("CYCLE=DRILL CLEARANCE=2 DEPTH=-5", "PROGRAM=END");
        Assert.Null(vm.State.Cycle.Name);

        vm.Execute("CYCLE=DRILL CLEARANCE=2 DEPTH=-5", "CYCLE=OFF");
        Assert.Null(vm.State.Cycle.Name);
        Assert.Empty(vm.State.Cycle.Parameters);
    }

    // VM 4, row "variables": program lifetime; V1 to V33 per call, restored at SUB=END and RETURN.
    [Fact]
    public void Variables_ProgramLifetime_LocalsPerCallRestoredAtSubEnd()
    {
        string text = """
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="VARIABLES"
            VAR:Q1=10 VAR:V1=1
            CALL=10 ARG:V2=5
            PROGRAM=END
            SUB=BEGIN NAME=10
            VAR:Q2=3 VAR:V3=7
            SUB=END
            FILE=END
            """;

        VmHarness vm = VmHarness.Run(text, VmMachines.Default());

        vm.AssertNoDiagnostics();
        Assert.Equal("10", vm.State.Vars.Get("Q1")?.ToString());
        Assert.Equal("3", vm.State.Vars.Get("Q2")?.ToString());
        Assert.Equal("1", vm.State.Vars.Get("V1")?.ToString());
        Assert.Null(vm.State.Vars.Get("V2"));
        Assert.Null(vm.State.Vars.Get("V3"));
    }

    // VM 4, row "workpiece holder": modal; kept at PROGRAM=END.
    [Fact]
    public void WorkpieceHolder_ModalUntilAnExplicitWord_KeptAtProgramEnd()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("WORKPIECE=SUB", "COOLANT=ON", "PROGRAM=END");

        Assert.Equal("S2", vm.State.Frame.WorkpieceHolder);
    }

    private static List<TransformKind> Kinds(IEnumerable<TransformEntry> chain)
    {
        var kinds = new List<TransformKind>();
        foreach (TransformEntry entry in chain)
        {
            kinds.Add(entry.Kind);
        }

        return kinds;
    }

    private static List<string> Keys(IEnumerable<Word> words)
    {
        var keys = new List<string>();
        foreach (Word word in words)
        {
            keys.Add(word.Key);
        }

        return keys;
    }
}
