namespace Ncx.Core.Catalog;

/// <summary>
/// The group of a word: one per table of language 4, and the pseudo-words of generated blocks (architecture 4).
/// </summary>
public enum WordKind
{
    /// <summary>
    /// File and program: FILE, PROGRAM, NAME, UNITS, SKIP (language 4.1).
    /// </summary>
    Program,

    /// <summary>
    /// Frame: WORKPLANE, ORIGIN, SHIFT, TILT (language 4.2).
    /// </summary>
    Frame,

    /// <summary>
    /// Motion: RAPID, LINE, ARC, the axis words, F (language 4.3).
    /// </summary>
    Motion,

    /// <summary>
    /// Tool: PRELOAD, TOOL, OFFSET, COMP (language 4.4).
    /// </summary>
    Tool,

    /// <summary>
    /// Spindle: SPINDLE, RPM, ORIENT (language 4.5).
    /// </summary>
    Spindle,

    /// <summary>
    /// Coolant and machine functions: COOLANT, FUNC, MFUNC (language 4.6).
    /// </summary>
    Function,

    /// <summary>
    /// Cycles: CYCLE, DEPTH, CYCLE_CALL, CONTOUR (language 4.7).
    /// </summary>
    Cycle,

    /// <summary>
    /// Channels and synchronization: SYNC, WITH, START_CHANNEL (language 4.8).
    /// </summary>
    Channel,

    /// <summary>
    /// Variables and control flow: VAR, LABEL, JUMP, CALL (language 4.9).
    /// </summary>
    Flow,

    /// <summary>
    /// Lathe words: CSS, VC, RPM_MAX (language 4.11).
    /// </summary>
    Lathe,

    /// <summary>
    /// Resources and roles: WORKPIECE (language 4.10).
    /// </summary>
    Resource,

    /// <summary>
    /// The pseudo-words @SAVE and @RESTORE of generated blocks (language 4.15, D95).
    /// </summary>
    Pseudo,
}
