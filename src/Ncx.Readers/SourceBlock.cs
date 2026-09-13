namespace Ncx.Readers;

/// <summary>
/// One block of a controller file as its tokenizer read it: the line, the text, the words, the comment and the block
/// skip (architecture 7). A reader rule of a plugin sees the blocks in this form (D106).
/// </summary>
public sealed record SourceBlock
{
    /// <summary>
    /// The 1-based line of the block in its file, which the NCX blocks read from it and their diagnostics cite (D98).
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// The first line of the block as read, without its line ending; RAW keeps it verbatim (D5, language 4.1).
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// The words of the block in source order; empty for a blank or comment-only line, which is trivia (D92).
    /// </summary>
    public IReadOnlyList<SourceWord> Words { get; init; } = [];

    /// <summary>
    /// The comment of the block without the controller's delimiters and trimmed, "SIDE MILL D10" of Fanuc
    /// "(SIDE MILL D10)" or Heidenhain "; SIDE MILL D10"; null for a block without one (controller-mapping 1).
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// True when the block carries the optional block skip, a slash at the block start (language 4.1, SKIP;
    /// controller-mapping 1).
    /// </summary>
    public bool BlockSkip { get; init; }

    /// <summary>
    /// The switch n of a block skip written /n, 1 to 9; null for a bare slash and for a block without one (language
    /// 4.1, SKIP).
    /// </summary>
    public int? SkipSwitch { get; init; }

    /// <summary>
    /// The further lines of a block the controller continues over several lines (Heidenhain ~), as read, in order; they
    /// stand on the lines after Line. Empty for a block of one line.
    /// </summary>
    public IReadOnlyList<string> Continuation { get; init; } = [];

    /// <summary>
    /// True for a blank or comment-only line: no words and no block skip, trivia rather than a block (D92).
    /// </summary>
    public bool IsTrivia => Words.Count == 0 && !BlockSkip;

    /// <summary>
    /// Finds the first word with this address in source order.
    /// </summary>
    /// <param name="address">The address, "M".</param>
    /// <returns>The word, or null when the block has none.</returns>
    public SourceWord? Find(string address)
    {
        foreach (SourceWord word in Words)
        {
            if (word.Address == address)
            {
                return word;
            }
        }

        return null;
    }
}
