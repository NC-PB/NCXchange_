namespace Ncx.Core.Model;

/// <summary>
/// The line ending of an NCX file: LF or CRLF (language 3, Encoding).
/// </summary>
public enum LineEnding
{
    /// <summary>
    /// LF, a line feed.
    /// </summary>
    Lf,

    /// <summary>
    /// CRLF, a carriage return and a line feed.
    /// </summary>
    CrLf,
}
