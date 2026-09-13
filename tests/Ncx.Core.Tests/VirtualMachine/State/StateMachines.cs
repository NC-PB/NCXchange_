using Ncx.Core.Machine;

namespace Ncx.Core.Tests.VirtualMachine.State;

/// <summary>
/// The machine configurations of the state tests, built by hand, because Ncx.Core.Tests does not load TOML.
/// </summary>
internal static class StateMachines
{
    /// <summary>
    /// The built-in machine of D103 in the shape Ncx.Config builds it (DefaultMachine): the work spindle S1 with the C
    /// axis (role MAIN), the tool spindle S2 (role TOOL) in the holder H1, the axes X Y Z A B C without limits and
    /// without reference points, the coolant channel STANDARD, no named functions.
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
                new AxisDef { Id = "X", NcxName = "X", Kind = AxisKind.Linear },
                new AxisDef { Id = "Y", NcxName = "Y", Kind = AxisKind.Linear },
                new AxisDef { Id = "Z", NcxName = "Z", Kind = AxisKind.Linear },
                new AxisDef { Id = "A", NcxName = "A", Kind = AxisKind.Rotary },
                new AxisDef { Id = "B", NcxName = "B", Kind = AxisKind.Rotary },
                new AxisDef { Id = "C", NcxName = "C", Kind = AxisKind.Rotary, Owner = "S1" },
            ],
            Coolant = new Dictionary<string, FunctionTable> { ["STANDARD"] = new FunctionTable() },
        };
    }

    /// <summary>
    /// A mill-turn in the shape of millturn1.toml (D104) with reference points in machine coordinates (D100): home on
    /// X, Z, C and Z2, a second reference point on X, none on Y and C2. The main spindle S1 with C, the sub spindle S2
    /// with C2 on the slide Z2, the tool spindle S3 in the turret H1 and a second turret H2; the coolant channels
    /// STANDARD and THROUGH; the functions SUB_CHUCK and MAIN_CHUCK; SYS_TOOL and SYS_WEAR_Z mapped, SYS_PART_MAIN
    /// mapped to a value the control cannot read.
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
                new AxisDef { Id = "C1", NcxName = "C", Kind = AxisKind.Rotary, Owner = "S1", Home = 0m },
                new AxisDef { Id = "Z2", NcxName = "Z2", Kind = AxisKind.Linear, Owner = "S2", Home = -20.5m },
                new AxisDef { Id = "C2", NcxName = "C2", Kind = AxisKind.Rotary, Owner = "S2" },
            ],
            Coolant = new Dictionary<string, FunctionTable>
            {
                ["STANDARD"] = OnOff("M8", "M9"),
                ["THROUGH"] = OnOff("M51", "M9"),
            },
            Functions = new Dictionary<string, FunctionTable>
            {
                ["SUB_CHUCK"] = OpenClose("M68", "M69"),
                ["MAIN_CHUCK"] = OpenClose("M66", "M67"),
            },
            SystemVariables = new SystemVariables
            {
                Entries = new Dictionary<string, string>
                {
                    ["SYS_TOOL"] = "$P_TOOLNO",
                    ["SYS_WEAR_Z"] = "RG{index}",
                    ["SYS_PART_MAIN"] = "",
                },
            },
        };
    }

    private static FunctionTable OnOff(string on, string off)
    {
        return new FunctionTable { States = new Dictionary<string, string> { ["ON"] = on, ["OFF"] = off } };
    }

    private static FunctionTable OpenClose(string open, string close)
    {
        return new FunctionTable { States = new Dictionary<string, string> { ["OPEN"] = open, ["CLOSE"] = close } };
    }
}
