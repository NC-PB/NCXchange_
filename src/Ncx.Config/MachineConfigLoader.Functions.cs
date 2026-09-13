using Ncx.Core.Machine;

namespace Ncx.Config;

// Machine-config 5 and 5a: the function tables of the spindles, the synchronization, the coolant and the named
// functions with their expansion rules, and the tables workpiece, sync, func_meta, transform, retract, tolerance and
// raw.
public static partial class MachineConfigLoader
{
    private const string FrameSuffix = "_frame";
    private const string MirrorSuffix = "_mirror";

    // The states of the function tables whose word takes fixed values: the spindle words of language 4.5 and 4.11,
    // the modes of SPINDLE_MODE, the synchronization and a coolant channel (machine-config 5, F23). The states of a
    // [func] entry are free identifiers.
    private static readonly string[] s_spindleStates =
        ["CW", "CCW", "OFF", "ORIENT", "RPM", "VC", "CSS_OFF", "RPM_MAX"];

    private static readonly string[] s_spindleModeStates = ["AXIS", "SPINDLE"];
    private static readonly string[] s_spindleSyncStates = ["ON", "OFF", "PHASE"];
    private static readonly string[] s_coolantStates = ["ON", "OFF"];

    // The keys every function table may carry besides its states: the channel binding (D56) and the four keys of the
    // expansion rule (machine-config 5a); a spindle table also carries the machine limits (D64).
    private static readonly string[] s_bindingAndRuleKeys =
        ["channel", "channels", "pre", "post", "requires", "restore"];

    private static readonly string[] s_ruleKeys = ["pre", "post", "requires", "restore"];
    private static readonly string[] s_spindleLimitKeys = ["rpm_min", "rpm_max", "accel_time"];
    private static readonly string[] s_allChannels = ["all"];

    private static readonly string[] s_workpieceFrames = ["datum", "mirror"];

    private static readonly string[] s_syncKeys =
        ["wait", "paths", "mark_range", "start_mark", "groups", "start_channel", "wait_channel"];

    private static readonly string[] s_syncPaths = ["list", "bitmask"];
    private static readonly string[] s_syncGroupKeys = ["channels", "range"];

    private static readonly string[] s_transformKeys =
    [
        "CYLINDER_ON", "CYLINDER_OFF", "POLAR_ON", "POLAR_OFF", "TCPM_ON", "TCPM_OFF", "TILT_ON", "TILT_OFF",
        "TILT_AXIS_ON", "TILT_TURN", "move", "ROTARY_PATH_SHORTEST", "ROTARY_PATH_FULL", "ROTARY_FEED_MM_MIN",
        "ROTARY_FEED_DEG_MIN",
    ];

    private static readonly string[] s_moveOptions = ["TURN", "MOVE", "STAY"];
    private static readonly string[] s_retractKeys = ["MAX", "BY"];
    private static readonly string[] s_toleranceKeys = ["ON", "OFF", "mode"];
    private static readonly string[] s_toleranceModes = ["FINISH", "ROUGH"];
    private static readonly string[] s_rawKeys = ["known"];

    // [spindle.ROLE] and [spindle_mode.ROLE]: one function table per role; the VM only knows SPINDLE:role=CW, the
    // template is the machine's business (machine-config 5).
    private static Dictionary<string, FunctionTable> ReadRoleTables(
        ConfigTable? tables, string tableName, IReadOnlyList<string> states, bool spindleLimits)
    {
        var byRole = new Dictionary<string, FunctionTable>(StringComparer.Ordinal);
        if (tables is null)
        {
            return byRole;
        }

        foreach (string role in tables.Keys)
        {
            if (tables.Table(role, "[" + tableName + "." + role + "]") is ConfigTable table)
            {
                byRole[role] = ReadFunctionTable(table, states, spindleLimits);
            }
        }

        return byRole;
    }

    // [coolant] and [func]: one function table per coolant channel or named function (machine-config 5).
    private static Dictionary<string, FunctionTable> ReadNamedTables(
        ConfigTable? tables, IReadOnlyList<string>? states)
    {
        var byName = new Dictionary<string, FunctionTable>(StringComparer.Ordinal);
        if (tables is null)
        {
            return byName;
        }

        foreach (string name in tables.Keys)
        {
            if (tables.Table(name) is ConfigTable table)
            {
                byName[name] = ReadFunctionTable(table, states, spindleLimits: false);
            }
        }

        return byName;
    }

