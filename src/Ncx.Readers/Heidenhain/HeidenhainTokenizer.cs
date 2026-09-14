using System.Text.RegularExpressions;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// Cuts a Klartext program into source blocks (controllers heidenhain.md 1, 7 rule 8): the block number the editor
/// writes in front of every block, the ~ that continues a block on the next line, the ; that starts a comment, the
/// structuring block * - title, the / of the optional skip, the comma or the dot as the decimal separator. The words of
/// a block are its tokens: an address with its value (X+10, IX-5, Q200=5, DR-, FMAX), a number (200 of CYCL DEF 200), a
/// quoted name, a group in parentheses; the assignment of a formula or of FN 0 to FN 5 is one word, the parameter with
/// its right side.
/// </summary>
internal sealed partial class HeidenhainTokenizer : ISourceTokenizer
{
    // The content of every block, its words as text without the block number, the skip and the comments, by line.
    private readonly Dictionary<int, string> _contents = [];
    private List<SourceBlock> _blocks = [];

    /// <summary>
    /// The blocks of the file cut last, which the reader classifies as a whole before it reads them (controllers
    /// heidenhain.md 7 rule 3).
    /// </summary>
    public IReadOnlyList<SourceBlock> Blocks => _blocks;

    /// <summary>
    /// Cuts the whole file; every line is a block in file order, a blank or comment-only line a block without words,
    /// and a line that ends with ~ takes the next line into its block (controllers heidenhain.md 1).
    /// </summary>
    /// <param name="text">The whole text, line endings as in the file.</param>
    public IEnumerable<SourceBlock> Tokenize(string text)
    {
        _contents.Clear();
        var blocks = new List<SourceBlock>();
        List<string> lines = LinesOf(text);
        int index = 0;
        while (index < lines.Count)
        {
            int first = index;
            var continuation = new List<string>();
            while (Continues(lines[index]) && index + 1 < lines.Count)
            {
                index++;
                continuation.Add(lines[index]);
            }

            blocks.Add(ReadBlock(lines[first], continuation, first + 1));
            index++;
        }

        _blocks = blocks;
        return blocks;
    }

    /// <summary>
    /// The content of a block as the words stand in it, without its block number, its skip and its comments, the
    /// continued lines joined with a blank: "CYCL DEF 247 INIT. REF.PKT Q339=1".
    /// </summary>
    /// <param name="block">A block of the file cut last.</param>
    public string ContentOf(SourceBlock block)
    {
        return _contents.TryGetValue(block.Line, out string? content) ? content : "";
    }

    private SourceBlock ReadBlock(string first, List<string> continuation, int line)
    {
        // The block number and the / of the optional skip stand in front of the block (controllers heidenhain.md 1).
        string head = WithoutContinuation(first);
        int position = SkipBlanks(head, 0);
        bool blockSkip = false;
        if (position < head.Length && head[position] == '/')
        {
            blockSkip = true;
            position = SkipBlanks(head, position + 1);
        }

        int digits = position;
        while (digits < head.Length && char.IsAsciiDigit(head[digits]))
        {
            digits++;
        }

        if (digits > position && (digits == head.Length || head[digits] is ' ' or '\t' or '/'))
        {
            position = SkipBlanks(head, digits);
        }

        if (position < head.Length && head[position] == '/')
        {
            blockSkip = true;
            position = SkipBlanks(head, position + 1);
        }

        string rest = head.Substring(position);

        // * - title is a structuring block shown in the program outline; the whole line is its title (controllers
        // heidenhain.md 1).
        if (rest.StartsWith('*'))
        {
            string title = rest.Substring(1).TrimStart();
            title = (title.StartsWith('-') ? title.Substring(1) : title).Trim();
            _contents[line] = rest;
            return new SourceBlock
            {
                Line = line,
                Text = first,
                Words = [new SourceWord { Address = "*", Text = title }],
                BlockSkip = blockSkip,
                Continuation = continuation,
            };
        }

        // ; starts a comment at the end of a block or of a continued line; the comments of one block are one comment,
        // their texts in order (controllers heidenhain.md 1; controller-mapping 1).
        var comments = new List<string>();
        string content = WithoutComment(rest, comments);
        foreach (string next in continuation)
        {
            content += " " + WithoutComment(WithoutContinuation(next), comments);
        }

        content = content.Trim();
        _contents[line] = content;
        return new SourceBlock
        {
            Line = line,
            Text = first,
            Words = WordsOf(content),
            Comment = comments.Count == 0 ? null : string.Join(" ", comments),
            BlockSkip = blockSkip,
            Continuation = continuation,
        };
    }

