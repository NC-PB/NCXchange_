using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config;

// Machine-config 1 to 3: the identity and dialect, the output format, the tool change, home and setpos.
public static partial class MachineConfigLoader
{
    private static readonly string[] s_machineKeys =
    [
        "name", "controller", "dialect", "builder", "gcode_system", "s_binds_to_spindle_word", "limits", "channels",
        "units_default", "default_spindle", "default_holder", "default_workpiece", "tool_table", "kinematics",
    ];

    private static readonly string[] s_controllers = ["fanuc", "heidenhain", "siemens"];
    private static readonly string[] s_gcodeSystems = ["A", "B", "C"];
    private static readonly string[] s_limitPolicies = ["warn", "clamp"];
    private static readonly string[] s_units = ["MM", "INCH"];

    private static readonly string[] s_formatKeys =
    [
        "decimal_separator", "decimals", "trailing_zeros", "block_numbers", "line_ending", "program_end", "sub_end",
        "program_layout", "max_line_length", "comment_charset",
    ];

    private static readonly string[] s_blockNumberKeys = ["enabled", "start", "step"];
    private static readonly string[] s_lineEndings = ["CRLF", "LF"];
    private static readonly string[] s_programLayouts = ["one_file", "file_per_program"];

    private static readonly string[] s_toolChangeKeys =
    [
        "change", "change_preloaded", "preload", "unload", "auto_preload", "preload_position", "offsets_with_change",
        "tool_name_allowed", "kind_map", "pre", "post", "requires", "restore",
    ];

    private static readonly string[] s_preloadPositions = ["after_change", "before_first_motion"];
    private static readonly string[] s_homeKeys = ["template", "point"];
    private static readonly string[] s_setposKeys = ["template"];

    // [machine]: name and controller are required; the controller selects the reader and the compiler, the defaults
    // resolve the words without a role address, units_default is a value of UNITS (machine-config 1, language 4.2).
    private static MachineIdentity ReadMachine(ConfigTable? table)
    {
        if (table is null)
        {
            return new MachineIdentity { Name = "" };
        }

        table.WarnUnknownKeys(s_machineKeys);
        return new MachineIdentity
        {
            Name = table.RequiredText("name") ?? "",
            Controller = table.RequiredChoice("controller", s_controllers) switch
            {
                "fanuc" => Controller.Fanuc,
                "heidenhain" => Controller.Heidenhain,
                "siemens" => Controller.Siemens,
                _ => null,
            },
            Dialect = table.Text("dialect"),
            Builder = table.Text("builder"),
            GcodeSystem = table.Choice("gcode_system", s_gcodeSystems) switch
            {
                "A" => GcodeSystem.A,
                "B" => GcodeSystem.B,
                "C" => GcodeSystem.C,
                _ => null,
            },
            SBindsToSpindleWord = table.Flag("s_binds_to_spindle_word"),
            Channels = table.IntegerList("channels"),
            UnitsDefault = table.Choice("units_default", s_units),
            DefaultSpindle = table.Text("default_spindle"),
            DefaultHolder = table.Text("default_holder"),
            DefaultWorkpiece = table.Text("default_workpiece"),
        };
    }

    // [machine] limits: what the expander does beyond the machine limits; the limits warn by default (machine-config
    // 1, D64).
    private static LimitPolicy ReadLimitPolicy(ConfigTable? table)
    {
        return table?.Choice("limits", s_limitPolicies) switch
        {
            "clamp" => LimitPolicy.Clamp,
            _ => LimitPolicy.Warn,
        };
    }

    // [format]: how the compiler writes numbers, block numbers and line endings; program_end and sub_end are templates
    // whose M and G codes are normalized (machine-config 2, 5, D105).
    private static OutputFormat? ReadFormat(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_formatKeys);
        return new OutputFormat
        {
            DecimalSeparator = table.Text("decimal_separator"),
            Decimals = table.Table("decimals")?.Integers() ?? new Dictionary<string, int>(),
            TrailingZeros = table.Flag("trailing_zeros"),
            BlockNumbers = ReadBlockNumbers(table.Table("block_numbers")),
            LineEnding = table.Choice("line_ending", s_lineEndings) switch
            {
                "CRLF" => LineEnding.CrLf,
                "LF" => LineEnding.Lf,
                _ => null,
            },
            ProgramEnd = table.Template("program_end"),
            SubEnd = table.Template("sub_end"),
            ProgramLayout = table.Choice("program_layout", s_programLayouts) switch
            {
                "one_file" => ProgramLayout.OneFile,
                "file_per_program" => ProgramLayout.FilePerProgram,
                _ => null,
            },
            MaxLineLength = table.Integer("max_line_length"),
            CommentCharset = table.Text("comment_charset"),
        };
    }

    // block_numbers = { enabled = true, start = 10, step = 10 } (machine-config 2).
    private static BlockNumbering? ReadBlockNumbers(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_blockNumberKeys);
        return new BlockNumbering
        {
            Enabled = table.Flag("enabled"),
            Start = table.Integer("start"),
            Step = table.Integer("step"),
        };
    }

    // [tool_change]: the templates written for PRELOAD and TOOL, normalized like every template (D105), the preload
    // settings, the values of {kind} (D52) and the expansion rule of the change (machine-config 3, 5a).
    private static ToolChangeConfig? ReadToolChange(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_toolChangeKeys);
        return new ToolChangeConfig
        {
            Change = table.Template("change"),
            ChangePreloaded = table.Template("change_preloaded"),
            Preload = table.Template("preload"),
            Unload = table.Template("unload"),
            AutoPreload = table.Flag("auto_preload"),
            PreloadPosition = table.Choice("preload_position", s_preloadPositions) switch
            {
                "after_change" => PreloadPosition.AfterChange,
                "before_first_motion" => PreloadPosition.BeforeFirstMotion,
                _ => null,
            },
            OffsetsWithChange = table.Flag("offsets_with_change"),
            ToolNameAllowed = table.Flag("tool_name_allowed"),
            KindMap = table.Table("kind_map")?.PlaceholderValues(null) ?? new Dictionary<string, string>(),
            Rule = ReadRule(table),
        };
    }

    // [home]: the templates of reference point 1 and of the further points (machine-config 3).
    private static HomeConfig? ReadHome(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_homeKeys);
        return new HomeConfig { Template = table.Template("template"), Point = table.Template("point") };
    }

    // [setpos]: the template of SETPOS (machine-config 3).
    private static SetposConfig? ReadSetpos(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_setposKeys);
        return new SetposConfig { Template = table.Template("template") };
    }
}
