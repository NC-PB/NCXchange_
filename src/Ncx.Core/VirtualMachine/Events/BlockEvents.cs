using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Handlers;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// The events of one block (virtual machine 7), raised in step 7 from the block, its Before and its After, in the order
/// the block acts: what it opens, its state words, its motion, its flow words, the state it changed, what it closes
/// (virtual machine 3, architecture 5.1).
/// </summary>
internal sealed class BlockEvents
{
    // Tool 0 is the empty spindle: no tool enters or leaves with it (language 4.4).
    private static readonly ToolRef s_emptySpindle = new(0);

    private readonly BlockContext _context;
    private readonly Block _block;
    private readonly ChannelSnapshot _before;
    private readonly ChannelSnapshot _after;
    private readonly NcxProgram? _program;
    private readonly List<VmEvent> _events = [];

    /// <summary>
    /// The events of a block.
    /// </summary>
    /// <param name="context">The block after step 6, with the resources step 2 resolved.</param>
    /// <param name="before">The state before the block: the After of the events before it.</param>
    /// <param name="after">The state after the block.</param>
    /// <param name="program">The program of the run, for the caller of a subprogram; null for blocks executed one by
    /// one.</param>
    public BlockEvents(BlockContext context, ChannelSnapshot before, ChannelSnapshot after, NcxProgram? program)
    {
        _context = context;
        _block = context.Block;
        _before = before;
        _after = after;
        _program = program;
    }

    /// <summary>
    /// Raises the events of the block in their order and counts the block under its tool and its program.
    /// </summary>
    /// <param name="arc">The arc step 5 resolved; null otherwise.</param>
    /// <param name="cycleMotions">The motions of an expanded CYCLE_CALL; empty otherwise.</param>
    /// <param name="underTool">The distance and the blocks under the tool of each holder, by holder.</param>
    /// <param name="underProgram">The distance and the blocks of the program that runs.</param>
    public List<VmEvent> Raise(PlaneArc? arc, IReadOnlyList<CycleMotion> cycleMotions,
        Dictionary<string, RunStatistics> underTool, RunStatistics underProgram)
    {
        // 1. What the block opens: PROGRAM_BEGIN at PROGRAM=BEGIN, SUB_BEGIN at SUB=BEGIN, SECTION (virtual machine 7).
        if (_block.Has("PROGRAM", null, "BEGIN"))
        {
            underProgram.Reset();
            _events.Add(ProgramEvent(EventPhase.Begin, underProgram));
        }

        if (_block.Has("SUB", null, "BEGIN"))
        {
            _events.Add(SubEvent(EventPhase.Begin));
        }

        if (_block.Find("SECTION")?.Value is StringValue section)
        {
            _events.Add(new SectionEvent
            {
                Channel = _after.ChannelId,
                Block = _block,
                Before = _before,
                After = _after,
                Text = section.Content,
            });
        }

        // 2. The state words, applied before the verb (virtual machine 3 step 3): TOOL_END and TOOL_BEGIN, PRELOAD,
        // FUNCTION, VAR_CHANGE.
        AddToolChanges(underTool);
        AddPreloads();
        AddFunctions();
        AddVariables();

        // 3. The verb (step 5): CYCLE_CALL, MOTION; then DWELL and STOP, which act when the motion of the block is done.
        List<MotionEvent> motions = MotionEvents.Of(_context, _before, _after, arc, cycleMotions);
        AddCycleCall();
        _events.AddRange(motions);
        AddDwellAndStop();

        // 4. The flow words (architecture 5.1): JUMP, CALL, RETURN, REPEAT.
        AddFlow();

        // 5. A STATE_CHANGE for every changed variable, with Before and After (architecture 5.1).
        _events.AddRange(StateChanges.Between(_block, _before, _after));

        // The block counts under the tool of the holder called last and under the program, with the lengths of its
        // motions (P1-05: the distance under a tool is summed from MOTION lengths per holder).
        // TODO(question): virtual machine 7 gives the distance and block count under the tool without saying, on a
        // machine with several holders, under whose tool a block and its motions are; they count under the tool of the
        // holder of the last TOOL (lastHolder, 2.3), which OFFSET addresses as well (3.8 rule 2), until that is
        // answered.
        if (_after.LastHolder is string holder
            && _after.Holders.TryGetValue(holder, out HolderSnapshot? lastHolder)
            && lastHolder.SpindleTool != s_emptySpindle
            && underTool.TryGetValue(holder, out RunStatistics? underTheTool))
        {
            underTheTool.Count(motions);
        }

        underProgram.Count(motions);

        // 6. What the block closes: SUB_END at SUB=END, where the walk leaves the subprogram, PROGRAM_END at
        // PROGRAM=END with the run statistics (virtual machine 7). STATIC mode records RETURN, JUMP=END and REPEAT
        // without following them (virtual machine 1, D99), so it leaves a subprogram at its SUB=END and a program at its
        // PROGRAM=END.
        if (_block.Has("SUB", null, "END"))
        {
            _events.Add(SubEvent(EventPhase.End));
        }

        if (_block.Has("PROGRAM", null, "END"))
        {
            _events.Add(ProgramEvent(EventPhase.End, underProgram));
        }

        return _events;
    }

