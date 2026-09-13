using System.Text;
using Ncx.Core.Catalog;
using Ncx.Core.Model;

namespace Ncx.Core.Writing;

/// <summary>
/// Writes a program in canonical form, the one way an NCX program is written, so that two programs that mean the same
/// thing are the same text (language 2 rule 7, D43): the words of every block in the canonical order of the rank table
/// (language 5 rule 6, D90), the numbers as stored (language 2 rule 5), the comment in its column (language 5 rule 7),
/// the comment-only and blank lines as read (D92). ncx format and every reader write their output with it
/// (architecture 4.1). It takes no machine file, so the canonical text of a program never depends on one (D91, D93).
/// </summary>
public static class NcxWriter
{
    // The semicolon of a block's comment stands in column 57 (language 5 rule 7, D92).
    private const int CommentColumn = 57;

    // Words that reach column 54 or later are followed by three spaces before the comment (language 5 rule 7, D92).
    private const int SpacesAfterLongWords = 3;

    /// <summary>
    /// Writes a program in canonical form without its generated blocks, as ncx format does (language 4.15).
    /// </summary>
    /// <param name="program">The program, read from a file or built by a reader.</param>
    /// <returns>The canonical text, every line ended with the line ending of the program.</returns>
    public static string Write(NcxProgram program)
    {
        return Write(program, new WriterOptions());
    }

    /// <summary>
    /// Writes a program in canonical form.
    /// </summary>
    /// <param name="program">The program, read from a file or built by a reader.</param>
    /// <param name="options">Whether the generated blocks are written as well.</param>
    /// <returns>The canonical text, every line ended with the line ending of the program.</returns>
    public static string Write(NcxProgram program, WriterOptions options)
    {
        // Every line ends with the line ending of the file, the last line too; a program that was never a file, such as
        // one a reader built, ends its lines with LF (language 3, Encoding; phase 0, P0-06).
        string lineEnding = program.LineEnding == LineEnding.CrLf ? "\r\n" : "\n";
        var text = new StringBuilder();

        // For every line of the file, a comment-only or blank line or a block: the trivia are interleaved with the
        // blocks by line number, each before the first block of a greater line, and written back as read (D92). The
        // blocks stand in file order, and with them the programs and subprograms (language 4.13).
        int nextTrivia = 0;
        foreach (Block block in program.Blocks)
        {
            // ncx format never writes a generated block; it is written when the options ask for it (language 4.15,
            // architecture 4.1).
            if (WrittenBlock(block, options) is not Block written)
            {
                continue;
            }

            while (nextTrivia < program.Trivia.Count && program.Trivia[nextTrivia].Line < written.Line)
            {
                text.Append(program.Trivia[nextTrivia].Text).Append(lineEnding);
                nextTrivia++;
            }

            text.Append(WriteBlock(written)).Append(lineEnding);
        }

        // The trivia after the last block, the lines after FILE=END among them (D92).
        while (nextTrivia < program.Trivia.Count)
        {
            text.Append(program.Trivia[nextTrivia].Text).Append(lineEnding);
            nextTrivia++;
        }

        return text.ToString();
    }

    /// <summary>
    /// Writes one block in canonical form: its words in canonical order, one space apart, then its comment in the
    /// comment column (language 5 rules 6 and 7).
    /// </summary>
    /// <param name="block">The block; its words stay as they are.</param>
    /// <returns>The line without its line ending.</returns>
    public static string WriteBlock(Block block)
    {
        // The words in the order of the rank table, a machine axis word behind X Y Z A B C, the native parameters of a
        // cycle in source order, each as key, address, equals sign and the value as stored: numbers untouched, strings
        // escaped (language 2 rule 5; language 3, Word; 5 rule 6; D90, D93, D94).
        var line = new StringBuilder();
        foreach (Word word in CanonicalOrder.Sort(block))
        {
            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word.ToCanonical());
        }

        if (block.Comment is null)
        {
            return line.ToString();
        }

        // Then the comment as read: spaces up to column 57, so that the semicolon stands in column 57, or three spaces
        // when the words reach column 54 or later (language 5 rule 7, D92).
        // TODO(question): language 5 rule 7 counts columns and does not say what a column is beyond ASCII (a UTF-16
        // unit, a code point, a character as an editor shows it). A column is one character of the string here, as the
        // lexer counts the columns of its diagnostics, until that is answered.
        int spaces = Math.Max(CommentColumn - 1 - line.Length, SpacesAfterLongWords);
        return line.Append(' ', spaces).Append(block.Comment).ToString();
    }

    // The block the writer writes for a block of the program: the block itself, and a generated block only when the
    // options ask for it; for a block the expander rewrote in place, the block of the file it stands for, as read
    // (language 4.15, D64).
    private static Block? WrittenBlock(Block block, WriterOptions options)
    {
        if (!block.IsGenerated || options.IncludeGenerated)
        {
            return block;
        }

        return block.Generated is { Placement: GeneratedPlacement.InPlace } generated ? generated.Origin : null;
    }
}
