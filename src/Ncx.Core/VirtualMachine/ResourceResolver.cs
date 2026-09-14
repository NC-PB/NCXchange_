using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// Resolves role addresses, default resources, axis names, functions and coolant channels against the machine
/// configuration (virtual machine 3.8 rules 1 to 3), with the WARNING path of the built-in default machine when no
/// machine file is given (D103), and checks the spindle rules 4 and 5. One per run, so that its WARNINGs come once per
/// name and the resources it creates on the spot stay the same resources for the whole run.
/// </summary>
internal sealed class ResourceResolver
{
    // The rotary axis a work spindle created on the spot gets is named after its role, C_SUB for SUB, a name no program
    // can write as an axis word (language 3, KEY; D93), so it never meets a machine axis of the file (D103).
    private const string CreatedAxisPrefix = "C_";

    // The name that resolves to the rotary axis of a work spindle created on the spot while it holds the workpiece
    // (virtual machine 3.8 rule 3, D103).
    private const string SpindleAxisName = "C";

    // The coolant channel that a bare COOLANT addresses, always there (virtual machine 2.5, F29).
    private const string DefaultCoolantChannel = "STANDARD";

    private readonly MachineConfig _machine;
    private readonly HashSet<string> _warned = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ResourceDef> _createdResources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AxisKind> _createdAxes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _createdFunctions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _createdCoolant = new(StringComparer.Ordinal);

    /// <summary>
    /// A resolver for one run against a machine file or the built-in default machine.
    /// </summary>
    /// <param name="machine">The machine configuration of the run.</param>
    public ResourceResolver(MachineConfig machine)
    {
        _machine = machine;

        // Only the built-in default machine of D103 has no controller: every machine file names one, because the
        // controller selects its reader and compiler (machine-config 1), and the default machine serves neither (D77,
        // MachineIdentity.Controller).
        WithoutMachineFile = machine.Machine.Controller is null;
    }

    /// <summary>
    /// True when the run has no machine file and checks against the built-in default machine of D103.
    /// </summary>
    public bool WithoutMachineFile { get; }

    /// <summary>
    /// The spindle of SPINDLE, RPM, CSS, VC, RPM_MAX and ORIENT: the spindle of the role, or default_spindle without
    /// one (virtual machine 3.8 rules 1 and 2).
    /// </summary>
    public string? ResolveSpindle(string? role, Block block, ChannelState state, Diagnostics diagnostics)
    {
        // TODO(question): D103 creates a resource on the spot for a role the default machine lacks without saying of
        // which kind for a spindle word; it is a work spindle, which D103 gives a rotary axis of its own.
        return Resolve(role, _machine.ResolveDefaultSpindle(), static resource => resource.IsSpindle, "spindle",
            ResourceType.WorkSpindle, block, state, diagnostics);
    }

    /// <summary>
    /// The work spindle of SPINDLE_MODE and of each role of SPINDLE_SYNC: a spindle that holds the workpiece and can
    /// become a positioning C axis (language 4.5, virtual machine 3.8 rules 1, 2 and 5).
    /// </summary>
    public string? ResolveWorkSpindle(string? role, Block block, ChannelState state, Diagnostics diagnostics)
    {
        return Resolve(role, _machine.ResolveDefaultSpindle(),
            static resource => resource.Type == ResourceType.WorkSpindle, "work spindle", ResourceType.WorkSpindle,
            block, state, diagnostics);
    }

    /// <summary>
    /// The tool holder of TOOL and PRELOAD: the holder of the role, or default_holder without one (virtual machine 3.8
    /// rules 1 and 2).
    /// </summary>
    public string? ResolveHolder(string? role, Block block, ChannelState state, Diagnostics diagnostics)
    {
        return Resolve(role, _machine.ResolveDefaultHolder(),
            static resource => resource.Type == ResourceType.ToolHolder, "tool holder", ResourceType.ToolHolder, block,
            state, diagnostics);
    }

    /// <summary>
    /// The holder of WORKPIECE=role: the work spindle or the table that holds the part (language 4.10, virtual machine
    /// 3.8 rule 1).
    /// </summary>
    public string? ResolveWorkpieceHolder(string role, Block block, ChannelState state, Diagnostics diagnostics)
    {
        return Resolve(role, null,
            static resource => resource.Type is ResourceType.WorkSpindle or ResourceType.Table,
            "work spindle or table", ResourceType.WorkSpindle, block, state, diagnostics);
    }

