namespace Ncx.Core.Model;

/// <summary>
/// A line of an NCX file that holds words: the words in the order they were written, the trailing comment, and where
/// the block came from (language 3, architecture 4). A block never sorts its words; the canonical writer does.
/// </summary>
public sealed record Block
{
    // The word of the optional block skip (language 4.1).
    private const string SkipKey = "SKIP";

    /// <summary>
    /// The 1-based line of the block in its file, which diagnostics cite (D98).
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// The words of the block in the order they were written (language 3, Word).
    /// </summary>
    public required IReadOnlyList<Word> Words { get; init; }

    /// <summary>
    /// The verb of the block, one of its words; null for a block without a verb. A block has at most one verb
    /// (language 5 rule 1), and the parser decides which word it is.
    /// </summary>
    public Word? Verb { get; init; }

    /// <summary>
    /// The trailing comment as language 3 defines a comment, from the semicolon to the end of the line, the semicolon
    /// included, as read; null for a block without one (D92).
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// The text the block was read from, as read; null when it was not kept (architecture 4.1).
    /// </summary>
    public string? SourceText { get; init; }

    /// <summary>
    /// True for a block the expander inserted before or after the block it was generated for: the virtual machine
    /// executes it, ncx format never writes it (language 4.15).
    /// </summary>
    public bool IsGenerated { get; init; }

    /// <summary>
    /// For a generated block, the line of the block it was generated for, which every diagnostic on the generated
    /// block carries (language 4.15, D98); null for a block of the file.
    /// </summary>
    public int? OriginLine { get; init; }

    /// <summary>
    /// For a generated block, its origin: the block it was generated for, the rule or rewriter that made it, the
    /// reason, and whether it stands before, in place of or after that block (virtual machine 3.10); null for a block
    /// of the file.
    /// </summary>
    public GeneratedBlock? Generated { get; init; }

    /// <summary>
    /// True when the block carries SKIP, the optional block skip (language 4.1).
    /// </summary>
    public bool Skip => Has(SkipKey);

    /// <summary>
    /// The number n of the block skip switch of SKIP=n, 1 to 9 (language 4.1); null for a bare SKIP, for a block
    /// without SKIP, and for a number outside 1 to 9, which the parser reports.
    /// </summary>
    public int? SkipNumber
    {
        get
        {
            // SKIP takes no value or an integer 1 to 9, the number of the block skip switch (language 4.1).
            if (Find(SkipKey)?.Value is IntegerValue switchNumber && switchNumber.Number is >= 1 and <= 9)
            {
                return (int)switchNumber.Number;
            }

            return null;
        }
    }

    /// <summary>
    /// Finds the first word with this key, whatever its address, in the order the words were written.
    /// </summary>
    /// <param name="key">The key, uppercase: "TOOL".</param>
    /// <returns>The word, or null when the block has no word with this key.</returns>
    public Word? Find(string key)
    {
        foreach (Word word in Words)
        {
            if (word.Key == key)
            {
                return word;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the word with this key and this address: OFFSET:LEN and OFFSET:RAD are two words, and a valid block
    /// holds each at most once (language 5 rule 4).
    /// </summary>
    /// <param name="key">The key, uppercase: "OFFSET".</param>
    /// <param name="addr">The address, uppercase: "LEN"; null for the word without an address.</param>
    /// <returns>The word, or null when the block has no such word.</returns>
    public Word? Find(string key, string? addr)
    {
        // Keys with different addresses are different words (language 5 rule 4).
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
    /// Tells whether the block has a word with this key, whatever its address.
    /// </summary>
    /// <param name="key">The key, uppercase: "COOLANT".</param>
    public bool Has(string key)
    {
        return Find(key) is not null;
    }

    /// <summary>
    /// Tells whether the block has the word with this key and address and this value, the value compared as
    /// canonical NCX writes it after the equals sign: Has("COOLANT", "THROUGH", "ON") for COOLANT:THROUGH=ON.
    /// </summary>
    /// <param name="key">The key, uppercase: "COOLANT".</param>
    /// <param name="addr">The address, uppercase: "THROUGH"; null for the word without an address.</param>
    /// <param name="value">The value as written after the equals sign: "ON", "4", "10.5"; empty for a bare word.</param>
    public bool Has(string key, string? addr, string value)
    {
        return Find(key, addr) is Word word && word.Value.ToCanonical() == value;
    }
}
