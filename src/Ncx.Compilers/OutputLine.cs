using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// One line of the output with the NCX block it was written for, which a diagnostic about the line cites (D98).
/// </summary>
/// <param name="Text">The line in the syntax of the controller, without block number and line ending.</param>
/// <param name="Block">The block the line was written for.</param>
internal sealed record OutputLine(string Text, Block Block);
