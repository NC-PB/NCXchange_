using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Core.Parsing;

/// <summary>
/// Reads the text of an NCX file line by line as the lexical rules of language 3 describe it: the line ending of the
/// file, one block per line, the comment from the first semicolon outside strings and expressions, the words separated
/// by whitespace outside strings and expressions (architecture 4.1). It never throws; what breaks the rules is a PAR
/// diagnostic with its line and column.
/// </summary>
internal static class Lexer
{
    /// <summary>
    /// The line ending of the file: the first line break of the text decides, LF or CRLF; null for a text without a
    /// line break. A line that ends otherwise is a WARNING, reported once (language 3, Encoding).
    /// </summary>
    /// <param name="text">The whole text of the file.</param>
    /// <param name="diagnostics">Where the WARNING goes.</param>
    public static LineEnding? DetectLineEnding(string text, Diagnostics diagnostics)
    {
        LineEnding? fileEnding = null;
        int line = 1;
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] != '\n')
            {
                continue;
            }

            LineEnding ending = index > 0 && text[index - 1] == '\r' ? LineEnding.CrLf : LineEnding.Lf;
            if (fileEnding is null)
            {
                fileEnding = ending;
            }
            else if (ending != fileEnding)
            {
                diagnostics.Warning(line, DiagnosticCodes.MixedLineEndings, string.Create(CultureInfo.InvariantCulture,
                    $"Line {line} ends with {NameOf(ending)} while the first line break of the file is "
                    + $"{NameOf(fileEnding.Value)}; the file keeps {NameOf(fileEnding.Value)} "
                    + $"(language 3, Encoding)."));
                return fileEnding;
            }

            line++;
        }

        return fileEnding;
    }

    /// <summary>
    /// The lines of the text without their line endings: LF ends a line and a CR before it belongs to the CRLF; the
    /// line break at the end of the last line starts no further line (language 3, Encoding, Block).
    /// </summary>
    /// <param name="text">The whole text of the file.</param>
    public static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        int start = 0;
        while (start < text.Length)
        {
            int lineFeed = text.IndexOf('\n', start);
            if (lineFeed < 0)
            {
                lines.Add(text.Substring(start));
                break;
            }

            int end = lineFeed > start && text[lineFeed - 1] == '\r' ? lineFeed - 1 : lineFeed;
            lines.Add(text.Substring(start, end - start));
            start = lineFeed + 1;
        }

        return lines;
    }

    /// <summary>
    /// Reads one line: trivia when it holds no word, otherwise the words of a block and its comment (language 3,
    /// Block, Word, Comment; D92).
    /// </summary>
    /// <param name="text">The line without its line ending.</param>
    /// <param name="line">The 1-based line number, which every diagnostic carries.</param>
    /// <param name="options">Whether pseudo-words are lexed (D95).</param>
    /// <param name="diagnostics">Where a malformed word is reported.</param>
    public static LexedLine LexLine(string text, int line, ParserOptions options, Diagnostics diagnostics)
    {
        var words = new List<LexedWord>();
        bool holdsWords = false;
        int wordStart = -1;
        int wordsEnd = text.Length;
        bool inString = false;
        bool escaped = false;
        int braceDepth = 0;
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];

            // Inside a string \" is a quote and \\ a backslash; only a quote that is not escaped ends the string, and
            // a string may contain spaces and semicolons (language 3, string).
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            // Language 3 lets a semicolon outside a string start the comment, and the lexer honours quoted strings
            // and {...} expressions (architecture 4.1; phase 0, P0-04): a semicolon inside braces belongs to the
            // expression, which reports it as an ERROR. Read the other way it would be the ERROR of an unclosed brace,
            // so only the diagnostics of that invalid line differ (wave-1 question #56).
            // The comment starts at the first semicolon outside strings and expressions and runs to the end of the
            // line (language 3, Comment; architecture 4.1).
            if (braceDepth == 0 && character == ';')
            {
                wordsEnd = index;
                break;
            }

            // Words are separated by whitespace outside strings and expressions; whitespace inside {} is free
            // (language 3, Word; 4.12).
            if (braceDepth == 0 && char.IsWhiteSpace(character))
            {
                AddWord(words, text, wordStart, index, line, options, diagnostics);
                wordStart = -1;
                continue;
            }

            if (wordStart < 0)
            {
                wordStart = index;
                holdsWords = true;
            }

            if (character == '"' && braceDepth == 0)
            {
                inString = true;
            }
            else if (character == '{')
            {
                braceDepth++;
            }
            else if (character == '}' && braceDepth > 0)
            {
                braceDepth--;
            }
        }

        AddWord(words, text, wordStart, wordsEnd, line, options, diagnostics);
        string? comment = wordsEnd < text.Length ? text.Substring(wordsEnd) : null;

        // A line that holds nothing but whitespace and a comment is trivia, not a block (language 3, Block; D92).
        return new LexedLine(line, text, words, comment, IsTrivia: !holdsWords);
    }

    // The word from its start to its end, read by the word lexer; nothing when no word has started.
    private static void AddWord(List<LexedWord> words, string text, int start, int end, int line,
        ParserOptions options, Diagnostics diagnostics)
    {
        if (start < 0)
        {
            return;
        }

        LexedWord? word = WordLexer.Lex(text.Substring(start, end - start), start + 1, line, options, diagnostics);
        if (word is not null)
        {
            words.Add(word);
        }
    }

    // The line endings as the specification writes them (language 3, Encoding).
    private static string NameOf(LineEnding ending)
    {
        return ending == LineEnding.CrLf ? "CRLF" : "LF";
    }
}
