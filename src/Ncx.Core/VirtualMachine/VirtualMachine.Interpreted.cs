using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

// INTERPRETED mode (virtual machine 1, 3.6, 3.9): the program that runs is executed, the pc over its blocks, the
// variables evaluated, the jumps, repeats and calls followed, within the configured depth and under a block cap against
// endless loops. What analyze uses by default, and what ncx trace --interpreted shows. The calls are in
// VirtualMachine.Calls.cs, the resolution of the expressions of a block in ExpressionResolver.cs, the run step by step
// in VirtualMachine.Steps.cs.
public sealed partial class VirtualMachine
{
    // JUMP=END continues at the PROGRAM=END of the current program; END is no label
    // (language 4.9, virtual machine 2.7).
    private const string EndTarget = "END";

    /// <summary>
    /// Runs a parsed program in INTERPRETED mode: the program the command line or the job names, the first of the file
    /// without one, from its PROGRAM=BEGIN to its PROGRAM=END with its flow followed; the other programs of the file
    /// are not executed, and a subprogram only when a CALL enters it (virtual machine 1, 3.6, 3.9). Afterwards the
    /// diagnostics hold what the run found.
    /// </summary>
    /// <param name="program">The parsed program, with the diagnostics the parser reported.</param>
    /// <param name="programName">The NAME of the program to run; null for the first program of the file (language
    /// 4.13).</param>
    /// <returns>Whether an ERROR stopped the run.</returns>
    public RunResult RunInterpreted(NcxProgram program, string? programName = null)
    {
        if (Mode != ExecutionMode.Interpreted)
        {
            throw new InvalidOperationException(
                "RunInterpreted needs a virtual machine built in INTERPRETED mode; Run walks STATIC "
                + "(virtual machine 1).");
        }

        // The program runs on the channel its header names (virtual machine 2.8); without a job nothing waits between
        // its steps, so they are taken one after the other (VirtualMachine.Steps.cs).
        if (!Begin(program, programName, channelId: null, prePass: true))
        {
            return new RunResult { Stopped = true };
        }

        StepResult step = Step();
        while (!step.Ended)
        {
            step = Step();
        }

        return new RunResult { Stopped = step.Stopped };
    }

    // The program that runs is the first of the file unless the job manifest or the command line names another
    // (language 4.13, virtual machine 3.6); a name no program of the file has is an ERROR. A file without a program is
    // the ERROR of the parser (PAR028).
    private Section? ProgramToRun(NcxProgram program, string? programName)
    {
        if (programName is null)
        {
            return program.Programs.Count > 0 ? program.Programs[0] : null;
        }

        if (FindSection(program.Programs, programName) is Section named)
        {
            return named;
        }

        Diagnostics.Error(program.FileBegin?.Line ?? 1, DiagnosticCodes.ProgramToRunMissing,
            $"The file has no program {programName} to run; the program that runs is a PROGRAM section of the file, "
            + "found by its NAME (language 4.13, virtual machine 3.6).");
        return null;
    }

    // A missing call target is an ERROR before execution (virtual machine 3.6, 5), also for a CALL the flow may never
    // reach: a CALL that names neither a subprogram nor a program of the file. A string names an external program by
    // its file name, which the CALL loads when the flow reaches it (language 4.9).
    private void CheckCallTargets(NcxProgram program)
    {
        foreach (Section section in program.Sections)
        {
            for (int index = section.FirstBlock; index <= section.LastBlock; index++)
            {
                Block block = program.Blocks[index];
                if (block.Find("CALL") is not Word call || call.Value is StringValue)
                {
                    continue;
                }

                string name = NameOf(call.Value);
                if (FindSection(program.Subs, name) is null && FindSection(program.Programs, name) is null)
                {
                    Diagnostics.Error(block, DiagnosticCodes.CallTargetMissing,
                        $"{call.ToCanonical()}: the file has no subprogram {name} (virtual machine 3.6, missing call "
                        + "target).");
                }
            }
        }
    }

