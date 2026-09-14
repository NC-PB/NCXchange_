using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;
using Ncx.Core.VirtualMachine.Validation;

namespace Ncx.Core.VirtualMachine;

// The calls of INTERPRETED mode (language 4.9, 4.13; virtual machine 3.6, 3.9): CALL enters a subprogram of the file by
// its NAME, or loads an external program by its file name from the working directory, TIMES=n times in sequence. Each
// pass pushes the return pc and the locals V1 to V33 and assigns the ARG words to the callee's locals; SUB=END and
// RETURN pop them, within the depth that calls and repeats share.
public sealed partial class VirtualMachine
{
    // The external programs the run has loaded, by the name their CALL gives: each is read, and the rules about its
    // blocks as they are written reported, once per run, however often a CALL enters it (virtual machine 3.6, 5).
    private readonly Dictionary<string, NcxProgram> _externalFiles = new(StringComparer.Ordinal);

    /// <summary>
    /// Loads the external program of CALL="name" in INTERPRETED mode, searched in the working directory (virtual
    /// machine 3.6): the program parsed and expanded, with what reading it found in its diagnostics under its own file
    /// name; null when the working directory holds none. The composition root sets it, because Ncx.Core reads no file
    /// (code-guidelines 4, dependency inversion); without it every external program is missing.
    /// </summary>
    public Func<string, NcxProgram?>? ExternalPrograms { get; init; }

    // CALL enters the SUB section of the file by its NAME or loads the external program, TIMES=n times in sequence,
    // each pass from the state the pass before left (language 4.9, virtual machine 3.6). A CALL that names a program:
    // ERROR; programs are entered from the job only (language 4.13).
    private SectionExit FollowInterpretedCall(Block block, Word call, int callPc)
    {
        NcxProgram file = _program ?? throw new InvalidOperationException("A call runs in the file of a run.");
        string name = NameOf(call.Value);
        Section? sub = FindSection(file.Subs, name);
        if (sub is null && FindSection(file.Programs, name) is not null)
        {
            Diagnostics.Error(block, DiagnosticCodes.CallOfProgram,
                $"{call.ToCanonical()} names a program; a CALL enters a subprogram, and programs are entered from the "
                + "job only (language 4.13, virtual machine 3.6).");
            return SectionExit.Stopped;
        }

        // A CALL that names no section of the file names an external program by its file name as a string; any other
        // is the missing call target the pre-pass reports before execution (CheckCallTargets).
        if (sub is null && call.Value is not StringValue)
        {
            throw new InvalidOperationException(
                $"{call.ToCanonical()} names no subprogram, which the pre-pass reports (virtual machine 3.6).");
        }

        int passes = TimesOf(block, withoutTimes: 1);
        for (int pass = 0; pass < passes; pass++)
        {
            SectionExit exit = sub is null
                ? CallExternalProgram(name, block, callPc)
                : EnterInterpretedSub(sub, name, block, callPc);
            if (exit != SectionExit.Returned)
            {
                return exit;
            }
        }

        return SectionExit.Returned;
    }

    // One pass through a subprogram of the file with the caller's state, exactly as on the control (virtual machine
    // 3.6, 3.9).
    private SectionExit EnterInterpretedSub(Section sub, string name, Block callBlock, int callPc)
    {
        if (!EnterCall(name, callBlock, callPc))
        {
            return SectionExit.Stopped;
        }

        Section? caller = _state.Program.Section;
        _state.Program.Section = sub;
        SectionExit exit = RunSection(sub, calledProgram: false);
        if (exit == SectionExit.Stopped)
        {
            return exit;
        }

        _state.Program.Section = caller;
        LeaveCall(callPc);
        return exit;
    }

    // CALL="name" loads the external program from the working directory (virtual machine 3.6). It must have its own
    // PROGRAM frame, which the parser asks of every file (PAR028), and must not contradict the caller's UNITS and
    // WORKPLANE: ERROR. The first program of its file runs with the caller's state, in its own file: its diagnostics
    // carry its file name (virtual machine 2.9), and its labels and subprograms are its own (language 4.13).
    // TODO(question): virtual machine 3.6 gives the external program its own PROGRAM frame without saying what its
    // PROGRAM=BEGIN and PROGRAM=END do in a call; they frame it as SUB=BEGIN and SUB=END frame a subprogram: the
    // program runs from the block after its PROGRAM=BEGIN, and its PROGRAM=END returns to the caller, without ending
    // the channel and without the resets of virtual machine 4, until that is answered.
    private SectionExit CallExternalProgram(string name, Block callBlock, int callPc)
    {
        bool firstCall = !_externalFiles.TryGetValue(name, out NcxProgram? external);
        if (firstCall)
        {
            external = ExternalPrograms?.Invoke(name);
            if (external is null)
            {
                Diagnostics.Error(callBlock, DiagnosticCodes.ExternalProgramNotFound,
                    $"CALL=\"{name}\": the working directory holds no external program {name} that can be read "
                    + "(virtual machine 3.6, missing call target).");
                return SectionExit.Stopped;
            }

            // What reading the external program found carries its own file name (virtual machine 2.9).
            _externalFiles[name] = external;
            foreach (Diagnostic diagnostic in external.Diagnostics.Items)
            {
                Diagnostics.Add(diagnostic);
            }
        }

        if (external is null
            || external.Diagnostics.HasErrors
            || !AgreesWithCaller(external)
            || !EnterCall(name, callBlock, callPc))
        {
            return SectionExit.Stopped;
        }

        Section program = external.Programs[0];
        Section? caller = _state.Program.Section;
        NcxProgram callerFile = _program ?? throw new InvalidOperationException("A call runs in the file of a run.");
        Diagnostics callerDiagnostics = Diagnostics;
        RunValidation callerValidation = _validation;

        EnterFile(external, new Diagnostics(external.FileName), prePass: firstCall);
        _state.Program.Section = program;
        SectionExit exit = Diagnostics.HasErrors ? SectionExit.Stopped : RunSection(program, calledProgram: true);

        // Back in the caller's file, what the called program reported joins the diagnostics of the caller, in the order
        // it was reported.
        Diagnostics called = Diagnostics;
        Diagnostics = callerDiagnostics;
        _validation = callerValidation;
        foreach (Diagnostic diagnostic in called.Items)
        {
            Diagnostics.Add(diagnostic);
        }

        if (exit == SectionExit.Stopped)
        {
            return exit;
        }

        FillPrePassOf(callerFile);
        _state.Program.Section = caller;
        LeaveCall(callPc);
        return exit;
    }

