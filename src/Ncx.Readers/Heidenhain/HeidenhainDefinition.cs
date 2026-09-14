using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The machining cycle a CYCL DEF defines, which stays active until the next CYCL DEF and which CYCL CALL, M99 and
/// CYCL CALL PAT call (controllers heidenhain.md 5): the words of its CYCLE block, or the reason it is kept as RAW, in
/// which case its calls stay RAW too (D5).
/// </summary>
internal sealed record HeidenhainDefinition
{
    /// <summary>
    /// The cycle number, "200" of CYCL DEF 200, "12" of CYCL DEF 12.0; for a CYCL DEF whose cycle number the reader
    /// cannot read, the word after CYCL DEF as written, empty where there is none.
    /// </summary>
    public required string Native { get; init; }

    /// <summary>
    /// The words of the CYCLE block, the cycle word first (language 4.7, 4.7.1); empty for a definition kept as RAW.
    /// </summary>
    public IReadOnlyList<Word> Words { get; init; } = [];

    /// <summary>
    /// Why the definition is kept as RAW; null for one the reader writes.
    /// </summary>
    public string? RawReason { get; init; }
}
