using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Core.Tests.VirtualMachine.Events;

/// <summary>
/// BLOCK_WRITE of the compilers (virtual machine 7, architecture 5.3 and 8): under VmOptions.RaiseBlockWrite every
/// executed block raises it, the last of its events, with the words of the block and no output lines yet, so that the
/// compiler subscribed to the run writes each block from its Before and After.
/// </summary>
public sealed class BlockWriteEventTests
{
    // Lines 3 to 6: the header, a comment that raises no other event, a tool change, the end of the program.
    private static readonly string s_file = VmHarness.File("UNITS=MM", "COMMENT=\"A\"", "TOOL=1 RPM=1000",
        "PROGRAM=END");

    // Virtual machine 7: every executed block raises BLOCK_WRITE once, in the order the blocks run, PROGRAM=BEGIN to
    // PROGRAM=END; FILE=BEGIN and FILE=END, which no walk executes, raise their FILE events only.
    [Fact]
    public void RaiseBlockWrite_EveryExecutedBlock_RaisesBlockWriteOnce()
    {
        FakeListener listener = EventRuns.Run(s_file, options: WithBlockWrite());

        var lines = new List<int>();
        foreach (BlockWriteEvent blockWrite in listener.Of<BlockWriteEvent>())
        {
            lines.Add(blockWrite.Block.Line);
        }

        Assert.Equal([2, 3, 4, 5, 6], lines);
    }

    // Virtual machine 7: a block that raises no other event, a COMMENT, raises BLOCK_WRITE all the same.
    [Fact]
    public void RaiseBlockWrite_BlockWithoutOtherEvents_RaisesBlockWriteAlone()
    {
        FakeListener listener = EventRuns.Run(s_file, options: WithBlockWrite());

        VmEvent comment = Assert.Single(listener.Events, vmEvent => vmEvent.Block.Line == 4);
        Assert.IsType<BlockWriteEvent>(comment);
    }

    // Architecture 8: BLOCK_WRITE is the last event of its block, with its Before and After, the words of the block
    // and the lines still empty for the compiler to fill.
    [Fact]
    public void RaiseBlockWrite_BlockWithOtherEvents_RaisesBlockWriteLastWithTheWordsOfTheBlock()
    {
        FakeListener listener = EventRuns.Run(s_file, options: WithBlockWrite());

        List<VmEvent> ofTheToolBlock = listener.Events.FindAll(vmEvent => vmEvent.Block.Line == 5);
        var blockWrite = Assert.IsType<BlockWriteEvent>(ofTheToolBlock[^1]);
        Assert.True(ofTheToolBlock.Count > 1);
        Assert.Same(ofTheToolBlock[0].Before, blockWrite.Before);
        Assert.Same(ofTheToolBlock[0].After, blockWrite.After);
        Assert.Equal(["TOOL", "RPM"], blockWrite.Words.ConvertAll(word => word.Key));
        Assert.Empty(blockWrite.OutputLines);
    }

    // VmOptions: without the option, check, trace and analyze see no BLOCK_WRITE.
    [Fact]
    public void RaiseBlockWrite_Default_RaisesNoBlockWrite()
    {
        FakeListener listener = EventRuns.Run(s_file);

        Assert.Empty(listener.Of<BlockWriteEvent>());
    }

    private static VmOptions WithBlockWrite()
    {
        return VmOptions.ForMachine(VmMachines.Default()) with { RaiseBlockWrite = true };
    }
}
