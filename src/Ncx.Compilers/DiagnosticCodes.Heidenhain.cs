namespace Ncx.Compilers;

// The codes of the Heidenhain compiler, CMP100-CMP299 (D98; a compiler family takes a range of its own,
// DiagnosticCodes.cs). A template the machine file does not give is the CMP010 of the framework, a template without a
// value for a placeholder the CFG code of Ncx.Config; the codes here are the findings of writing Klartext.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CMP100: HOME names an axis without the reference point of the block (home, home2 under POINT=2) in the [[axis]]
    /// table; Klartext has no reference point command, and the compiler writes the M91 move to those coordinates
    /// (controllers heidenhain.md 8 rule 5, D100).
    /// </summary>
    public const string HeidenhainHomeWithoutReferencePoint = "CMP100";

    /// <summary>
    /// CMP101: a word of the block has no Klartext form in the Heidenhain compiler, and nothing is written for it
    /// (controllers heidenhain.md 8; language 2 rule 8).
    /// </summary>
    public const string HeidenhainWordWithoutKlartext = "CMP101";

    /// <summary>
    /// CMP102: the value of a word, an expression, has no Klartext form: a coordinate takes a number or a Q parameter,
    /// a formula the functions and operators of controllers heidenhain.md 6.
    /// </summary>
    public const string HeidenhainValueWithoutKlartext = "CMP102";

    /// <summary>
    /// CMP103: a cycle whose catalog entry gives no signature, the Q parameters in the control's order, cannot be
    /// written as CYCL DEF (controllers heidenhain.md 8 rule 7, machine-config 6, D178).
    /// </summary>
    public const string HeidenhainCycleWithoutSignature = "CMP103";

    /// <summary>
    /// CMP104: a Q parameter of the signature of a cycle has no value, neither from a word of the cycle block nor from
    /// a rule of the compiler (controllers heidenhain.md 8 rule 7, D164).
    /// </summary>
    public const string HeidenhainCycleParameterWithoutValue = "CMP104";

    /// <summary>
    /// CMP105, a WARNING: a word of a cycle block that the catalog entry of the cycle maps to no Q parameter is not
    /// written (machine-config 6; language 2 rule 8).
    /// </summary>
    public const string HeidenhainCycleWordNotWritten = "CMP105";

    /// <summary>
    /// CMP106: a transform is appended while one of its kind stands in the chain; cycle 7, 8, 10 and PLANE replace the
    /// one before them on the control, so the chain cannot be written in program order (language 4.2, D31, D253).
    /// </summary>
    public const string HeidenhainTransformReplacesAnother = "CMP106";

    /// <summary>
    /// CMP107: the template of the word is empty, which means the word needs the kinematics module to be written (D82,
    /// D83; machine-config 5).
    /// </summary>
    public const string HeidenhainWordNeedsKinematics = "CMP107";

    /// <summary>
    /// CMP108, a WARNING: ROT=COORD of a tilt is not written, since the templates of the machine have no placeholder
    /// for it (language 4.2, D82; machine-config 5).
    /// </summary>
    public const string HeidenhainRotNotWritten = "CMP108";

    /// <summary>
    /// CMP109, a WARNING: MFUNC=n is written as the M function n that the machine configuration does not name
    /// (language 4.6).
    /// </summary>
    public const string HeidenhainMFunctionNotNamed = "CMP109";

    /// <summary>
    /// CMP110: WORKPLANE changes the plane of the last TOOL CALL in a block without TOOL; Klartext gives the working
    /// plane with the tool axis of TOOL CALL (controllers heidenhain.md 8 rule 3; controller-mapping 1, WORKPLANE).
    /// </summary>
    public const string HeidenhainWorkplaneWithoutToolCall = "CMP110";

    /// <summary>
    /// CMP111: a block with SKIP changes or preloads a tool; the tool change of the framework is written without the
    /// block skip.
    /// </summary>
    public const string HeidenhainSkipOnToolChange = "CMP111";

    /// <summary>
    /// CMP112: BEGIN PGM needs the units of the program, MM or INCH, and the program sets none (controllers
    /// heidenhain.md 8 rule 1).
    /// </summary>
    public const string HeidenhainProgramWithoutUnits = "CMP112";

    /// <summary>
    /// CMP113: SETPOS names an axis whose position before the block is not known in the workpiece frame of the
    /// program: known in the MACHINE frame only, after HOME or a machine-frame move, or not at all. The compiler folds
    /// SETPOS into the cycle 7 datum shift (machine-config 3, D55), a shift against the active preset, and does not
    /// know where the preset lies in the machine frame (virtual machine 3.4, D35, D101).
    /// </summary>
    public const string HeidenhainSetposWithoutWorkpiecePosition = "CMP113";

    /// <summary>
    /// CMP114: a RESET removes a ROTATE, MIRROR, TILT or TILT_AXIS that stood in the chain before the cycle 7 of a
    /// SETPOS while the setpos shift of an axis it turns stands; where that shift acts once the frame it was declared
    /// in is gone is not given (language 4.2; virtual machine 3.4; D253).
    /// </summary>
    public const string HeidenhainSetposFrameRemoved = "CMP114";

    /// <summary>
    /// CMP115: an arc, a CYCL CALL at the current position or M140 would run with another radius compensation than the
    /// program has after a COMP that no L block has written yet; Klartext writes R0, RL and RR at the end of an L block
    /// only (language 4.4; controllers heidenhain.md 2; differences.md, radius compensation).
    /// </summary>
    public const string HeidenhainCompensationWithoutLine = "CMP115";

    /// <summary>
    /// CMP116: two labels or subprograms of one output file would be written as the same LBL, which Klartext has once
    /// per program, its LBL sections included, or a label as LBL 0, the end of a subprogram (controllers heidenhain.md
    /// 1; language 4.9, 4.13).
    /// </summary>
    public const string HeidenhainLabelNotUnique = "CMP116";
}
