using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Handlers;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

// STATIC mode (virtual machine 1, 3.9, D99): one pass over every program of the file; JUMP and REPEAT recorded, not
// followed; every CALL of a subprogram of the file followed with the caller's state, TIMES=n in n passes, within the
// call depth; a CALL of an external program recorded, not followed; a subprogram nothing calls walked once from the
// default entry state. What convert, compile and check use (D91).
public sealed partial class VirtualMachine
{
    // The subprograms a program of the file entered by CALL during the run (D99).
    private readonly HashSet<Section> _calledSubs = [];

    private NcxProgram? _program;

    // The state at the first verb of the file's first program, the entry state of a subprogram nothing calls (D99).
    private ChannelSnapshot? _firstVerbState;

    /// <summary>
    /// Runs a parsed program: in STATIC mode every program of the file once, the calls of its subprograms followed, and
    /// every subprogram no program calls once from the default entry state (virtual machine 1, 3.9, D99). Afterwards
    /// the diagnostics hold what the run found.
    /// </summary>
    /// <param name="program">The parsed program, with the diagnostics the parser reported.</param>
    /// <returns>Whether an ERROR stopped the run.</returns>
    public RunResult Run(NcxProgram program)
    {
        // INTERPRETED mode executes the program with its flow and evaluates its expressions (virtual machine 1, 3.6).
        // TODO: INTERPRETED mode is built in P4-01; STATIC is the mode of convert, compile and check.
        if (Mode != ExecutionMode.Static)
        {
            throw new NotSupportedException("INTERPRETED mode is built in P4-01; run the virtual machine STATIC.");
        }

        // An ERROR stops the run: one the parser or the pre-pass reported stops it before the first block (virtual
        // machine 2.9, 3.6).
        if (program.Diagnostics.HasErrors || Diagnostics.HasErrors)
        {
            return new RunResult { Stopped = true };
        }

        _program = program;
        _resources = new ResourceResolver(Machine);
        _homeWarnings.Clear();
        _calledSubs.Clear();
        _firstVerbState = null;

        // One pass top to bottom over every program of the file (virtual machine 1).
        // TODO(question): virtual machine 1 walks every program of the file and does not say which state a program
        // after the first starts from; each starts from the state of a channel at the start of a run, as a program
        // that the job enters on its channel does (language 4.13), until that is answered.
        ChannelState? lastProgram = null;
        bool firstProgram = true;
        foreach (Section section in program.Programs)
        {
            StartWalk(NewState(section.Channel), suppressCallerRules: false);
            _state.Program.Section = section;
            if (!WalkSection(section, recordFirstVerb: firstProgram))
            {
                return new RunResult { Stopped = true };
            }

            lastProgram = _state;
            firstProgram = false;
        }

        // A subprogram that no program of the file calls is walked once from the default entry state, and inside it
        // the validations are suppressed whose state belongs to a caller that does not exist (virtual machine 1, 3.9,
        // 5, D99).
        foreach (Section sub in program.Subs)
        {
            if (_calledSubs.Contains(sub))
            {
                continue;
            }

            StartWalk(EntryStateOfUncalledSub(sub.Channel), suppressCallerRules: true);
            _state.Program.Section = sub;
            if (!WalkSection(sub, recordFirstVerb: false))
            {
                return new RunResult { Stopped = true };
            }
        }

        // After the run the state is the one the last program left.
        StartWalk(lastProgram ?? _state, suppressCallerRules: false);
        return new RunResult { Stopped = false };
    }

    /// <summary>
    /// The default entry state of a subprogram that no program of the file calls: units, workplane, diameter and feed
    /// mode as at the first verb of the file's first program, everything else initial, the position unknown (virtual
    /// machine 1, D99).
    /// </summary>
    /// <param name="channelId">The channel of the subprogram, 1.</param>
    internal ChannelState EntryStateOfUncalledSub(int channelId)
    {
        ChannelState state = NewState(channelId);

        // TODO(question): D99 takes units, workplane, diameter and feed mode "as at the first verb of the file's first
        // program" and does not say what they are when that program has no verb; they stay initial until that is
        // answered.
        if (_firstVerbState is ChannelSnapshot first)
        {
            state.Frame.Units = first.Frame.Units;
            state.Frame.Workplane = first.Frame.Workplane;
            state.Frame.Diameter = first.Frame.Diameter;
            state.Motion.FeedMode = first.Motion.FeedMode;
            state.Cycle.Axis = CycleState.ToolAxisOf(state.Frame.Workplane);
        }

        // The position is unknown, also on an axis whose home would make it known at the start of a program (D99).
        foreach (string axis in new List<string>(state.Motion.Position.Keys))
        {
            state.Motion.Position[axis] = AxisPosition.Unknown;
        }

        return state;
    }

