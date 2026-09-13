namespace Ncx.Core.Model;

// The codes of block execution, VM001-VM199 (the ranges in DiagnosticCodes.cs, D98): the resolution of roles, axes,
// functions and coolant channels (virtual machine 3.8, D103), the tool change (3.5), SETPOS (3.4, D101), HOME (3 step 5,
// D100), the STATIC walk of the calls (1, 3.9, D99) and the assignment of variables (2.7). P1-04 builds the full table of
// virtual machine 5 and docs/spec/generated/diagnostics.md from these and its own.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// VM001: a role address that [roles] does not name, with a machine file (virtual machine 3.8 rule 1).
    /// </summary>
    public const string UnknownRole = "VM001";

    /// <summary>
    /// VM002: a role that names a resource of the wrong type for its word, SPINDLE:TURRET1 or TOOL:MAIN (virtual
    /// machine 3.8 rules 1 and 5).
    /// </summary>
    public const string WrongResourceType = "VM002";

    /// <summary>
    /// VM003, a WARNING: a role, function, machine axis or coolant channel the built-in default machine lacks when no
    /// machine file is given, "not checked: no machine file", once per name; the word runs against a resource created on
    /// the spot (virtual machine 3.8, D103).
    /// </summary>
    public const string NotCheckedNoMachineFile = "VM003";

    /// <summary>
    /// VM004: a word without a role address on a machine that has no default resource of its kind (virtual machine 3.8
    /// rule 2).
    /// </summary>
    public const string NoDefaultResource = "VM004";

    /// <summary>
    /// VM005: an axis word that no [[axis]] entry declares, with a machine file (virtual machine 3.8 rule 3, D93).
    /// </summary>
    public const string UnknownMachineAxis = "VM005";

    /// <summary>
    /// VM006: FUNC with a function that [func] does not name, with a machine file (language 4.6, virtual machine 3.8,
    /// 5).
    /// </summary>
    public const string UnknownFunction = "VM006";

    /// <summary>
    /// VM007: FUNC with a state its function does not list, with a machine file (machine-config 5, virtual machine 5,
    /// unknown value).
    /// </summary>
    public const string UnknownFunctionState = "VM007";

    /// <summary>
    /// VM008: COOLANT with a channel that [coolant] does not name, with a machine file (language 4.6, machine-config 5).
    /// </summary>
    public const string UnknownCoolantChannel = "VM008";

    /// <summary>
    /// VM010: SPINDLE on a spindle in AXIS mode (virtual machine 3.8 rule 4, 5).
    /// </summary>
    public const string SpindleInAxisMode = "VM010";

    /// <summary>
    /// VM011: C= on the axis of a spindle in SPINDLE mode (virtual machine 3.8 rule 4, 5).
    /// </summary>
    public const string AxisOfSpindleInSpindleMode = "VM011";

    /// <summary>
    /// VM012: SPINDLE_SYNC with a spindle that is not in SPINDLE mode (virtual machine 3.8 rule 5).
    /// </summary>
    public const string SyncSpindleNotInSpindleMode = "VM012";

    /// <summary>
    /// VM013, a WARNING: RPM or SPINDLE on the following spindle while it runs synchronized (virtual machine 3.8 rule
    /// 5).
    /// </summary>
    public const string SynchronizedSpindleCommanded = "VM013";

    /// <summary>
    /// VM014: SPINDLE_SYNC with a list of more or fewer than two spindle roles (language 4.5, virtual machine 3.8 rule
    /// 5).
    /// </summary>
    public const string SyncNeedsTwoSpindles = "VM014";

    /// <summary>
    /// VM040, a WARNING: PRELOAD of the tool that is already in the spindle, a no-op on the machine (virtual machine
    /// 3.5, row PRELOAD=n).
    /// </summary>
    public const string ToolAlreadyInSpindle = "VM040";

    /// <summary>
    /// VM041: a bare TOOL with nothing preloaded (language 4.4, virtual machine 3.5, row TOOL).
    /// </summary>
    public const string NothingPreloaded = "VM041";

    /// <summary>
    /// VM042, a WARNING: a different tool preloaded than called; the magazine has to cycle twice (virtual machine 3.5,
    /// row TOOL=n, D42).
    /// </summary>
    public const string PreloadMismatch = "VM042";

    /// <summary>
    /// VM043, a WARNING: TOOL while a cycle is active; the change ends the cycle (virtual machine 3.5, row TOOL=0; 4,
    /// row cycle; 5).
    /// </summary>
    public const string ToolChangeWhileCycleActive = "VM043";

    /// <summary>
    /// VM044, a WARNING: TOOL while compensation is on (virtual machine 3.5, row TOOL=0; 5).
    /// </summary>
    public const string ToolChangeWithCompensationOn = "VM044";

    /// <summary>
    /// VM050: SETPOS on an axis unknown in every frame that no HOME without a reference point left there (virtual
    /// machine 3.4, 5, D101).
    /// </summary>
    public const string SetposAxisUnknown = "VM050";

    /// <summary>
    /// VM060, a WARNING: HOME on an axis without the reference point in the configuration, once per run and axis; the
    /// axis is unknown in every frame afterwards (virtual machine 3 step 5, 5, D100).
    /// </summary>
    public const string HomeWithoutReferencePoint = "VM060";

    /// <summary>
    /// VM061: HOME without an axis name (virtual machine 5).
    /// </summary>
    public const string HomeWithoutAxis = "VM061";

    /// <summary>
    /// VM070: a CALL beyond the configured call depth, "call depth exceeded"; the subprogram is not entered (virtual
    /// machine 3.9, D99).
    /// </summary>
    public const string CallDepthExceeded = "VM070";

    /// <summary>
    /// VM071: a CALL that names a program instead of a subprogram (virtual machine 3.6, 5).
    /// </summary>
    public const string CallOfProgram = "VM071";

    /// <summary>
    /// VM072: a CALL whose subprogram the file does not hold (virtual machine 3.6, 5, missing call target).
    /// </summary>
    public const string CallTargetMissing = "VM072";

    /// <summary>
    /// VM080: VAR assigns a SYS_ variable, which the program never assigns (virtual machine 2.7, 5).
    /// </summary>
    public const string SystemVariableAssigned = "VM080";
}
