using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// One NCX block that a SINUMERIK block reads into, before the reader writes it: its verb and its words in the order
/// the reader found them; the builder sorts them into canonical order (language 5 rule 6).
/// </summary>
internal sealed class SiemensDraftBlock
{
    /// <summary>
    /// The verb, RAPID, LINE, ARC, RETRACT, HOME, CYCLE_CALL, SHIFT, TILT, TILT_AXIS, SETPOS; null for a block of state
    /// words.
    /// </summary>
    public string? Verb { get; private set; }

    /// <summary>
    /// The value of the verb, CW of ARC=CW; NoValue for the verbs without one.
    /// </summary>
    public Value VerbValue { get; private set; } = NoValue.Instance;

    /// <summary>
    /// The words other than the verb.
    /// </summary>
    public List<Word> Words { get; } = [];

    /// <summary>
    /// True while the block holds nothing to write.
    /// </summary>
    public bool IsEmpty => Verb is null && Words.Count == 0;

    /// <summary>
    /// Sets the verb of the block, a block has at most one (language 5 rule 1).
    /// </summary>
    /// <param name="verb">The verb, "LINE".</param>
    public SiemensDraftBlock WithVerb(string verb)
    {
        return WithVerb(verb, NoValue.Instance);
    }

    /// <summary>
    /// Sets the verb of the block with its value, ARC=CW (language 4.3).
    /// </summary>
    /// <param name="verb">The verb, "ARC".</param>
    /// <param name="value">Its value.</param>
    public SiemensDraftBlock WithVerb(string verb, Value value)
    {
        Verb = verb;
        VerbValue = value;
        return this;
    }

    /// <summary>
    /// Adds a word without an address.
    /// </summary>
    /// <param name="key">The key, "F".</param>
    /// <param name="value">The value.</param>
    public SiemensDraftBlock Add(string key, Value value)
    {
        return Add(key, null, value);
    }

    /// <summary>
    /// Adds a word.
    /// </summary>
    /// <param name="key">The key, "OFFSET".</param>
    /// <param name="addr">The address; null for none.</param>
    /// <param name="value">The value; NoValue for a bare word.</param>
    public SiemensDraftBlock Add(string key, string? addr, Value value)
    {
        Words.Add(new Word { Key = key, Addr = addr, Value = value });
        return this;
    }

    /// <summary>
    /// Removes the words of a key without an address: the plane words of a line that a chamfer or a rounding shortens
    /// (D58).
    /// </summary>
    /// <param name="key">The key, "X".</param>
    public void Remove(string key)
    {
        Words.RemoveAll(word => word.Key == key && word.Addr is null);
    }

    /// <summary>
    /// Tells whether the block holds a word of this key and address.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="addr">The address; null for the word without one.</param>
    public bool Has(string key, string? addr)
    {
        return Find(key, addr) is not null;
    }

    /// <summary>
    /// The word of this key and address; null when the block has none.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="addr">The address; null for the word without one.</param>
    public Word? Find(string key, string? addr)
    {
        foreach (Word word in Words)
        {
            if (word.Key == key && word.Addr == addr)
            {
                return word;
            }
        }

        return null;
    }

    /// <summary>
    /// The block as its words, the verb first; for comparing two blocks.
    /// </summary>
    public string ToText()
    {
        var words = new List<string>();
        if (Verb is not null)
        {
            words.Add(new Word { Key = Verb, Value = VerbValue }.ToCanonical());
        }

        foreach (Word word in Words)
        {
            words.Add(word.ToCanonical());
        }

        return string.Join(" ", words);
    }
}
