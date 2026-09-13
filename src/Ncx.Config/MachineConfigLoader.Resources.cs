using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config;

// Machine-config 4: roles, resources, axes, positions, dynamics and diameter, and the checks of the resources that
// virtual machine 3.8 and F23 ask for when the machine is loaded.
public static partial class MachineConfigLoader
{
    private static readonly string[] s_resourceKeys = ["id", "type", "axis", "spindle", "magazine", "channel"];
    private static readonly string[] s_resourceTypes = ["work_spindle", "tool_spindle", "tool_holder", "table"];

    private static readonly string[] s_axisKeys =
    [
        "id", "ncx", "letter", "incremental_letter", "kind", "owner", "limits", "rapid", "max_feed", "acceleration",
        "programming", "home", "home2", "clamp",
    ];

    private static readonly string[] s_axisKinds = ["linear", "rotary"];
    private static readonly string[] s_programmings = ["diameter", "radius", "switchable"];
    private static readonly string[] s_dynamicsKeys = ["block_time", "path_mode", "corner_speed"];
    private static readonly string[] s_pathModes = ["continuous", "exact_stop"];
    private static readonly string[] s_onOff = ["ON", "OFF"];

    // [roles]: every key is a role, every value the id of a resource (machine-config 4, language 4.10).
    private static IReadOnlyDictionary<string, string> ReadRoles(ConfigTable? table)
    {
        return table?.Strings(null) ?? new Dictionary<string, string>();
    }

    // [[resource]]: id and type are required; a resource that names an axis no [[axis]] declares is an ERROR
    // (machine-config 4, F23).
    private static List<ResourceDef> ReadResources(ConfigTable root, List<AxisDef> axes)
    {
        var resources = new List<ResourceDef>();
        foreach (ConfigTable table in root.Tables("resource", "[[resource]]", "machine-config 4"))
        {
            table.WarnUnknownKeys(s_resourceKeys);
            string? id = table.RequiredText("id");
            ResourceType? type = table.RequiredChoice("type", s_resourceTypes) switch
            {
                "work_spindle" => ResourceType.WorkSpindle,
                "tool_spindle" => ResourceType.ToolSpindle,
                "tool_holder" => ResourceType.ToolHolder,
                "table" => ResourceType.Table,
                _ => null,
            };
            string? axis = table.Text("axis");
            string? spindle = table.Text("spindle");
            bool magazine = table.Flag("magazine");
            int? channel = table.Integer("channel");
            if (axis is not null && !axes.Exists(declared => declared.Id == axis))
            {
                table.Diagnostics.Error(table.LineOf("axis"), DiagnosticCodes.UndeclaredAxis,
                    $"{table.Name} names the axis {axis}, which no [[axis]] declares (machine-config 4, F23).");
            }

            if (id is not null && type is ResourceType resourceType)
            {
                resources.Add(new ResourceDef
                {
                    Id = id,
                    Type = resourceType,
                    Axis = axis,
                    Spindle = spindle,
                    Magazine = magazine,
                    Channel = channel,
                });
            }
        }

        return resources;
    }

