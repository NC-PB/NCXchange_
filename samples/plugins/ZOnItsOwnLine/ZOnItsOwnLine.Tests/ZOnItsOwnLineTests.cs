namespace ZOnItsOwnLine.Tests;

/// <summary>
/// The Z writer on BLOCK_WRITE (virtual machine 7): the lines a Heidenhain compiler writes for
/// a block in, the lines that reach the NC file out. The state around the block comes from a
/// run of the virtual machine over a small program, as in a compile.
/// </summary>
public sealed class ZOnItsOwnLineTests
{
    // Implementation 17, P7-02: the Z writer splits L X+10 Z-5 into two Heidenhain lines; Z
    // goes down from 50 to -5, so it moves after X.
    [Fact]
    public void ZOnItsOwnLine_PlungeWithX_SplitsIntoTwoHeidenhainLinesZLast()
    {
        BlockWriteEvent blockWrite = BlockWriteOf("LINE X=10 Z=-5 F=500", "L X+10 Z-5");

        new ZOnItsOwnLine().Write(blockWrite);

        Assert.Equal(["L X+10", "L Z-5"], blockWrite.OutputLines);
    }

    // Up first: Z goes up from 50 to 80, so it moves before X; FMAX counts for its own line
    // only, so both lines carry it.
    [Fact]
    public void ZOnItsOwnLine_RetractWithX_PutsZFirstAndKeepsTheRapidOnBothLines()
    {
        BlockWriteEvent blockWrite = BlockWriteOf("RAPID X=10 Z=80", "L X+10 Z+80 R0 FMAX");

        new ZOnItsOwnLine().Write(blockWrite);

        Assert.Equal(["L Z+80 FMAX", "L X+10 R0 FMAX"], blockWrite.OutputLines);
    }

    // A line that moves Z alone is on its own line already.
    [Fact]
    public void ZOnItsOwnLine_ZAlone_StaysAsItIs()
    {
        BlockWriteEvent blockWrite = BlockWriteOf("LINE Z=-5 F=500", "L Z-5 F500");

        new ZOnItsOwnLine().Write(blockWrite);

        Assert.Equal(["L Z-5 F500"], blockWrite.OutputLines);
    }

    // The block of a program whose position the virtual machine does not know yet before it
    // stays as it is: nobody knows whether Z goes up or down.
    [Fact]
    public void ZOnItsOwnLine_ZNotKnownBefore_StaysAsItIs()
    {
        BlockWriteEvent blockWrite = BlockWriteOf("RAPID X=0 Y=0 Z=50", "L X+0 Y+0 Z+50 FMAX", line: 4);

        new ZOnItsOwnLine().Write(blockWrite);

        Assert.Equal(["L X+0 Y+0 Z+50 FMAX"], blockWrite.OutputLines);
    }

    // BLOCK_WRITE of one block of a small program that starts at X0 Y0 Z50 (line 4) and runs
    // the block on line 5, with the lines a compiler wrote for it.
    private static BlockWriteEvent BlockWriteOf(string block, string heidenhainLine, int line = 5)
    {
        string program = $"""
            FILE=BEGIN NCX=1
            PROGRAM=BEGIN NAME="Z"
            UNITS=MM
            RAPID X=0 Y=0 Z=50
            {block}
            PROGRAM=END
            FILE=END

            """;
        NcxProgram parsed = Parser.Parse(program, "test.ncx", new ParserOptions());
        MachineConfig machine = DefaultMachine.Create();
        var recorder = new BlockWriteRecorder();
        var vm = new VirtualMachine(machine, VmOptions.ForMachine(machine) with { RaiseBlockWrite = true },
            parsed.Diagnostics);
        vm.Subscribe(recorder);
        vm.Run(parsed);

        foreach (BlockWriteEvent blockWrite in recorder.BlockWrites)
        {
            if (blockWrite.Block.Line == line)
            {
                return blockWrite with { OutputLines = [heidenhainLine] };
            }
        }

        throw new InvalidOperationException($"No BLOCK_WRITE on line {line}:\n{parsed.Diagnostics.ToText()}");
    }

    // Keeps the BLOCK_WRITE of every block the virtual machine runs.
    private sealed class BlockWriteRecorder : IVmListener
    {
        public List<BlockWriteEvent> BlockWrites { get; } = [];

        public void On(VmEvent vmEvent)
        {
            if (vmEvent is BlockWriteEvent blockWrite)
            {
                BlockWrites.Add(blockWrite);
            }
        }
    }
}
