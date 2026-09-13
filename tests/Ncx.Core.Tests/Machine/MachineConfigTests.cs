using Ncx.Core.Machine;

namespace Ncx.Core.Tests.Machine;

/// <summary>
/// The lookups of the machine model that readers, compilers and the virtual machine share (virtual machine 3.8,
/// machine-config 4 and 5, architecture 6).
/// </summary>
public sealed class MachineConfigTests
{
    // VM 3.8 rule 1: a role address resolves through [roles] to a resource id.
    [Fact]
    public void ResolveRole_KnownRole_ReturnsItsResource()
    {
        ResourceDef? resource = MillTurn().ResolveRole("SUB");

        Assert.NotNull(resource);
        Assert.Equal("S2", resource.Id);
        Assert.Equal(ResourceType.WorkSpindle, resource.Type);
    }

    // VM 3.8 rule 1: an unknown role is the caller's ERROR; the lookup finds nothing.
    [Fact]
    public void ResolveRole_UnknownRole_ReturnsNull()
    {
        Assert.Null(MillTurn().ResolveRole("TURRET2"));
    }

    // A role that names no declared resource resolves to nothing either.
    [Fact]
    public void ResolveRole_RoleOfAnUndeclaredResource_ReturnsNull()
    {
        MachineConfig machine = MillTurn() with
        {
            Roles = new Dictionary<string, string> { ["MAIN"] = "S9" },
        };

        Assert.Null(machine.ResolveRole("MAIN"));
    }

    // VM 3.8 rule 2: SPINDLE without a role address targets default_spindle.
    [Fact]
    public void ResolveDefaultSpindle_DefaultSpindleGiven_ReturnsIt()
    {
        Assert.Equal("S1", MillTurn().ResolveDefaultSpindle()?.Id);
    }

    // VM 3.8 rule 2: only more than one spindle without a default is an ERROR, so the one spindle of a mill is the
    // default.
    [Fact]
    public void ResolveDefaultSpindle_OneSpindleWithoutDefault_ReturnsTheOnlySpindle()
    {
        Assert.Equal("S1", Mill().ResolveDefaultSpindle()?.Id);
    }

    // VM 3.8 rule 2: several spindles and no default leave nothing to target.
    [Fact]
    public void ResolveDefaultSpindle_SeveralSpindlesWithoutDefault_ReturnsNull()
    {
        MachineConfig machine = MillTurn() with
        {
            Machine = new MachineIdentity { Name = "Mill-turn without defaults" },
        };

        Assert.Null(machine.ResolveDefaultSpindle());
    }

    // VM 3.8 rule 2: TOOL and PRELOAD without a role address target default_holder, or the only holder.
    [Fact]
    public void ResolveDefaultHolder_OneHolderWithoutDefault_ReturnsIt()
    {
        Assert.Equal("H1", Mill().ResolveDefaultHolder()?.Id);
    }

    // VM 3.8 rule 3, D93: an explicit machine axis name resolves through the ncx names of [[axis]].
    [Fact]
    public void ResolveAxis_MachineAxisName_ResolvesThroughTheNcxName()
    {
        Assert.Equal("Z2", MillTurn().ResolveAxis("Z2")?.Id);
    }

    // VM 3.8 rule 3: anything else is the caller's ERROR; the lookup finds nothing.
    [Fact]
    public void ResolveAxis_UndeclaredName_ReturnsNull()
    {
        Assert.Null(MillTurn().ResolveAxis("W"));
    }

    // VM 3.8 rule 3, language 4.3: C names the rotary axis of the workpiece holder, C2 after WORKPIECE=SUB.
    [Fact]
    public void ResolveAxis_CWithTheSubSpindleHoldingThePart_ResolvesTheSubSpindleAxis()
    {
        Assert.Equal("C2", MillTurn().ResolveAxis("C", "S2")?.Id);
    }

    // VM 3.8 rule 3: with the main spindle as holder, C is the main spindle's C1.
    [Fact]
    public void ResolveAxis_CWithTheMainSpindleHoldingThePart_ResolvesC1()
    {
        Assert.Equal("C1", MillTurn().ResolveAxis("C", "S1")?.Id);
    }

    // The holder's rotary axis answers only to its own letter: A stays the A slide of the machine.
    [Fact]
    public void ResolveAxis_AWithTheSubSpindleHoldingThePart_ResolvesTheAxisNamedA()
    {
        Assert.Equal("A1", MillTurn().ResolveAxis("A", "S2")?.Id);
    }

    // VM 3.8 rule 3: a holder without a rotary axis leaves C to the machine axis with that NCX name.
    [Fact]
    public void ResolveAxis_HolderWithoutAnAxis_ResolvesByTheNcxName()
    {
        Assert.Equal("C1", MillTurn().ResolveAxis("C", "H1")?.Id);
    }

