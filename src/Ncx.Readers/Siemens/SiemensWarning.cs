namespace Ncx.Readers.Siemens;

/// <summary>
/// A WARNING that says what the NCX blocks of a source block hold, reported when the reader writes them (D98).
/// </summary>
/// <param name="Code">The RDR code.</param>
/// <param name="Message">The message, naming the rule and the words.</param>
internal sealed record SiemensWarning(string Code, string Message);
