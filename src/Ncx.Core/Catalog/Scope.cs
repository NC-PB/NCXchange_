namespace Ncx.Core.Catalog;

// TODO(question): the Scope column of language 4.1 gives PROGRAM=BEGIN and PROGRAM=END the scope "program", which
// the Scope of architecture 4 does not list, and several rows name a partner word ("with CYCLE") or no scope at all.
// The enumeration keeps the four scopes of architecture 4 until the word catalog, which records these rows, settles
// it.

/// <summary>
/// How long a word applies, the Scope column of the tables of language 4 (architecture 4).
/// </summary>
public enum Scope
{
    /// <summary>
    /// A word of the file: FILE=BEGIN, NCX, FILE=END, SUB=BEGIN (language 4.1, 4.9).
    /// </summary>
    File,

    /// <summary>
    /// A header word of a program, after PROGRAM=BEGIN: CHANNEL (language 4.1).
    /// </summary>
    Header,

    /// <summary>
    /// The word applies to its block only: DWELL, COMMENT (language 4).
    /// </summary>
    Block,

    /// <summary>
    /// The value stays until changed: F, UNITS, SPINDLE (language 4).
    /// </summary>
    Modal,
}