    /// <summary>
    /// Resolves an axis name of a program to the key of the position store (virtual machine 3.8 rule 3): A, B and C to
    /// the rotary axis of the current workpiece holder when it has one, every other name through the [[axis]] list by
    /// its NCX name; null when the name resolves to no axis, an ERROR with a machine file.
    /// </summary>
    /// <param name="name">The axis name as written, without the I of the incremental form: C, Z2.</param>
    /// <param name="block">The block, which a diagnostic names.</param>
    /// <param name="state">The channel state: the workpiece holder, and the position store an axis is added to.</param>
    /// <param name="diagnostics">Where an ERROR or the D103 WARNING goes.</param>
    public string? ResolveAxis(string name, Block block, ChannelState state, Diagnostics diagnostics)
    {
        // A work spindle created on the spot has a rotary axis of its own, so C resolves to it while it is the
        // workpiece holder (virtual machine 3.8 rule 3, D103).
        string? holder = state.Frame.WorkpieceHolder;
        if (holder is not null
            && name == SpindleAxisName
            && _createdResources.TryGetValue(holder, out ResourceDef? createdHolder)
            && createdHolder.Axis is string createdHolderAxis)
        {
            EnsureAxisInState(createdHolderAxis, state);
            return createdHolderAxis;
        }

        // A, B, C resolve to the rotary axis of the current workpiece holder when it has one, otherwise to the machine
        // axis with that NCX name; explicit machine axis names resolve through the [[axis]] list (rule 3, D93).
        AxisDef? axis = holder is null ? _machine.ResolveAxis(name) : _machine.ResolveAxis(name, holder);
        if (axis is not null)
        {
            return axis.NcxName;
        }

        if (_createdAxes.ContainsKey(name))
        {
            EnsureAxisInState(name, state);
            return name;
        }

        // Anything else is an ERROR with a machine file; without one it is the WARNING of D103 and the word runs
        // against an axis created on the spot (rule 3, D93, D103).
        if (!WithoutMachineFile)
        {
            diagnostics.Error(block, DiagnosticCodes.UnknownMachineAxis,
                $"{name} is no axis of the [[axis]] list of the machine (virtual machine 3.8 rule 3, D93).");
            return null;
        }

        _createdAxes[name] = name[0] is 'A' or 'B' or 'C' ? AxisKind.Rotary : AxisKind.Linear;
        EnsureAxisInState(name, state);
        WarnNotChecked("axis", name, "an axis", block, diagnostics);
        return name;
    }

    /// <summary>
    /// The key of the position store an axis name resolves to, found without resolving the name anew: no diagnostic
    /// and no axis created on the spot; null for a name no axis of the run carries (virtual machine 3.8 rule 3). For
    /// the axes a motion reads without a word of its block on them: the plane axes of an ARC, the drilling axis of a
    /// cycle whose sequence the VM does not know, the linear axes under a tilted RETRACT.
    /// </summary>
    /// <param name="name">The axis name: X, C.</param>
    /// <param name="state">The channel state with the workpiece holder.</param>
    public string? KeyOfAxis(string name, ChannelState state)
    {
        string? holder = state.Frame.WorkpieceHolder;
        if (holder is not null
            && name == SpindleAxisName
            && _createdResources.TryGetValue(holder, out ResourceDef? createdHolder)
            && createdHolder.Axis is string createdHolderAxis)
        {
            return createdHolderAxis;
        }

        AxisDef? axis = holder is null ? _machine.ResolveAxis(name) : _machine.ResolveAxis(name, holder);
        if (axis is not null)
        {
            return axis.NcxName;
        }

        return _createdAxes.ContainsKey(name) ? name : null;
    }

