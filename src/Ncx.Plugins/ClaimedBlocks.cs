using Ncx.Core.Model;
using Ncx.Core.Writing;

namespace Ncx.Plugins;

/// <summary>
/// Hands the blocks and comment lines a reader rule wrote for a claimed source block to the builder of the program
/// being read, as if the rule had written them there (architecture 7; D92).
/// </summary>
internal static class ClaimedBlocks
{
    /// <summary>
    /// Copies what a rule wrote into a builder of its own into the builder of the program, in its order.
    /// </summary>
    /// <param name="claimed">What the rule's own builder built.</param>
    /// <param name="builder">The builder of the program being read.</param>
    public static void CopyInto(NcxProgram claimed, NcxBuilder builder)
    {
        // A comment line takes the line of the block begun last before it, 0 before the first (NcxBuilder.Trivia), and
        // the writer places it by that line (D92); each is copied where its line puts it: before the first block, or
        // after the last block of its line.
        var copied = new HashSet<int>();
        CopyTrivia(claimed, 0, copied, builder);
        for (int index = 0; index < claimed.Blocks.Count; index++)
        {
            Block block = claimed.Blocks[index];
            builder.Begin(block.Line);
            foreach (Word word in block.Words)
            {
                builder.Word(word.Key, word.Addr, word.Value);
            }

            if (block.Comment is string comment)
            {
                builder.Comment(comment);
            }

            builder.End();
            bool lastOfItsLine = index + 1 == claimed.Blocks.Count || claimed.Blocks[index + 1].Line != block.Line;
            if (lastOfItsLine)
            {
                CopyTrivia(claimed, block.Line, copied, builder);
            }
        }
    }

    // The comment lines of one line that are not copied yet, in their order.
    private static void CopyTrivia(NcxProgram claimed, int line, HashSet<int> copied, NcxBuilder builder)
    {
        for (int index = 0; index < claimed.Trivia.Count; index++)
        {
            if (claimed.Trivia[index].Line == line && copied.Add(index))
            {
                builder.Trivia(claimed.Trivia[index].Text);
            }
        }
    }
}
