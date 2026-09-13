using Ncx.Core.Catalog;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// The virtual machine: executes the blocks of an NCX program against the state of a channel, each block in the seven
/// steps of virtual machine 3, and reports what it finds as diagnostics, so that readers, compilers and analytics know
/// what the program means at every block (virtual machine 1, architecture 5). STATIC mode walks every program of the
/// file and follows the calls of its subprograms (D99); INTERPRETED mode comes with P4-01.
/// </summary>
public sealed partial class VirtualMachine
{
    // The coolant channel that a bare COOLANT addresses (virtual machine 2.5, F29).
    private const string DefaultCoolantChannel = "STANDARD";

    // The verbs of language 5 rule 1 as the state keeps them (virtual machine 2.2).
    private static readonly Dictionary<string, Verb> s_verbs = new(StringComparer.Ordinal)
    {
        ["RAPID"] = Verb.Rapid,
        ["LINE"] = Verb.Line,
        ["ARC"] = Verb.Arc,
        ["RETRACT"] = Verb.Retract,
        ["HOME"] = Verb.Home,
        ["CYCLE_CALL"] = Verb.CycleCall,
        ["SHIFT"] = Verb.Shift,
        ["TILT"] = Verb.Tilt,
        ["TILT_AXIS"] = Verb.TiltAxis,
        ["SETPOS"] = Verb.Setpos,
    };

    private readonly IReadOnlyDictionary<string, Value>? _startValues;

    // HOME on an axis without a reference point warns once per run and axis (D100).
    private readonly HashSet<string> _homeWarnings = new(StringComparer.Ordinal);

    // The axes a HOME without a reference point left unknown and no block has named since; SETPOS accepts them (D101).
    private readonly HashSet<string> _homedWithoutReference = new(StringComparer.Ordinal);

    // The diagnostics of the rules suppressed inside a subprogram that no program calls; nobody reads them (D99).
    private readonly Diagnostics _suppressed;

    private ResourceResolver _resources;
    private ChannelState _state;
    private bool _suppressCallerRules;

    /// <summary>
    /// A virtual machine for one channel.
    /// </summary>
    /// <param name="machine">The machine file, or the built-in default machine of D103 when no file is given.</param>
    /// <param name="options">The options of the run (architecture 5).</param>
    /// <param name="diagnostics">Where the diagnostics of the run go, for the file the program comes from (D98).</param>
    /// <param name="mode">STATIC, the default, or INTERPRETED (virtual machine 1).</param>
    /// <param name="startValues">The start values of &lt;file&gt;.vars.toml; null without one (virtual machine 2.7).</param>
    public VirtualMachine(MachineConfig machine, VmOptions options, Diagnostics diagnostics,
        ExecutionMode mode = ExecutionMode.Static, IReadOnlyDictionary<string, Value>? startValues = null)
    {
        Machine = machine;
        Options = options;
        Diagnostics = diagnostics;
        Mode = mode;
        _startValues = startValues;
        _suppressed = new Diagnostics(diagnostics.File);
        _resources = new ResourceResolver(machine);
        _state = NewState(channelId: 1);
    }

    /// <summary>
    /// STATIC or INTERPRETED (virtual machine 1).
    /// </summary>
    public ExecutionMode Mode { get; }

    /// <summary>
    /// The options of the run.
    /// </summary>
    public VmOptions Options { get; }

    /// <summary>
    /// The machine file, or the built-in default machine of D103.
    /// </summary>
    public MachineConfig Machine { get; }

    /// <summary>
    /// What the run found (virtual machine 2.9): an ERROR stops the run, a WARNING is reported and the run continues.
    /// </summary>
    public Diagnostics Diagnostics { get; }

    /// <summary>
    /// The state of the channel: during a run the state of the block that runs, after a run the state the last
    /// program left.
    /// </summary>
    internal ChannelState State => _state;

    /// <summary>
    /// The arc the last block resolved in step 5, in the coordinates of its working plane: start, end, center and
    /// sweep, which the MOTION event of step 7 carries (virtual machine 3.2, 7); null after any other block.
    /// </summary>
    internal PlaneArc? LastArc { get; private set; }

    /// <summary>
    /// The individual motions of the last block's CYCLE_CALL under the option ExpandCycles, raised as MOTION events in
    /// step 7 (virtual machine 3.3, D37); empty after any other block.
    /// </summary>
    internal IReadOnlyList<CycleMotion> LastCycleMotions { get; private set; } = [];