    // [[axis]]: id, ncx and kind are required; home and limits are machine coordinates, the G53 / M91 frame, and the
    // dynamics serve the runtime estimate (machine-config 4, D100, D64).
    private static List<AxisDef> ReadAxes(ConfigTable root)
    {
        var axes = new List<AxisDef>();
        foreach (ConfigTable table in root.Tables("axis", "[[axis]]", "machine-config 4"))
        {
            table.WarnUnknownKeys(s_axisKeys);
            string? id = table.RequiredText("id");
            string? ncxName = table.RequiredText("ncx");
            AxisKind? kind = table.RequiredChoice("kind", s_axisKinds) switch
            {
                "linear" => AxisKind.Linear,
                "rotary" => AxisKind.Rotary,
                _ => null,
            };
            string? letter = table.Text("letter");
            string? incrementalLetter = table.Text("incremental_letter");
            string? owner = table.Text("owner");
            IReadOnlyList<decimal>? limits = table.NumberPair("limits");
            decimal? rapid = table.Number("rapid");
            decimal? maxFeed = table.Number("max_feed");
            decimal? acceleration = table.Number("acceleration");
            Programming? programming = table.Choice("programming", s_programmings) switch
            {
                "diameter" => Programming.Diameter,
                "radius" => Programming.Radius,
                "switchable" => Programming.Switchable,
                _ => null,
            };
            decimal? home = table.Number("home");
            decimal? home2 = table.Number("home2");
            IReadOnlyDictionary<string, string> clamp =
                table.Table("clamp")?.Templates(s_onOff) ?? new Dictionary<string, string>();
            if (id is null || ncxName is null || kind is not AxisKind axisKind)
            {
                continue;
            }

            axes.Add(new AxisDef
            {
                Id = id,
                NcxName = ncxName,
                Letter = letter,
                IncrementalLetter = incrementalLetter,
                Kind = axisKind,
                Owner = owner,
                Min = limits?[0],
                Max = limits?[1],
                Rapid = rapid,
                MaxFeed = maxFeed,
                Acceleration = acceleration,
                Programming = programming,
                Home = home,
                Home2 = home2,
                Clamp = clamp,
            });
        }

        return axes;
    }

    // [positions]: every key names a position, every value is a table of axis values in machine coordinates, which
    // expansion rules reach through {position:NAME} (machine-config 4, 5a, D100).
    private static PositionsTable ReadPositions(ConfigTable? table)
    {
        var entries = new Dictionary<string, IReadOnlyDictionary<string, decimal>>(StringComparer.Ordinal);
        if (table is not null)
        {
            foreach (string name in table.Keys)
            {
                if (table.Table(name) is ConfigTable position)
                {
                    entries[name] = position.Numbers();
                }
            }
        }

        return new PositionsTable { Entries = entries };
    }

    // [dynamics]: the control behaviour for the runtime estimate (machine-config 4, D64).
    private static DynamicsConfig? ReadDynamics(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_dynamicsKeys);
        return new DynamicsConfig
        {
            BlockTime = table.Number("block_time"),
            PathMode = table.Choice("path_mode", s_pathModes) switch
            {
                "continuous" => PathMode.Continuous,
                "exact_stop" => PathMode.ExactStop,
                _ => null,
            },
            CornerSpeed = table.Number("corner_speed"),
        };
    }

    // [diameter]: the templates that switch diameter programming (machine-config 4, D60).
    private static DiameterConfig? ReadDiameter(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_onOff);
        return new DiameterConfig { On = table.Template("ON"), Off = table.Template("OFF") };
    }

    // More than one resource of a kind without an explicit default in the configuration is an ERROR when the machine
    // is loaded: spindles without default_spindle, tool holders without default_holder (virtual machine 3.8 rule 2).
    // TODO(question): rule 2 says "of a kind"; the kinds here are what the two defaults serve, the spindles for
    // default_spindle, work and tool spindles counted together because SPINDLE without an address may target either,
    // and the tool holders for default_holder.
    private static void CheckDefaults(MachineConfig machine, int line, Diagnostics diagnostics)
    {
        var spindles = new List<string>();
        var holders = new List<string>();
        foreach (ResourceDef resource in machine.Resources)
        {
            if (resource.IsSpindle)
            {
                spindles.Add(resource.Id);
            }
            else if (resource.Type == ResourceType.ToolHolder)
            {
                holders.Add(resource.Id);
            }
        }

        if (spindles.Count > 1 && machine.Machine.DefaultSpindle is null)
        {
            diagnostics.Error(line, DiagnosticCodes.SeveralWithoutDefault,
                $"The machine has the spindles {string.Join(", ", spindles)} and [machine] names no default_spindle "
                + "(virtual machine 3.8 rule 2).");
        }

        if (holders.Count > 1 && machine.Machine.DefaultHolder is null)
        {
            diagnostics.Error(line, DiagnosticCodes.SeveralWithoutDefault,
                $"The machine has the tool holders {string.Join(", ", holders)} and [machine] names no default_holder "
                + "(virtual machine 3.8 rule 2).");
        }
    }
}