    // Runs a program or a subprogram from its first block with its flow (virtual machine 3.6): after each block pc goes
    // to the next block, to a label of the section, into a called subprogram and back to the block of the CALL, or to
    // the PROGRAM=END of the current program, until the section returns or the program ends. A called external program
    // runs from the block after its PROGRAM=BEGIN and returns at its PROGRAM=END (CallExternalProgram). Every executed
    // block is one step, handed out before its flow is followed, so that the job scheduler can advance the channel by
    // one block per round, also inside a called subprogram (virtual machine 3.7); how the section ended is in end once
    // the steps are taken.
    private IEnumerable<Block> RunSection(Section section, bool calledProgram, SectionEnd end)
    {
        NcxProgram file = _program ?? throw new InvalidOperationException("A section runs in the file of a run.");
        int repeatsAtEntry = _state.Flow.Repeats.Count;
        int pc = calledProgram ? section.FirstBlock + 1 : section.FirstBlock;
        while (pc <= section.LastBlock)
        {
            if (calledProgram && pc == section.LastBlock)
            {
                end.Exit = Leave(SectionExit.Returned, repeatsAtEntry);
                yield break;
            }

            _state.Flow.Pc = pc;
            Block? executed = ExecuteInterpreted(file.Blocks[pc]);

            // An ERROR stops the run (virtual machine 2.9).
            if (Diagnostics.HasErrors)
            {
                end.Exit = SectionExit.Stopped;
                yield break;
            }

            if (executed is null)
            {
                pc++;
                continue;
            }

            // Every channel of a job that is neither finished nor waiting executes one block per round (virtual machine
            // 3.7).
            yield return executed;

            // PROGRAM=END ends execution of the program: the channel is finished and the run statistics are raised
            // (virtual machine 3.6, 7). SUB=END pops the call and returns to the caller.
            if (executed.Has("PROGRAM", null, "END"))
            {
                end.Exit = Leave(SectionExit.ProgramEnded, repeatsAtEntry);
                yield break;
            }

            if (executed.Has("SUB", null, "END"))
            {
                end.Exit = Leave(SectionExit.Returned, repeatsAtEntry);
                yield break;
            }

            // CALL enters the subprogram or the external program and the flow comes back to its block, whose other flow
            // words then take effect; a JUMP=END inside the call ends the program from there (virtual machine 3.6).
            bool toProgramEnd = false;
            if (executed.Find("CALL") is Word call)
            {
                var callEnd = new SectionEnd();
                foreach (Block step in FollowInterpretedCall(executed, call, pc, callEnd))
                {
                    yield return step;
                }

                if (callEnd.Exit == SectionExit.Stopped)
                {
                    end.Exit = SectionExit.Stopped;
                    yield break;
                }

                toProgramEnd = callEnd.Exit == SectionExit.JumpedToEnd;
            }

            // RETURN pops as SUB=END does, and a called external program returns to its caller; in a program, whose
            // stack is empty, RETURN is treated as JUMP=END, and the pre-pass warns (virtual machine 3.6).
            if (!toProgramEnd && executed.Has("RETURN"))
            {
                if (section.Kind == SectionKind.Sub || calledProgram)
                {
                    end.Exit = Leave(SectionExit.Returned, repeatsAtEntry);
                    yield break;
                }

                toProgramEnd = true;
            }

            // JUMP=END sets pc to the PROGRAM=END of the current program, also from inside a subprogram, whose calls
            // unwind to it; the pre-pass warns that it ends the program from a call (virtual machine 3.6).
            if (toProgramEnd || executed.Has("JUMP", null, EndTarget))
            {
                if (section.Kind == SectionKind.Sub)
                {
                    end.Exit = Leave(SectionExit.JumpedToEnd, repeatsAtEntry);
                    yield break;
                }

                EndRepeatsOutside(section.LastBlock, section, repeatsAtEntry);
                pc = section.LastBlock;
                continue;
            }

            // JUMP sets pc to a LABEL of the current section, forward or backward (virtual machine 3.6).
            if (executed.Find("JUMP") is Word jump)
            {
                pc = LabelPc(section, jump.Value);
                EndRepeatsOutside(pc, section, repeatsAtEntry);
                continue;
            }

            // REPEAT with TIMES re-executes the section from the label to the current block (virtual machine 3.6).
            if (executed.Find("REPEAT") is Word repeat)
            {
                if (FollowRepeat(executed, repeat, pc, section, repeatsAtEntry) is not int next)
                {
                    end.Exit = SectionExit.Stopped;
                    yield break;
                }

                pc = next;
                continue;
            }

            pc++;
        }

        throw new InvalidOperationException(
            "A section ran past its last block; PROGRAM=END and SUB=END end it (language 4.13).");
    }

    // One block in INTERPRETED mode (virtual machine 3.6): a SKIP block is executed unless the run option skip_blocks
    // skips it (D53); a block whose IF is 0 does not execute (language 4.9); every other block counts against the block
    // cap and executes in the seven steps of virtual machine 3 with its expressions resolved, so that its state words
    // apply exactly as in STATIC mode (implementation 14, P4-01). Returns the block as it executed; null when it did
    // not execute or an ERROR stopped the run.
    private Block? ExecuteInterpreted(Block block)
    {
        if (Options.SkipBlocks.Skips(block)
            || ExpressionResolver.ConditionHolds(block, _state.Vars, Options.Unassigned, Diagnostics) != true)
        {
            return null;
        }

        // Block cap from the configuration (default 1 000 000): ERROR "possible endless loop" (virtual machine 3.6).
        FlowState flow = _state.Flow;
        if (flow.BlocksExecuted >= Options.BlockCap)
        {
            Diagnostics.Error(block, DiagnosticCodes.BlockCapExceeded, string.Create(CultureInfo.InvariantCulture,
                $"The run has executed {Options.BlockCap} blocks, the block cap: possible endless loop; [variables] "
                + $"block_cap of the machine configuration sets the cap (virtual machine 3.6)."));
            return null;
        }

        if (ExpressionResolver.Resolve(block, _state.Vars, Options.Unassigned, Diagnostics) is not Block resolved)
        {
            return null;
        }

        // blocksExecuted counts every executed block (virtual machine 2.7).
        flow.BlocksExecuted++;
        Execute(resolved);
        return resolved;
    }