    /// <summary>
    /// An immutable copy of the state of the channel as it is now (virtual machine 7, architecture 5.3).
    /// </summary>
    public ChannelSnapshot Snapshot()
    {
        return _state.Snapshot();
    }

    /// <summary>
    /// Executes one block in the seven steps of virtual machine 3, in this order: the state words of a block are
    /// applied before its verb (architecture 5.1).
    /// </summary>
    /// <param name="block">A parsed block of the program, or a block the expander generated.</param>
    /// <returns>Where the flow goes after the block.</returns>
    internal BlockFlow Execute(Block block)
    {
        // 1. Parse the words.
        if (!ParseAndValidate(block))
        {
            return BlockFlow.Skipped;
        }

        // 2. Resolve role addresses and axis names against the machine configuration.
        BlockContext context = ResolveRolesAndAxes(block);

        // 3. Apply the state words.
        ApplyStateWords(context);

        // 4. If the verb is SHIFT, TILT, TILT_AXIS or SETPOS, update the frame.
        ApplyFrameVerb(context);

        // 5. If the verb is a motion verb, resolve the target and execute it.
        ExecuteMotion(context);

        // The flow words, then 6. reset the block-scoped items (architecture 5.1).
        BlockFlow flow = ApplyFlowWords(context);
        ResetBlockScope(context);

        // 7. Raise the block's events.
        RaiseEvents(context);
        return flow;
    }

    // Step 1: the parser read the words and reported unknown keys, wrong value types, duplicate keys, two verbs, axis
    // words without a verb and pseudo-words in a user file (D95), each an ERROR that stops the run before it starts;
    // the block reaches the virtual machine parsed. A SKIP block is executed unless the run option skips it, and a
    // skipped block changes nothing (architecture 5.1, D53). The verb and the skip of the block are block items of the
    // state (virtual machine 2.2).
    private bool ParseAndValidate(Block block)
    {
        if (Options.SkipBlocks.Skips(block))
        {
            return false;
        }

        _state.Motion.BlockVerb = block.Verb is Word verb && s_verbs.TryGetValue(verb.Key, out Verb blockVerb)
            ? blockVerb
            : null;
        _state.Motion.Skip = block.Skip;
        _state.Motion.SkipNumber = block.SkipNumber;
        return true;
    }

    // Step 2: role addresses resolve through [roles] to a resource, a word without one targets the default resource of
    // its kind, OFFSET the holder of the last TOOL, and axis names resolve through the [[axis]] list, A, B, C to the
    // rotary axis of the current workpiece holder (virtual machine 3.8 rules 1 to 3, D103).
    private BlockContext ResolveRolesAndAxes(Block block)
    {
        var context = new BlockContext
        {
            Block = block,
            State = _state,
            Machine = Machine,
            Mode = Mode,
            Resources = _resources,
            Diagnostics = Diagnostics,
            CallerRuleDiagnostics = _suppressCallerRules ? _suppressed : Diagnostics,
        };

        string? toolHolder = null;
        foreach (Word word in block.Words)
        {
            if (ResolveResource(word, context) is not string resource)
            {
                continue;
            }

            context.ResourceOf[word] = resource;
            if (word.Key == "TOOL")
            {
                toolHolder = resource;
            }
        }

        // OFFSET and OFFSET:* address the holder of the last TOOL, which is the TOOL of this block when it has one
        // (rule 2): TOOL:TURRET1=12 OFFSET:LEN=12 sets the offset of TURRET1.
        foreach (Word word in block.Words)
        {
            if (word.Key != "OFFSET")
            {
                continue;
            }

            if ((toolHolder ?? _state.LastHolder) is string holder)
            {
                context.ResourceOf[word] = holder;
                continue;
            }

            Diagnostics.Error(block, DiagnosticCodes.NoDefaultResource,
                $"{word.ToCanonical()} addresses the holder of the last TOOL, and the machine has no tool holder "
                + "(virtual machine 3.8 rule 2).");
        }

        ResolveAxisWords(context);
        return context;
    }

