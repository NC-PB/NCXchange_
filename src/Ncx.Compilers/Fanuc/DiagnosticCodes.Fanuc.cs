namespace Ncx.Compilers;

// The codes of the Fanuc compiler (P3-06), CMP300 to CMP499: CMP300-CMP319 the program frame and the flow,
// CMP320-CMP339 the motion, CMP340-CMP359 the tool, spindle and function words, CMP360-CMP379 the frames,
// CMP380-CMP399 the cycles, CMP400-CMP419 the variables and expressions, CMP420-CMP429 the wait marks.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CMP300: a program without NUMBER has no O number, which names a Fanuc program (controllers fanuc.md 1).
    /// </summary>
    public const string FanucProgramWithoutNumber = "CMP300";

    /// <summary>
    /// CMP301: a SUB whose NAME is no number has no O number, which names a Fanuc subprogram (controllers fanuc.md 1).
    /// </summary>
    public const string FanucSubNameNotAProgramNumber = "CMP301";

    /// <summary>
    /// CMP302: a conditional JUMP=END in a subprogram has no Fanuc form, since a GOTO stays in its program (controllers
    /// fanuc.md 7).
    /// </summary>
    public const string FanucConditionalEndInSub = "CMP302";

    /// <summary>
    /// CMP303: IF with CALL has no Fanuc form, since IF [ ] THEN only assigns and IF [ ] GOTO only jumps (controllers
    /// fanuc.md 7).
    /// </summary>
    public const string FanucConditionalCall = "CMP303";

    /// <summary>
    /// CMP304: REPEAT is not written, since the counter variable of controller-mapping 6 is named nowhere.
    /// </summary>
    public const string FanucRepeatNotWritten = "CMP304";

    /// <summary>
    /// CMP305: CALL of a name that is no Fanuc program number (controllers fanuc.md 1, 7).
    /// </summary>
    public const string FanucCallOfNoProgramNumber = "CMP305";

    /// <summary>
    /// CMP306: an ARG outside the letter table of G65 (controllers fanuc.md 7).
    /// </summary>
    public const string FanucArgumentWithoutLetter = "CMP306";

    /// <summary>
    /// CMP307: SKIP on a block whose tool change or preload the compiler framework writes, which gets no skip mark.
    /// </summary>
    public const string FanucSkipOnToolChange = "CMP307";

    /// <summary>
    /// CMP308: a word of the block has no Fanuc form the compiler writes; nothing is dropped silently (language 2 rule
    /// 8).
    /// </summary>
    public const string FanucWordNotWritten = "CMP308";

    /// <summary>
    /// CMP320: LINE or ARC with FRAME=MACHINE, which G53 would move at rapid (D242).
    /// </summary>
    public const string FanucFeedMotionInMachineFrame = "CMP320";

    /// <summary>
    /// CMP321: an incremental word on an axis without the incremental letter of G-code system A, or under G53
    /// (controllers fanuc.md 3, 4).
    /// </summary>
    public const string FanucIncrementalWordNotWritable = "CMP321";

    /// <summary>
    /// CMP322: the start of an arc is unknown, so I J K of its absolute CENTER cannot be written (controllers fanuc.md
    /// 4).
    /// </summary>
    public const string FanucArcStartUnknown = "CMP322";

    /// <summary>
    /// CMP323: a block that mixes absolute and incremental words ends at a point that is not known, so it cannot be
    /// written in one of the two modes of G90 and G91 (controllers fanuc.md 3).
    /// </summary>
    public const string FanucMixedWordsUnknownTarget = "CMP323";

    /// <summary>
    /// CMP340, a WARNING: MFUNC is written as the M code it names, which no function table of the machine names
    /// (language 4.6).
    /// </summary>
    public const string FanucMFuncWritten = "CMP340";

    /// <summary>
    /// CMP360: the transform chain of the block cannot be written with the one G52, G68, G51.1 and G68.2 of a Fanuc
    /// control in program order (D31).
    /// </summary>
    public const string FanucChainNotWritable = "CMP360";

    /// <summary>
    /// CMP361, a WARNING: a word the Fanuc control has no option for is not written: ROT, ROTARY_PATH and ROTARY_FEED
    /// without a template (controller-mapping 1, machine-config 5).
    /// </summary>
    public const string FanucOptionNotWritten = "CMP361";

    /// <summary>
    /// CMP362: TILT_AXIS without a TILT_AXIS_ON template, which the target cannot write without the kinematics module
    /// (D82).
    /// </summary>
    public const string FanucTiltAxisNotWritable = "CMP362";

    /// <summary>
    /// CMP363: RETRACT without a template of [retract], which the target cannot write without the kinematics module
    /// (controller-mapping 1, D83).
    /// </summary>
    public const string FanucRetractNotWritable = "CMP363";

    /// <summary>
    /// CMP380: a cycle that the Fanuc cycle catalog of the machine does not name (machine-config 6, language 4.7.1).
    /// </summary>
    public const string FanucCycleNotInCatalog = "CMP380";

    /// <summary>
    /// CMP381: a drilling cycle of a mill whose AXIS is not the tool axis of the plane (controller-mapping 5, AXIS).
    /// </summary>
    public const string FanucCycleAxisNotTheToolAxis = "CMP381";

    /// <summary>
    /// CMP382, a WARNING: G98 returns to the initial level, where the drilling axis stood before the cycle, and SAFE is
    /// another plane (controller-mapping 5).
    /// </summary>
    public const string FanucSafeIsNotTheInitialLevel = "CMP382";

    /// <summary>
    /// CMP383: CYCLE_RETRACT=SAFE on a lathe of G-code system A, whose G98 and G99 are the feed mode (controllers
    /// fanuc.md 3).
    /// </summary>
    public const string FanucSafeRetractOnSystemA = "CMP383";

    /// <summary>
    /// CMP384: a multiple repetitive cycle of a lathe with its CONTOUR, whose contour blocks the compiler does not
    /// write inside the program yet (controllers fanuc.md 6, D65).
    /// </summary>
    public const string FanucContourCycleNotWritten = "CMP384";

    /// <summary>
    /// CMP385: a cycle word that the catalog entry maps to no address of the Fanuc cycle block (machine-config 6).
    /// </summary>
    public const string FanucCycleWordNotMapped = "CMP385";

    /// <summary>
    /// CMP386: PITCH of a tapping cycle without CYCLE_F in feed per minute where the speed of the spindle is not known,
    /// so the feed PITCH x S of G84 cannot be written (controller-mapping 5, TAP; language 2 rule 8).
    /// </summary>
    public const string FanucTapFeedNotKnown = "CMP386";

    /// <summary>
    /// CMP400: a variable that neither the V names of custom macro B nor [variables] map nor [system_variables] name a
    /// Fanuc variable for (language 4.9, machine-config 7, D51).
    /// </summary>
    public const string FanucVariableNotMapped = "CMP400";

    /// <summary>
    /// CMP401: an expression, a function or a value that custom macro B cannot write (controllers fanuc.md 7).
    /// </summary>
    public const string FanucExpressionNotWritable = "CMP401";

    /// <summary>
    /// CMP420: a SYNC mark outside the mark_range of [sync], or a SYNC on a machine without a wait template
    /// (machine-config 5, controllers fanuc.md 8).
    /// </summary>
    public const string FanucMarkNotWritable = "CMP420";
}
