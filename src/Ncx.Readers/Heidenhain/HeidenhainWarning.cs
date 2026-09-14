namespace Ncx.Readers.Heidenhain;

/// <summary>
/// A WARNING about what the NCX blocks of a Klartext block hold, reported when the reader writes them (D98).
/// </summary>
/// <param name="Code">The code, RDR300.</param>
/// <param name="Message">The message, which names the rule and the block.</param>
internal sealed record HeidenhainWarning(string Code, string Message);
