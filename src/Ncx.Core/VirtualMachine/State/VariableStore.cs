using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The variables of a channel (virtual machine 2.7): what VAR and ARG assign, the locals V1 to V33 of each call, and
/// the SYS_ names that read the state of the channel. UNKNOWN is a value like any other: in STATIC mode a variable set
/// from an expression holds it (virtual machine 1). The expression evaluator reads through Get and GetSystem.
/// </summary>
internal sealed class VariableStore
{
    // Names starting with SYS_ are reserved for the values the control provides (language 4.12, D51).
    private const string SystemPrefix = "SYS_";

    // The active tool (language 4.12).
    private const string ActiveToolName = "SYS_TOOL";

    // The locals of a call are V1 to V33 (virtual machine 3.6).
    private const string LocalPrefix = "V";
    private const int LastLocal = 33;

    private readonly ChannelState _channel;
    private readonly UnassignedVariable _unassigned;
    private readonly SystemVariables _systemVariables;
    private readonly Dictionary<string, VariableValue> _variables = new();
    private readonly Stack<Dictionary<string, VariableValue>> _callerLocals = new();
    private readonly Dictionary<string, VariableValue> _systemStartValues = new();
    private Dictionary<string, VariableValue> _locals = new();

    /// <summary>
    /// The variables of a channel at the start of a run.
    /// </summary>
    /// <param name="channel">The channel whose state the SYS_ names read.</param>
    /// <param name="machine">The machine configuration: [variables] unassigned and [system_variables].</param>
    /// <param name="startValues">The start values of &lt;file&gt;.vars.toml by name; empty without one.</param>
    public VariableStore(ChannelState channel, MachineConfig machine, IReadOnlyDictionary<string, Value> startValues)
    {
        _channel = channel;
        _unassigned = machine.Variables.Unassigned;
        _systemVariables = machine.SystemVariables;

        // Variables start from <file>.vars.toml when there is one (virtual machine 2.7, 3.6). A SYS_ name is never
        // assigned (2.7): its start value is kept apart, for the INTERPRETED mode that reads a register the virtual
        // machine does not hold from the vars file.
        // TODO: the evaluator of phase 4 reads these SYS_ start values in INTERPRETED mode (virtual machine 2.7).
        foreach (KeyValuePair<string, Value> start in startValues)
        {
            if (IsSystem(start.Key))
            {
                _systemStartValues[start.Key] = VariableValue.Of(start.Value);
            }
            else
            {
                Set(start.Key, VariableValue.Of(start.Value));
            }
        }
    }