    // FN n: opens the FN functions, and Q5 = ... at the start of a block is an assignment whose right side is one word
    // (controllers heidenhain.md 6); every other word is a token of its own.
    private static List<SourceWord> WordsOf(string content)
    {
        var words = new List<SourceWord>();
        Match function = FunctionHead().Match(content);
        if (function.Success)
        {
            string number = function.Groups[1].Value;
            words.Add(new SourceWord { Address = "FN", Text = number, Number = HeidenhainNumbers.Parse(number) });
            content = content.Substring(function.Length);
        }

        Match assignment = Assignment().Match(content);
        if (assignment.Success)
        {
            words.Add(ValueWord(assignment.Groups[1].Value.ToUpperInvariant(), assignment.Groups[2].Value.Trim()));
            return words;
        }

        foreach (string token in TokensOf(content))
        {
            words.Add(WordOf(token));
        }

        return words;
    }

    // An address with its value: the letters in front, or the name before an equals sign, Q200=5.
    private static SourceWord WordOf(string token)
    {
        if (token.StartsWith('"') || token.StartsWith('('))
        {
            return new SourceWord { Address = "", Text = token };
        }

        int equals = token.IndexOf('=', StringComparison.Ordinal);
        if (equals > 0)
        {
            return ValueWord(token.Substring(0, equals).ToUpperInvariant(), token.Substring(equals + 1));
        }

        int letters = 0;
        while (letters < token.Length && char.IsAsciiLetter(token[letters]))
        {
            letters++;
        }

        return ValueWord(token.Substring(0, letters).ToUpperInvariant(), token.Substring(letters));
    }

    // The value is a number when it reads as one, and the expression as written when it holds a letter: +Q1, MAX.
    private static SourceWord ValueWord(string address, string text)
    {
        bool hasLetter = false;
        foreach (char character in text)
        {
            hasLetter |= char.IsAsciiLetter(character);
        }

        decimal? number = hasLetter ? null : HeidenhainNumbers.Parse(text);
        return new SourceWord
        {
            Address = address,
            Text = text,
            Number = number,
            Expression = number is null && hasLetter ? text : null,
        };
    }

    // The tokens are separated by blanks; a quoted name and a group in parentheses are one token each.
    private static List<string> TokensOf(string content)
    {
        var tokens = new List<string>();
        int position = 0;
        while (position < content.Length)
        {
            position = SkipBlanks(content, position);
            if (position >= content.Length)
            {
                break;
            }

            int start = position;
            int depth = 0;
            bool quoted = false;
            while (position < content.Length && (quoted || depth > 0 || content[position] is not (' ' or '\t')))
            {
                char character = content[position];
                quoted = character == '"' ? !quoted : quoted;
                depth += !quoted && character == '(' ? 1 : 0;
                depth -= !quoted && character == ')' && depth > 0 ? 1 : 0;
                position++;
            }

            tokens.Add(content.Substring(start, position - start));
        }

        return tokens;
    }

    // The text before the first ; outside a quoted name; the comment after it is kept.
    private static string WithoutComment(string text, List<string> comments)
    {
        bool quoted = false;
        for (int index = 0; index < text.Length; index++)
        {
            quoted = text[index] == '"' ? !quoted : quoted;
            if (!quoted && text[index] == ';')
            {
                string comment = text.Substring(index + 1).Trim();
                if (comment.Length > 0)
                {
                    comments.Add(comment);
                }

                return text.Substring(0, index);
            }
        }

        return text;
    }

    // ~ at the end of a line continues the block on the next line (controllers heidenhain.md 1).
    private static bool Continues(string line)
    {
        return line.TrimEnd().EndsWith('~');
    }

    private static string WithoutContinuation(string line)
    {
        string trimmed = line.TrimEnd();
        return trimmed.EndsWith('~') ? trimmed.Substring(0, trimmed.Length - 1).TrimEnd() : trimmed;
    }

    private static int SkipBlanks(string text, int position)
    {
        while (position < text.Length && text[position] is ' ' or '\t')
        {
            position++;
        }

        return position;
    }

    // Every line of the file, CR LF or LF, without its line ending.
    private static List<string> LinesOf(string text)
    {
        string[] split = text.Split('\n');
        int count = text.EndsWith('\n') ? split.Length - 1 : split.Length;
        var lines = new List<string>(count);
        for (int index = 0; index < count; index++)
        {
            string line = split[index];
            lines.Add(line.EndsWith('\r') ? line.Substring(0, line.Length - 1) : line);
        }

        return lines;
    }

    // FN 9: in front of the FN functions (controllers heidenhain.md 6).
    [GeneratedRegex(@"^FN\s*([0-9]+)\s*:\s*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex FunctionHead();

    // Q5 = +10, QL3 = Q1 * 2: a parameter and the right side of its assignment (controllers heidenhain.md 6).
    [GeneratedRegex(@"^(Q[LRS]?[0-9]+)\s*=\s*(.*)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Assignment();
}
