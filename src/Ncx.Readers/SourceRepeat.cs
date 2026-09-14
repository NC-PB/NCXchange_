namespace Ncx.Readers;

/// <summary>
/// The block range a repeat names by the labels of its first and its last block, which stand before it in its section:
/// "START" and "END" of a SINUMERIK REPEAT START END P=3 whose range does not end directly before the repeat
/// (controller-mapping 6, REPEAT + TIMES: a block range, lowered to a SUB; controllers siemens.md 11 rule 6).
/// </summary>
/// <param name="First">The label of the first block of the range.</param>
/// <param name="Last">The label of the last block of the range; the first label again for a range of one block.</param>
public sealed record SourceRepeat(string First, string Last);