    // [spindle_sync]: ON, OFF and PHASE, bound to a channel or to all of them (machine-config 5, D56).
    private static FunctionTable? ReadSpindleSync(ConfigTable? table)
    {
        return table is null ? null : ReadFunctionTable(table, s_spindleSyncStates, spindleLimits: false);
    }

    // A function table: its states are function values, a string or a bare integer that means M and the number, with
    // every M and G code normalized (D105); channel and channels bind it (D56); pre, post, requires and restore make
    // its expansion rule (5a); a spindle table carries the machine limits (D64). The states of a [func] entry are free,
    // those of the other tables the values of their word, and another key there is unknown (machine-config 5, F23).
    private static FunctionTable ReadFunctionTable(
        ConfigTable table, IReadOnlyList<string>? states, bool spindleLimits)
    {
        var otherKeys = new List<string>(s_bindingAndRuleKeys);
        if (spindleLimits)
        {
            otherKeys.AddRange(s_spindleLimitKeys);
        }

        if (states is not null)
        {
            var known = new List<string>(states);
            known.AddRange(otherKeys);
            table.WarnUnknownKeys(known);
        }

        var templates = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in table.Keys)
        {
            bool isState = states is null ? !otherKeys.Contains(key) : states.Contains(key);
            if (isState && table.FunctionValue(key) is string template)
            {
                templates[key] = template;
            }
        }

