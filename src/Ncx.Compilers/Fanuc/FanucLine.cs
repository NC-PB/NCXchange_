namespace Ncx.Compilers.Fanuc;

// TODO(question): the documents fix no order of the words in a Fanuc block, and the control executes them together
// (controllers fanuc.md 1); fanuc 10 rule 2 and controller-mapping 4 write M3 S1000 and M88 S1000, the sources S1592 M3
// and M29 S500. A block is written as the sources write theirs, which the round trips of phase 3 compare word for
// word: the modal codes by group, the motion code first, the one-shot codes last (G0 G17 X50.4, G91 G28 Z0); the axis
// words; I J K or R; the cycle words; H, D and F; then the M codes of the named functions, S with the M code of its
// spindle, the coolant and the other M codes (M29 S500, S1592 M3).

/// <summary>
/// One line of a Fanuc program being composed: the G codes in the order of their groups, the address words in the
/// order they are added, then the M codes and the S (controllers fanuc.md 1, 3).
/// </summary>
internal sealed class FanucLine
{
    private readonly List<KeyValuePair<int, string>> _codes = [];
    private readonly List<string> _words = [];
    private readonly List<string> _functions = [];

    /// <summary>
    /// True while nothing is added.
    /// </summary>
    public bool IsEmpty => _codes.Count == 0 && _words.Count == 0 && _functions.Count == 0;

    /// <summary>
    /// The words of the functions, M codes and S, in their order.
    /// </summary>
    public IReadOnlyList<string> Functions => _functions;

    /// <summary>
    /// Adds a G code at the place of its group; codes of one place keep the order they are added in.
    /// </summary>
    /// <param name="rank">The place of the group, FanucCodes.RankOf.</param>
    /// <param name="code">The code, "G17", or a code with its words, "G54.1 P1".</param>
    public void Code(int rank, string code)
    {
        int index = _codes.Count;
        while (index > 0 && _codes[index - 1].Key > rank)
        {
            index--;
        }

        _codes.Insert(index, new KeyValuePair<int, string>(rank, code));
    }

    /// <summary>
    /// True when a code is added already.
    /// </summary>
    public bool HasCode(string code)
    {
        foreach (KeyValuePair<int, string> added in _codes)
        {
            if (added.Value == code)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Adds an address word, X50.4, or a text of several words, G28 Z0, after the codes.
    /// </summary>
    public void Word(string word)
    {
        _words.Add(word);
    }

    /// <summary>
    /// Adds the words of a function, M8, S1592 M3, after the address words.
    /// </summary>
    public void Function(string words)
    {
        _functions.Add(words);
    }

    /// <summary>
    /// The line: codes, address words, functions, separated by one space.
    /// </summary>
    public string Text()
    {
        var parts = new List<string>();
        foreach (KeyValuePair<int, string> code in _codes)
        {
            parts.Add(code.Value);
        }

        parts.AddRange(_words);
        parts.AddRange(_functions);
        return string.Join(" ", parts);
    }
}