    /// <summary>
    /// Resolves the function of FUNC:name (language 4.6); false for a function a machine file does not name, an ERROR.
    /// </summary>
    public bool ResolveFunction(string name, Block block, ChannelState state, Diagnostics diagnostics)
    {
        // A named machine function is defined in the machine configuration (language 4.6); one the default machine
        // lacks is the WARNING of D103 and the word runs against a function created on the spot.
        if (_machine.Functions.ContainsKey(name) || _createdFunctions.Contains(name))
        {
            state.Functions.TryAdd(name, null);
            return true;
        }

        if (!WithoutMachineFile)
        {
            diagnostics.Error(block, DiagnosticCodes.UnknownFunction,
                $"FUNC:{name} names no function of [func] of the machine (language 4.6, virtual machine 5).");
            return false;
        }

        _createdFunctions.Add(name);
        state.Functions.TryAdd(name, null);
        WarnNotChecked("function", name, "a function", block, diagnostics);
        return true;
    }

    /// <summary>
    /// Tells whether a state is one of the states of a function; false for a state a machine file does not list for
    /// it, an ERROR (machine-config 5: the states of a [func] entry are the values FUNC:name= takes).
    /// </summary>
    public bool AcceptsFunctionState(string name, string value, Block block, Diagnostics diagnostics)
    {
        // A function created on the spot has no states to check against (D103).
        if (!_machine.Functions.TryGetValue(name, out FunctionTable? function) || function.States.ContainsKey(value))
        {
            return true;
        }

        diagnostics.Error(block, DiagnosticCodes.UnknownFunctionState,
            $"FUNC:{name}={value} names no state of the function {name}; its states are "
            + $"{string.Join(", ", function.States.Keys)} (machine-config 5, virtual machine 5).");
        return false;
    }

    /// <summary>
    /// Resolves the channel of COOLANT[:channel] (language 4.6): STANDARD without an address, the default channel
    /// (virtual machine 2.5, F29), or a named channel of [coolant]; false for a channel a machine file does not name.
    /// </summary>
    public bool ResolveCoolantChannel(string channel, Block block, ChannelState state, Diagnostics diagnostics)
    {
        if (channel == DefaultCoolantChannel || _machine.Coolant.ContainsKey(channel)
            || _createdCoolant.Contains(channel))
        {
            state.Coolant.TryAdd(channel, false);
            return true;
        }

        // TODO(question): language 4.6 addresses "a named channel from the machine configuration", and neither virtual
        // machine 3.8 nor 5 says what a channel the configuration does not name is; it is treated like an unknown
        // function: an ERROR with a machine file, the D103 WARNING without one.
        if (!WithoutMachineFile)
        {
            diagnostics.Error(block, DiagnosticCodes.UnknownCoolantChannel,
                $"COOLANT:{channel} names no channel of [coolant] of the machine (language 4.6, machine-config 5).");
            return false;
        }

        _createdCoolant.Add(channel);
        state.Coolant.TryAdd(channel, false);
        WarnNotChecked("coolant channel", channel, "a coolant channel", block, diagnostics);
        return true;
    }

    /// <summary>
    /// The reference point of an axis in machine coordinates: home for point 1, home2 for POINT=2; null when the
    /// configuration has none (machine-config 4, virtual machine 3 step 5, D100).
    /// </summary>
    /// <param name="axis">The key of the position store, the NCX name of the axis.</param>
    /// <param name="point">1, or the POINT of the HOME block.</param>
    public decimal? ReferencePoint(string axis, int point)
    {
        AxisDef? definition = _machine.ResolveAxis(axis);
        return point switch
        {
            1 => definition?.Home,
            2 => definition?.Home2,
            _ => null,
        };
    }

    /// <summary>
    /// Tells whether an axis of the position store is a rotary axis (machine-config 4).
    /// </summary>
    public bool IsRotary(string axis)
    {
        if (_createdAxes.TryGetValue(axis, out AxisKind created))
        {
            return created == AxisKind.Rotary;
        }

        return _machine.ResolveAxis(axis)?.Kind == AxisKind.Rotary;
    }

    /// <summary>
    /// The work spindle whose C axis an axis of the position store is, the axis it becomes in AXIS mode
    /// (machine-config 4, [[resource]] axis); null for any other axis.
    /// </summary>
    public string? SpindleOfAxis(string axis)
    {
        foreach (ResourceDef resource in _machine.Resources)
        {
            if (resource.Type != ResourceType.WorkSpindle || resource.Axis is not string axisId)
            {
                continue;
            }

            string ncxName = _machine.FindAxis(axisId)?.NcxName ?? axisId;
            if (ncxName == axis)
            {
                return resource.Id;
            }
        }

        foreach (ResourceDef created in _createdResources.Values)
        {
            if (created.Axis == axis)
            {
                return created.Id;
            }
        }

        return null;
    }

