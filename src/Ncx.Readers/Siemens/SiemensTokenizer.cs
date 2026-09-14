using System.Globalization;
using System.Text;

namespace Ncx.Readers.Siemens;

/// <summary>
/// Cuts a SINUMERIK 840D sl program into source blocks (controllers siemens.md 1, 8): the unit headers %_N_NAME_MPF,
/// _SPF and _INI of a job archive, the ; comments with the ;$PATH lines, the / and /n skip levels, the N numbers, the
/// labels NAME: at the block start, and the words: an address with its number (X10, G0, M30, L100, F.08), an address
/// with = and a value that may be an expression or a string (X=R1*2, CR=5, S2=1000, M2=3, T="DRILL", SPOS[2]=90), a
/// call with its argument list (CYCLE81(3,1,2,-50,), MSG("TEXT"), SETMS(2)), and a word alone (TRAORI, MCALL). A
/// statement of the high-level language, IF, WHILE, FOR, CASE, DEF, PROC, EXTERN, the jumps and the synchronized
/// actions, is one word with the rest of the block as its text. DEFINE macros are expanded before a block is cut, and
/// the recipe block of the STAMA post between its markers is kept whole.
/// </summary>
/// <remarks>
/// The words keep what the tokenizer read: the address in capitals, "N", "X", "X1", "SPOS[2]", "CYCLE81", "%" for a
/// unit header, ":" for a label with the label as its text, "&lt;" for a line of a recipe block and "?" for text that
/// is no word, which keeps its block RAW (D5).
/// </remarks>
internal sealed class SiemensTokenizer : ISourceTokenizer
{
    // The statements whose words the reader reads from the whole rest of the block (controllers siemens.md 8, 9).
    private static readonly HashSet<string> s_statements = new(StringComparer.Ordinal)
    {
        "IF", "WHILE", "UNTIL", "FOR", "CASE", "DEF", "PROC", "EXTERN", "DEFINE", "REPEAT", "REPEATB", "CALL", "PCALL",
        "GOTOF", "GOTOB", "GOTO", "GOTOC", "GOTOS", "ID", "IDS", "WHEN", "WHENEVER", "EVERY", "FROM", "DO", "SETINT",
        "ISOCALL", "CALLPATH",
    };

    private readonly Dictionary<int, List<string>> _spans = [];
    private readonly Dictionary<int, string> _contents = [];
    private readonly Dictionary<string, string> _macros = new(StringComparer.Ordinal);
    private List<SourceBlock> _blocks = [];

    /// <summary>
    /// The blocks of the file cut last, which the reader lays out as a whole before it reads them.
    /// </summary>
    public IReadOnlyList<SourceBlock> Blocks => _blocks;

    /// <summary>
    /// Cuts the whole file; every line is a block in file order, a blank or comment-only line a block without words
    /// (D92).
    /// </summary>
    /// <param name="text">The whole text, line endings as in the file.</param>
    public IEnumerable<SourceBlock> Tokenize(string text)
    {
        _spans.Clear();
        _contents.Clear();
        _macros.Clear();
        List<string> lines = LinesOf(text);
        HashSet<int> recipe = RecipeLines(lines);
        var blocks = new List<SourceBlock>(lines.Count);
        for (int index = 0; index < lines.Count; index++)
        {
            blocks.Add(recipe.Contains(index)
                ? RecipeBlock(lines[index], index + 1)
                : ReadBlock(lines[index], index + 1));
        }

        _blocks = blocks;
        return blocks;
    }

    /// <summary>
    /// The words of a block as text, without its skip, its N number, its label and its comment, macros expanded:
    /// "G1 X10 Y=R2", "IF R1==1 GOTOF END1".
    /// </summary>
    /// <param name="block">A block of the file cut last.</param>
    public string ContentOf(SourceBlock block)
    {
        return _contents.TryGetValue(block.Line, out string? content) ? content : "";
    }

    /// <summary>
    /// A word of a block as the source writes it, "X=AC(10)", "G641", "FGROUP(X,Y)"; a RAW word keeps it verbatim.
    /// </summary>
    /// <param name="block">A block of the file cut last.</param>
    /// <param name="index">The place of the word among the words of the block.</param>
    public string SpanOf(SourceBlock block, int index)
    {
        if (_spans.TryGetValue(block.Line, out List<string>? spans) && index >= 0 && index < spans.Count)
        {
            return spans[index];
        }

        SourceWord word = block.Words[index];
        return word.Address + word.Text;
    }