    // A tool that leaves the spindle raises TOOL_END with the distance and the blocks under it, then the tool that
    // enters raises TOOL_BEGIN (virtual machine 3.5, 7); only a change to another tool is one (architecture 5.2), and
    // tool 0 is the empty spindle, no tool.
    // TODO(question): virtual machine 7 raises TOOL_END when a tool leaves the spindle, and PROGRAM=END keeps the tool
    // in the spindle (virtual machine 4), so the last tool of a program gets no TOOL_END and its distance and block
    // count reach no listener, while the tool list needs them (virtual machine 8); no TOOL_END is raised at
    // PROGRAM_END until that is answered.
    private void AddToolChanges(Dictionary<string, RunStatistics> underTool)
    {
        foreach (KeyValuePair<string, HolderSnapshot> holder in _after.Holders)
        {
            ToolRef oldTool = _before.Holders.TryGetValue(holder.Key, out HolderSnapshot? old)
                ? old.SpindleTool
                : s_emptySpindle;
            ToolRef newTool = holder.Value.SpindleTool;
            if (oldTool == newTool)
            {
                continue;
            }

            if (oldTool != s_emptySpindle)
            {
                RunStatistics? used = underTool.TryGetValue(holder.Key, out RunStatistics? counted) ? counted : null;
                underTool.Remove(holder.Key);
                _events.Add(ToolEvent(EventPhase.End, oldTool, holder.Key, RpmOf(_before, holder.Key), used));
            }

            if (newTool != s_emptySpindle)
            {
                underTool[holder.Key] = new RunStatistics();
                _events.Add(ToolEvent(EventPhase.Begin, newTool, holder.Key, RpmOf(_after, holder.Key), null));
            }
        }
    }

    // PRELOAD at every PRELOAD with its tool and holder (virtual machine 7); PRELOAD=0 clears (3.5).
    private void AddPreloads()
    {
        foreach (Word word in _block.Words)
        {
            if (word.Key != "PRELOAD"
                || !_context.ResourceOf.TryGetValue(word, out string? holder)
                || ToolOf(word) is not ToolRef tool)
            {
                continue;
            }

            _events.Add(new PreloadEvent
            {
                Channel = _after.ChannelId,
                Block = _block,
                Before = _before,
                After = _after,
                Tool = tool,
                Holder = holder,
            });
        }
    }

    // FUNCTION at every FUNC, MFUNC and COOLANT, with the function or coolant channel step 2 resolved (virtual
    // machine 2.5, 7 as amended by F18).
    private void AddFunctions()
    {
        foreach (Word word in _block.Words)
        {
            if (word.Key is not ("FUNC" or "MFUNC" or "COOLANT"))
            {
                continue;
            }

            _events.Add(new FunctionEvent
            {
                Channel = _after.ChannelId,
                Block = _block,
                Before = _before,
                After = _after,
                Word = word,
                Name = _context.ResourceOf.TryGetValue(word, out string? name) ? name : null,
            });
        }
    }