    // The resource of a word that addresses one (rules 1 and 2): a spindle, a holder, the workpiece holder, a named
    // function, a coolant channel; SPINDLE_SYNC resolves its two spindles into the context.
    private string? ResolveResource(Word word, BlockContext context)
    {
        Block block = context.Block;
        switch (word.Key)
        {
            case "SPINDLE" or "RPM" or "CSS" or "VC" or "RPM_MAX" or "ORIENT":
                return _resources.ResolveSpindle(word.Addr, block, _state, Diagnostics);
            case "SPINDLE_MODE":
                return _resources.ResolveWorkSpindle(word.Addr, block, _state, Diagnostics);
            case "TOOL" or "PRELOAD":
                return _resources.ResolveHolder(word.Addr, block, _state, Diagnostics);
            case "WORKPIECE":
                return BlockContext.IdentOf(word) is string role
                    ? _resources.ResolveWorkpieceHolder(role, block, _state, Diagnostics)
                    : null;
            case "FUNC":
                return word.Addr is string function && _resources.ResolveFunction(function, block, _state, Diagnostics)
                    ? function
                    : null;
            case "COOLANT":
                return ResolveCoolant(word, block);
            case "SPINDLE_SYNC":
                ResolveSyncSpindles(word, context);
                return null;
            default:
                return null;
        }
    }

    // COOLANT without an address addresses the default channel STANDARD, with one a named channel of the machine
    // configuration (language 4.6, virtual machine 2.5, F29).
    private string? ResolveCoolant(Word word, Block block)
    {
        string channel = word.Addr ?? DefaultCoolantChannel;
        return _resources.ResolveCoolantChannel(channel, block, _state, Diagnostics) ? channel : null;
    }

    // SPINDLE_SYNC=a,b: a list of two spindle roles, work spindles, of which the second follows the first (language
    // 4.5, virtual machine 3.8 rule 5); SPINDLE_SYNC=OFF has none.
    private void ResolveSyncSpindles(Word word, BlockContext context)
    {
        if (word.Value is not ListValue roles)
        {
            return;
        }

        if (roles.Items.Count != 2)
        {
            Diagnostics.Error(context.Block, DiagnosticCodes.SyncNeedsTwoSpindles,
                $"{word.ToCanonical()} names {roles.Items.Count} spindles; SPINDLE_SYNC takes two spindle roles, the "
                + "second following the first (language 4.5, virtual machine 3.8 rule 5).");
            return;
        }

        foreach (string role in roles.Items)
        {
            if (_resources.ResolveWorkSpindle(role, context.Block, _state, Diagnostics) is not string spindle)
            {
                context.SyncSpindles.Clear();
                return;
            }

            context.SyncSpindles.Add(spindle);
        }
    }

    // The axis words of a verb that carries axis words resolve against the machine (rule 3). TILT carries the spatial
    // angles A, B, C about the axes of the active frame, which are no machine axes (language 4.2).
    private void ResolveAxisWords(BlockContext context)
    {
        Block block = context.Block;
        if (block.Verb is not Word verb || verb.Definition is not { TakesAxisWords: true } || verb.Key == "TILT")
        {
            return;
        }

        bool nativeBlock = WordCatalog.IsNativeParameterAllowed(block);
        foreach (Word word in block.Words)
        {
            if (AxisNameOf(word, nativeBlock) is string name
                && _resources.ResolveAxis(name, block, _state, Diagnostics) is string axis)
            {
                context.AxisOf[word] = axis;
            }
        }
    }

    // Step 3: the state words, in any order; they do not depend on each other within a block (virtual machine 3). Where
    // a rule reads another word of its block the order is fixed: the change comes before the preload of the next tool
    // (language 4.4, the Nakamura G340 T0101. A02. is TOOL=1 OFFSET=1 PRELOAD=2), and the cycle is defined last, its
    // default axis being the tool axis of the workplane the block leaves (virtual machine 2.6). OFFSET found the holder
    // of the TOOL of its block in step 2, and the spindle rules are checked against the state all the words leave.
    private void ApplyStateWords(BlockContext context)
    {
        foreach (Word word in context.Block.Words)
        {
            if (word.Key == "TOOL")
            {
                Apply(word, context);
            }
        }

        foreach (Word word in context.Block.Words)
        {
            if (word.Key is not ("TOOL" or "CYCLE"))
            {
                Apply(word, context);
            }
        }

        foreach (Word word in context.Block.Words)
        {
            if (word.Key == "CYCLE")
            {
                Apply(word, context);
            }
        }

        _resources.CheckSpindleRules(context);
    }

