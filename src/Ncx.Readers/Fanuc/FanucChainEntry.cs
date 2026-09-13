using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// One entry of the chain of transforms the reader wrote (language 4.2, D31): its kind, SHIFT, ROTATE, MIRROR or TILT,
/// the Fanuc function it stands for, G52, G68, G51.1 or G68.2, and its words as written. A SHIFT that holds a G52 shift
/// and the origin of a G68.2 added together keeps the words of the G52 shift alone, which the chain holds again after
/// the G69 (FanucTilt, FanucFrames).
/// </summary>
/// <param name="Kind">SHIFT, ROTATE, MIRROR or TILT.</param>
/// <param name="Owner">The Fanuc function: "G52", "G68", "G51.1" or "G68.2".</param>
/// <param name="Words">The axis words of SHIFT and TILT; the ROTATE or MIRROR word itself.</param>
/// <param name="LocalShift">The words of the G52 shift in the SHIFT of a G68.2; null otherwise.</param>
internal sealed record FanucChainEntry(string Kind, string Owner, IReadOnlyList<Word> Words,
    IReadOnlyList<Word>? LocalShift = null)
{
    /// <summary>
    /// The NCX block that appends the entry: the verb SHIFT or TILT with its axis words, or the word ROTATE=30 or
    /// MIRROR=X (language 4.2).
    /// </summary>
    public DraftBlock ToDraft()
    {
        var draft = new DraftBlock();
        if (Kind is "SHIFT" or "TILT")
        {
            draft.WithVerb(Kind);
        }

        foreach (Word word in Words)
        {
            draft.Add(word.Key, word.Addr, word.Value);
        }

        return draft;
    }

    /// <summary>
    /// The entry as text, "G52 SHIFT X=5", for comparing the chains two callers leave.
    /// </summary>
    public string ToKey()
    {
        string key = Owner + " " + Kind + " " + Canonical(Words);
        return LocalShift is null ? key : key + " / " + Canonical(LocalShift);
    }

    private static string Canonical(IReadOnlyList<Word> words)
    {
        var texts = new List<string>(words.Count);
        foreach (Word word in words)
        {
            texts.Add(word.ToCanonical());
        }

        return string.Join(" ", texts);
    }
}