    // Machine-config 5: FUNC:SUB_CHUCK=OPEN compiles to the code of that state.
    [Fact]
    public void FindFunction_KnownState_ReturnsTheTemplate()
    {
        Assert.Equal("M68", MillTurn().FindFunction("SUB_CHUCK", "OPEN"));
    }

    // A function or state the file does not name is the caller's ERROR; the lookup finds nothing.
    [Fact]
    public void FindFunction_UnknownFunctionOrState_ReturnsNull()
    {
        Assert.Null(MillTurn().FindFunction("DOOR", "OPEN"));
        Assert.Null(MillTurn().FindFunction("SUB_CHUCK", "HALF_OPEN"));
    }

    // D100: expansion rules reach a named position through {position:NAME}.
    [Fact]
    public void PositionsFind_ToolChange_ReturnsItsAxisValues()
    {
        IReadOnlyDictionary<string, decimal>? toolChange = MillTurn().Positions.Find("tool_change");

        Assert.NotNull(toolChange);
        Assert.Equal(300m, toolChange["X"]);
        Assert.Equal(200m, toolChange["Z"]);
        Assert.Null(MillTurn().Positions.Find("program_end"));
    }

    // Machine-config 5, D40: the reader fallback rule of one function state.
    [Fact]
    public void FuncMetaFind_SubChuckClose_ReturnsTheRule()
    {
        Assert.Equal("workpiece_transfer_to = SUB", MillTurn().FuncMeta.Find("SUB_CHUCK", "CLOSE"));
        Assert.Null(MillTurn().FuncMeta.Find("SUB_CHUCK", "OPEN"));
    }

    // A mill-turn in the shape of millturn1.toml (D104): main spindle S1 with C1, sub spindle S2 with C2 on the slide
    // Z2, tool spindle S3 in turret H1, and an A slide as on the Mori Seiki, to keep A apart from the holder's C.
    private static MachineConfig MillTurn()
    {
        return new MachineConfig
        {
            Machine = new MachineIdentity
            {
                Name = "Mill-turn",
                Controller = Controller.Siemens,
                DefaultSpindle = "S1",
                DefaultHolder = "H1",
                DefaultWorkpiece = "S1",
            },
            Roles = new Dictionary<string, string>
            {
                ["MAIN"] = "S1",
                ["SUB"] = "S2",
                ["TOOL"] = "S3",
                ["TURRET1"] = "H1",
            },
            Resources =
            [
                new ResourceDef { Id = "S1", Type = ResourceType.WorkSpindle, Axis = "C1" },
                new ResourceDef { Id = "S2", Type = ResourceType.WorkSpindle, Axis = "C2" },
                new ResourceDef { Id = "S3", Type = ResourceType.ToolSpindle },
                new ResourceDef { Id = "H1", Type = ResourceType.ToolHolder, Spindle = "S3" },
            ],
            Axes =
            [
                new AxisDef { Id = "X1", NcxName = "X", Kind = AxisKind.Linear },
                new AxisDef { Id = "Z1", NcxName = "Z", Kind = AxisKind.Linear },
                new AxisDef { Id = "C1", NcxName = "C", Kind = AxisKind.Rotary, Owner = "S1" },
                new AxisDef { Id = "Z2", NcxName = "Z2", Kind = AxisKind.Linear, Owner = "S2" },
                new AxisDef { Id = "C2", NcxName = "C2", Kind = AxisKind.Rotary, Owner = "S2" },
                new AxisDef { Id = "A1", NcxName = "A", Kind = AxisKind.Linear, Owner = "S2" },
            ],
            Positions = new PositionsTable
            {
                Entries = new Dictionary<string, IReadOnlyDictionary<string, decimal>>
                {
                    ["tool_change"] = new Dictionary<string, decimal> { ["X"] = 300m, ["Z"] = 200m },
                },
            },
            Functions = new Dictionary<string, FunctionTable>
            {
                ["SUB_CHUCK"] = new FunctionTable
                {
                    States = new Dictionary<string, string> { ["OPEN"] = "M68", ["CLOSE"] = "M69" },
                },
            },
            FuncMeta = new FuncMeta
            {
                Entries = new Dictionary<string, string> { ["SUB_CHUCK.CLOSE"] = "workpiece_transfer_to = SUB" },
            },
        };
    }

    // A mill with one spindle and one holder and no defaults in [machine].
    private static MachineConfig Mill()
    {
        return new MachineConfig
        {
            Machine = new MachineIdentity { Name = "Mill" },
            Resources =
            [
                new ResourceDef { Id = "S1", Type = ResourceType.ToolSpindle },
                new ResourceDef { Id = "H1", Type = ResourceType.ToolHolder, Spindle = "S1", Magazine = true },
                new ResourceDef { Id = "TABLE1", Type = ResourceType.Table },
            ],
        };
    }
}