    // Step 4: SHIFT, TILT, TILT_AXIS and SETPOS update the frame (virtual machine 3 step 4, 3.4). Their RESET forms are
    // state words of step 3.
    // TODO(question): language 5 rule 2 gives the frame verbs their own axis words without saying whether they take
    // the incremental forms (SHIFT IX=5, SETPOS IC=0); the parser lets them stand (BlockRules), and they change nothing
    // here until that is answered.
    private void ApplyFrameVerb(BlockContext context)
    {
        switch (context.Block.Verb?.Key)
        {
            case "SHIFT":
                ApplyShift(context);
                break;
            case "TILT":
                ApplyTilt(context, TransformKind.Tilt);
                break;
            case "TILT_AXIS":
                ApplyTilt(context, TransformKind.TiltAxis);
                break;
            case "SETPOS":
                ApplySetpos(context);
                break;
        }
    }

    // SHIFT X=60 Y=40 Z=-5: a datum shift in the frame active where the word stands, appended to the chain and folded
    // into the position; omitted axes are 0 (language 4.2, virtual machine 3.4, D31).
    // TODO(question): D60 halves "X" under DIAMETER=ON and keeps "other radial distances" radius values; whether the X
    // of a SHIFT, a distance of the frame, is a diameter is not said. It is kept as written, like CENTER:IX, until that
    // is answered.
    private void ApplyShift(BlockContext context)
    {
        var shift = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var unknownAxes = new List<string>();
        foreach (Word word in context.Block.Words)
        {
            if (!context.AxisOf.TryGetValue(word, out string? axis) || IsIncremental(word))
            {
                continue;
            }

            // TODO(question): a chain entry has no UNKNOWN form for a shift from an expression in STATIC mode (virtual
            // machine 1, wave-1 question #100); the entry keeps 0 for it and names the axis, which is unknown outside
            // the MACHINE frame (FrameRules.AppendShift).
            if (NumberOf(word) is decimal value)
            {
                shift[axis] = value;
            }
            else
            {
                shift[axis] = 0m;
                unknownAxes.Add(axis);
            }
        }

        FrameRules.AppendShift(_state, shift, unknownAxes);
    }

    // TILT A= B= C= by spatial angles and TILT_AXIS A= B= C= by the rotary axis positions of the machine, appended to
    // the chain with MOVE (default STAY) and ROT (default TABLE) of the block (language 4.2, D82). MOVE=TURN or
    // MOVE=MOVE additionally marks the rotary axes as moved to the plane: their positions are known only from TILT_AXIS
    // words, unknown after a spatial TILT without the kinematics module; MOVE=STAY leaves them (virtual machine 3.4).
    private void ApplyTilt(BlockContext context, TransformKind kind)
    {
        Block block = context.Block;
        var angles = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var axisAngles = new Dictionary<string, decimal?>(StringComparer.Ordinal);
        bool nativeBlock = WordCatalog.IsNativeParameterAllowed(block);
        foreach (Word word in block.Words)
        {
            if (AxisNameOf(word, nativeBlock) is null || IsIncremental(word))
            {
                continue;
            }

            // TODO(question): a chain entry has no UNKNOWN form for an angle from an expression in STATIC mode
            // (virtual machine 1); the entry keeps 0 for it, and a rotary axis moved to it is unknown.
            decimal? angle = NumberOf(word);
            angles[word.Key] = angle ?? 0m;
            if (kind == TransformKind.TiltAxis && context.AxisOf.TryGetValue(word, out string? axis))
            {
                axisAngles[axis] = angle;
            }
        }

        TiltMove move = IdentOf(block, "MOVE") switch
        {
            "TURN" => TiltMove.Turn,
            "MOVE" => TiltMove.Move,
            _ => TiltMove.Stay,
        };
        TiltRot rot = IdentOf(block, "ROT") == "COORD" ? TiltRot.Coord : TiltRot.Table;
        FrameRules.AppendTransform(_state,
            new TransformEntry { Kind = kind, Angles = angles, Move = move, Rot = rot });
        if (move == TiltMove.Stay)
        {
            return;
        }

        if (kind == TransformKind.Tilt)
        {
            foreach (string axis in new List<string>(_state.Motion.Position.Keys))
            {
                if (_resources.IsRotary(axis))
                {
                    _state.Motion.Position[axis] = AxisPosition.Unknown;
                }
            }

            return;
        }

        // TODO(question): virtual machine 3.4 says the rotary axis positions are known from the TILT_AXIS words
        // without naming the frame; they are the axis positions of this machine (language 4.2) and are kept in the
        // MACHINE frame until that is answered.
        foreach (KeyValuePair<string, decimal?> axisAngle in axisAngles)
        {
            _state.Motion.Position[axisAngle.Key] = axisAngle.Value is decimal known
                ? new AxisPosition(known, PositionFrame.Machine, Known: true)
                : AxisPosition.Unknown;
        }
    }

