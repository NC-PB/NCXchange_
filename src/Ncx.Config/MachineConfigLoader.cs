using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config;

/// <summary>
/// Loads a machine file into the records of the machine model (D107). The TOML document is walked table by table, not
/// bound by reflection, so that an unknown key is a WARNING on its line that names the nearest known key, a wrong type
/// or a missing required key an ERROR on its line, and the checks of the resources run when the machine is loaded
/// (P2-01, virtual machine 3.8). The class has one part per group of sections of machine-config.
/// </summary>
public static partial class MachineConfigLoader
{
    // The tables of machine-config 1 to 9 in the order of the specification, [raw] with F23.
    private static readonly string[] s_rootTables =
    [
        "machine", "format", "tool_change", "home", "setpos", "roles", "resource", "axis", "positions", "dynamics",
        "diameter", "spindle", "spindle_mode", "spindle_sync", "workpiece", "sync", "coolant", "func", "func_meta",
        "transform", "retract", "tolerance", "raw", "cycles", "cycle", "variables", "system_variables", "node",
    ];

    // The tables the file writes [[name]], one per entry.
    private static readonly string[] s_rootTableArrays = ["resource", "axis", "cycle", "node"];

    /// <summary>
    /// Loads the machine file at a path. A file that cannot be read is an I/O error, thrown for the composition root,
    /// which reports it with the file name (code-guidelines 6).
    /// </summary>
    /// <param name="path">The machine file.</param>
    /// <param name="diagnostics">Where the mistakes of the file are reported.</param>
    /// <returns>The machine, or null when the file has an ERROR.</returns>
    public static MachineConfig? Load(string path, Diagnostics diagnostics)
    {
        return LoadText(File.ReadAllText(path), diagnostics);
    }

    /// <summary>
    /// Loads a machine file from its text: unknown keys are WARNINGs, wrong types, missing required keys and the
    /// failed checks of the resources are ERRORs, each on its line (P2-01).
    /// </summary>
    /// <param name="text">The TOML text of the machine file.</param>
    /// <param name="diagnostics">Where the mistakes of the file are reported.</param>
    /// <returns>The machine, or null when the file has an ERROR.</returns>
    public static MachineConfig? LoadText(string text, Diagnostics diagnostics)
    {
        int errorsBefore = TomlDocument.ErrorCount(diagnostics);
        TomlDocument? document = TomlDocument.Parse(text, diagnostics);
        if (document is null)
        {
            return null;
        }

        // Every table of machine-config 1 to 9 is known; any other is a WARNING that names the nearest one, because
        // the schema is a sketch until the compiler exists (P2-01).
        ConfigTable root = document.Root("machine-config");
        root.WarnUnknownTables(s_rootTables, s_rootTableArrays);

        // [machine] names the machine and its controller (machine-config 1).
        // TODO(question): machine-config names no required key. The loader requires [machine] with name and
        // controller, id and type of every [[resource]], id, ncx and kind of every [[axis]] and id of every [[node]],
        // the keys without which a record cannot be built or referenced; every other key is optional.
        if (!root.Has("machine"))
        {
            diagnostics.Error(1, DiagnosticCodes.MissingKey,
                "The machine file has no [machine] table, which is required (machine-config 1).");
        }

        ConfigTable? machineTable = root.Table("machine", section: "machine-config 1");
        List<AxisDef> axes = ReadAxes(root);
        var machine = new MachineConfig
        {
            Machine = ReadMachine(machineTable),
            Limits = ReadLimitPolicy(machineTable),
            ToolTable = machineTable?.Text("tool_table"),
            Format = ReadFormat(root.Table("format", section: "machine-config 2")),
            ToolChange = ReadToolChange(root.Table("tool_change", section: "machine-config 3")),
            Home = ReadHome(root.Table("home", section: "machine-config 3")),
            Setpos = ReadSetpos(root.Table("setpos", section: "machine-config 3")),
            Roles = ReadRoles(root.Table("roles", section: "machine-config 4")),
            Resources = ReadResources(root, axes),
            Axes = axes,
            Positions = ReadPositions(root.Table("positions", section: "machine-config 4")),
            Dynamics = ReadDynamics(root.Table("dynamics", section: "machine-config 4")),
            Diameter = ReadDiameter(root.Table("diameter", section: "machine-config 4")),
            SpindleTables = ReadRoleTables(
                root.Table("spindle", section: "machine-config 5"), "spindle", s_spindleStates, spindleLimits: true),
            SpindleModeTables = ReadRoleTables(
                root.Table("spindle_mode", section: "machine-config 5"), "spindle_mode", s_spindleModeStates,
                spindleLimits: false),
            SpindleSync = ReadSpindleSync(root.Table("spindle_sync", section: "machine-config 5")),
            Workpiece = ReadWorkpiece(root.Table("workpiece", section: "machine-config 5")),
            Sync = ReadSync(root.Table("sync", section: "machine-config 5")),
            Coolant = ReadNamedTables(root.Table("coolant", section: "machine-config 5"), s_coolantStates),
            Functions = ReadNamedTables(root.Table("func", section: "machine-config 5"), null),
            FuncMeta = ReadFuncMeta(root.Table("func_meta", section: "machine-config 5")),
            Transform = ReadTransform(root.Table("transform", section: "machine-config 5")),
            Retract = ReadRetract(root.Table("retract", section: "machine-config 5")),
            Tolerance = ReadTolerance(root.Table("tolerance", section: "machine-config 5")),
            Raw = ReadRaw(root.Table("raw", section: "machine-config 5")),
            Cycles = ReadCycles(root.Table("cycles", section: "machine-config 6")),
            Variables = ReadVariables(root.Table("variables", section: "machine-config 7")),
            SystemVariables = ReadSystemVariables(root.Table("system_variables", section: "machine-config 7")),
            Kinematics = ReadKinematics(root, machineTable?.Text("kinematics")),
        };

        // TODO: [[cycle]] entries override the catalog per machine (machine-config 6); the table is known, and its
        // entries are loaded into the catalog records of the cycle catalog task (P2-03), which does not exist yet.
        CheckDefaults(machine, machineTable?.Line ?? 1, diagnostics);
        return TomlDocument.ErrorCount(diagnostics) > errorsBefore ? null : machine;
    }
}