    // VAR_CHANGE at every VAR, and at every ARG of a CALL of a subprogram of the file, which gives the callee's local
    // its value as the walk enters the subprogram (virtual machine 3.6, 7). A SYS_ name is never assigned (2.7).
    private void AddVariables()
    {
        foreach (Word word in _block.Words)
        {
            if (word.Key == "VAR"
                && word.Addr is string name
                && !VariableStore.IsSystem(name)
                && _after.Vars.TryGetValue(name, out VariableValue? value))
            {
                _events.Add(VarChangeEvent(name, _before.Vars.TryGetValue(name, out VariableValue? old) ? old : null,
                    value));
            }
        }

        if (_block.Find("CALL") is not Word call || !_after.Flow.Subs.ContainsKey(CallTarget(call.Value)))
        {
            return;
        }

        // The callee's locals V1 to V33 start unassigned (wave-1 question #75); another name keeps its value until the
        // ARG assigns it (wave-1 question #109).
        foreach (Word word in _block.Words)
        {
            if (word.Key != "ARG" || word.Addr is not string name || VariableStore.IsSystem(name))
            {
                continue;
            }

            VariableValue? old = !VariableStore.IsLocal(name) && _before.Vars.TryGetValue(name, out VariableValue? known)
                ? known
                : null;
            _events.Add(VarChangeEvent(name, old, VariableHandlers.ValueOf(word.Value)));
        }
    }

    // CYCLE_CALL at every call, with the cycle name and parameters and the call point (virtual machine 3.3, 7); under
    // ExpandCycles its MOTION events follow it.
    // TODO(question): virtual machine 7 raises CYCLE_CALL at "every call", while 3.3 raises the call "as the individual
    // MOTION events" under ExpandCycles and architecture 5.1 raises "CYCLE_CALL or expanded MOTION events"; CYCLE_CALL
    // is raised at every call and the MOTION events follow it under ExpandCycles, until that is answered.
    private void AddCycleCall()
    {
        if (_block.Verb?.Key != "CYCLE_CALL" || _after.Cycle.Name is not string name)
        {
            return;
        }

        _events.Add(new CycleCallEvent
        {
            Channel = _after.ChannelId,
            Block = _block,
            Before = _before,
            After = _after,
            Cycle = name,
            Controller = _after.Cycle.Controller,
            Parameters = _after.Cycle.Parameters,
            At = _after.Motion.Position,
        });
    }

    // DWELL with its seconds and STOP with its kind (language 4.1; virtual machine 7 as amended by F18).
    private void AddDwellAndStop()
    {
        if (_block.Find("DWELL") is Word dwell)
        {
            _events.Add(new DwellEvent
            {
                Channel = _after.ChannelId,
                Block = _block,
                Before = _before,
                After = _after,
                Seconds = MotionRules.NumberOf(dwell),
            });
        }

        if (_block.Find("STOP") is Word stop)
        {
            _events.Add(new StopEvent
            {
                Channel = _after.ChannelId,
                Block = _block,
                Before = _before,
                After = _after,
                Optional = BlockContext.IdentOf(stop) == "OPTIONAL",
            });
        }
    }

    // JUMP, CALL, RETURN and REPEAT with the target, the condition of IF in the block and the call depth (virtual
    // machine 7; language 4.9).
    private void AddFlow()
    {
        if (_block.Find("JUMP") is Word jump)
        {
            _events.Add(FlowEvent(FlowKind.Jump, jump.Value.ToCanonical()));
        }

        if (_block.Find("CALL") is Word call)
        {
            _events.Add(FlowEvent(FlowKind.Call, CallTarget(call.Value)));
        }

        if (_block.Has("RETURN"))
        {
            _events.Add(FlowEvent(FlowKind.Return, null));
        }

        if (_block.Find("REPEAT") is Word repeat)
        {
            _events.Add(FlowEvent(FlowKind.Repeat, repeat.Value.ToCanonical()));
        }
    }

    private FlowEvent FlowEvent(FlowKind flow, string? target)
    {
        return new FlowEvent
        {
            Channel = _after.ChannelId,
            Block = _block,
            Before = _before,
            After = _after,
            Flow = flow,
            Target = target,
            Condition = _block.Find("IF")?.Value,
            Depth = _after.Flow.Calls.Count,
        };
    }

