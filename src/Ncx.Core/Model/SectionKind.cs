namespace Ncx.Core.Model;

/// <summary>
/// The two kinds of section of a file (language 4.13).
/// </summary>
public enum SectionKind
{
    /// <summary>
    /// A program, PROGRAM=BEGIN ... PROGRAM=END.
    /// </summary>
    Program,

    /// <summary>
    /// A subprogram, SUB=BEGIN NAME= ... SUB=END.
    /// </summary>
    Sub,
}
