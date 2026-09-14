using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Compilers.Tests.Fakes;

/// <summary>
/// A block writer as a plugin writes one (code-guidelines 11): every line with a Z word gets the Z word on a line of
/// its own after it.
/// </summary>
internal sealed class ZOnItsOwnLine : IBlockWriter
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
