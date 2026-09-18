namespace Ncx.Compilers;

// The codes of the Siemens compiler (P5-02, the folder Siemens/), CMP500 to CMP699 (D98): CMP500-CMP509 the block and
// the program layout, CMP510-CMP529 the tools, the spindles and the functions, CMP530-CMP549 the frames and the
// transformations, CMP550-CMP569 the motion, CMP570-CMP589 the cycles, CMP590-CMP609 the flow, the variables and the
// expressions, CMP610-CMP619 the channels.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CMP500: a word of the block has no SINUMERIK form the compiler writes, and the compiler never drops a word
    /// silently (language 2 rule 8; controllers siemens.md 12).
    /// </summary>
    public const string SiemensWordNotWritten = "CMP500";

    /// <summary>
    /// CMP501, a WARNING: the name of a program or subprogram holds a character a SINUMERIK name does not, and is
    /// written with an underscore for it (controllers siemens.md 1).
    /// </summary>
    public const string SiemensNameChanged = "CMP501";

    /// <summary>
    /// CMP502: SKIP on a block with TOOL or PRELOAD; the tool change is written from [tool_change] without the skip
    /// mark (controller-mapping 1, SKIP; machine-config 3).
    /// </summary>
    public const string SiemensSkipOnToolChange = "CMP502";

    /// <summary>
    /// CMP503, a WARNING: two programs or two subprograms of the file have one unit name, and the later one is written
    /// with _2 behind it, so that neither is lost (controllers siemens.md 1, 12 rule 1; language 2 rule 8).
    /// </summary>
    public const string SiemensUnitNameTaken = "CMP503";

    /// <summary>
    /// CMP510, a WARNING: OFFSET:LEN and OFFSET:RAD name two registers, and the D of the control is one register for
    /// length and radius (controller-mapping 3, OFFSET:LEN; controllers siemens.md 5).
    /// </summary>
    public const string SiemensOffsetRegistersDiffer = "CMP510";

    /// <summary>
    /// CMP511: SETMS needs the number of a spindle, which neither the templates of its role nor its resource give
    /// (controllers siemens.md 5, 12 rule 3).
    /// </summary>
    public const string SiemensSpindleNumberUnknown = "CMP511";

    /// <summary>
    /// CMP512: SPINDLE_SYNC names another pair than the one [spindle_sync] couples, the default spindle leading the
    /// other work spindle (machine-config 5; language 4.5; D154).
    /// </summary>
    public const string SiemensSpindlePairNotWritable = "CMP512";

    /// <summary>
    /// CMP513, a WARNING: MFUNC writes a raw M function that the machine configuration does not name (language 4.6).
    /// </summary>
    public const string SiemensRawMFunction = "CMP513";

    /// <summary>
    /// CMP530: ORIGIN names a datum beyond G599, the last settable frame of the control (controllers siemens.md 4;
    /// controller-mapping 1, ORIGIN).
    /// </summary>
    public const string SiemensOriginNotWritable = "CMP530";

    /// <summary>
    /// CMP531: an entry of the frame chain has a value that is not known where the compiler must write it (language
    /// 4.2; D31).
    /// </summary>
    public const string SiemensChainNotWritable = "CMP531";

    /// <summary>
    /// CMP532: a tilt the [transform] template of the machine cannot write: TILT_AXIS without TILT_AXIS_ON, or a MOVE
    /// that {dir} has no value for (machine-config 5; controller-mapping 1, TILT, MOVE; D82).
    /// </summary>
    public const string SiemensTiltNotWritable = "CMP532";

    /// <summary>
    /// CMP533, a WARNING: an option the control or the machine has no form for is not written, ROT of a tilt or a
    /// rotary option without its template (controller-mapping 1, MOVE and ROT; machine-config 5; D82, D86).
    /// </summary>
    public const string SiemensOptionNotWritten = "CMP533";

    /// <summary>
    /// CMP534: RETRACT without a template in [retract], which only the kinematics module could compute
    /// (controller-mapping 1, RETRACT; D83).
    /// </summary>
    public const string SiemensRetractNotWritable = "CMP534";

    /// <summary>
    /// CMP550: an ARC with ANGLE whose start, center or end the virtual machine does not know, so that neither AR nor
    /// TURN can be written (language 4.3, ANGLE; controllers siemens.md 12 rule 2; D84).
    /// </summary>
    public const string SiemensArcNotWritable = "CMP550";

    /// <summary>
    /// CMP570: a cycle the cycle catalog of the machine has no entry for (language 4.7.1; machine-config 6).
    /// </summary>
    public const string SiemensCycleNotInCatalog = "CMP570";

    /// <summary>
    /// CMP571: a word of the cycle that its entry maps to no position of the signature, and no rule of the family
    /// carries (machine-config 6; controllers siemens.md 12 rule 5).
    /// </summary>
    public const string SiemensCycleWordNotWritable = "CMP571";

    /// <summary>
    /// CMP572: a value the call of the cycle needs is not known: the plane RTP returns to, the reference plane, or the
    /// drilling axis (controller-mapping 5; D188).
    /// </summary>
    public const string SiemensCycleValueMissing = "CMP572";

    /// <summary>
    /// CMP590: an expression holds an operator or a function the SINUMERIK language has no form for (controllers
    /// siemens.md 8; language 4.12).
    /// </summary>
    public const string SiemensExpressionNotWritable = "CMP590";

    /// <summary>
    /// CMP591: a variable of the program has no SINUMERIK name and no entry of [variables] map covers it
    /// (machine-config 7; language 4.9; D173).
    /// </summary>
    public const string SiemensVariableNotMapped = "CMP591";

    /// <summary>
    /// CMP592: a SYS_ name that [system_variables] of the machine does not map (language 4.12; machine-config 7; D51).
    /// </summary>
    public const string SiemensSystemVariableNotMapped = "CMP592";

    /// <summary>
    /// CMP593: a call of a program outside the file with arguments or a repeat count, which EXTCALL does not take
    /// (controllers siemens.md 8; controller-mapping 6, CALL).
    /// </summary>
    public const string SiemensExternalCallNotWritable = "CMP593";

    /// <summary>
    /// CMP594: JUMP=END in a subprogram, which ends the program from the call (language 4.9; virtual machine 3.6),
    /// and a SINUMERIK subprogram has no documented form that ends the program: a GOTOF goes to a label of its own
    /// unit (controllers siemens.md 8).
    /// </summary>
    public const string SiemensJumpToEndInSubprogram = "CMP594";

    /// <summary>
    /// CMP610: SYNC without a wait template in [sync], or with a mark outside its mark_range (machine-config 5;
    /// controllers siemens.md 9).
    /// </summary>
    public const string SiemensMarkNotWritable = "CMP610";
}
