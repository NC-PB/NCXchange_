namespace Ncx.Readers;

/// <summary>
/// The three things a reader does in the order the structure pass lays out: keep a trivia line, read a source block,
/// write a block of the file structure.
/// </summary>
internal enum ReadStepKind
{
    /// <summary>
    /// A blank or comment-only source line, kept as trivia in its place (D92).
    /// </summary>
    Trivia,

    /// <summary>
    /// A source block read by the reader rules and the reader of the controller family (architecture 7).
    /// </summary>
    Read,

    /// <summary>
    /// A block of the file structure the structure pass writes: FILE, PROGRAM, SUB, LABEL, JUMP, RETURN (language
    /// 4.13).
    /// </summary>
    Write,
}
