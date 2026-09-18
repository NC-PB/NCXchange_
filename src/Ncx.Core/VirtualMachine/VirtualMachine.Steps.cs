using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.Validation;

namespace Ncx.Core.VirtualMachine;

// A run step by step (architecture 5, Step): Begin starts one program on its channel, Step executes the next block the
// flow reaches. The job scheduler advances every channel that is neither finished nor waiting by one step per round
// (virtual machine 3.7, architecture 5.4); INTERPRETED mode without a job takes the steps one after the other
// (RunInterpreted).
public sealed partial class VirtualMachine
{
    // The steps of the run that Begin started, how its walk ended, and the file it runs.
    private IEnumerator<Block>? _steps;
    private SectionEnd _stepsEnd = new();
    private NcxProgram? _stepsProgram;

    /// <summary>
    /// Starts a run of one program: the program the job or the command line names, the first of the file without one
    /// (language 4.13, virtual machine 3.6), on the channel the job runs it on, or the channel of its header without a
    /// job (virtual machine 2.8). INTERPRETED mode follows its flow, STATIC mode walks it once with its calls (virtual
    /// machine 1); Step then executes it block by block.
    /// </summary>
    /// <param name="program">The parsed program, with the diagnostics the parser reported.</param>
    /// <param name="programName">The NAME of the program to run; null for the first program of the file.</param>
    /// <param name="channelId">The channel the job runs the program on; null for the CHANNEL of its header.</param>
    /// <param name="prePass">False when another channel of the job has run the pre-pass over the same file already,
    /// whose rules about a block as it is written are reported once per file (virtual machine 3.6, 5).</param>
    /// <returns>False when an ERROR stopped the run before its first block.</returns>
    internal bool Begin(NcxProgram program, string? programName, int? channelId, bool prePass)
    {
        // An ERROR stops the run: one the parser reported stops it before the first block (virtual machine 2.9).
        if (program.Diagnostics.HasErrors || Diagnostics.HasErrors)
        {
            return false;
        }

        _program = program;
        _stepsProgram = program;
        _resources = new ResourceResolver(Machine);
        _homeWarnings.Clear();
        _externalFiles.Clear();
        _calledSubs.Clear();
        _firstVerbState = null;

        // The pre-pass over the file: duplicates, missing targets, a LABEL=END, a SUB inside a PROGRAM or a block
        // outside every section are ERRORs before execution (virtual machine 3.6); the rules about a block as it is
        // written are reported once per block, however often the flow passes it. INTERPRETED mode reports a missing
        // call target before execution, STATIC mode where the walk meets it (3.9).
        _validation = NewValidation(Diagnostics);
        if (prePass)
        {
            _validation.CheckFile(program);
            if (Mode == ExecutionMode.Interpreted)
            {
                CheckCallTargets(program);
            }
        }

        if (Diagnostics.HasErrors || ProgramToRun(program, programName) is not Section running)
        {
            return false;
        }

        StartWalk(NewState(channelId ?? running.Channel), suppressCallerRules: false);
        _state.Program.Section = running;
        RaiseFileEvent(program, EventPhase.Begin);
        _stepsEnd = new SectionEnd();
        IEnumerable<Block> steps = Mode == ExecutionMode.Interpreted
            ? RunSection(running, calledProgram: false, _stepsEnd)
            : WalkSection(running, recordFirstVerb: running == program.Programs[0], _stepsEnd);
        _steps = steps.GetEnumerator();
        return true;
    }

    /// <summary>
    /// Executes the next block the flow of the run reaches: one step of a round of the job scheduler (virtual machine
    /// 3.7). A block the flow passes without executing it, a SKIP block the run option skips or a block whose IF is 0,
    /// takes no step.
    /// </summary>
    internal StepResult Step()
    {
        if (_steps is not null && _steps.MoveNext())
        {
            Block executed = _steps.Current;

            // PROGRAM=END ends the program, and the run with it: the flow reaches no block after it (virtual machine
            // 2.8, 3.6).
            if (!_state.Program.Ended)
            {
                return new StepResult { Executed = executed };
            }

            EndSteps();
            return new StepResult { Executed = executed, Ended = true };
        }

        EndSteps();
        return new StepResult { Ended = true, Stopped = _stepsEnd.Exit == SectionExit.Stopped };
    }

    /// <summary>
    /// Gives the run up where it stands, for a job that stops at an ERROR or a deadlock (virtual machine 2.9, 3.7): a
    /// channel inside a called external program hands what the call reported back to the diagnostics of its file.
    /// </summary>
    internal void Abandon()
    {
        _steps?.Dispose();
        _steps = null;
    }

    // The walk is over: its program ended, or an ERROR stopped it. After the program that ran, INTERPRETED mode raises
    // FILE_END (virtual machine 7); a STATIC walk of a job raises it once the rest of its file is walked
    // (FinishStaticJob).
    private void EndSteps()
    {
        if (_steps is null)
        {
            return;
        }

        if (_steps.MoveNext())
        {
            throw new InvalidOperationException(
                "The flow reached a block after PROGRAM=END, the last block of a program (language 4.13).");
        }

        _steps.Dispose();
        _steps = null;
        if (Mode == ExecutionMode.Interpreted
            && _stepsEnd.Exit != SectionExit.Stopped
            && _stepsProgram is NcxProgram program)
        {
            RaiseFileEvent(program, EventPhase.End);
        }
    }

    // The validation of a run, which knows whether the run is a channel of a job (virtual machine 3.7, 5): a channel of
    // the job scheduler, or the channel program a job compile writes (VmOptions.JobChannels).
    private RunValidation NewValidation(Diagnostics diagnostics)
    {
        return new RunValidation(Machine, diagnostics, Mode, JobChannels ?? Options.JobChannels);
    }
}
