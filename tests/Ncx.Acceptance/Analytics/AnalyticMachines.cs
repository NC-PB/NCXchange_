using Ncx.Core.Machine;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// The machine files of the analytics tests, built by hand (machine-config 4, 5): a three-axis mill whose numbers make
/// the times of the runtime estimate easy to compute by hand.
/// </summary>
internal static class AnalyticMachines
{
    /// <summary>
    /// [dynamics] with path_mode exact_stop and a block_time of 1 ms (machine-config 4).
    /// </summary>
    public static DynamicsConfig ExactStop { get; } = new() { BlockTime = 0.001m, PathMode = PathMode.ExactStop };

    /// <summary>
    /// [dynamics] with path_mode continuous, a block_time of 1 ms and the corner_speed given in mm/min.
    /// </summary>
    public static DynamicsConfig Continuous(decimal cornerSpeed)
    {
        return new DynamicsConfig { BlockTime = 0.001m, PathMode = PathMode.Continuous, CornerSpeed = cornerSpeed };
    }

    /// <summary>
    /// A three-axis mill: X, Y and Z with rapid 6000 mm/min (100 mm/s) and max_feed 6000 mm/min, X and Z with the
    /// acceleration given and Y with half of it; the tool spindle S1 (role TOOL, the default spindle) with the
    /// accel_time given, in the holder H1; the table TABLE1. No axis has a reference point, so every axis starts
    /// unknown and the first motion of a program has no length (D100).
    /// </summary>
    /// <param name="dynamics">[dynamics]; null for a machine file without it.</param>
    /// <param name="acceleration">acceleration of X and Z in mm/s^2; null for axes without it.</param>
    /// <param name="accelTime">accel_time of the spindle in seconds; null for a spindle table without it.</param>
    public static MachineConfig Mill(DynamicsConfig? dynamics, decimal? acceleration = 1000m, decimal? accelTime = null)
    {
        decimal? accelerationOfY = acceleration / 2;
        return new MachineConfig
        {
            Machine = new MachineIdentity
            {
                Name = "Runtime mill",
                Controller = Controller.Fanuc,
                Channels = [1],
                UnitsDefault = "MM",
                DefaultSpindle = "S1",
                DefaultHolder = "H1",
                DefaultWorkpiece = "TABLE1",
            },
            Roles = new Dictionary<string, string> { ["TOOL"] = "S1", ["TABLE"] = "TABLE1" },
            Resources =
            [
                new ResourceDef { Id = "S1", Type = ResourceType.ToolSpindle },
                new ResourceDef { Id = "H1", Type = ResourceType.ToolHolder, Spindle = "S1", Magazine = true },
                new ResourceDef { Id = "TABLE1", Type = ResourceType.Table },
            ],
            Axes =
            [
                Linear("X", acceleration),
                Linear("Y", accelerationOfY),
                Linear("Z", acceleration),
            ],
            Dynamics = dynamics,
            SpindleTables = new Dictionary<string, FunctionTable>
            {
                ["TOOL"] = new FunctionTable { AccelTime = accelTime },
            },
            Coolant = new Dictionary<string, FunctionTable> { ["STANDARD"] = new FunctionTable() },
        };
    }

    private static AxisDef Linear(string name, decimal? acceleration)
    {
        return new AxisDef
        {
            Id = name + "1",
            NcxName = name,
            Letter = name,
            Kind = AxisKind.Linear,
            Rapid = 6000m,
            MaxFeed = 6000m,
            Acceleration = acceleration,
        };
    }
}