    // Walks the blocks of a program or a subprogram from its BEGIN block to its END block; false when an ERROR stopped
    // the run.
    private bool WalkSection(Section section, bool recordFirstVerb)
    {
        NcxProgram program = _program ?? throw new InvalidOperationException("A walk needs the program of a run.");
        for (int index = section.FirstBlock; index <= section.LastBlock; index++)
        {
            Block block = program.Blocks[index];
            _state.Flow.Pc = index;
            BlockFlow flow = Execute(block);

            // Units, workplane, diameter and feed mode as at the first verb of the file's first program are the entry
            // state of a subprogram that no program calls (D99).
            if (recordFirstVerb && _firstVerbState is null && flow.Executed && block.Verb is not null)
            {
                _firstVerbState = _state.Snapshot();
            }

            // An ERROR stops the run (virtual machine 2.9).
            if (Diagnostics.HasErrors)
            {
                return false;
            }

            // A CALL is followed once its block is done (virtual machine 1, D99; architecture 5.1).
            if (flow.FollowsCall && !FollowCall(block))
            {
                return false;
            }
        }

        return true;
    }

    // CALL enters the SUB section of the file by its NAME (language 4.9, 4.13); false when an ERROR stopped the run.
    private bool FollowCall(Block block)
    {
        NcxProgram program = _program ?? throw new InvalidOperationException("A call needs the program of a run.");
        if (block.Find("CALL") is not Word call)
        {
            return true;
        }

        string name = NameOf(call.Value);
        if (FindSection(program.Subs, name) is not Section sub)
        {
            // A CALL that names a program instead of a subprogram: ERROR; programs are entered from the job only
            // (language 4.13, virtual machine 3.6, 5).
            if (FindSection(program.Programs, name) is not null)
            {
                Diagnostics.Error(block, DiagnosticCodes.CallOfProgram,
                    $"{call.ToCanonical()} names a program; a CALL enters a subprogram, and programs are entered from "
                    + "the job only (language 4.13, virtual machine 3.6).");
                return false;
            }

            // A CALL of an external program by file name is not followed in STATIC mode: the call is recorded, the
            // state after it is the state before it, and the position becomes unknown (virtual machine 1, 3.9, D99).
            if (call.Value is StringValue)
            {
                ForgetPosition();
                return true;
            }

            Diagnostics.Error(block, DiagnosticCodes.CallTargetMissing,
                $"{call.ToCanonical()}: the file has no subprogram {name} (virtual machine 3.6, missing call target).");
            return false;
        }

        // TIMES=n walks the subprogram n times in sequence, each pass from the state the previous one left, so the
        // caller continues with the state after the last pass (virtual machine 1, 3.9, D99).
        int passes = PassesOf(block);
        for (int pass = 0; pass < passes; pass++)
        {
            if (!EnterSub(sub, name, block))
            {
                return false;
            }
        }

        return true;
    }

    // One pass through a subprogram with the caller's state at that point (D99).
    private bool EnterSub(Section sub, string name, Block callBlock)
    {
        // The STATIC walk keeps a call stack and applies the configured call depth: a CALL beyond it is the ERROR
        // "call depth exceeded", and the subprogram is not entered again (virtual machine 3.9, D99).
        FlowState flow = _state.Flow;
        if (flow.Calls.Count >= Options.CallDepth)
        {
            Diagnostics.Error(callBlock, DiagnosticCodes.CallDepthExceeded,
                $"CALL={name} is deeper than the call depth of {Options.CallDepth}: call depth exceeded; the "
                + "subprogram is not entered (virtual machine 3.9, D99).");
            return false;
        }

        if (!_suppressCallerRules)
        {
            _calledSubs.Add(sub);
        }

        // CALL pushes the return pc and the locals V1 to V33 and assigns the ARG words to the callee's locals; SUB=END
        // pops them (virtual machine 3.6, 4).
        int callPc = flow.Pc;
        Section? caller = _state.Program.Section;
        flow.Calls.Push(new CallFrame(callPc + 1, name));
        _state.Vars.PushLocals();
        if (!AssignArguments(callBlock))
        {
            return false;
        }

        _state.Program.Section = sub;
        if (!WalkSection(sub, recordFirstVerb: false))
        {
            return false;
        }

        _state.Program.Section = caller;
        _state.Vars.PopLocals();
        flow.Calls.Pop();
        flow.Pc = callPc;
        return true;
    }

