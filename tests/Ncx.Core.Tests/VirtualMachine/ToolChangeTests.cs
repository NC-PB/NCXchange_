using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// The tool change of virtual machine 3.5, one test per row of its table, and one per transition of the tool change
/// states of architecture 5.2; the offsets on the holder of the last TOOL (3.8 rule 2).
/// </summary>
public sealed class ToolChangeTests
{
    // VM 3.5, row PRELOAD=n: preloaded = n.
    [Fact]
    public void PreloadN_NothingInTheSpindle_PreparesTheTool()
    {
        HolderState holder = Holder(new VmHarness(VmMachines.Default()).Execute("PRELOAD=5"));

        Assert.Equal(new ToolRef(5), holder.Preloaded);
        Assert.Equal(new ToolRef(0), holder.SpindleTool);
    }

    // VM 3.5, row PRELOAD=n: a WARNING when n is already in the spindle, a no-op on the machine (architecture 5.2:
    // changes nothing).
    [Fact]
    public void PreloadN_ToolAlreadyInTheSpindle_WarnsAndChangesNothing()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOOL=4", "PRELOAD=5", "PRELOAD=4");

        Assert.Equal([DiagnosticCodes.ToolAlreadyInSpindle], vm.Codes());
        Assert.Equal(new ToolRef(5), Holder(vm).Preloaded);
    }

    // VM 3.5, row PRELOAD=n: 0 clears.
    [Fact]
    public void Preload0_PendingPreload_ClearsIt()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOOL=4", "PRELOAD=5", "PRELOAD=0");

        vm.AssertNoDiagnostics();
        Assert.Null(Holder(vm).Preloaded);
    }

    // VM 3.5, row TOOL=n: spindleTool = n; the preload of n is consumed.
    [Fact]
    public void ToolN_ThePreloadedTool_ConsumesThePreload()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("PRELOAD=4", "TOOL=4");

        vm.AssertNoDiagnostics();
        Assert.Equal(new ToolRef(4), Holder(vm).SpindleTool);
        Assert.Null(Holder(vm).Preloaded);
    }

    // VM 3.5, row TOOL=n, D42: a different tool preloaded is a WARNING, the magazine has to cycle twice.
    [Fact]
    public void ToolN_DifferentToolPreloaded_WarnsPreloadMismatch()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("PRELOAD=5", "TOOL=4");

        Assert.Equal([DiagnosticCodes.PreloadMismatch], vm.Codes());
        Assert.Equal(["Tool 5 was preloaded but tool 4 is called (virtual machine 3.5)."],
            vm.Messages(DiagnosticCodes.PreloadMismatch));
        Assert.Equal(new ToolRef(4), Holder(vm).SpindleTool);
    }

    // VM 3.5, row TOOL: spindleTool = preloaded; preloaded = none.
    [Fact]
    public void BareTool_AToolPreloaded_ChangesToThePreloadedTool()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("PRELOAD=3", "TOOL");

        vm.AssertNoDiagnostics();
        Assert.Equal(new ToolRef(3), Holder(vm).SpindleTool);
        Assert.Null(Holder(vm).Preloaded);
    }

    // VM 3.5, row TOOL: an ERROR when nothing is preloaded.
    [Fact]
    public void BareTool_NothingPreloaded_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOOL");

        Assert.Equal([DiagnosticCodes.NothingPreloaded], vm.Codes());
        Assert.Equal(new ToolRef(0), Holder(vm).SpindleTool);
    }

    // VM 3.5, row TOOL=0: spindleTool = 0.
    [Fact]
    public void Tool0_AToolInTheSpindle_EmptiesTheSpindle()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOOL=4", "TOOL=0");

        vm.AssertNoDiagnostics();
        Assert.Equal(new ToolRef(0), Holder(vm).SpindleTool);
    }

    // VM 3.5, row TOOL=0: a WARNING while a cycle is still active; the change ends the cycle (VM 4).
    [Fact]
    public void Tool0_CycleActive_WarnsAndEndsTheCycle()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("CYCLE=DRILL CLEARANCE=2 DEPTH=-5", "TOOL=0");

        Assert.Equal([DiagnosticCodes.ToolChangeWhileCycleActive], vm.Codes());
        Assert.False(vm.State.Cycle.Active);
    }

    // VM 3.5, row TOOL=0: a WARNING while compensation is on.
    [Fact]
    public void Tool0_CompensationOn_Warns()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("COMP=LEFT", "TOOL=0");

        Assert.Equal([DiagnosticCodes.ToolChangeWithCompensationOn], vm.Codes());
    }

    // VM 5: TOOL while compensation is on is a WARNING for every change, not only TOOL=0.
    [Fact]
    public void ToolN_CompensationOn_Warns()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("COMP=RIGHT", "TOOL=4");

        Assert.Equal([DiagnosticCodes.ToolChangeWithCompensationOn], vm.Codes());
    }

    // Architecture 5.2: Empty to Loaded by TOOL=n.
    [Fact]
    public void Transition_EmptyByToolN_GoesToLoaded()
    {
        Assert.Equal(ToolChangeState.Loaded, StateAfter("TOOL=4"));
    }

    // Architecture 5.2: Empty to Pending by PRELOAD=n.
    [Fact]
    public void Transition_EmptyByPreloadN_GoesToPending()
    {
        Assert.Equal(ToolChangeState.Pending, StateAfter("PRELOAD=5"));
    }

    // Architecture 5.2: Loaded to Pending by PRELOAD=m.
    [Fact]
    public void Transition_LoadedByPreloadM_GoesToPending()
    {
        Assert.Equal(ToolChangeState.Pending, StateAfter("TOOL=4", "PRELOAD=5"));
    }

    // Architecture 5.2: Pending to Loaded by TOOL=m, the preloaded tool.
    [Fact]
    public void Transition_PendingByToolM_GoesToLoaded()
    {
        Assert.Equal(ToolChangeState.Loaded, StateAfter("PRELOAD=5", "TOOL=5"));
    }

    // Architecture 5.2: Pending to Loaded by a bare TOOL.
    [Fact]
    public void Transition_PendingByBareTool_GoesToLoaded()
    {
        Assert.Equal(ToolChangeState.Loaded, StateAfter("PRELOAD=5", "TOOL"));
    }

    // Architecture 5.2: PRELOAD=0 only drops the preload and returns to Loaded when the spindle holds a tool.
    [Fact]
    public void Transition_PendingWithAToolInTheSpindleByPreload0_ReturnsToLoaded()
    {
        Assert.Equal(ToolChangeState.Loaded, StateAfter("TOOL=4", "PRELOAD=5", "PRELOAD=0"));
    }

    // Architecture 5.2: PRELOAD=0 returns to Empty when the spindle held no tool.
    [Fact]
    public void Transition_PendingWithoutAToolInTheSpindleByPreload0_ReturnsToEmpty()
    {
        Assert.Equal(ToolChangeState.Empty, StateAfter("PRELOAD=5", "PRELOAD=0"));
    }

    // Architecture 5.2: Loaded to Empty by TOOL=0.
    [Fact]
    public void Transition_LoadedByTool0_GoesToEmpty()
    {
        Assert.Equal(ToolChangeState.Empty, StateAfter("TOOL=4", "TOOL=0"));
    }

    // Architecture 5.2: TOOL=k in Loaded changes the tool without a preload and stays in Loaded.
    [Fact]
    public void Transition_LoadedByToolK_StaysLoaded()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOOL=4", "TOOL=7");

        Assert.Equal(ToolChangeState.Loaded, Holder(vm).ToolChange);
        Assert.Equal(new ToolRef(7), Holder(vm).SpindleTool);
    }

    // Architecture 5.2: PRELOAD=m2 in Pending replaces the pending preload.
    [Fact]
    public void Transition_PendingByPreloadM2_ReplacesThePreload()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("PRELOAD=5", "PRELOAD=6");

        Assert.Equal(ToolChangeState.Pending, Holder(vm).ToolChange);
        Assert.Equal(new ToolRef(6), Holder(vm).Preloaded);
    }

    // Architecture 5.2: TOOL=k with another tool changes too, with the WARNING of VM 3.5; VM 3.5 and the sample of
    // code-guidelines 2 keep the other preload (an open question against the diagram, ToolChangeRules).
    [Fact]
    public void Transition_PendingByToolK_ChangesWithAWarningAndKeepsThePreload()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("PRELOAD=5", "TOOL=4");

        Assert.Equal([DiagnosticCodes.PreloadMismatch], vm.Codes());
        Assert.Equal(new ToolRef(4), Holder(vm).SpindleTool);
        Assert.Equal(new ToolRef(5), Holder(vm).Preloaded);
    }

    // Architecture 5.2: a bare TOOL in Empty or Loaded is an ERROR (nothing preloaded).
    [Fact]
    public void Transition_BareToolInLoaded_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOOL=4", "TOOL");

        Assert.Equal([DiagnosticCodes.NothingPreloaded], vm.Codes());
        Assert.Equal(new ToolRef(4), Holder(vm).SpindleTool);
    }

    // VM 3.8 rule 2, D103: TOOL:TURRET1=3 then OFFSET=3 goes to the holder called last, here a holder created on the
    // spot because the default machine has no TURRET1.
    [Fact]
    public void ToolWithRoleThenOffset_WithoutAMachineFile_OffsetGoesToTheHolderCalledLast()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOOL:TURRET1=3", "OFFSET=3");

        Assert.Equal([DiagnosticCodes.NotCheckedNoMachineFile], vm.Codes());
        Assert.Equal(3, vm.State.Holders["TURRET1"].OffsetCombined);
        Assert.Equal(0, vm.State.Holders["H1"].OffsetCombined);
        Assert.Equal("TURRET1", vm.State.LastHolder);
    }

    // VM 3.8 rule 2: the same with a machine file, the second turret being no default.
    [Fact]
    public void ToolWithRoleThenOffset_WithAMachineFile_OffsetGoesToTheHolderCalledLast()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("TOOL:TURRET2=3", "OFFSET=3");

        vm.AssertNoDiagnostics();
        Assert.Equal(3, vm.State.Holders["H2"].OffsetCombined);
        Assert.Equal(0, vm.State.Holders["H1"].OffsetCombined);
    }

    // Language 4.4: OFFSET:LEN, OFFSET:RAD and OFFSET set the length, radius and combined registers.
    [Fact]
    public void OffsetForms_SetTheLengthRadiusAndCombinedRegisters()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("TOOL=1", "OFFSET:LEN=11 OFFSET:RAD=12", "OFFSET=13");

        Assert.Equal(11, Holder(vm).OffsetLen);
        Assert.Equal(12, Holder(vm).OffsetRad);
        Assert.Equal(13, Holder(vm).OffsetCombined);
    }

    // VM 3.8 rule 2: in the block of a TOOL the offsets go to that TOOL's holder.
    [Fact]
    public void ToolAndOffsetInOneBlock_OffsetGoesToThatToolsHolder()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("TOOL:TURRET2=3 OFFSET:LEN=3");

        Assert.Equal(3, vm.State.Holders["H2"].OffsetLen);
        Assert.Equal(0, vm.State.Holders["H1"].OffsetLen);
    }

    // Language 4.4: TOOL=1 OFFSET=1 PRELOAD=2 in one block changes to 1 and prepares 2 (the Nakamura G340).
    [Fact]
    public void ToolAndPreloadInOneBlock_ChangesFirstAndPreloadsTheNextTool()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("PRELOAD=2 TOOL=1 OFFSET=1");

        vm.AssertNoDiagnostics();
        Assert.Equal(new ToolRef(1), Holder(vm).SpindleTool);
        Assert.Equal(new ToolRef(2), Holder(vm).Preloaded);
        Assert.Equal(1, Holder(vm).OffsetCombined);
    }

    // Language 4.4: the tool is an integer or a string.
    [Fact]
    public void ToolByName_ChangesToTheNamedTool()
    {
        VmHarness vm = new VmHarness(VmMachines.Default()).Execute("PRELOAD=\"DRILL_D8\"", "TOOL=\"DRILL_D8\"");

        vm.AssertNoDiagnostics();
        Assert.Equal(new ToolRef("DRILL_D8"), Holder(vm).SpindleTool);
        Assert.Null(Holder(vm).Preloaded);
    }

    // VM 2.3: lastHolder is set by TOOL:r.
    [Fact]
    public void Tool_WithARole_BecomesTheHolderCalledLast()
    {
        VmHarness vm = new VmHarness(VmMachines.MillTurn()).Execute("TOOL:TURRET2=5");

        Assert.Equal("H2", vm.State.LastHolder);
    }

    private static HolderState Holder(VmHarness vm)
    {
        return vm.State.Holders["H1"];
    }

    private static ToolChangeState StateAfter(params string[] lines)
    {
        return Holder(new VmHarness(VmMachines.Default()).Execute(lines)).ToolChange;
    }
}
