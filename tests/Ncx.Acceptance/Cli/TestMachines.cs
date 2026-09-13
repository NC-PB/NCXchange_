namespace Ncx.Acceptance.Cli;

/// <summary>
/// Machine files the CLI tests hand to --machine by path (machine-config 1 to 5a).
/// </summary>
internal static class TestMachines
{
    /// <summary>
    /// A three-axis mill with one tool spindle S1 (role TOOL), the holder H1 and the coolant channels STANDARD and
    /// THROUGH, whose clutch engages only while the spindle stands: the expansion rule of machine-config 5a,
    /// requires = { SPINDLE = "OFF" } with restore = ["SPINDLE"]. It has no role TURRET1.
    /// </summary>
    public const string ClutchMill = """
        [machine]
        name = "Clutch mill"
        controller = "fanuc"
        channels = [1]
        units_default = "MM"
        default_spindle = "S1"
        default_holder = "H1"
        default_workpiece = "TABLE1"

        [roles]
        TOOL = "S1"
        TABLE = "TABLE1"

        [[resource]]
        id = "S1"
        type = "tool_spindle"

        [[resource]]
        id = "H1"
        type = "tool_holder"
        spindle = "S1"

        [[resource]]
        id = "TABLE1"
        type = "table"

        [[axis]]
        id = "X1"
        ncx = "X"
        letter = "X"
        kind = "linear"

        [[axis]]
        id = "Y1"
        ncx = "Y"
        letter = "Y"
        kind = "linear"

        [[axis]]
        id = "Z1"
        ncx = "Z"
        letter = "Z"
        kind = "linear"

        [spindle.TOOL]
        CW = "M3"
        CCW = "M4"
        OFF = "M5"

        [coolant]
        STANDARD = { ON = "M8", OFF = "M9" }
        THROUGH = { ON = "M51", OFF = "M9", requires = { SPINDLE = "OFF" }, restore = ["SPINDLE"] }
        """;

    /// <summary>
    /// The clutch mill with a key in [machine] that machine-config does not know, which the loader reports as a CFG
    /// WARNING that names the nearest known key (P2-01).
    /// </summary>
    public static string ClutchMillWithUnknownKey =>
        ClutchMill.Replace("channels = [1]", "channels = [1]\ncolour = \"red\"", StringComparison.Ordinal);

    /// <summary>
    /// A machine file that reads but has an ERROR: [machine] without its controller (machine-config 1).
    /// </summary>
    public const string WithoutController = """
        [machine]
        name = "No controller"
        """;
}
