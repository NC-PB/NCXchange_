namespace Ncx.Compilers.Siemens;

/// <summary>
/// The main line of a SINUMERIK block being composed: the G codes and the modal keywords, then the words of the
/// addresses, then the M functions, each part in the order of its rank, so that a line reads in the recommended order
/// N G X Y Z F S T D M H of the control (controllers siemens.md 1), which the control does not require.
/// </summary>
internal sealed class SiemensLine
{
    /// <summary>
    /// The rank of the motion code G0 to G3, the first of a line: G0 G53 Z360 D0 (controller-mapping 1).
    /// </summary>
    public const int MotionRank = 0;

    /// <summary>
    /// The rank of G53, the frame suppression of one block (controllers siemens.md 2, group 9).
    /// </summary>
    public const int SuppressionRank = 1;

    /// <summary>
    /// The rank of G17 to G19 (group 6).
    /// </summary>
    public const int PlaneRank = 2;

    /// <summary>
    /// The rank of G40 to G42 (group 7).
    /// </summary>
    public const int CompensationRank = 3;

    /// <summary>
    /// The rank of G54 to G57, G505 to G599 and G500 (group 8).
    /// </summary>
    public const int DatumRank = 4;

    /// <summary>
    /// The rank of G90 (group 14).
    /// </summary>
    public const int DistanceRank = 5;

    /// <summary>
    /// The rank of G94 and G95 (group 15).
    /// </summary>
    public const int FeedTypeRank = 6;

    /// <summary>
    /// The rank of G70 and G71 (group 13).
    /// </summary>
    public const int UnitsRank = 7;

    /// <summary>
    /// The rank of DIAMON and DIAMOF (group 29) and the other modal keywords.
    /// </summary>
    public const int KeywordRank = 8;

    /// <summary>
    /// The rank of the axis words.
    /// </summary>
    public const int AxisRank = 0;

    /// <summary>
    /// The rank of the arc words I J K, CR, AR and TURN.
    /// </summary>
    public const int ArcRank = 1;

    /// <summary>
    /// The rank of the orientation words A3 B3 C3 and A5 B5 C5.
    /// </summary>
    public const int VectorRank = 2;

    /// <summary>
    /// The rank of F.
    /// </summary>
    public const int FeedRank = 3;

    /// <summary>
    /// The rank of the spindle words, S, S2=, LIMS=, SPOS=.
    /// </summary>
    public const int SpindleRank = 4;

    /// <summary>
    /// The rank of D.
    /// </summary>
    public const int OffsetRank = 5;

    /// <summary>
    /// The rank of the variable assignments, R1=5.
    /// </summary>
    public const int VariableRank = 6;

    private readonly List<KeyValuePair<int, string>> _codes = [];
    private readonly List<KeyValuePair<int, string>> _words = [];
    private readonly List<string> _functions = [];

    /// <summary>
    /// True when nothing stands in the line.
    /// </summary>
    public bool IsEmpty => _codes.Count == 0 && _words.Count == 0 && _functions.Count == 0;

    /// <summary>
    /// Adds a G code or a modal keyword at its rank: G1, G53, DIAMON.
    /// </summary>
    public void Code(int rank, string code)
    {
        _codes.Add(new KeyValuePair<int, string>(rank, code));
    }

    /// <summary>
    /// Adds a word of an address at its rank: X42, Z2=-58, CR=5, F0.2.
    /// </summary>
    public void Word(int rank, string word)
    {
        _words.Add(new KeyValuePair<int, string>(rank, word));
    }

    /// <summary>
    /// Adds an M function or the words of a function template, in the order they come: M3, M2=70, M68.
    /// </summary>
    public void Function(string function)
    {
        _functions.Add(function);
    }

    /// <summary>
    /// True when the line holds this G code or word already, so that a template that writes it again adds it once.
    /// </summary>
    public bool Holds(string text)
    {
        foreach (KeyValuePair<int, string> code in _codes)
        {
            if (code.Value == text)
            {
                return true;
            }
        }

        foreach (KeyValuePair<int, string> word in _words)
        {
            if (word.Value == text)
            {
                return true;
            }
        }

        return _functions.Contains(text);
    }

    /// <summary>
    /// The text of the line, the parts separated by one blank.
    /// </summary>
    public string Text()
    {
        var parts = new List<string>();
        AddInRankOrder(parts, _codes);
        AddInRankOrder(parts, _words);
        parts.AddRange(_functions);
        return string.Join(" ", parts);
    }

    // The entries by their rank, of one rank in the order they were added.
    private static void AddInRankOrder(List<string> parts, List<KeyValuePair<int, string>> entries)
    {
        var ranks = new SortedSet<int>();
        foreach (KeyValuePair<int, string> entry in entries)
        {
            ranks.Add(entry.Key);
        }

        foreach (int rank in ranks)
        {
            foreach (KeyValuePair<int, string> entry in entries)
            {
                if (entry.Key == rank)
                {
                    parts.Add(entry.Value);
                }
            }
        }
    }
}
