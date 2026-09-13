using System.Globalization;
using System.Text.RegularExpressions;

namespace Ncx.Readers.Tests.Fakes;

/// <summary>
/// A tokenizer for a small Fanuc-like syntax, enough to drive the reader framework: words of letters and a number,
/// "( )" comments, "/" and "/n" block skip, "%", "GOTO n", and "* - title" as a structuring comment.
/// </summary>
internal sealed partial class FakeTokenizer : ISourceTokenizer
{
    public IEnumerable<SourceBlock> Tokenize(string text)
    {
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int count = text.EndsWith('\n') ? lines.Length - 1 : lines.Length;
        for (int index = 0; index < count; index++)
        {
            yield return Block(lines[index], index + 1);
        }
    }

    private static SourceBlock Block(string line, int number)
    {
        string content = line.Trim();
        bool skip = false;
        int? skipSwitch = null;
        if (content.StartsWith('/'))
        {
            skip = true;
            content = content.Substring(1);
            if (content.Length > 0 && char.IsAsciiDigit(content[0]))
            {
                skipSwitch = content[0] - '0';
                content = content.Substring(1);
            }
        }

        string? comment = null;
        Match commentMatch = CommentPattern().Match(content);
        if (commentMatch.Success)
        {
            comment = commentMatch.Groups["text"].Value.Trim();
            content = content.Remove(commentMatch.Index, commentMatch.Length);
        }

        var words = new List<SourceWord>();
        content = content.Trim();
        if (content.StartsWith("* -", StringComparison.Ordinal))
        {
            words.Add(new SourceWord { Address = "*", Text = content.Substring(3).Trim() });
        }
        else if (content == "%")
        {
            words.Add(new SourceWord { Address = "%" });
        }
        else
        {
            foreach (Match word in WordPattern().Matches(content))
            {
                string value = word.Groups["value"].Value;
                decimal? parsed = decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out decimal wordNumber) ? wordNumber : null;
                words.Add(new SourceWord { Address = word.Groups["address"].Value, Text = value, Number = parsed });
            }
        }

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

    [GeneratedRegex(@"\((?<text>[^)]*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"(?<address>[A-Z]+)\s*(?<value>[-+]?[0-9.,]*)", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();
}