    private SourceBlock ReadBlock(string line, int number)
    {
        // ; starts a comment at the end of a block or a comment block (controllers siemens.md 1); a ; inside a string
        // belongs to the string.
        int comment = CommentStart(line);
        string head = comment < 0 ? line : line.Substring(0, comment);
        string? commentText = comment < 0 ? null : line.Substring(comment + 1).Trim();
        var words = new List<SourceWord>();
        var spans = new List<string>();
        int position = SiemensScanner.SkipBlanks(head, 0);

        // A unit of a job archive starts with %_N_NAME_MPF, _SPF or _INI (controllers siemens.md 1).
        if (position < head.Length && head[position] == '%')
        {
            string header = head.Substring(position + 1).Trim();
            Add(words, spans, new SourceWord { Address = "%", Text = header }, head.Substring(position).Trim());
            return Finish(line, number, words, spans, commentText, header, skip: false, skipSwitch: null);
        }

        // / or /0 skips the block on the first skip level, /1 to /9 on the others (controllers siemens.md 1).
        bool skip = false;
        int? skipSwitch = null;
        if (position < head.Length && head[position] == '/')
        {
            skip = true;
            position++;
            if (position < head.Length && char.IsAsciiDigit(head[position]))
            {
                int level = head[position] - '0';
                skipSwitch = level == 0 ? null : level;
                position++;
            }

            position = SiemensScanner.SkipBlanks(head, position);
        }

        position = ReadBlockNumber(head, position, words, spans);
        position = ReadLabel(head, position, words, spans);
        string content = ExpandMacros(head.Substring(Math.Min(position, head.Length)).Trim());
        ReadWords(content, words, spans);
        return Finish(line, number, words, spans, commentText, content, skip, skipSwitch);
    }

    // N numbers are optional and unique for the block search (controllers siemens.md 1).
    private static int ReadBlockNumber(string head, int position, List<SourceWord> words, List<string> spans)
    {
        if (position >= head.Length || head[position] is not ('N' or 'n'))
        {
            return position;
        }

        int end = position + 1;
        while (end < head.Length && char.IsAsciiDigit(head[end]))
        {
            end++;
        }

        bool boundary = end == head.Length || head[end] is ' ' or '\t';
        if (end == position + 1 || !boundary)
        {
            return position;
        }

        string digits = head.Substring(position + 1, end - position - 1);
        Add(words, spans, NumberWord("N", digits), head.Substring(position, end - position));
        return SiemensScanner.SkipBlanks(head, end);
    }

    // A label NAME: stands at the block start, after the N number (controllers siemens.md 8).
    private static int ReadLabel(string head, int position, List<SourceWord> words, List<string> spans)
    {
        int end = SiemensScanner.ReadIdentifier(head, position);
        if (end < 0 || end >= head.Length || head[end] != ':' || (end + 1 < head.Length && head[end + 1] is ':' or '='))
        {
            return position;
        }

        string label = head.Substring(position, end - position);
        Add(words, spans, new SourceWord { Address = ":", Text = label.ToUpperInvariant() }, label + ":");
        return SiemensScanner.SkipBlanks(head, end + 1);
    }

    // The words of the content; a statement of the high-level language is one word with the rest as its text.
    private static void ReadWords(string content, List<SourceWord> words, List<string> spans)
    {
        int first = SiemensScanner.ReadIdentifier(content, 0);
        if (first > 0)
        {
            string keyword = content.Substring(0, first).ToUpperInvariant();
            if (s_statements.Contains(keyword))
            {
                string rest = content.Substring(first).Trim();
                Add(words, spans, new SourceWord { Address = keyword, Text = rest, Expression = rest }, content);
                return;
            }
        }

        int position = 0;
        while (position < content.Length)
        {
            int start = SiemensScanner.SkipBlanks(content, position);
            if (start >= content.Length)
            {
                return;
            }

            int end = ReadWord(content, start, out SourceWord? word);
            if (word is null || end <= start)
            {
                // Text that is no word keeps its block RAW (D5).
                string rest = content.Substring(start);
                Add(words, spans, new SourceWord { Address = "?", Text = rest }, rest);
                return;
            }

            Add(words, spans, word, content.Substring(start, end - start).Trim());
            position = end;
        }
    }

