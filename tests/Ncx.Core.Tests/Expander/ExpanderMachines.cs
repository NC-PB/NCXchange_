using Ncx.Core.Machine;

namespace Ncx.Core.Tests.Expander;

/// <summary>
/// The machine configurations of the expander tests, built by hand, because Ncx.Core.Tests does not load TOML: a
/// machine file with the work spindle S1 (role MAIN, rpm_min 45, rpm_max 6000), the tool holder H1, the axes X, Y, Z
/// and C with limits and max_feed in machine coordinates, a reference point on X, Z and C but none on Y, the position
/// tool_change = { X = 0, Z = -120 }, the coolant channels STANDARD and THROUGH and the functions PALLET_CHANGE and
/// PECK_MODE. Each test adds the expansion rule it is about (machine-config 4, 5, 5a; D64, D100).
/// </summary>
internal static class ExpanderMachines
{
    /// <summary>
    /// The machine without an expansion rule, limits = "warn".
    /// </summary>
    public static MachineConfig Mill()
    {
        return new MachineConfig
        {
            Machine = new MachineIdentity
            {
                Name = "Test mill",
                Controller = Controller.Fanuc,
                DefaultSpindle = "S1",
                DefaultHolder = "H1",
                DefaultWorkpiece = "S1",
            },
            Roles = new Dictionary<string, string> { ["MAIN"] = "S1" },
            Resources =
            [
                new ResourceDef { Id = "S1", Type = ResourceType.WorkSpindle, Axis = "C1" },
                new ResourceDef { Id = "H1", Type = ResourceType.ToolHolder, Magazine = true },
            ],
            Axes =
            [
                new AxisDef
                {
                    Id = "X1", NcxName = "X", Kind = AxisKind.Linear, Home = 300m, Min = -10m, Max = 650m,
                    MaxFeed = 10000m,
                },
                new AxisDef
                {
                    Id = "Y1", NcxName = "Y", Kind = AxisKind.Linear, Min = -100m, Max = 100m, MaxFeed = 10000m,
                },
                new AxisDef
                {
                    Id = "Z1", NcxName = "Z", Kind = AxisKind.Linear, Home = 450m, Min = -200m, Max = 450m,
                    MaxFeed = 8000m,
                },
                new AxisDef
                {
                    Id = "C1", NcxName = "C", Kind = AxisKind.Rotary, Owner = "S1", Home = 0m, Min = 0m, Max = 360m,
                    MaxFeed = 3600m,
                },
            ],
            Positions = new PositionsTable
            {
                Entries = new Dictionary<string, IReadOnlyDictionary<string, decimal>>
                {
                    ["tool_change"] = new Dictionary<string, decimal> { ["X"] = 0m, ["Z"] = -120m },
                },
            },
            SpindleTables = new Dictionary<string, FunctionTable>
            {
                ["MAIN"] = new FunctionTable
                {
                    States = new Dictionary<string, string> { ["CW"] = "M3", ["CCW"] = "M4", ["OFF"] = "M5" },
                    RpmMin = 45m,
                    RpmMax = 6000m,
                },
            },
            Coolant = new Dictionary<string, FunctionTable>
            {
                ["STANDARD"] = States("ON", "M8", "OFF", "M9", null),
                ["THROUGH"] = States("ON", "M51", "OFF", "M9", null),
            },
            Functions = new Dictionary<string, FunctionTable>
            {
                ["PALLET_CHANGE"] = new FunctionTable { States = new Dictionary<string, string> { ["RUN"] = "M60" } },
                ["PECK_MODE"] = States("RETRACT", "M291", "CHIP_BREAK", "M292", null),
            },
        };
    }

    /// <summary>
    /// The shop-floor example of machine-config 5a: THROUGH = { ON = "M51", OFF = "M9", requires = { SPINDLE = "OFF" },
    /// restore = ["SPINDLE"] }.
    /// </summary>
    public static MachineConfig CoolantClutch()
    {
        var clutch = new ExpansionRule
        {
            Requires = new Dictionary<string, string> { ["SPINDLE"] = "OFF" },
            Restore = ["SPINDLE"],
        };
        return Mill() with
        {
            Coolant = new Dictionary<string, FunctionTable>
            {
                ["STANDARD"] = States("ON", "M8", "OFF", "M9", null),
                ["THROUGH"] = States("ON", "M51", "OFF", "M9", clutch),
            },
        };
    }

    /// <summary>
    /// The machine with an expansion rule on [coolant] THROUGH.
    /// </summary>
    public static MachineConfig CoolantRule(ExpansionRule rule)
    {
        return Mill() with
        {
            Coolant = new Dictionary<string, FunctionTable>
            {
                ["STANDARD"] = States("ON", "M8", "OFF", "M9", null),
                ["THROUGH"] = States("ON", "M51", "OFF", "M9", rule),
            },
        };
    }

    /// <summary>
    /// The machine with an expansion rule on [tool_change].
    /// </summary>
    public static MachineConfig ToolChange(ExpansionRule rule)
    {
        return Mill() with { ToolChange = new ToolChangeConfig { Change = "T{tool} M6", Rule = rule } };
    }

    /// <summary>
    /// The machine with an expansion rule on [func] PALLET_CHANGE = { RUN = "M60" }.
    /// </summary>
    public static MachineConfig PalletChange(ExpansionRule rule)
    {
        return Mill() with
        {
            Functions = new Dictionary<string, FunctionTable>
            {
                ["PALLET_CHANGE"] = new FunctionTable
                {
                    States = new Dictionary<string, string> { ["RUN"] = "M60" },
                    Rule = rule,
                },
                ["PECK_MODE"] = States("RETRACT", "M291", "CHIP_BREAK", "M292", null),
            },
        };
    }

    /// <summary>
    /// The machine with a catalog entry PECK on G83 that carries an expansion rule, as the Doosan entry of
    /// machine-config 5a.
    /// </summary>
    public static MachineConfig Peck(ExpansionRule rule)
    {
        return Mill() with
        {
            CycleCatalog = new CycleCatalog
            {
                Controller = Controller.Fanuc,
                Entries = [new CycleEntry { Name = "PECK", Native = "G83", Rule = rule }],
            },
        };
    }

    /// <summary>
    /// The machine with limits = "clamp" (machine-config 1, D64).
    /// </summary>
    public static MachineConfig Clamp()
    {
        return Mill() with { Limits = LimitPolicy.Clamp };
    }

    private static FunctionTable States(string first, string firstCode, string second, string secondCode,
        ExpansionRule? rule)
    {
        return new FunctionTable
        {
            States = new Dictionary<string, string> { [first] = firstCode, [second] = secondCode },
            Rule = rule,
        };
    }
}
