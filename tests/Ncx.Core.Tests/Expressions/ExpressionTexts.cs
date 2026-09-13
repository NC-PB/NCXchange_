namespace Ncx.Core.Tests.Expressions;

/// <summary>
/// Finds the expressions in NCX text as the lexer finds them: the text between { and } of a word value, outside
/// strings and before the comment (language 3). The word parser of P0-04 hands the same text to the expression
/// parser; until it exists, the example tests extract it here.
/// </summary>
internal static class ExpressionTexts
{
    // The heading of the examples of the language, the start of any section heading and the fence of a code block in
    // the language document.
    private const string Section6Heading = "## 6. Examples";
    private const string SectionHeadingStart = "## ";
    private const string CodeFence = "```";

    /// <summary>
    /// Every expression of an NCX file, line by line, in the order written.
    /// </summary>
    public static List<string> InFile(string ncx)
    {
        var expressions = new List<string>();
        foreach (string line in ncx.Split('\n'))
        {
            expressions.AddRange(InLine(line.TrimEnd('\r')));
        }

        return expressions;
    }

    /// <summary>
    /// Every expression of one line of NCX.
    /// </summary>
    public static List<string> InLine(string line)
    {
        var expressions = new List<string>();
        bool inString = false;
        int openBrace = -1;
        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (inString)
            {
                // Inside a string, \" is a quote and \\ a backslash; only a plain quote ends it (language 3, string).
                if (character == '\\')
                {
                    index++;
                }
                else if (character == '"')
                {
                    inString = false;
                }
            }
            else if (openBrace >= 0)
            {
                if (character == '}')
                {
                    expressions.Add(line.Substring(openBrace + 1, index - openBrace - 1));
                    openBrace = -1;
                }
            }
            else if (character == '"')
            {
                inString = true;
            }
            else if (character == '{')
            {
                openBrace = index;
            }
            else if (character == ';')
            {
                // A semicolon outside a string starts the comment, which holds no words (language 3, Comment).
                break;
            }
        }

        return expressions;
    }

    /// <summary>
    /// The lines of the code blocks of section 6 of the language document: the snippets of the examples.
    /// </summary>
    public static List<string> Section6CodeLines(string languageDocument)
    {
        var codeLines = new List<string>();
        bool inSection6 = false;
        bool inCodeBlock = false;
        foreach (string rawLine in languageDocument.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith(SectionHeadingStart, StringComparison.Ordinal))
            {
                inSection6 = line.StartsWith(Section6Heading, StringComparison.Ordinal);
            }
            else if (inSection6 && line.StartsWith(CodeFence, StringComparison.Ordinal))
            {
                inCodeBlock = !inCodeBlock;
            }
            else if (inSection6 && inCodeBlock)
            {
                codeLines.Add(line);
            }
        }

        return codeLines;
    }
}
