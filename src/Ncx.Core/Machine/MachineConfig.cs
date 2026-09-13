namespace Ncx.Core.Machine;

/// <summary>
/// One machine file, loaded once and never changed: the tables of machine-config 1 to 9 as typed records, the
/// template values kept as text (code-guidelines 7, D107). Readers, compilers and the virtual machine resolve roles,
/// axes and functions through its methods, so that all of them ask the same object (architecture 6).
/// </summary>
public sealed record MachineConfig
{
    private static readonly char[] s_digits = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

    /// <summary>
    /// [machine]: which machine, controller and dialect, and the default resources (machine-config 1).
    /// </summary>
    public required MachineIdentity Machine { get; init; }

    /// <summary>
    /// [machine] limits: what the expander does with RPM, F and targets beyond the machine limits; warn when the file
    /// leaves it out (machine-config 1, D64).
    /// </summary>
    public LimitPolicy Limits { get; init; } = LimitPolicy.Warn;

    /// <summary>
    /// [machine] tool_table: the optional tool table file (machine-config 1, D10); null without one.
    /// </summary>
    public string? ToolTable { get; init; }

    /// <summary>
    /// [format]: how the compiler writes numbers, block numbers and the program end (machine-config 2); null when the
    /// file has no such table.
    /// </summary>
    public OutputFormat? Format { get; init; }

    /// <summary>
    /// [tool_change]: the tool change templates (machine-config 3); null when the file has no such table.
    /// </summary>
    public ToolChangeConfig? ToolChange { get; init; }

    /// <summary>
    /// [home]: the reference point templates (machine-config 3); null when the file has none, as on Heidenhain.
    /// </summary>
    public HomeConfig? Home { get; init; }

    /// <summary>
    /// [setpos]: the SETPOS template (machine-config 3); null when the file has none, as on Heidenhain.
    /// </summary>
    public SetposConfig? Setpos { get; init; }