    // An external program must not contradict the caller's UNITS and WORKPLANE: ERROR (virtual machine 3.6). A UNITS or
    // a WORKPLANE word of the program the CALL runs contradicts the caller when it names another value than the caller
    // has at the CALL; while the caller has no UNITS yet, there is nothing to contradict. False after the ERROR, which
    // stands on the block of the called program, in its file (virtual machine 2.9).
    private bool AgreesWithCaller(NcxProgram external)
    {
        Section program = external.Programs[0];
        FrameState caller = _state.Frame;
        var programDiagnostics = new Diagnostics(external.FileName);
        for (int index = program.FirstBlock; index <= program.LastBlock; index++)
        {
            Block block = external.Blocks[index];
            if (block.Find("UNITS") is Word units
                && caller.Units != Units.Unknown
                && Enum.TryParse(BlockContext.IdentOf(units), ignoreCase: true, out Units calledUnits)
                && calledUnits != caller.Units)
            {
                string callerUnits = caller.Units == Units.Inch ? "INCH" : "MM";
                programDiagnostics.Error(block, DiagnosticCodes.ExternalProgramContradictsCaller,
                    $"{units.ToCanonical()} contradicts UNITS={callerUnits} of the program that calls it; an external "
                    + "program must not contradict the caller's UNITS and WORKPLANE (virtual machine 3.6).");
            }

            if (block.Find("WORKPLANE") is Word workplane
                && Enum.TryParse(BlockContext.IdentOf(workplane), out Workplane calledPlane)
                && calledPlane != caller.Workplane)
            {
                programDiagnostics.Error(block, DiagnosticCodes.ExternalProgramContradictsCaller,
                    $"{workplane.ToCanonical()} contradicts WORKPLANE={caller.Workplane} of the program that calls "
                    + "it; an external program must not contradict the caller's UNITS and WORKPLANE (virtual machine "
                    + "3.6).");
            }
        }

        foreach (Diagnostic diagnostic in programDiagnostics.Items)
        {
            Diagnostics.Add(diagnostic);
        }

        return !programDiagnostics.HasErrors;
    }

    // CALL pushes the return pc and the local variables V1 to V33 and assigns the ARG words to the callee's locals
    // (virtual machine 3.6), within the configured depth (default 8) that nested repeats and calls share: a CALL beyond
    // it is the ERROR "call depth exceeded" and the subprogram is not entered (3.6, 3.9). False when an ERROR stopped
    // the run.
    private bool EnterCall(string name, Block callBlock, int callPc)
    {
        FlowState flow = _state.Flow;
        if (flow.Calls.Count + flow.Repeats.Count >= Options.CallDepth)
        {
            Diagnostics.Error(callBlock, DiagnosticCodes.CallDepthExceeded, string.Create(CultureInfo.InvariantCulture,
                $"CALL={name} would nest deeper than the call depth of {Options.CallDepth}, which calls and repeats "
                + $"share: call depth exceeded; the subprogram is not entered (virtual machine 3.6, 3.9)."));
            return false;
        }

        flow.Calls.Push(new CallFrame(callPc + 1, name));
        _state.Vars.PushLocals();
        return AssignArguments(callBlock);
    }

    // SUB=END and RETURN pop the call and restore the caller's locals (virtual machine 3.6, 4); the flow is back at the
    // block of the CALL.
    private void LeaveCall(int callPc)
    {
        _state.Vars.PopLocals();
        _state.Flow.Calls.Pop();
        _state.Flow.Pc = callPc;
    }

    // The file a CALL enters: its diagnostics under its own name (virtual machine 2.9), its pre-pass at the first call
    // of the run (3.6, 5), and the pre-pass tables of the channel, the programs, subprograms and labels of that file
    // (2.1, 2.7).
    private void EnterFile(NcxProgram file, Diagnostics diagnostics, bool prePass)
    {
        Diagnostics = diagnostics;
        _validation = new RunValidation(Machine, diagnostics, Mode);
        if (prePass)
        {
            _validation.CheckFile(file);
            CheckCallTargets(file);
        }

        FillPrePassOf(file);
    }

    // file.programs, file.subs and the labels describe the file whose blocks run (virtual machine 2.1, 2.7).
    private void FillPrePassOf(NcxProgram file)
    {
        _program = file;
        _state.Flow.Programs.Clear();
        _state.Flow.Subs.Clear();
        _state.Flow.Labels.Clear();
        FillPrePass(_state);
    }
}
