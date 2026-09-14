using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// A cycle as the reader wrote its CYCLE block (controllers siemens.md 7; language 4.7): the words of the block, the
/// drilling axis, and the plane the cycle returns to, where the drilling axis stands after each call (virtual machine
/// 3.3). MCALL keeps it modal until MCALL alone.
/// </summary>
internal sealed record SiemensCycle
{
    /// <summary>
    /// The native cycle as the source names it, "CYCLE81".
    /// </summary>
    public required string Native { get; init; }

    /// <summary>
    /// The words of the CYCLE block, CYCLE=DRILL and its parameters.
    /// </summary>
    public required IReadOnlyList<Word> Words { get; init; }

    /// <summary>
    /// The drilling axis, "Z" under G17.
    /// </summary>
    public required string Axis { get; init; }

    /// <summary>
    /// Where the drilling axis stands after a call, RTP; null when the reader does not know it.
    /// </summary>
    public decimal? ReturnPlane { get; init; }

    /// <summary>
    /// The words as text, to tell whether two callers leave the same cycle.
    /// </summary>
    public string ToText()
    {
        var words = new List<string>();
        foreach (Word word in Words)
        {
            words.Add(word.ToCanonical());
        }

        return string.Join(" ", words);
    }
}
