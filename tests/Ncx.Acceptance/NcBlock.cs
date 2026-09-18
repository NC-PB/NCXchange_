namespace Ncx.Acceptance;

/// <summary>
/// One block of an NC program as the comparison rules of phase 3 compare it (implementation 13, "Comparison rules").
/// </summary>
/// <param name="Line">The line of the file the block starts on, which a difference names.</param>
/// <param name="Text">The block without its block number and its comments, its blanks made one, every number
/// formatted with the decimals of its address.</param>
/// <param name="IsHeader">True for a line of the header block: the program start (%, O, BEGIN PGM) and the lines of G
/// codes alone that follow it, which are compared as a set of words.</param>
internal sealed record NcBlock(int Line, string Text, bool IsHeader);