    /// <summary>
    /// [roles]: role name to resource id, MAIN to S1 (machine-config 4, language 4.10).
    /// </summary>
    public IReadOnlyDictionary<string, string> Roles { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// [[resource]]: the spindles, holders and tables, in file order (machine-config 4).
    /// </summary>
    public IReadOnlyList<ResourceDef> Resources { get; init; } = [];

    /// <summary>
    /// [[axis]]: the axes of the machine, in file order (machine-config 4).
    /// </summary>
    public IReadOnlyList<AxisDef> Axes { get; init; } = [];

    /// <summary>
    /// [positions]: named positions in machine coordinates (machine-config 4, D100); empty when the file has none.
    /// </summary>
    public PositionsTable Positions { get; init; } = new();

    /// <summary>
    /// [dynamics]: the control behaviour for the runtime estimate (machine-config 4, D64); null when the file has none.
    /// </summary>
    public DynamicsConfig? Dynamics { get; init; }

    /// <summary>
    /// [diameter]: how the machine switches diameter programming (machine-config 4, D60); null when the file has none.
    /// </summary>
    public DiameterConfig? Diameter { get; init; }

    /// <summary>
    /// [spindle.ROLE]: the function table of each spindle role (machine-config 5), by role.
    /// </summary>
    public IReadOnlyDictionary<string, FunctionTable> SpindleTables { get; init; } =
        new Dictionary<string, FunctionTable>();

    /// <summary>
    /// [spindle_mode.ROLE]: the AXIS and SPINDLE templates of each spindle role (machine-config 5), by role.
    /// </summary>
    public IReadOnlyDictionary<string, FunctionTable> SpindleModeTables { get; init; } =
        new Dictionary<string, FunctionTable>();

    /// <summary>
    /// [spindle_sync]: the synchronization templates (machine-config 5, D56); null when the file has none.
    /// </summary>
    public FunctionTable? SpindleSync { get; init; }

    /// <summary>
    /// [workpiece]: the native selection of the workpiece holder and how its side is programmed (machine-config 5,
    /// D57); null when the file has none.
    /// </summary>
    public WorkpieceConfig? Workpiece { get; init; }

    /// <summary>
    /// [sync]: the wait templates and marks of a multi-channel machine (machine-config 5); null when the file has none.
    /// </summary>
    public SyncConfig? Sync { get; init; }

    /// <summary>
    /// [coolant]: the function table of each coolant channel, STANDARD, AIR, THROUGH (machine-config 5), by channel.
    /// </summary>
    public IReadOnlyDictionary<string, FunctionTable> Coolant { get; init; } = new Dictionary<string, FunctionTable>();

    /// <summary>
    /// [func]: the function table of each named function, SUB_CHUCK, DOOR (machine-config 5), by name.
    /// </summary>
    public IReadOnlyDictionary<string, FunctionTable> Functions { get; init; } =
        new Dictionary<string, FunctionTable>();

    /// <summary>
    /// [func_meta]: the reader fallback rules per function state (machine-config 5, D40); empty when the file has none.
    /// </summary>
    public FuncMeta FuncMeta { get; init; } = new();

    /// <summary>
    /// [transform]: the transformation templates (machine-config 5); null when the file has none.
    /// </summary>
    public TransformTable? Transform { get; init; }

    /// <summary>
    /// [retract]: the RETRACT templates (machine-config 5, D83); null when the file has none.
    /// </summary>
    public RetractTable? Retract { get; init; }

    /// <summary>
    /// [tolerance]: the TOLERANCE templates (machine-config 5, D85); null when the file has none.
    /// </summary>
    public ToleranceTable? Tolerance { get; init; }

    /// <summary>
    /// [raw]: the builder codes the reader keeps as RAW (machine-config 5, F23); null when the file has none.
    /// </summary>
    public RawTable? Raw { get; init; }

    /// <summary>
    /// [cycles]: the cycle catalog of the controller family (machine-config 6); null when the file has none.
    /// </summary>
    public CyclesConfig? Cycles { get; init; }

    /// <summary>
    /// [[cycle]]: the catalog entries of the machine file as it writes them, which override the cycle catalog of its
    /// controller family per machine (machine-config 6); empty when the file has none.
    /// </summary>
    public IReadOnlyList<CycleEntry> CycleEntries { get; init; } = [];

    /// <summary>
    /// The cycle catalog of the machine: the built-in drilling family of its controller, the catalog file of [cycles]
    /// once it is loaded (CycleCatalogLoader.WithCatalog of Ncx.Config) and the [[cycle]] entries of the machine file
    /// over both (machine-config 6); empty on the default machine of D103.
    /// </summary>
    public CycleCatalog CycleCatalog { get; init; } = new();

    /// <summary>
    /// [variables]: unassigned variables, the name map, block cap and call depth (machine-config 7), with the defaults
    /// of virtual machine 3.6 when the file has no such table.
    /// </summary>
    public VariablesConfig Variables { get; init; } = new();

    /// <summary>
    /// [system_variables]: NCX SYS_ names to native system variables (machine-config 7, D51); empty when the file has
    /// none.
    /// </summary>
    public SystemVariables SystemVariables { get; init; } = new();

    /// <summary>
    /// [[node]] and [machine] kinematics: the kinematic tree, loaded and not interpreted (machine-config 9); null when
    /// the file has neither.
    /// </summary>
    public KinematicTree? Kinematics { get; init; }

    /// <summary>
    /// The resource with this id of [[resource]]; null when the file declares none.
    /// </summary>
    /// <param name="id">The resource id, S1.</param>
    public ResourceDef? FindResource(string id)
    {
        foreach (ResourceDef resource in Resources)
        {
            if (resource.Id == id)
            {
                return resource;
            }
        }

        return null;
    }

    /// <summary>
    /// The axis with this id of [[axis]]; null when the file declares none.
    /// </summary>
    /// <param name="id">The axis id, C2.</param>
    public AxisDef? FindAxis(string id)
    {
        foreach (AxisDef axis in Axes)
        {
            if (axis.Id == id)
            {
                return axis;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a role address through [roles] to its resource (virtual machine 3.8 rule 1); null for an unknown role
    /// or a role that names no declared resource, which the caller reports.
    /// </summary>
    /// <param name="role">The role of the word's address, MAIN.</param>
    public ResourceDef? ResolveRole(string role)
    {
        // A role address resolves through [roles] to a resource id (virtual machine 3.8 rule 1).
        if (!Roles.TryGetValue(role, out string? id))
        {
            return null;
        }

        return FindResource(id);
    }

    /// <summary>
    /// The spindle that SPINDLE, RPM, VC, CSS, RPM_MAX and ORIENT without a role address target: default_spindle, or
    /// the only spindle of the machine (virtual machine 3.8 rule 2); null otherwise.
    /// </summary>
    public ResourceDef? ResolveDefaultSpindle()
    {
        // The words without a role address target default_spindle. Only more than one spindle without it is an ERROR
        // when the machine is loaded, so a machine with one spindle needs no default (virtual machine 3.8 rule 2).
        if (Machine.DefaultSpindle is string id)
        {
            return FindResource(id);
        }

        return OnlyResource(spindles: true);
    }

    /// <summary>
    /// The holder that TOOL and PRELOAD without a role address target: default_holder, or the only tool holder of the
    /// machine (virtual machine 3.8 rule 2); null otherwise.
    /// </summary>
    public ResourceDef? ResolveDefaultHolder()
    {
        // TOOL and PRELOAD without a role address target default_holder; one holder needs no default (virtual machine
        // 3.8 rule 2).
        if (Machine.DefaultHolder is string id)
        {
            return FindResource(id);
        }

        return OnlyResource(spindles: false);
    }

    /// <summary>
    /// Resolves an axis name of a program through the NCX names of [[axis]], Z2 to the axis with ncx = "Z2" (virtual
    /// machine 3.8 rule 3, D93); null when no axis carries the name.
    /// </summary>
    /// <param name="ncxName">The axis name as the program writes it.</param>
    public AxisDef? ResolveAxis(string ncxName)
    {
        // Explicit machine axis names resolve through the [[axis]] list by the name used in NCX programs (virtual
        // machine 3.8 rule 3, machine-config 4).
        foreach (AxisDef axis in Axes)
        {
            if (axis.NcxName == ncxName)
            {
                return axis;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves an axis name of a program for the workpiece holder in use: A, B and C name the rotary axis of that
    /// holder when it has one, every other name the axis with that NCX name (virtual machine 3.8 rule 3, language
    /// 4.3); null when no axis fits.
    /// </summary>
    /// <param name="ncxName">The axis name as the program writes it.</param>
    /// <param name="workpieceHolder">The resource id of the current workpiece holder, S2 after WORKPIECE=SUB.</param>
    public AxisDef? ResolveAxis(string ncxName, string workpieceHolder)
    {
        // A, B, C resolve to the rotary axis of the current workpiece holder when it has one, otherwise to the machine
        // axis with that NCX name (virtual machine 3.8 rule 3). The rotary axis of a holder is the axis its
        // [[resource]] names, the C axis the spindle becomes in AXIS mode (machine-config 4).
        // TODO(question): rule 3 lets A, B and C all resolve to the holder's rotary axis; this resolves only the name
        // whose letter the axis carries (C to C2 of the sub spindle), so that A stays the A slide of the Mori Seiki.
        bool standardRotaryName = ncxName is "A" or "B" or "C";
        ResourceDef? holder = FindResource(workpieceHolder);
        if (standardRotaryName && holder?.Axis is string holderAxisId)
        {
            AxisDef? holderAxis = FindAxis(holderAxisId);
            if (holderAxis is not null
                && holderAxis.Kind == AxisKind.Rotary
                && holderAxis.NcxName.TrimEnd(s_digits) == ncxName)
            {
                return holderAxis;
            }
        }

        return ResolveAxis(ncxName);
    }

    /// <summary>
    /// The template of a named function in one state, [func] NAME = { STATE = "..." } (machine-config 5):
    /// FindFunction("SUB_CHUCK", "OPEN") is "M68" on millturn1.toml; null when the file names no such function or
    /// state.
    /// </summary>
    /// <param name="name">The function name, the address of FUNC.</param>
    /// <param name="state">The state, the value of FUNC.</param>
    public string? FindFunction(string name, string state)
    {
        // FUNC:SUB_CHUCK=OPEN compiles to the code of that state of the function (machine-config 5).
        if (!Functions.TryGetValue(name, out FunctionTable? function))
        {
            return null;
        }

        return function.States.TryGetValue(state, out string? template) ? template : null;
    }

    // The one resource of a kind, spindles or tool holders, when the machine has exactly one (virtual machine 3.8
    // rule 2).
    private ResourceDef? OnlyResource(bool spindles)
    {
        ResourceDef? found = null;
        foreach (ResourceDef resource in Resources)
        {
            bool ofTheKind = spindles ? resource.IsSpindle : resource.Type == ResourceType.ToolHolder;
            if (!ofTheKind)
            {
                continue;
            }

            if (found is not null)
            {
                return null;
            }

            found = resource;
        }

        return found;
    }
}
