namespace Ncx.Compilers.Tests.Fakes;

/// <summary>
/// Machine files for the fake compiler: a three-axis mill with the tool spindle S1 (role TOOL), the holder H1 with a
/// magazine and the table TABLE1, and the [format] and [tool_change] tables a test gives it (machine-config 1 to 5).
/// </summary>
internal static class FakeMachines
{
    /// <summary>
    /// [format] with the point, three decimals on the axes, one on F, no trailing zeros, N10 in steps of 10, LF, one
    /// file (machine-config 2).
    /// </summary>
    public const string PointFormat = """
        [format]
        decimal_separator = "."
        decimals = { X = 3, Y = 3, Z = 3, F = 1, S = 0 }
        trailing_zeros = false
        block_numbers = { enabled = true, start = 10, step = 10 }
        line_ending = "LF"
        program_end = "M30"
        sub_end = "M99"
        program_layout = "one_file"
        """;

    /// <summary>
    /// [format] with the comma, four decimals on the axes, none on F, trailing zeros, N1 in steps of 1, CRLF
    /// (machine-config 2).
    /// </summary>
    public const string CommaFormat = """
        [format]
        decimal_separator = ","
        decimals = { X = 4, Y = 4, Z = 4, F = 0, S = 0 }
        trailing_zeros = true
        block_numbers = { enabled = true, start = 1, step = 1 }
        line_ending = "CRLF"
        program_end = "M30"
        sub_end = "M99"
        program_layout = "one_file"
        """;

    /// <summary>
    /// [tool_change] with T{tool} M6 and the preload T{tool} (machine-config 3).
    /// </summary>
    public const string PlainToolChange = """
        [tool_change]
        change = "T{tool} M6"
        preload = "T{tool}"
        """;

    /// <summary>
    /// The mill with a controller, a [format] and a [tool_change] table.
    /// </summary>
    /// <param name="controller">The controller of [machine]: "fanuc" or "heidenhain".</param>
    /// <param name="format">The [format] table.</param>
    /// <param name="toolChange">The [tool_change] table.</param>
    public static string Mill(string controller = "fanuc", string format = PointFormat,
        string toolChange = PlainToolChange)
    {
        return $$"""
            [machine]
            name = "Fake mill"
            controller = "{{controller}}"
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
            magazine = true

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

            {{format}}

            {{toolChange}}

            [spindle.TOOL]
            CW = "M3"
            CCW = "M4"
            OFF = "M5"
            """;
    }
}
