using Ncx.Core.Machine;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// The machine configurations of the tests of the virtual machine, built by hand, because Ncx.Core.Tests does not load
/// TOML.
/// </summary>
internal static class VmMachines
{
    /// <summary>
    /// The built-in machine of D103 in the shape Ncx.Config builds it (DefaultMachine): no controller, the work spindle
    /// S1 with the C axis (role MAIN), the tool spindle S2 (role TOOL) in the holder H1, the axes X Y Z A B C without
    /// limits and without reference points, the coolant channel STANDARD, no named functions.
    /// </summary>
    public static MachineConfig Default()
    {
        return new MachineConfig
        {
            Machine = new MachineIdentity
            {
                Name = "Default machine (D103)",
                Channels = [1],
                UnitsDefault = "MM",
                DefaultSpindle = "S2",
                DefaultHolder = "H1",
                DefaultWorkpiece = "S1",
            },
            Roles = new Dictionary<string, string> { ["MAIN"] = "S1", ["TOOL"] = "S2" },
            Resources =
            [
                new ResourceDef { Id = "S1", Type = ResourceType.WorkSpindle, Axis = "C" },
                new ResourceDef { Id = "S2", Type = ResourceType.ToolSpindle },
                new ResourceDef { Id = "H1", Type = ResourceType.ToolHolder, Spindle = "S2" },
            ],
            Axes =
            [
                new AxisDef { Id = "X", NcxName = "X", Letter = "X", Kind = AxisKind.Linear },
                new AxisDef { Id = "Y", NcxName = "Y", Letter = "Y", Kind = AxisKind.Linear },
                new AxisDef { Id = "Z", NcxName = "Z", Letter = "Z", Kind = AxisKind.Linear },
                new AxisDef { Id = "A", NcxName = "A", Letter = "A", Kind = AxisKind.Rotary },
                new AxisDef { Id = "B", NcxName = "B", Letter = "B", Kind = AxisKind.Rotary },
                new AxisDef { Id = "C", NcxName = "C", Letter = "C", Kind = AxisKind.Rotary, Owner = "S1" },
            ],
            Coolant = new Dictionary<string, FunctionTable> { ["STANDARD"] = new FunctionTable() },
        };
    }

    /// <summary>
    /// A machine file of a mill-turn in the shape of millturn1.toml (D104) with reference points in machine
    /// coordinates (D100): the main spindle S1 with C (home 90), the sub spindle S2 with C2 (home 0) on the slide Z2
    /// (home 0), the tool spindle S3 in the turret H1 and a second turret H2; X (home 300, home2 150), Y without home,
    /// Z (home 450), a tilting B (home 0); the coolant channels STANDARD and THROUGH; the functions SUB_CHUCK and
    /// MAIN_CHUCK.
    /// </summary>
    public static MachineConfig MillTurn()
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
                ["TURRET2"] = "H2",
            },
            Resources =
            [
                new ResourceDef { Id = "S1", Type = ResourceType.WorkSpindle, Axis = "C1" },
                new ResourceDef { Id = "S2", Type = ResourceType.WorkSpindle, Axis = "C2" },
                new ResourceDef { Id = "S3", Type = ResourceType.ToolSpindle },
                new ResourceDef { Id = "H1", Type = ResourceType.ToolHolder, Spindle = "S3", Magazine = true },
                new ResourceDef { Id = "H2", Type = ResourceType.ToolHolder },
            ],
            Axes =
            [
                new AxisDef { Id = "X1", NcxName = "X", Kind = AxisKind.Linear, Home = 300m, Home2 = 150m },
                new AxisDef { Id = "Y1", NcxName = "Y", Kind = AxisKind.Linear },
                new AxisDef { Id = "Z1", NcxName = "Z", Kind = AxisKind.Linear, Home = 450m },
                new AxisDef { Id = "B1", NcxName = "B", Kind = AxisKind.Rotary, Home = 0m },
                new AxisDef { Id = "C1", NcxName = "C", Kind = AxisKind.Rotary, Owner = "S1", Home = 90m },
                new AxisDef { Id = "Z2", NcxName = "Z2", Kind = AxisKind.Linear, Owner = "S2", Home = 0m },
                new AxisDef { Id = "C2", NcxName = "C2", Kind = AxisKind.Rotary, Owner = "S2", Home = 0m },
            ],
            Coolant = new Dictionary<string, FunctionTable>
            {
                ["STANDARD"] = States("ON", "M8", "OFF", "M9"),
                ["THROUGH"] = States("ON", "M51", "OFF", "M9"),
            },
            Functions = new Dictionary<string, FunctionTable>
            {
                ["SUB_CHUCK"] = States("OPEN", "M68", "CLOSE", "M69"),
                ["MAIN_CHUCK"] = States("OPEN", "M66", "CLOSE", "M67"),
            },
        };
    }

    /// <summary>
    /// A machine file of a machine without a tool holder: TOOL and OFFSET have no holder to address (virtual machine
    /// 3.8 rule 2).
    /// </summary>
    public static MachineConfig WithoutHolder()
    {
        return new MachineConfig
        {
            Machine = new MachineIdentity { Name = "Without holder", Controller = Controller.Fanuc },
            Resources = [new ResourceDef { Id = "S1", Type = ResourceType.WorkSpindle }],
            Axes = [new AxisDef { Id = "X", NcxName = "X", Kind = AxisKind.Linear }],
        };
    }

    private static FunctionTable States(string first, string firstCode, string second, string secondCode)
    {
        return new FunctionTable
        {
            States = new Dictionary<string, string> { [first] = firstCode, [second] = secondCode },
        };
    }
}
