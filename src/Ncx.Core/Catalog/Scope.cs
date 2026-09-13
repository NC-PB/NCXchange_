namespace Ncx.Core.Catalog;

/// <summary>
/// How long a word applies, the Scope column of the tables of language 4 (architecture 4). The column says "file",
/// "program", "header", "block", "modal" with its qualifiers ("until consumed", "per channel", "part of the chain"),
/// names a partner word ("with CYCLE"), or is empty.
/// </summary>
public enum Scope
{
    /// <summary>
    /// The Scope column of language 4 leaves the row empty: VAR, LABEL (language 4.9).
    /// </summary>
    None,

    /// <summary>
    /// A word of the file: FILE, NCX, SUB (language 4.1, 4.9).
    /// </summary>
    File,

    /// <summary>
    /// A word of the program: PROGRAM=BEGIN, PROGRAM=END (language 4.1).
    /// </summary>
    Program,

    /// <summary>
    /// A header word of a program, after PROGRAM=BEGIN: CHANNEL (language 4.1).
    /// </summary>
    Header,

    /// <summary>
    /// The word applies to its block only: DWELL, COMMENT (language 4).
    /// </summary>
    Block,

    /// <summary>
    /// The value stays until changed: F, UNITS, SPINDLE; PRELOAD until a change consumes it, COOLANT per channel,
    /// SHIFT as part of the frame chain (language 4).
    /// </summary>
    Modal,

    /// <summary>
    /// The word belongs to a partner word of its block and applies as long as that word does; the Scope column names
    /// the partner: the cycle words with CYCLE, NUMBER with PROGRAM=BEGIN, PHASE with SPINDLE_SYNC (language 4.1,
    /// 4.5, 4.7).
    /// </summary>
    WithPartner,
}
