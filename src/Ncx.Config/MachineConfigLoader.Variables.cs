using Ncx.Core.Machine;

namespace Ncx.Config;

// Machine-config 6, 7 and 9: the cycle catalog, the variables, the system variables and the kinematic tree.
public static partial class MachineConfigLoader
{
    private static readonly string[] s_cyclesKeys = ["catalog"];
    private static readonly string[] s_variablesKeys = ["unassigned", "map", "block_cap", "call_depth"];
    private static readonly string[] s_unassignedValues = ["error", "0"];
    private static readonly string[] s_nodeKeys = ["id", "parent", "type", "direction"];

    // [cycles]: the catalog file of the controller family (machine-config 6).
    private static CyclesConfig? ReadCycles(ConfigTable? table)
    {
        if (table is null)
        {
            return null;
        }

        table.WarnUnknownKeys(s_cyclesKeys);
        return new CyclesConfig { CatalogFile = table.Text("catalog") };
    }

    // [variables]: unassigned is "error" or 0; block_cap and call_depth keep the defaults of virtual machine 3.6 when
    // the file leaves them out (machine-config 7, D38).
    private static VariablesConfig ReadVariables(ConfigTable? table)
    {
        var defaults = new VariablesConfig();
        if (table is null)
        {
            return defaults;
        }

        table.WarnUnknownKeys(s_variablesKeys);
        return new VariablesConfig
        {
            Unassigned = ReadUnassigned(table),
            Map = table.Table("map")?.Strings(null) ?? new Dictionary<string, string>(),
            BlockCap = table.LongInteger("block_cap") ?? defaults.BlockCap,
            CallDepth = table.Integer("call_depth") ?? defaults.CallDepth,
        };
    }

    // Reading an unassigned variable is an ERROR unless the configuration sets unassigned = 0 (virtual machine 3.6,
    // machine-config 7).
    private static UnassignedVariable ReadUnassigned(ConfigTable table)
    {
        string? written = table.PlaceholderValue("unassigned");
        if (written is null || written == "error")
        {
            return UnassignedVariable.Error;
        }

        if (written == "0")
        {
            return UnassignedVariable.Zero;
        }

        table.NotAllowed("unassigned", written, s_unassignedValues);
        return UnassignedVariable.Error;
    }

    // [system_variables]: every key an NCX SYS_ name, every value the native template kept as written, parsed on the
    // line of its key (machine-config 7, D51; wave-1 question #61).
    private static SystemVariables ReadSystemVariables(ConfigTable? table)
    {
        if (table is null)
        {
            return new SystemVariables { Entries = new Dictionary<string, string>() };
        }

        IReadOnlyDictionary<string, string> entries = table.Strings(null);
        foreach (KeyValuePair<string, string> entry in entries)
        {
            table.CheckTemplate(entry.Key, entry.Value);
        }

        return new SystemVariables { Entries = entries };
    }

    // [[node]] and [machine] kinematics: the tree is loaded and not interpreted, only the kinematics module reads it;
    // every node needs its id (machine-config 9).
    private static KinematicTree? ReadKinematics(ConfigTable root, string? file)
    {
        IReadOnlyList<ConfigTable> tables = root.Tables("node", "[[node]]", "machine-config 9");
        if (file is null && tables.Count == 0)
        {
            return null;
        }

        var nodes = new List<KinematicNode>();
        foreach (ConfigTable table in tables)
        {
            table.WarnUnknownKeys(s_nodeKeys);
            string? id = table.RequiredText("id");
            string? parent = table.Text("parent");
            string? type = table.Text("type");
            IReadOnlyList<decimal> direction = table.NumberList("direction");
            if (id is not null)
            {
                nodes.Add(new KinematicNode { Id = id, Parent = parent, Type = type, Direction = direction });
            }
        }

        return new KinematicTree { File = file, Nodes = nodes };
    }
}
