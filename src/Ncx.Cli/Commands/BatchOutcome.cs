namespace Ncx.Cli.Commands;

/// <summary>
/// What became of one file of ncx convert --batch (implementation 13, P3-07).
/// </summary>
internal enum BatchOutcome
{
    /// <summary>
    /// The reader read it and the check ran, whatever they reported.
    /// </summary>
    Converted,

    /// <summary>
    /// The file could not be read (CLI002).
    /// </summary>
    Unreadable,

    /// <summary>
    /// The conversion stopped with an exception, a bug of ncx (CLI351; controllers sample-corpus 3).
    /// </summary>
    Crashed,
}
