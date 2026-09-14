namespace FaultyRules;

/// <summary>
/// A block writer that edits the lines of every RAPID block and then throws, halfway through its work.
/// </summary>
public sealed class ThrowingWriter : IBlockWriter
{
    public void Write(BlockWriteEvent blockWrite)
    {
        if (!blockWrite.Block.Has("RAPID"))
        {
            return;
        }

        blockWrite.OutputLines.Insert(0, "( EDITED )");
        throw new InvalidOperationException("the post is out of paper");
    }
}