    // One word at the position: an address with its number, an address with = and a value, a call with its argument
    // list, or a word alone; the position after it, and null for text that is no word.
    private static int ReadWord(string content, int start, out SourceWord? word)
    {
        word = null;
        int nameEnd = SiemensScanner.ReadIdentifier(content, start);
        if (nameEnd < 0)
        {
            return start;
        }

        // A single letter with its number, X10, X-10.5, F.08, G0, L0100, also with a blank between them, F 100
        // (controllers siemens.md 1).
        if (char.IsAsciiLetter(content[start]) && DinNumber(content, start + 1, out int numberEnd))
        {
            string address = content.Substring(start, 1).ToUpperInvariant();
            string digits = content.Substring(start + 1, numberEnd - start - 1).Trim();
            word = NumberWord(address, digits);
            return numberEnd;
        }

        string name = content.Substring(start, nameEnd - start).ToUpperInvariant();
        int position = nameEnd;
        if (position < content.Length && content[position] == '[')
        {
            int indexEnd = SiemensScanner.ReadGroup(content, position);
            if (indexEnd < 0)
            {
                return start;
            }

            name += content.Substring(position, indexEnd - position).ToUpperInvariant().Replace(" ", "");
            position = indexEnd;
        }

        int next = SiemensScanner.SkipBlanks(content, position);

        // = after every multi-letter address and every address with an extension: the value may be an expression
        // (controllers siemens.md 1).
        if (next < content.Length && content[next] == '=' && (next + 1 >= content.Length || content[next + 1] != '='))
        {
            int valueStart = SiemensScanner.SkipBlanks(content, next + 1);
            int valueEnd = SiemensScanner.ReadValue(content, valueStart);
            if (valueEnd < 0)
            {
                return start;
            }

            string value = content.Substring(valueStart, valueEnd - valueStart).Trim();
            word = ValueWord(name, value);
            return valueEnd;
        }

        // A call with its argument list, a blank allowed before it: MCALL CYCLE81 (3,1,2,-50,) in the corpus.
        if (next < content.Length && content[next] == '(')
        {
            int argumentsEnd = SiemensScanner.ReadGroup(content, next);
            if (argumentsEnd < 0)
            {
                return start;
            }

            word = new SourceWord { Address = name, Text = content.Substring(next, argumentsEnd - next) };
            return argumentsEnd;
        }

        word = new SourceWord { Address = name };
        return position;
    }

    // The number of a single-letter address runs to a blank, the end, or the letter of the next word.
    private static bool DinNumber(string content, int position, out int end)
    {
        int from = position;
        if (from < content.Length && content[from] is ' ' or '\t')
        {
            from = SiemensScanner.SkipBlanks(content, from);
            if (from >= content.Length || !(char.IsAsciiDigit(content[from]) || content[from] is '-' or '+' or '.'))
            {
                end = -1;
                return false;
            }
        }

        end = SiemensScanner.ReadNumber(content, from);
        if (end < 0)
        {
            return false;
        }

        return end == content.Length || content[end] is ' ' or '\t' || char.IsAsciiLetter(content[end]);
    }

    private static SourceWord NumberWord(string address, string digits)
    {
        return new SourceWord { Address = address, Text = digits, Number = NumberOf(digits) };
    }

    // A value that is a number keeps it as the number; any other value, an expression, a string, AC(10), is the
    // expression the reader converts (language 4.12).
    private static SourceWord ValueWord(string address, string value)
    {
        decimal? number = NumberOf(value);
        return new SourceWord
        {
            Address = address,
            Text = value,
            Number = number,
            Expression = number is null ? value : null,
        };
    }