    // REPEAT=label repeats the blocks from the label to this block TIMES more times (language 4.9, virtual machine
    // 3.6): the first time the flow reaches the block, a repeat with its passes goes on the repeat stack and pc goes
    // back to the label; every later time one pass is used, until none is left and the flow goes on after the block.
    // A REPEAT assigns no ARG: an ARG stands only beside a CALL (language 4.9, 5 rule 5; wave-1 question #57).
    // Returns the pc to continue at; null when an ERROR stopped the run.
    // TODO(question): language 4.9 repeats the blocks "TIMES more times" and virtual machine 3.6 speaks of "REPEAT with
    // TIMES"; neither says what a REPEAT without TIMES does. It repeats them once, as a Heidenhain CALL LBL without REP
    // and a Siemens REPEAT without P do, until D212 is answered.
    private int? FollowRepeat(Block block, Word repeat, int pc, Section section, int repeatsAtEntry)
    {
        Stack<RepeatFrame> repeats = _state.Flow.Repeats;
        int labelPc = LabelPc(section, repeat.Value);
        if (repeats.Count > repeatsAtEntry && repeats.Peek().RepeatPc == pc)
        {
            RepeatFrame running = repeats.Pop();
            if (running.Remaining == 0)
            {
                return pc + 1;
            }

            repeats.Push(running with { Remaining = running.Remaining - 1 });
            return labelPc;
        }

        int times = TimesOf(block, withoutTimes: 1);
        if (times == 0)
        {
            return pc + 1;
        }

        // Nested repeats and calls up to the configured depth (default 8): calls and repeats nest within one depth
        // (virtual machine 3.6; machine-config 7, call_depth).
        if (_state.Flow.Calls.Count + repeats.Count >= Options.CallDepth)
        {
            Diagnostics.Error(block, DiagnosticCodes.RepeatDepthExceeded, string.Create(CultureInfo.InvariantCulture,
                $"{repeat.ToCanonical()} would nest deeper than the depth of {Options.CallDepth}, which calls and "
                + $"repeats share; the blocks are not repeated (virtual machine 3.6)."));
            return null;
        }

        repeats.Push(new RepeatFrame(pc, repeat.Value.ToCanonical(), times - 1));
        return labelPc;
    }

    // A repeat re-executes the blocks from its label to its REPEAT block (virtual machine 3.6); a JUMP that leaves them
    // ends the repeat.
    // TODO(question): virtual machine 3.6 does not say what becomes of a repeat whose blocks a JUMP leaves before its
    // passes are used (a Heidenhain FN 9 out of a CALL LBL REP section); the repeat ends, and a flow that reaches the
    // REPEAT block again starts it anew, until D213 is answered.
    private void EndRepeatsOutside(int targetPc, Section section, int repeatsAtEntry)
    {
        Stack<RepeatFrame> repeats = _state.Flow.Repeats;
        while (repeats.Count > repeatsAtEntry)
        {
            RepeatFrame repeat = repeats.Peek();
            if (targetPc >= LabelPc(section, repeat.Label) && targetPc <= repeat.RepeatPc)
            {
                return;
            }

            repeats.Pop();
        }
    }

    // Leaving a program or a subprogram ends the repeats it started (virtual machine 3.6).
    private SectionExit Leave(SectionExit exit, int repeatsAtEntry)
    {
        while (_state.Flow.Repeats.Count > repeatsAtEntry)
        {
            _state.Flow.Repeats.Pop();
        }

        return exit;
    }

    // The index of the block a LABEL of the section stands on, from the pre-pass (virtual machine 2.7, 3.6), which
    // reports a JUMP or REPEAT to a label its section does not hold before execution.
    private int LabelPc(Section section, Value label)
    {
        return LabelPc(section, label.ToCanonical());
    }

    private int LabelPc(Section section, string label)
    {
        if (_state.Flow.Labels.TryGetValue(section, out Dictionary<string, int>? labels)
            && labels.TryGetValue(label, out int index))
        {
            return index;
        }

        throw new InvalidOperationException(
            $"LABEL={label} is not in the pre-pass of its section, which reports a missing jump target "
            + "(virtual machine 3.6).");
    }

    // TIMES=n of a CALL or a REPEAT, a whole number once its expression is resolved (language 4.9); a count below 0
    // runs nothing, as in the STATIC walk.
    private static int TimesOf(Block block, int withoutTimes)
    {
        if (block.Find("TIMES")?.Value is not IntegerValue count)
        {
            return withoutTimes;
        }

        return (int)Math.Clamp(count.Number, 0, int.MaxValue);
    }
}
