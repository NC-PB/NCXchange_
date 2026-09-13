using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.Catalog;

/// <summary>
/// The language specification as the catalog tests read it: docs/spec/ncx-language.md of the repository, found from
/// the test assembly (tests/README.md), never through the working directory (code-guidelines 8). The specification
/// stays the single source of the tables and examples the catalog is checked against.
/// </summary>
internal static class LanguageDocument
{
    // The line of a Markdown code fence.
    private const string Fence = "```";

    // The line of an elided passage inside an example of language 6: "TILT_AXIS ...", "...", "RETRACT".
    private const string Elision = "...";

    /// <summary>
    /// The lines of a chapter, from its heading ("## 4. Word catalog") to the next chapter heading.
    /// </summary>
    /// <param name="number">The chapter number: "4".</param>
    public static List<string> Chapter(string number)
    {
        string path = Path.Combine(Fixture.RepositoryRoot(), "docs", "spec", "ncx-language.md");
        var chapter = new List<string>();
        bool inChapter = false;
        foreach (string line in File.ReadAllLines(path))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inChapter = line.StartsWith("## " + number + ". ", StringComparison.Ordinal);
                continue;
            }

            if (inChapter)
            {
                chapter.Add(line);
            }
        }

        return chapter;
    }

    /// <summary>
    /// The NCX text of the examples of language 6: the lines of its code blocks, without the elision lines.
    /// </summary>
    public static string ExamplesOfChapter6()
    {
        var example = new List<string>();
        bool inCode = false;
        foreach (string line in Chapter("6"))
        {
            if (line.StartsWith(Fence, StringComparison.Ordinal))
            {
                inCode = !inCode;
                continue;
            }

            if (inCode && line.Trim() != Elision)
            {
                example.Add(line);
            }
        }

        return string.Join('\n', example);
    }

    /// <summary>
    /// The keys written in the Word column of the tables of a chapter: every key of the backticked words in the first
    /// cell of a table row, so "SHIFT X=60 Y=40 Z=-5" gives SHIFT, X, Y and Z.
    /// </summary>
    public static List<string> KeysOfTheWordColumn(List<string> chapter)
    {
        var keys = new List<string>();
        foreach (string line in chapter)
        {
            // A table row whose first cell holds a word: "| `SPINDLE=CW` | ...".
            if (!line.StartsWith("| `", StringComparison.Ordinal))
            {
                continue;
            }

            string firstCell = line.Substring(1, line.IndexOf('|', 1) - 1);
            string[] parts = firstCell.Split('`');

            // The backticked spans are the odd parts between the backticks.
            for (int index = 1; index < parts.Length; index += 2)
            {
                foreach (string word in ExampleBlocks.SplitWords(parts[index]))
                {
                    keys.Add(KeyOf(word));
                }
            }
        }

        return keys;
    }

    // The key stands before the address and before the value (language 3, Word).
    private static string KeyOf(string word)
    {
        int end = word.IndexOfAny([':', '=']);
        return end < 0 ? word : word.Substring(0, end);
    }
}