    private static decimal? NumberOf(string text)
    {
        if (SiemensScanner.ReadNumber(text, 0) != text.Length)
        {
            return null;
        }

        return decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out decimal number)
            ? number
            : null;
    }

    // DEFINE NAME AS text defines a macro; later blocks read with the name replaced by the text (controllers
    // siemens.md 8, controller-mapping 6). Strings are not searched.
    private string ExpandMacros(string content)
    {
        if (content.StartsWith("DEFINE", StringComparison.OrdinalIgnoreCase))
        {
            RecordMacro(content);
            return content;
        }

        if (_macros.Count == 0)
        {
            return content;
        }

        var expanded = new StringBuilder();
        int position = 0;
        while (position < content.Length)
        {
            char character = content[position];
            if (character == '"')
            {
                int end = SiemensScanner.ReadString(content, position);
                end = end < 0 ? content.Length : end;
                expanded.Append(content, position, end - position);
                position = end;
            }
            else if (SiemensScanner.StartsIdentifier(character))
            {
                int end = SiemensScanner.ReadIdentifier(content, position);
                string name = content.Substring(position, end - position).ToUpperInvariant();
                expanded.Append(_macros.TryGetValue(name, out string? text) ? text : content.Substring(position,
                    end - position));
                position = end;
            }
            else
            {
                expanded.Append(character);
                position++;
            }
        }

        return expanded.ToString();
    }

    private void RecordMacro(string content)
    {
        string[] parts = content.Split((char[]?)null, 3, StringSplitOptions.RemoveEmptyEntries);
        int asAt = content.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
        if (parts.Length >= 3 && asAt > 0)
        {
            string text = content.Substring(asAt + 4).Trim();
            _macros[parts[1].ToUpperInvariant()] = text;
        }
    }

    private SourceBlock Finish(string line, int number, List<SourceWord> words, List<string> spans, string? comment,
        string content, bool skip, int? skipSwitch)
    {
        _contents[number] = content;
        _spans[number] = spans;
        return new SourceBlock
        {
            Line = number,
            Text = line,
            Words = words,
            Comment = comment,
            BlockSkip = skip,
            SkipSwitch = skipSwitch,
        };
    }

    // A line of the recipe block of the STAMA post, which is not NC syntax, is one word (machine-builders 2, STAMA;
    // controller-mapping 9).
    private SourceBlock RecipeBlock(string line, int number)
    {
        string content = line.Trim();
        var words = new List<SourceWord> { new() { Address = "<", Text = content } };
        return Finish(line, number, words, [content], null, content, skip: false, skipSwitch: null);
    }

    // The recipe block runs from a marker that holds BEGIN, <PROG_BEGIN_C1>, to the next marker that holds END,
    // <PROG_END_PAR>; without an end marker only the begin marker is a recipe line.
    private static HashSet<int> RecipeLines(List<string> lines)
    {
        var recipe = new HashSet<int>();
        int? open = null;
        for (int index = 0; index < lines.Count; index++)
        {
            string trimmed = lines[index].Trim();
            bool marker = trimmed.Length > 2 && trimmed[0] == '<' && trimmed[^1] == '>';
            if (marker && open is null && trimmed.Contains("BEGIN", StringComparison.OrdinalIgnoreCase))
            {
                open = index;
            }
            else if (marker && open is int begin && trimmed.Contains("END", StringComparison.OrdinalIgnoreCase))
            {
                for (int inside = begin; inside <= index; inside++)
                {
                    recipe.Add(inside);
                }

                open = null;
            }
            else if (marker)
            {
                recipe.Add(index);
            }
        }

        if (open is int unclosed)
        {
            recipe.Add(unclosed);
        }

        return recipe;
    }

    private static void Add(List<SourceWord> words, List<string> spans, SourceWord word, string span)
    {
        words.Add(word);
        spans.Add(span);
    }

    // The first ; outside a string.
    private static int CommentStart(string line)
    {
        bool inString = false;
        for (int index = 0; index < line.Length; index++)
        {
            if (line[index] == '"')
            {
                inString = !inString;
            }
            else if (line[index] == ';' && !inString)
            {
                return index;
            }
        }

        return -1;
    }

    // Every line of the file, its line ending removed; a text that ends with a line ending has no empty last line.
    private static List<string> LinesOf(string text)
    {
        var lines = new List<string>();
        int start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                lines.Add(text.Substring(start, index - start).TrimEnd('\r'));
                start = index + 1;
            }
        }

        if (start < text.Length)
        {
            lines.Add(text.Substring(start).TrimEnd('\r'));
        }

        return lines;
    }
}