    // SETPOS C=0 declares the current position of the named axes; nothing moves (language 4.2, virtual machine 3.4,
    // D55, D101). Under DIAMETER=ON its X is a diameter and halved on the way in (D60).
    private void ApplySetpos(BlockContext context)
    {
        foreach (Word word in context.Block.Words)
        {
            if (!context.AxisOf.TryGetValue(word, out string? axis) || IsIncremental(word))
            {
                continue;
            }

            decimal? declared = NumberOf(word) is decimal value
                ? DiameterRules.ToRadius(word, value, _state.Frame.Diameter, cycleAxis: null)
                : null;
            FrameRules.Setpos(_state, axis, declared, _homedWithoutReference.Contains(axis), context.Block,
                Diagnostics);
            _homedWithoutReference.Remove(axis);
        }
    }

    // Step 5: a motion verb resolves its target and executes it (virtual machine 3 step 5, 3.1 to 3.3): RAPID and LINE
    // (MotionRules), ARC in its working plane (ArcRules, D102), RETRACT along the tool axis (RetractRules, D83), HOME
    // to the reference point of the configuration (HomeRules, D100), CYCLE_CALL (CycleRules, D37). Every one of them
    // needs UNITS and one form per axis; under POLAR=ON or CYLINDER=n it puts the axes of the transformation into its
    // frame (3.4, D102); the vector words and the rotary words of the block set or forget the tool vector (2.2, 3.1,
    // D81); and the machine position of an axis whose setpos shift was recorded against it follows the axes the motion
    // moved and the frame it ran in (3.4, D101).
    private void ExecuteMotion(BlockContext context)
    {
        LastArc = null;
        LastCycleMotions = [];
        if (!MotionRules.IsMotion(context.Block))
        {
            return;
        }

        // HOME and a FRAME=MACHINE block move in machine coordinates and leave the other axes where they are in the
        // machine frame (virtual machine 3 step 5, 3.4, D35); every other motion moves in the workpiece frame, and the
        // records of the setpos shifts follow the axes it moved, read against the position store before the block.
        Dictionary<string, AxisPosition>? before = context.Block.Verb?.Key != "HOME"
            && !_state.Frame.MachineFrameBlock
            && _state.Frame.SetposAgainstMachine.Count > 0
                ? new Dictionary<string, AxisPosition>(_state.Motion.Position)
                : null;

        MotionRules.CheckUnits(context);
        MotionRules.CheckOneFormPerAxis(context);
        MotionRules.EnterTransformation(context);
        decimal arcTolerance = ArcRules.Tolerance(Options, _state.Frame.Units);
        switch (context.Block.Verb?.Key)
        {
            case "RAPID" or "LINE":
                MotionRules.MoveStraight(context);
                break;
            case "ARC":
                LastArc = ArcRules.Execute(context, arcTolerance);
                break;
            case "RETRACT":
                RetractRules.Retract(context);
                break;
            case "HOME":
                HomeRules.Home(context, _homeWarnings, _homedWithoutReference);
                break;
            case "CYCLE_CALL":
                LastCycleMotions = CycleRules.Call(context, Options.ExpandCycles);
                break;
        }

        ToolVectorRules.Apply(context, arcTolerance);
        if (before is not null)
        {
            FrameRules.FollowMotion(context, before);
        }
    }

