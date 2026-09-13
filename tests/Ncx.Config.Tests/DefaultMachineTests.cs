using Ncx.Core.Machine;

namespace Ncx.Config.Tests;

/// <summary>
/// The built-in default machine that check, trace, annotate and analyze use without a machine file (D103, virtual
/// machine 3.8).
/// </summary>
public sealed class DefaultMachineTests
{
    // D103: one work spindle MAIN with the C axis.
    [Fact]
    public void DefaultMachine_Main_IsAWorkSpindleWithTheCAxis()
    {
        MachineConfig machine = DefaultMachine.Create();

        ResourceDef? main = machine.ResolveRole("MAIN");
        Assert.NotNull(main);
        Assert.Equal(ResourceType.WorkSpindle, main.Type);
        Assert.NotNull(main.Axis);
        AxisDef? cAxis = machine.FindAxis(main.Axis);
        Assert.Equal("C", cAxis?.NcxName);
        Assert.Equal(AxisKind.Rotary, cAxis?.Kind);
        Assert.Equal(cAxis, machine.ResolveAxis("C", main.Id));
    }

    // D103: one tool holder with the tool spindle TOOL; TOOL and PRELOAD without a role address go to that holder.
    [Fact]
    public void DefaultMachine_Tool_IsTheToolSpindleOfTheDefaultHolder()
    {
        MachineConfig machine = DefaultMachine.Create();

        ResourceDef? toolSpindle = machine.ResolveRole("TOOL");
        ResourceDef? holder = machine.ResolveDefaultHolder();
        Assert.NotNull(toolSpindle);
        Assert.Equal(ResourceType.ToolSpindle, toolSpindle.Type);
        Assert.NotNull(holder);
        Assert.Equal(ResourceType.ToolHolder, holder.Type);
        Assert.Equal(toolSpindle.Id, holder.Spindle);
    }

    // D103: axes X Y Z A B C without limits and without reference points.
    [Fact]
    public void DefaultMachine_Axes_AreXYZABCWithoutLimitsOrReferencePoints()
    {
        MachineConfig machine = DefaultMachine.Create();

        var ncxNames = new List<string>();
        foreach (AxisDef axis in machine.Axes)
        {
            ncxNames.Add(axis.NcxName);
            Assert.Null(axis.Min);
            Assert.Null(axis.Max);
            Assert.Null(axis.Home);
            Assert.Null(axis.Home2);
        }

        Assert.Equal(["X", "Y", "Z", "A", "B", "C"], ncxNames);
        Assert.Equal(AxisKind.Linear, machine.ResolveAxis("Z")?.Kind);
        Assert.Equal(AxisKind.Rotary, machine.ResolveAxis("B")?.Kind);
    }

    // D103: coolant channel STANDARD, no named functions.
    [Fact]
    public void DefaultMachine_CoolantAndFunctions_StandardAndNone()
    {
        MachineConfig machine = DefaultMachine.Create();

        Assert.Equal(["STANDARD"], machine.Coolant.Keys);
        Assert.Empty(machine.Functions);
    }

    // D103: units default MM; D77: the default machine serves no reader and no compiler, so it has no controller.
    [Fact]
    public void DefaultMachine_Identity_UnitsMmAndNoController()
    {
        MachineIdentity identity = DefaultMachine.Create().Machine;

        Assert.Equal("MM", identity.UnitsDefault);
        Assert.Null(identity.Controller);
        Assert.Equal([1], identity.Channels);
    }

    // VM 3.8 rule 2: every word without a role address finds its resource on the default machine.
    [Fact]
    public void DefaultMachine_Defaults_ResolveToDeclaredResources()
    {
        MachineConfig machine = DefaultMachine.Create();

        Assert.NotNull(machine.ResolveDefaultSpindle());
        Assert.NotNull(machine.ResolveDefaultHolder());
        Assert.NotNull(machine.FindResource(machine.Machine.DefaultWorkpiece ?? ""));
    }
}