    // The ARG words of the CALL block go to the callee's locals; an ARG from an expression is UNKNOWN in STATIC mode
    // (language 4.9, virtual machine 1, 3.6). False when an ERROR stopped the run.
    // TODO(question): language 4.9 names an argument by its address (ARG:A=1) and "the callee sees it as a local
    // variable", while the locals a CALL pushes are V1 to V33 (virtual machine 3.6); an ARG is assigned under its own
    // name, which is a local of the callee for V1 to V33 only, until that is answered.
    private bool AssignArguments(Block callBlock)
    {
        foreach (Word word in callBlock.Words)
        {
            if (word.Key != "ARG" || word.Addr is not string name)
            {
                continue;
            }

            if (VariableStore.IsSystem(name))
            {
                Diagnostics.Error(callBlock, DiagnosticCodes.SystemVariableAssigned,
                    $"ARG:{name} assigns a system variable, which the program never assigns (virtual machine 2.7).");
                return false;
            }

            _state.Vars.Set(name, VariableHandlers.ValueOf(word.Value));
        }

        return true;
    }

    // After a CALL of an external program the position is unknown in every frame (virtual machine 1, 3.9, D99), the
    // MACHINE frame included; with it goes the machine position a SETPOS recorded its shift against (D101).
    private void ForgetPosition()
    {
        foreach (string axis in new List<string>(_state.Motion.Position.Keys))
        {
            _state.Motion.Position[axis] = AxisPosition.Unknown;
        }

        _state.Frame.SetposAgainstMachine.Clear();
    }

    // A walk runs on its own channel state; the rules a subprogram nothing calls suppresses stay suppressed inside it
    // (D99), and "directly after a HOME" never reaches across two walks (D101).
    private void StartWalk(ChannelState state, bool suppressCallerRules)
    {
        _state = state;
        _suppressCallerRules = suppressCallerRules;
        _homedWithoutReference.Clear();
    }

    // file.programs, file.subs and the labels of every section come from the pre-pass over the file (virtual machine
    // 2.1, 2.7, 3.6); a duplicate keeps its first occurrence, the ERROR is the pre-pass's.
    private void FillPrePass(ChannelState state)
    {
        if (_program is null)
        {
            return;
        }

        foreach (Section section in _program.Sections)
        {
            if (section.Kind == SectionKind.Program)
            {
                state.Flow.Programs.Add(section);
            }
            else if (section.Name is string name)
            {
                state.Flow.Subs.TryAdd(name, section);
            }

            var labels = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = section.FirstBlock; index <= section.LastBlock; index++)
            {
                if (_program.Blocks[index].Find("LABEL") is Word label)
                {
                    labels.TryAdd(label.Value.ToCanonical(), index);
                }
            }

            state.Flow.Labels[section] = labels;
        }
    }

    private static Section? FindSection(IReadOnlyList<Section> sections, string name)
    {
        foreach (Section section in sections)
        {
            if (section.Name == name)
            {
                return section;
            }
        }

        return null;
    }

    // The name a CALL gives: the content of a string, an identifier, or the digits of an integer, as the NAME of a
    // section is read; CALL=100 and CALL="100" name the same subprogram (language 4.9).
    private static string NameOf(Value value)
    {
        return value switch
        {
            StringValue text => text.Content,
            IdentValue ident => ident.Name,
            _ => value.ToCanonical(),
        };
    }

    // TIMES=n, and one pass without TIMES (language 4.9).
    private static int PassesOf(Block block)
    {
        if (block.Find("TIMES") is not Word times)
        {
            return 1;
        }

        if (times.Value is IntegerValue count)
        {
            return (int)Math.Clamp(count.Number, 0, int.MaxValue);
        }

        // TODO(question): TIMES from an expression is not evaluated in STATIC mode (virtual machine 1), and D99 walks
        // the subprogram n times; with n unknown it is walked once, so that its blocks are checked, until that is
        // answered.
        return 1;
    }
}
