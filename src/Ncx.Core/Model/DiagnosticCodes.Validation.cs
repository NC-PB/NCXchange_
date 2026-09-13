namespace Ncx.Core.Model;

// The codes of the validation list of virtual machine 5 that block execution and motion do not raise already,
// VM400-VM599 (the ranges in DiagnosticCodes.cs, D98), grouped by the families of VirtualMachine/Validation/: structure
// VM400-VM419, frame VM420-VM439, motion VM440-VM459, arc VM460-VM479, tool VM480-VM499, spindle VM500-VM519, flow
// VM520-VM539, expression VM540-VM549, resource VM550-VM569, channel VM570-VM589. Every code of Ncx.Core has its row in
// the table of the validation (DiagnosticTable), which docs/spec/generated/diagnostics.md is written from.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// VM400, a WARNING: RAW present, source text that NCX could not express and keeps verbatim; it compiles only to
    /// its own controller or builder (language 4.1, virtual machine 5).
    /// </summary>
    public const string RawPresent = "VM400";

    /// <summary>
    /// VM420: TOLERANCE:ROTARY or TOLERANCE_MODE without an active TOLERANCE (language 4.1, virtual machine 5, D85).
    /// </summary>
    public const string ToleranceWordWithoutTolerance = "VM420";

    /// <summary>
    /// VM421: SETPOS without an axis word (language 4.2, virtual machine 5).
    /// </summary>
    public const string SetposWithoutAxis = "VM421";

    /// <summary>
    /// VM422, a WARNING: ROT on a target without table kinematics, a machine file without a rotary axis on a table
    /// (language 4.2, virtual machine 5, D82).
    /// </summary>
    public const string RotWithoutTableKinematics = "VM422";

    /// <summary>
    /// VM440, a WARNING: F in a RAPID block (virtual machine 5).
    /// </summary>
    public const string FeedInRapid = "VM440";

    /// <summary>
    /// VM441, a WARNING: F above the max_feed of an axis (virtual machine 5, machine-config 4, D64).
    /// </summary>
    public const string FeedAboveMaxFeed = "VM441";

    /// <summary>
    /// VM442, a WARNING: a target beyond the limits of its axis, compared in the MACHINE frame and not checked while
    /// the machine position is unknown (virtual machine 5, machine-config 4, D64, D100).
    /// </summary>
    public const string TargetBeyondLimits = "VM442";

    /// <summary>
    /// VM460: a COMP change in an ARC block (virtual machine 5).
    /// </summary>
    public const string CompensationChangeInArc = "VM460";

    /// <summary>
    /// VM480: OFFSET mixed with OFFSET:LEN or OFFSET:RAD in one program (virtual machine 5). Suppressed inside a
    /// subprogram that no program of the file calls (3.9, D99).
    /// </summary>
    public const string OffsetFormsMixed = "VM480";

    /// <summary>
    /// VM500, a WARNING: the spindle is OFF before a LINE, the spindle of the current tool holder or the default
    /// spindle when the holder has none (virtual machine 5). Suppressed inside a subprogram that no program of the file
    /// calls (3.9, D99).
    /// </summary>
    public const string SpindleOffBeforeLine = "VM500";

    /// <summary>
    /// VM501, a WARNING: RPM above the rpm_max of its spindle (virtual machine 5, machine-config 5, D64).
    /// </summary>
    public const string RpmAboveMax = "VM501";

    /// <summary>
    /// VM502, a WARNING: RPM below the rpm_min of its spindle (virtual machine 5, machine-config 5, D64).
    /// </summary>
    public const string RpmBelowMin = "VM502";

    /// <summary>
    /// VM520: a LABEL written twice in one program or subprogram (language 4.9, virtual machine 3.6, 5).
    /// </summary>
    public const string DuplicateLabel = "VM520";

    /// <summary>
    /// VM521: a JUMP or REPEAT to a label its program or subprogram does not hold, a missing jump target (language 4.9,
    /// virtual machine 3.6, 5).
    /// </summary>
    public const string JumpTargetMissing = "VM521";

    /// <summary>
    /// VM522, a WARNING: JUMP=END from inside a subprogram; it ends the program from a call (virtual machine 3.6, 5).
    /// </summary>
    public const string JumpEndInSub = "VM522";

    /// <summary>
    /// VM523, a WARNING: a block of a program after an unconditional JUMP that no LABEL makes reachable (language 4.13,
    /// virtual machine 3.9, 5, D89).
    /// </summary>
    public const string UnreachableBlock = "VM523";

    /// <summary>
    /// VM524, a WARNING: RETURN in a program, which has no caller; it is treated as JUMP=END (language 4.9, virtual
    /// machine 3.6, 5).
    /// </summary>
    public const string ReturnInProgram = "VM524";

    /// <summary>
    /// VM540, a WARNING: expressions left unresolved in STATIC mode, counted and reported once per run (virtual machine
    /// 1, 5).
    /// </summary>
    public const string UnresolvedExpressions = "VM540";

    /// <summary>
    /// VM550, a WARNING: MFUNC with a number whose M code the machine configuration writes for a word of its own
    /// (language 4.6, virtual machine 5).
    /// </summary>
    public const string MfuncNamedByMachine = "VM550";

    /// <summary>
    /// VM551, a WARNING: WORKPIECE changes the holder while the spindle of the old holder runs (virtual machine 5).
    /// </summary>
    public const string WorkpieceChangeWhileSpindleRuns = "VM551";

    /// <summary>
    /// VM570, a WARNING: SYNC in a single-channel job; nothing waits (virtual machine 3.7, 5).
    /// </summary>
    public const string SyncInSingleChannelJob = "VM570";

    /// <summary>
    /// VM571: deadlock at SYNC, every channel waiting with no releasable mark; raised by the job scheduler of a
    /// multi-channel job (virtual machine 3.7, 5).
    /// </summary>
    public const string SyncDeadlock = "VM571";

    /// <summary>
    /// VM572, a WARNING: two channels command one spindle between two marks; raised by the job scheduler of a
    /// multi-channel job (virtual machine 3.7, 5).
    /// </summary>
    public const string SpindleSharedBetweenMarks = "VM572";
}