    /// <summary>
    /// The spindle rules 4 and 5 of virtual machine 3.8, checked against the state the block's state words leave, so
    /// that the words of a block do not depend on their order (virtual machine 3 step 3): SPINDLE on a spindle in AXIS
    /// mode, C= on the axis of a spindle in SPINDLE mode, SPINDLE_SYNC of a spindle that is not in SPINDLE mode, and
    /// RPM or SPINDLE of the following spindle while it runs synchronized.
    /// </summary>
    public void CheckSpindleRules(BlockContext context)
    {
        ChannelState state = context.State;
        foreach (Word word in context.Block.Words)
        {
            if (word.Key is not ("SPINDLE" or "RPM") || !context.ResourceOf.TryGetValue(word, out string? spindleId))
            {
                continue;
            }

            SpindleState spindle = state.Spindles[spindleId];

            // SPINDLE_MODE:r=AXIS: SPINDLE:r is an ERROR until back in SPINDLE mode (rule 4).
            if (word.Key == "SPINDLE" && spindle.Mode == SpindleMode.Axis)
            {
                context.Diagnostics.Error(context.Block, DiagnosticCodes.SpindleInAxisMode,
                    $"{word.ToCanonical()} commands a spindle in AXIS mode; SPINDLE_MODE returns it to SPINDLE mode "
                    + "first (virtual machine 3.8 rule 4).");
            }

            // RPM:b and SPINDLE:b while synchronized are WARNINGs: b follows its partner (rule 5).
            if (spindle.SyncPartner is not null)
            {
                context.Diagnostics.Warning(context.Block, DiagnosticCodes.SynchronizedSpindleCommanded,
                    $"{word.ToCanonical()} commands a spindle that runs synchronized with {spindle.SyncPartner} and "
                    + "follows it (virtual machine 3.8 rule 5).");
            }
        }

        // SPINDLE_SYNC=a,b needs two work spindles in SPINDLE mode (rule 5).
        foreach (string spindleId in context.SyncSpindles)
        {
            if (state.Spindles[spindleId].Mode != SpindleMode.Spindle)
            {
                context.Diagnostics.Error(context.Block, DiagnosticCodes.SyncSpindleNotInSpindleMode,
                    $"SPINDLE_SYNC needs two work spindles in SPINDLE mode; {spindleId} is in AXIS mode (virtual "
                    + "machine 3.8 rule 5).");
            }
        }

        CheckSpindleAxisWords(context);
    }

    // In SPINDLE mode, C= on the axis of a work spindle is an ERROR; in AXIS mode it is allowed (rule 4). The words
    // that command or declare the position of an axis are those of the motion verbs and of SETPOS.
    // TODO(question): rule 4 names C= without naming the verbs; a C word under SHIFT or TILT_AXIS is a shift or an
    // angle, not a position of the spindle, and is not checked until that is answered.
    private void CheckSpindleAxisWords(BlockContext context)
    {
        if (context.Block.Verb?.Key is not ("RAPID" or "LINE" or "ARC" or "CYCLE_CALL" or "SETPOS"))
        {
            return;
        }

        foreach (Word word in context.Block.Words)
        {
            if (word.Value is NoValue
                || !context.AxisOf.TryGetValue(word, out string? axis)
                || SpindleOfAxis(axis) is not string spindleId
                || context.State.Spindles[spindleId].Mode != SpindleMode.Spindle)
            {
                continue;
            }

            context.Diagnostics.Error(context.Block, DiagnosticCodes.AxisOfSpindleInSpindleMode,
                $"{word.ToCanonical()} positions the axis of the spindle {spindleId}, which is in SPINDLE mode; "
                + "SPINDLE_MODE switches it to AXIS mode first (virtual machine 3.8 rule 4).");
        }
    }