        return new FunctionTable
        {
            States = templates,
            Channel = table.Integer("channel"),
            AllChannels = table.Choice("channels", s_allChannels) is not null,
            Rule = ReadRule(table),
            RpmMin = spindleLimits ? table.Number("rpm_min") : null,
            RpmMax = spindleLimits ? table.Number("rpm_max") : null,
            AccelTime = spindleLimits ? table.Number("accel_time") : null,
        };
    }

    // The four optional keys of a function state, the tool change or a catalog cycle: pre and post are NCX blocks,
    // requires the state conditions, restore the state variables put back; NCX text carries no native codes and is
    // kept as written (machine-config 5a, 5).
    private static ExpansionRule? ReadRule(ConfigTable table)
    {
        bool hasRule = false;
        foreach (string key in s_ruleKeys)
        {
            hasRule = hasRule || table.Has(key);
        }

        if (!hasRule)
        {
            return null;
        }

        return new ExpansionRule
        {
            Pre = table.TextList("pre"),
            Post = table.TextList("post"),
            Requires = table.Table("requires")?.Strings(null) ?? new Dictionary<string, string>(),
            Restore = table.TextList("restore"),
        };
    }

    // [func_meta]: FUNCTION.STATE = "rule", written as a dotted key, the reader fallback for machines without a
    // [workpiece] table (machine-config 5, D40).
    private static FuncMeta ReadFuncMeta(ConfigTable? table)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        if (table is null)
        {
            return new FuncMeta { Entries = entries };
        }

        foreach (string function in table.Keys)
        {
            if (!table.IsTable(function))
            {
                if (table.Text(function) is string rule)
                {
                    entries[function] = rule;
                }

                continue;
            }

            ConfigTable? states = table.Table(function);
            foreach (string state in states?.Keys ?? [])
            {
                if (states?.Text(state) is string rule)
                {
                    entries[function + "." + state] = rule;
                }
            }
        }

        return new FuncMeta { Entries = entries };
    }

    // [workpiece]: ROLE is the native selection of that holder, ROLE_frame how its side is programmed, datum or
    // mirror, and ROLE_mirror its mirror cycle (machine-config 5, D57).
    private static WorkpieceConfig? ReadWorkpiece(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        var templates = new Dictionary<string, string>(StringComparer.Ordinal);
        var frames = new Dictionary<string, WorkpieceFrame>(StringComparer.Ordinal);
        var mirrors = new Dictionary<string, FunctionTable>(StringComparer.Ordinal);
        foreach (string key in table.Keys)
        {
            if (key.EndsWith(FrameSuffix, StringComparison.Ordinal))
            {
                string role = key.Substring(0, key.Length - FrameSuffix.Length);
                WorkpieceFrame? frame = table.Choice(key, s_workpieceFrames) switch
                {
                    "datum" => WorkpieceFrame.Datum,
                    "mirror" => WorkpieceFrame.Mirror,
                    _ => null,
                };
                if (frame is WorkpieceFrame roleFrame)
                {
                    frames[role] = roleFrame;
                }
            }
            else if (key.EndsWith(MirrorSuffix, StringComparison.Ordinal))
            {
                string role = key.Substring(0, key.Length - MirrorSuffix.Length);
                if (table.Table(key) is ConfigTable mirror)
                {
                    mirrors[role] = new FunctionTable { States = mirror.Templates(s_onOff) };
                }
            }
            else if (table.Template(key) is string template)
            {
                templates[key] = template;
            }
        }

        return new WorkpieceConfig { Templates = templates, Frames = frames, Mirrors = mirrors };
    }

    // [sync]: the wait template, the form of {paths}, the marks, the wait groups and the Siemens channel templates
    // (machine-config 5, language 4.8).
    private static SyncConfig? ReadSync(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_syncKeys);
        IReadOnlyList<int>? markRange = table.IntegerPair("mark_range");
        var groups = new List<SyncGroup>();
        foreach (ConfigTable group in table.Tables("groups"))
        {
            // groups = [{ channels = [3, 4], range = [800, 849] }]: a second wait group between other channels.
            group.WarnUnknownKeys(s_syncGroupKeys);
            IReadOnlyList<int>? range = group.IntegerPair("range");
            groups.Add(new SyncGroup
            {
                Channels = group.IntegerList("channels"),
                Range = range is null ? null : new MarkRange(range[0], range[1]),
            });
        }

        return new SyncConfig
        {
            Wait = table.Template("wait"),
            Paths = table.Choice("paths", s_syncPaths) switch
            {
                "list" => SyncPaths.List,
                "bitmask" => SyncPaths.Bitmask,
                _ => null,
            },
            MarkRange = markRange is null ? null : new MarkRange(markRange[0], markRange[1]),
            StartMark = table.Integer("start_mark"),
            Groups = groups,
            StartChannel = table.Template("start_channel"),
            WaitChannel = table.Template("wait_channel"),
        };
    }

    // [transform]: the templates of the transformations, the tilted planes and the rotary axis options, and the values
    // of {move} per MOVE option; an empty template is kept, the target cannot write the word (machine-config 5, D82).
    private static TransformTable? ReadTransform(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_transformKeys);
        return new TransformTable
        {
            CylinderOn = table.Template("CYLINDER_ON"),
            CylinderOff = table.Template("CYLINDER_OFF"),
            PolarOn = table.Template("POLAR_ON"),
            PolarOff = table.Template("POLAR_OFF"),
            TcpmOn = table.Template("TCPM_ON"),
            TcpmOff = table.Template("TCPM_OFF"),
            TiltOn = table.Template("TILT_ON"),
            TiltOff = table.Template("TILT_OFF"),
            TiltAxisOn = table.Template("TILT_AXIS_ON"),
            TiltTurn = table.Template("TILT_TURN"),
            RotaryPathShortest = table.Template("ROTARY_PATH_SHORTEST"),
            RotaryPathFull = table.Template("ROTARY_PATH_FULL"),
            RotaryFeedMmMin = table.Template("ROTARY_FEED_MM_MIN"),
            RotaryFeedDegMin = table.Template("ROTARY_FEED_DEG_MIN"),
            Move = table.Table("move")?.PlaceholderValues(s_moveOptions) ?? new Dictionary<string, string>(),
        };
    }

    // [retract]: the templates of the RETRACT verb (machine-config 5, D83).
    private static RetractTable? ReadRetract(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_retractKeys);
        return new RetractTable { Max = table.Template("MAX"), By = table.Template("BY") };
    }

    // [tolerance]: the templates of the TOLERANCE words and the values of {mode} (machine-config 5, D85).
    private static ToleranceTable? ReadTolerance(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_toleranceKeys);
        return new ToleranceTable
        {
            On = table.Template("ON"),
            Off = table.Template("OFF"),
            Mode = table.Table("mode")?.PlaceholderValues(s_toleranceModes) ?? new Dictionary<string, string>(),
        };
    }

    // [raw]: the builder codes the reader keeps as RAW with the builder's name (machine-config 5, F23).
    private static RawTable? ReadRaw(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_rawKeys);
        return new RawTable { Known = table.TextList("known") };
    }
}
