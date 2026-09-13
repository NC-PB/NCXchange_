using System.Globalization;
using System.Text;
using Ncx.Core.Catalog;
using Ncx.Core.Model;
using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.Catalog;

/// <summary>
/// Cuts NCX text into blocks of words the way language 3 describes a line, for the catalog tests only. The parser
/// (P0-04) is built on the catalog after it, so these tests bring the little they need to ask the catalog about every
/// word of the examples: a comment starts at a semicolon outside a string, words are split at blanks outside strings
/// and braces, the key and the address stand before the equals sign. It checks nothing itself.
/// </summary>
internal static class ExampleBlocks
{
    /// <summary>
    /// The five example programs of the specification, by their path relative to docs/spec/examples
    /// (tests/README.md): every embedded .ncx file of the examples folder itself.
    /// </summary>
    public static List<string> NcxExamples()
    {
        var examples = new List<string>();
        foreach (string example in Fixture.List())
        {
            if (!example.Contains('/') && example.EndsWith(".ncx", StringComparison.Ordinal))
            {
                examples.Add(example);
            }
        }

        return examples;
    }

    /// <summary>
    /// The blocks of an NCX text: every line that holds a word, with its 1-based line number. Comment-only and blank
    /// lines are trivia and give no block (language 3, Block; D92).
    /// </summary>
    public static List<Block> Read(string text)
    {
        var blocks = new List<Block>();
        string[] lines = text.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string words = WithoutComment(lines[index]).Trim();
            if (words.Length > 0)
            {
                blocks.Add(ToBlock(index + 1, SplitWords(words)));
            }
        }

        return blocks;
    }

    /// <summary>
    /// The words of a text, split at whitespace outside strings and braces (language 3, Word; architecture 4.1).
    /// </summary>
    public static List<string> SplitWords(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        bool inString = false;
        int braceDepth = 0;
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];

            // An escaped quote or backslash stays inside its string (language 3, string).
            if (inString && character == '\\' && index + 1 < text.Length)
            {
                word.Append(character).Append(text[index + 1]);
                index++;
                continue;
            }

            if (character == '"')
            {
                inString = !inString;
            }
            else if (!inString && character == '{')
            {
                braceDepth++;
            }
            else if (!inString && character == '}')
            {
                braceDepth--;
            }

            if (char.IsWhiteSpace(character) && !inString && braceDepth == 0)
            {
                AddWord(words, word);
                continue;
            }

            word.Append(character);
        }

        AddWord(words, word);
        return words;
    }

    // A word ends at a blank; several blanks in a row end one word.
    private static void AddWord(List<string> words, StringBuilder word)
    {
        if (word.Length > 0)
        {
            words.Add(word.ToString());
            word.Clear();
        }
    }

    // A comment starts at a semicolon outside a string and runs to the end of the line (language 3, Comment).
    private static string WithoutComment(string line)
    {
        bool inString = false;
        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (inString && character == '\\')
            {
                index++;
            }
            else if (character == '"')
            {
                inString = !inString;
            }
            else if (character == ';' && !inString)
            {
                return line.Substring(0, index);
            }
        }

        return line;
    }

    // The block keeps its words as written; its verb is the word the catalog marks as one (language 5 rule 1).
    private static Block ToBlock(int line, List<string> tokens)
    {
        var words = new List<Word>();
        foreach (string token in tokens)
        {
            words.Add(ToWord(token));
        }

        Word? verb = null;
        foreach (Word word in words)
        {
            if (verb is null && WordCatalog.IsVerb(word))
            {
                verb = word;
            }
        }

        return new Block { Line = line, Words = words, Verb = verb };
    }

    // KEY, KEY=VALUE or KEY:ADDR=VALUE: the key and the address stand before the first equals sign (language 3, Word).
    private static Word ToWord(string token)
    {
        int equals = token.IndexOf('=');
        string name = equals < 0 ? token : token.Substring(0, equals);
        int colon = name.IndexOf(':');
        string key = colon < 0 ? name : name.Substring(0, colon);
        string? addr = colon < 0 ? null : name.Substring(colon + 1);
        Value value = equals < 0 ? NoValue.Instance : ToValue(token.Substring(equals + 1));
        return new Word { Key = key, Addr = addr, Value = value, Definition = WordCatalog.Lookup(key) };
    }

    // The value types of language 3 are told apart by their form: a string in quotes, an expression in braces, a list
    // with commas, an integer, a decimal, otherwise an identifier.
    private static Value ToValue(string text)
    {
        if (text.StartsWith('"'))
        {
            return new StringValue(Unescape(text.Substring(1, text.Length - 2)));
        }

        if (text.StartsWith('{'))
        {
            return new ExprValue(text.Substring(1, text.Length - 2), null);
        }

        if (text.Contains(','))
        {
            return new ListValue(text.Split(','));
        }

        string digits = text.StartsWith('-') ? text.Substring(1) : text;
        if (IsDigits(digits))
        {
            return new IntegerValue(long.Parse(text, CultureInfo.InvariantCulture), text);
        }

        int point = digits.IndexOf('.');
        if (point > 0 && IsDigits(digits.Substring(0, point)) && IsDigits(digits.Substring(point + 1)))
        {
            return new DecimalValue(decimal.Parse(text, CultureInfo.InvariantCulture), text);
        }

        return new IdentValue(text);
    }

    private static bool IsDigits(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (char character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    // \" is a quote and \\ a backslash inside a string (language 3, string).
    private static string Unescape(string content)
    {
        var text = new StringBuilder();
        for (int index = 0; index < content.Length; index++)
        {
            if (content[index] == '\\' && index + 1 < content.Length)
            {
                index++;
            }

            text.Append(content[index]);
        }

        return text.ToString();
    }
}