    // A role address resolves through [roles] to a resource; no role address targets the default resource of the kind.
    // Unknown role or wrong resource type: ERROR; without a machine file an unknown role is the WARNING "not checked:
    // no machine file" and the word runs against a resource created on the spot (rules 1 and 2, D103).
    private string? Resolve(string? role, ResourceDef? defaultResource, Func<ResourceDef, bool> fits, string kind,
        ResourceType createAs, Block block, ChannelState state, Diagnostics diagnostics)
    {
        ResourceDef? resource;
        if (role is null)
        {
            resource = defaultResource;
            if (resource is null)
            {
                diagnostics.Error(block, DiagnosticCodes.NoDefaultResource,
                    $"The machine has no default {kind} for a word without a role address (virtual machine 3.8 rule "
                    + "2).");
                return null;
            }
        }
        else
        {
            resource = _machine.ResolveRole(role) ?? FindCreated(role);
            if (resource is null)
            {
                if (!WithoutMachineFile)
                {
                    diagnostics.Error(block, DiagnosticCodes.UnknownRole,
                        $"{role} is no role of [roles] of the machine, or names no resource of it (virtual machine 3.8 "
                        + "rule 1).");
                    return null;
                }

                resource = CreateOnTheSpot(role, createAs);
                WarnNotChecked("role", role, "a " + KindName(createAs), block, diagnostics);
            }
        }

        if (!fits(resource))
        {
            diagnostics.Error(block, DiagnosticCodes.WrongResourceType,
                $"{role ?? resource.Id} is a {KindName(resource.Type)}, and the word needs a {kind} (virtual machine "
                + "3.8 rule 1).");
            return null;
        }

        EnsureInState(resource, state);
        return resource.Id;
    }

    private ResourceDef? FindCreated(string role)
    {
        return _createdResources.TryGetValue(role, out ResourceDef? created) ? created : null;
    }

    // A work spindle created on the spot gets a rotary axis of its own, so that C resolves to it while it is the
    // workpiece holder (D103); a holder created on the spot has no spindle. The resource is named by its role.
    private ResourceDef CreateOnTheSpot(string role, ResourceType type)
    {
        // TODO(question): D103 names neither the id of a resource created on the spot nor the name of the rotary axis
        // of a work spindle created so; the resource takes the role as its id and the axis is C_ and the role.
        ResourceDef created = type == ResourceType.WorkSpindle
            ? new ResourceDef { Id = role, Type = type, Axis = CreatedAxisPrefix + role }
            : new ResourceDef { Id = role, Type = type };
        _createdResources[role] = created;
        if (created.Axis is string axis)
        {
            _createdAxes[axis] = AxisKind.Rotary;
        }

        return created;
    }

    // Every run of a program starts from a new channel state (virtual machine 2), so a resource created on the spot
    // earlier in the run is added to the state of the program that addresses it.
    private static void EnsureInState(ResourceDef resource, ChannelState state)
    {
        if (resource.IsSpindle)
        {
            state.Spindles.TryAdd(resource.Id, new SpindleState());
        }
        else if (resource.Type == ResourceType.ToolHolder)
        {
            state.Holders.TryAdd(resource.Id, new HolderState());
        }

        if (resource.Axis is string axis && !state.Motion.Position.ContainsKey(axis))
        {
            EnsureAxisInState(axis, state);
        }
    }

    // An axis created on the spot starts unknown in every frame with a setpos shift of 0, as an axis without home
    // (virtual machine 2.1, 2.2, 3.4).
    private static void EnsureAxisInState(string axis, ChannelState state)
    {
        state.Motion.Position.TryAdd(axis, AxisPosition.Unknown);
        state.Frame.SetposShift.TryAdd(axis, 0m);
    }

    // "not checked: no machine file", once per name (D103).
    private void WarnNotChecked(string kind, string name, string createdAs, Block block, Diagnostics diagnostics)
    {
        if (!_warned.Add(kind + " " + name))
        {
            return;
        }

        diagnostics.Warning(block, DiagnosticCodes.NotCheckedNoMachineFile,
            $"The built-in default machine has no {kind} {name}: not checked: no machine file; the word runs against "
            + $"{createdAs} created on the spot (virtual machine 3.8, D103).");
    }

    private static string KindName(ResourceType type)
    {
        return type switch
        {
            ResourceType.WorkSpindle => "work spindle",
            ResourceType.ToolSpindle => "tool spindle",
            ResourceType.ToolHolder => "tool holder",
            ResourceType.Table => "table",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Not a resource type of machine-config 4."),
        };
    }
}