    /// <summary>
    /// True for a SYS_ name, which the program reads and never assigns (language 4.12, virtual machine 2.7).
    /// </summary>
    public static bool IsSystem(string name)
    {
        return name.StartsWith(SystemPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// True for V1 to V33, the locals that every CALL pushes (virtual machine 3.6).
    /// </summary>
    public static bool IsLocal(string name)
    {
        // V1 to V33 as written, without leading zeros: V01 is another name.
        for (int number = 1; number <= LastLocal; number++)
        {
            if (name == LocalPrefix + number.ToString(CultureInfo.InvariantCulture))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads a variable: its value, UNKNOWN included; 0 for an unassigned variable under unassigned = 0; null for an
    /// unassigned variable otherwise, which is the ERROR the caller reports on its block (virtual machine 3.6, D38).
    /// </summary>
    /// <param name="name">The name after the dollar sign: Q1, V101, V1, SYS_POS_X.</param>
    public VariableValue? Get(string name)
    {
        // A SYS_ name reads the state of the channel (virtual machine 2.7).
        if (IsSystem(name))
        {
            return GetSystem(name, null);
        }

        // A variable reads the value it was assigned last, UNKNOWN included (virtual machine 1, 2.7).
        if (TableOf(name).TryGetValue(name, out VariableValue? value))
        {
            return value;
        }

        // Reading an unassigned variable is an ERROR unless the configuration sets unassigned = 0 (virtual machine
        // 3.6, D38).
        if (_unassigned == UnassignedVariable.Zero)
        {
            return VariableValue.Of(new IntegerValue(0, "0"));
        }

        return null;
    }

    /// <summary>
    /// Assigns a variable, UNKNOWN included; the locals V1 to V33 of the call that runs.
    /// </summary>
    /// <param name="name">The address of VAR or the name ARG gives the callee: Q1, V101, V1.</param>
    /// <param name="value">The value, a number, a string or UNKNOWN.</param>
    public void Set(string name, VariableValue value)
    {
        // A SYS_ variable is never assigned by the program; the assignment is an ERROR that the virtual machine
        // reports before it gets here (virtual machine 2.7, 5).
        if (IsSystem(name))
        {
            throw new InvalidOperationException(
                $"{name} is a system variable, never assigned by the program (virtual machine 2.7).");
        }

        TableOf(name)[name] = value;
    }

    /// <summary>
    /// Reads a SYS_ name from the state of the channel through the mapping of the configuration (virtual machine 2.7,
    /// 3.6, D51); UNKNOWN when the configuration does not map it or the state is unknown. INTERPRETED mode turns
    /// UNKNOWN into the vars file value or the ERROR; STATIC mode keeps it (virtual machine 2.7).
    /// </summary>
    /// <param name="name">The SYS_ name without its index: SYS_TOOL, SYS_WEAR_Z.</param>
    /// <param name="index">The register or table row of [index], 99 of SYS_WEAR_Z[99]; null without one.</param>
    public VariableValue GetSystem(string name, int? index)
    {
        // A SYS_ name the configuration does not map, or maps to an empty template (a value the control cannot read),
        // is UNKNOWN (virtual machine 2.7, 3.6).
        if (string.IsNullOrEmpty(_systemVariables.Find(name)))
        {
            return VariableValue.Unknown;
        }

        // The active tool is the tool in the spindle of the holder called last (language 4.12, virtual machine 2.3).
        if (name == ActiveToolName)
        {
            return ActiveTool();
        }

        // Every other name reads a state the virtual machine does not hold, a wear register of any index, a tool
        // length, a part status: UNKNOWN (virtual machine 2.7).
        // TODO: SYS_POS_ and SYS_MPOS_ read the position of an axis once the frame rules of P1-02 fix how SETPOS and
        // HOME store it (virtual machine 3.4, D101); they are UNKNOWN until then.
        return VariableValue.Unknown;
    }

    /// <summary>
    /// Keeps the locals V1 to V33 of the caller at a CALL and starts the callee's own (virtual machine 3.6).
    /// </summary>
    public void PushLocals()
    {
        // CALL pushes the local variables V1 to V33; ARG words are assigned to the callee's locals (virtual machine
        // 3.6).
        // TODO(question): virtual machine 3.6 does not say whether the callee's locals start unassigned or as a copy
        // of the caller's; they start unassigned, as the locals of a Fanuc G65 macro call do.
        _callerLocals.Push(_locals);
        _locals = new();
    }

    /// <summary>
    /// Restores the locals of the caller at SUB=END or RETURN (virtual machine 3.6).
    /// </summary>
    public void PopLocals()
    {
        // SUB=END and RETURN restore the locals (virtual machine 3.6, 4). A RETURN in a program has no caller and is
        // treated as JUMP=END, so a pop without a push is a mistake of the virtual machine.
        if (_callerLocals.Count == 0)
        {
            throw new InvalidOperationException("PopLocals without a PushLocals: no CALL to return from.");
        }

        _locals = _callerLocals.Pop();
    }

    /// <summary>
    /// An immutable copy of the variables a block sees now: the assigned variables and the locals of the call that
    /// runs.
    /// </summary>
    public IReadOnlyDictionary<string, VariableValue> Snapshot()
    {
        var visible = new Dictionary<string, VariableValue>(_variables);
        foreach (KeyValuePair<string, VariableValue> local in _locals)
        {
            visible[local.Key] = local.Value;
        }

        return visible.AsReadOnly();
    }

    // The locals V1 to V33 live with the call that runs, every other variable for the program's lifetime (virtual
    // machine 4).
    private Dictionary<string, VariableValue> TableOf(string name)
    {
        return IsLocal(name) ? _locals : _variables;
    }

    // The tool in the spindle of the holder called last: its number, or its name as a string (language 4.4).
    private VariableValue ActiveTool()
    {
        if (_channel.LastHolder is not string holderId
            || !_channel.Holders.TryGetValue(holderId, out HolderState? holder))
        {
            return VariableValue.Unknown;
        }

        ToolRef tool = holder.SpindleTool;
        if (tool.Name is string toolName)
        {
            return VariableValue.Of(new StringValue(toolName));
        }

        int number = tool.Number ?? 0;
        return VariableValue.Of(new IntegerValue(number, number.ToString(CultureInfo.InvariantCulture)));
    }
}
