namespace ShopRules;

/// <summary>
/// Puts the Z word of every output line on a line of its own after it, the other thing plugin authors ask for first
/// (code-guidelines 11).
/// </summary>
public sealed class ZOnItsOwnLine : IBlockWriter
{
    public void Write(BlockWriteEvent blockWrite)
    {
        for (int index = 0; index < blockWrite.OutputLines.Count; index++)
        {
            string line = blockWrite.OutputLines[index];
            int z = line.IndexOf(" Z", StringComparison.Ordinal);
            if (z < 0)
            {
                continue;
            }

            blockWrite.OutputLines[index] = line.Substring(0, z);
            blockWrite.OutputLines.Insert(index + 1, line.Substring(z + 1));
            index++;
        }
    }
}
