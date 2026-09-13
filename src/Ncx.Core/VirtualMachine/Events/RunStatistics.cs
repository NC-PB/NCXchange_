namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// The distance and the block count the events keep under a tool (TOOL_END) and under a program (the run statistics
/// of PROGRAM_END), counted block by block from the MOTION lengths (virtual machine 7). Mutable; only the events of the
/// virtual machine count.
/// </summary>
internal sealed class RunStatistics
{
    /// <summary>
    /// The sum of the MOTION lengths that are known.
    /// </summary>
    public decimal Distance { get; private set; }

    /// <summary>
    /// The blocks counted.
    /// </summary>
    public int Blocks { get; private set; }

    /// <summary>
    /// Counts one executed block with the motions it raised; a motion of unknown length adds nothing to the distance.
    /// </summary>
    public void Count(IReadOnlyList<MotionEvent> motions)
    {
        Blocks++;
        foreach (MotionEvent motion in motions)
        {
            if (motion.Length is decimal length)
            {
                Distance += length;
            }
        }
    }

    /// <summary>
    /// Starts counting anew, at TOOL_BEGIN and PROGRAM_BEGIN.
    /// </summary>
    public void Reset()
    {
        Distance = 0m;
        Blocks = 0;
    }
}