    // The name and the number of the program (virtual machine 2.1), with the run statistics at PROGRAM_END.
    // TODO(question): virtual machine 7 gives PROGRAM_END "run statistics" and 3.6 raises them without saying what they
    // are; they are the blocks executed from PROGRAM=BEGIN to PROGRAM=END and the distance summed from the MOTION
    // lengths, the figures TOOL_END carries for a tool, until that is answered.
    private ProgramEvent ProgramEvent(EventPhase phase, RunStatistics underProgram)
    {
        bool end = phase == EventPhase.End;
        return new ProgramEvent
        {
            Channel = _after.ChannelId,
            Block = _block,
            Before = _before,
            After = _after,
            Phase = phase,
            Name = _after.Program.Name,
            Number = _after.Program.Number,
            Blocks = end ? underProgram.Blocks : 0,
            Distance = end ? underProgram.Distance : 0m,
        };
    }

    // The name of the subprogram and its caller: the section that holds the CALL the walk entered it by, the
    // innermost call (virtual machine 2.7, 3.9, D99).
    private SubEvent SubEvent(EventPhase phase)
    {
        Section? sub = _after.Program.Section;
        string name = sub is { Kind: SectionKind.Sub, Name: string subName }
            ? subName
            : _block.Find("NAME") is Word nameWord ? CallTarget(nameWord.Value) : "";
        Section? caller = null;
        if (_after.Flow.Calls.Count > 0 && _program is not null)
        {
            caller = SectionHolding(_program, _after.Flow.Calls[0].ReturnPc - 1);
        }

        return new SubEvent
        {
            Channel = _after.ChannelId,
            Block = _block,
            Before = _before,
            After = _after,
            Phase = phase,
            Name = name,
            Caller = caller,
        };
    }

    private ToolEvent ToolEvent(EventPhase phase, ToolRef tool, string holder, decimal? rpm, RunStatistics? used)
    {
        return new ToolEvent
        {
            Channel = _after.ChannelId,
            Block = _block,
            Before = _before,
            After = _after,
            Phase = phase,
            Tool = tool,
            Holder = holder,
            Rpm = rpm,
            Distance = used?.Distance ?? 0m,
            Blocks = used?.Blocks ?? 0,
        };
    }

    private VarChangeEvent VarChangeEvent(string name, VariableValue? oldValue, VariableValue newValue)
    {
        return new VarChangeEvent
        {
            Channel = _after.ChannelId,
            Block = _block,
            Before = _before,
            After = _after,
            Name = name,
            OldValue = oldValue,
            NewValue = newValue,
        };
    }

    // The rpm of the spindle of a holder ([[resource]] spindle), the default spindle for a holder without one, as
    // virtual machine 5 reads the spindle of the tool holder (F30); null when it is unknown (virtual machine 1).
    private decimal? RpmOf(ChannelSnapshot snapshot, string holder)
    {
        string? spindle = _context.Machine.FindResource(holder)?.Spindle
            ?? _context.Machine.ResolveDefaultSpindle()?.Id;
        if (spindle is null
            || !snapshot.Spindles.TryGetValue(spindle, out SpindleSnapshot? state)
            || snapshot.Unknown.Contains("RPM:" + spindle))
        {
            return null;
        }

        return state.Rpm;
    }

    // The tool of PRELOAD, an integer or a string (language 4.4); null for an expression, which STATIC mode does not
    // evaluate (virtual machine 1).
    private static ToolRef? ToolOf(Word word)
    {
        return word.Value switch
        {
            IntegerValue => new ToolRef(BlockContext.IntegerOf(word) ?? 0),
            StringValue name => new ToolRef(name.Content),
            _ => null,
        };
    }

    // The name a CALL or a NAME gives: the content of a string, an identifier, or the digits of an integer (language
    // 4.9).
    private static string CallTarget(Value value)
    {
        return value switch
        {
            StringValue text => text.Content,
            IdentValue ident => ident.Name,
            _ => value.ToCanonical(),
        };
    }

    private static Section? SectionHolding(NcxProgram program, int blockIndex)
    {
        foreach (Section section in program.Sections)
        {
            if (section.FirstBlock <= blockIndex && blockIndex <= section.LastBlock)
            {
                return section;
            }
        }

        return null;
    }
}
