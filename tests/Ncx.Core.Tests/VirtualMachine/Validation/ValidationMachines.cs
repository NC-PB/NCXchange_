using Ncx.Core.Machine;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The machine files of the rule tests that need the limits of a machine (machine-config 4 and 5, D64, D100) or a
/// rotary table (D82), built by hand from the machines of VmMachines.
/// </summary>
internal static class ValidationMachines
{
    /// <summary>
    /// The mill-turn of VmMachines with travel limits and max_feed on its linear axes X and Z, in machine coordinates:
    /// X (home 300) from -20 to 300 at 10000 mm/min, Z (home 450) from -10 to 450 at 8000 mm/min.
    /// </summary>
    public static MachineConfig MillTurnWithLimits()
    {
        MachineConfig machine = VmMachines.MillTurn();
        var axes = new List<AxisDef>();
        foreach (AxisDef axis in machine.Axes)
        {
            AxisDef limited = axis.NcxName switch
            {
                "X" => axis with { Min = -20m, Max = 300m, MaxFeed = 10000m },
                "Z" => axis with { Min = -10m, Max = 450m, MaxFeed = 8000m },
                _ => axis,
            };
            axes.Add(limited);
        }

        return machine with { Axes = axes };
    }

    /// <summary>
    /// The mill-turn of VmMachines with the speed limits of its main spindle, [spindle.MAIN] rpm_min 45 and rpm_max
    /// 6000.
    /// </summary>
    public static MachineConfig MillTurnWithSpindleLimits()
    {
        return VmMachines.MillTurn() with
        {
            SpindleTables = new Dictionary<string, FunctionTable>
            {
                ["MAIN"] = new FunctionTable
                {
                    States = new Dictionary<string, string> { ["CW"] = "M3", ["CCW"] = "M4", ["OFF"] = "M5" },
                    RpmMin = 45m,
                    RpmMax = 6000m,
                },
            },
        };
    }

    /// <summary>
    /// A five-axis mill with table kinematics: the head tilts about B, the table T1 turns about C (machine-config 4:
    /// the rotary axis C owned by the table).
    /// </summary>
    public static MachineConfig MillWithRotaryTable()
    {
        return new MachineConfig
        {
            Machine = new MachineIdentity
            {
                Name = "Table mill",
                Controller = Controller.Heidenhain,
                DefaultSpindle = "S1",
                DefaultHolder = "H1",
                DefaultWorkpiece = "T1",
            },
            Resources =
            [
                new ResourceDef { Id = "S1", Type = ResourceType.ToolSpindle },
                new ResourceDef { Id = "H1", Type = ResourceType.ToolHolder, Spindle = "S1" },
                new ResourceDef { Id = "T1", Type = ResourceType.Table },
            ],
            Axes =
            [
                new AxisDef { Id = "X", NcxName = "X", Kind = AxisKind.Linear },
                new AxisDef { Id = "Y", NcxName = "Y", Kind = AxisKind.Linear },
                new AxisDef { Id = "Z", NcxName = "Z", Kind = AxisKind.Linear },
                new AxisDef { Id = "B", NcxName = "B", Kind = AxisKind.Rotary },
                new AxisDef { Id = "C", NcxName = "C", Kind = AxisKind.Rotary, Owner = "T1" },
            ],
        };
    }
}
