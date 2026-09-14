using Ncx.Compilers.Tests.Fakes;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Compilers.Tests;

/// <summary>
/// BLOCK_WRITE and IBlockWriter: the lines of every block pass the block writers of the plugins before they reach the
/// output (virtual machine 7, architecture 8, D106; code-guidelines 11).
/// </summary>
public sealed class BlockWriterTests
{
    // Code-guidelines 11: the block writer of the plugin template puts Z on a line of its own, which gets its own
    // block number.
    [Fact]
    public void BlockWrite_ZOnItsOwnLine_SplitsTheLineBeforeTheOutput()
    {
        var options = new CompileOptions { BlockWriters = [new ZOnItsOwnLine()] };

        CompileResult result = FakeCompile.Run(FakeCompile.Program("RAPID X=0 Y=0 Z=5"), FakeMachines.Mill(),
            options: options);

        Assert.Equal("%\nO1 (T)\nN10 G0 X0. Y0.\nN20 Z5.\nN30 M30\n%\n", FakeCompile.TextOf(result));
    }

    // Virtual machine 7: BLOCK_WRITE comes for every block before it is written, FILE=BEGIN to FILE=END in walk order,
    // with the words of the block, its lines and the state around it.
    [Fact]
    public void BlockWrite_EveryBlock_ComesWithItsWordsLinesAndState()
    {
        var recorder = new RecordingWriter();

        FakeCompile.Run(FakeCompile.Program("RAPID X=0 Y=0 Z=5"), FakeMachines.Mill(),
            options: new CompileOptions { BlockWriters = [recorder] });

        Assert.Equal([1, 2, 3, 4, 5, 6], recorder.Lines);
        BlockWriteEvent rapid = recorder.Events[3];
        Assert.Equal("RAPID", rapid.Words[0].Key);
        Assert.Equal(["G0 X0. Y0. Z5."], rapid.OutputLines);
        Assert.Equal(5m, rapid.After.Motion.Position["Z"].Value);
    }

    // A block writer that records what it receives.
    private sealed class RecordingWriter : IBlockWriter
    {
        public List<int> Lines { get; } = [];

        public List<BlockWriteEvent> Events { get; } = [];

        public void Write(BlockWriteEvent blockWrite)
        {
            Lines.Add(blockWrite.Block.Line);
            Events.Add(blockWrite);
        }
    }
}
