using Ncx.Compilers.Tests.Fakes;
using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Tests;

/// <summary>
/// The frame chain written in program order on every target (D31, language 4.2): what a block removed, the last entry
/// first, then what it appended, from the Before and After of the STATIC run.
/// </summary>
public sealed class ChainWriterTests
{
    // ORIGIN, a shift, a rotation after it, the reset of the shift, a new shift, a new datum.
    private static readonly string s_chain = FakeCompile.Program(
        "ORIGIN=1", "SHIFT X=10", "ROTATE=30", "SHIFT=RESET", "SHIFT Y=5", "ORIGIN=2");

    // D31: SHIFT (line 5) and ROTATE (line 6) append to the chain in program order.
    [Fact]
    public void Between_ShiftThenRotate_AppendEachInProgramOrder()
    {
        BlockWrites run = Run(s_chain);

        ChainChange shift = ChainWriter.Between(run.At(5).Before.Frame, run.At(5).After.Frame);
        ChainChange rotate = ChainWriter.Between(run.At(6).Before.Frame, run.At(6).After.Frame);

        Assert.Empty(shift.Removed);
        Assert.Equal(TransformKind.Shift, Assert.Single(shift.Appended).Kind);
        Assert.Equal(TransformKind.Rotate, Assert.Single(rotate.Appended).Kind);
    }

    // Language 4.2: a RESET (line 7) removes its entry and everything after it, and the chain is unwound from the end,
    // so the rotation after the shift is cancelled first.
    [Fact]
    public void Write_ResetOfTheShiftBeforeARotation_CancelsTheRotationFirst()
    {
        BlockWrites run = Run(s_chain);
        var written = new List<string>();

        ChainWriter.Write(run.At(7).Before.Frame, run.At(7).After.Frame,
            entry => written.Add("cancel " + entry.Kind), entry => written.Add("write " + entry.Kind));

        Assert.Equal(["cancel Rotate", "cancel Shift"], written);
    }

    // Language 4.2: ORIGIN (line 9) starts an empty chain, so it removes what stands in it.
    [Fact]
    public void Between_OriginAfterAShift_RemovesTheShift()
    {
        BlockWrites run = Run(s_chain);

        ChainChange origin = ChainWriter.Between(run.At(9).Before.Frame, run.At(9).After.Frame);

        Assert.Equal(TransformKind.Shift, Assert.Single(origin.Removed).Kind);
        Assert.Empty(origin.Appended);
    }

    // A block that leaves the chain as it was, the header on line 3, changes nothing.
    [Fact]
    public void Between_BlockWithoutChainWords_IsEmpty()
    {
        BlockWrites run = Run(s_chain);

        Assert.True(ChainWriter.Between(run.At(3).Before.Frame, run.At(3).After.Frame).IsEmpty);
    }

    // The BLOCK_WRITE of every block of a STATIC run.
    private static BlockWrites Run(string text)
    {
        var diagnostics = new Diagnostics("fake.toml");
        MachineConfig machine = MachineConfigLoader.LoadText(FakeMachines.Mill(), diagnostics)
            ?? throw new InvalidOperationException(diagnostics.ToText());
        NcxProgram program = Parser.Parse(text, "T.ncx", new ParserOptions());
        var vm = new VirtualMachine(machine, VmOptions.ForMachine(machine) with { RaiseBlockWrite = true },
            program.Diagnostics);
        var listener = new BlockWrites();
        vm.Subscribe(listener);
        vm.Run(program);
        Assert.False(program.Diagnostics.HasErrors, program.Diagnostics.ToText());
        return listener;
    }

    // Records the BLOCK_WRITE of every block, found by the line of its block.
    private sealed class BlockWrites : IVmListener
    {
        public List<BlockWriteEvent> Events { get; } = [];

        public BlockWriteEvent At(int line)
        {
            return Events.Find(blockWrite => blockWrite.Block.Line == line)
                ?? throw new InvalidOperationException($"No block on line {line} ran.");
        }

        public void On(VmEvent vmEvent)
        {
            if (vmEvent is BlockWriteEvent blockWrite)
            {
                Events.Add(blockWrite);
            }
        }
    }
}