    // The flow words (architecture 5.1): PROGRAM=BEGIN makes the program active with its NAME and NUMBER, PROGRAM=END
    // ends it (virtual machine 2.1, 3.6, 4). STATIC mode records JUMP and REPEAT without following them and follows
    // CALL into the subprogram once the block is done; the walk returns at SUB=END. RETURN, SYNC, WAIT_CHANNEL and
    // START_CHANNEL are recorded as well: their events are P1-05's, the channels the job scheduler's (virtual machine
    // 1, 3.7, D99).
    // TODO: INTERPRETED mode follows JUMP, REPEAT and RETURN under IF (virtual machine 3.6, P4-01).
    private BlockFlow ApplyFlowWords(BlockContext context)
    {
        Block block = context.Block;
        if (block.Has("PROGRAM", null, "BEGIN"))
        {
            _state.Program.Active = true;
            _state.Program.Name = block.Find("NAME")?.Value is StringValue name ? name.Content : "";
            _state.Program.Number = block.Find("NUMBER") is Word number ? BlockContext.IntegerOf(number) ?? 0 : 0;
        }

        if (block.Has("PROGRAM", null, "END"))
        {
            ProgramEndRules.EndProgram(_state);
        }

        return block.Has("CALL") ? BlockFlow.Call : BlockFlow.Next;
    }

    // Step 6: FRAME, IF, ARG, TIMES and WITH end with their block (virtual machine 3 step 6), and so do the verb, SKIP,
    // PHASE and POINT (virtual machine 4). IF, ARG, TIMES, WITH, PHASE and POINT have no state row of their own: they
    // act inside their block only. A HOME that found no reference point is "directly before" a SETPOS only until
    // another block names the axis (D101).
    // TODO(question): D101 accepts SETPOS "directly after a HOME of that axis" without saying whether a block that
    // does not name the axis may stand between them (POLAR_FACE has none); it may, until that is answered.
    private void ResetBlockScope(BlockContext context)
    {
        _state.Frame.MachineFrameBlock = false;
        _state.Motion.BlockVerb = null;
        _state.Motion.Skip = false;
        _state.Motion.SkipNumber = null;
        if (context.Block.Verb?.Key == "HOME")
        {
            return;
        }

        foreach (string axis in context.AxisOf.Values)
        {
            _homedWithoutReference.Remove(axis);
        }
    }

    // Step 7: the block's events with the Before and After snapshots of the channel (virtual machine 7).
    // TODO: P1-05 raises the events here from the block of the context, TOOL_BEGIN and TOOL_END from the holders of
    // Before and After among them (virtual machine 3.5, 7).
    private static void RaiseEvents(BlockContext context)
    {
    }

    // The state of a channel at the start of a run, from the machine, with the pre-pass over the file; reading an
    // unassigned variable follows the options of the run (virtual machine 2, 3.6, D38).
    private ChannelState NewState(int channelId)
    {
        MachineConfig machine = Machine with { Variables = Machine.Variables with { Unassigned = Options.Unassigned } };
        var state = new ChannelState(machine, channelId, _startValues);
        FillPrePass(state);
        return state;
    }

    private static void Apply(Word word, BlockContext context)
    {
        WordHandlers.Find(word.Key)?.Invoke(word, context);
    }

    // The axis a word names: X Y Z A B C and IX to IC by the catalog, the incremental form without its I; a machine
    // axis word of the D93 form outside a CYCLE:controller=n block, where every unknown key is a native parameter
    // (language 4.3, D93, D94); null for any other word.
    private static string? AxisNameOf(Word word, bool nativeBlock)
    {
        if (word.Definition is not null)
        {
            if (word.Addr is not null || !WordCatalog.IsStandardAxis(word.Key))
            {
                return null;
            }

            return word.Key.StartsWith('I') ? word.Key.Substring(1) : word.Key;
        }

        return !nativeBlock && WordCatalog.TryMachineAxis(word.Key, out MachineAxisWord? machineAxis)
            ? machineAxis.AxisName
            : null;
    }

    // The incremental form of an axis word, IX or IZ2 (language 4.3).
    private static bool IsIncremental(Word word)
    {
        if (word.Definition is not null)
        {
            return word.Key.StartsWith('I');
        }

        return WordCatalog.TryMachineAxis(word.Key, out MachineAxisWord? machineAxis) && machineAxis.IsIncremental;
    }

    // A number of an axis word; null for an expression, which STATIC mode does not evaluate (virtual machine 1).
    private static decimal? NumberOf(Word word)
    {
        return word.Value switch
        {
            IntegerValue integer => integer.Number,
            DecimalValue value => value.Number,
            _ => null,
        };
    }

    // The identifier of a word of the block, TURN of MOVE=TURN; null without the word.
    private static string? IdentOf(Block block, string key)
    {
        return block.Find(key) is Word word ? BlockContext.IdentOf(word) : null;
    }
}
