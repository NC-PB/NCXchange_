using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine.State;

/// <summary>
/// The program rows of virtual machine 2.1: their start values, and a snapshot that keeps the values it was taken
/// with while the live state changes.
/// </summary>
public sealed class ProgramStateTests
{
    // VM 2.1: program.active, name and number start false, empty and 0; no section runs yet.
    [Fact]
    public void ProgramActiveNameNumber_AtStart_AreFalseEmptyAndZero()
    {
        ProgramState program = new ChannelState(StateMachines.MillTurn()).Program;

        Assert.False(program.Active);
        Assert.Empty(program.Name);
        Assert.Equal(0, program.Number);
        Assert.Null(program.Section);
    }

    // VM 2.1: ended starts false.
    [Fact]
    public void Ended_AtStart_IsFalse()
    {
        Assert.False(new ChannelState(StateMachines.MillTurn()).Program.Ended);
    }

    // VM 2.1: PROGRAM=BEGIN, NAME and NUMBER set them.
    [Fact]
    public void ProgramActiveNameNumber_ChangedAfterASnapshot_SnapshotKeepsThem()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        Section shaft = ProgramSection("SHAFT", 1);
        state.Program.Section = shaft;
        state.Program.Active = true;
        state.Program.Name = "SHAFT";
        state.Program.Number = 1;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Program.Section = ProgramSection("SHAFT_OP2", 2);
        state.Program.Active = false;
        state.Program.Name = "SHAFT_OP2";
        state.Program.Number = 2;

        Assert.Equal(shaft, snapshot.Program.Section);
        Assert.True(snapshot.Program.Active);
        Assert.Equal("SHAFT", snapshot.Program.Name);
        Assert.Equal(1, snapshot.Program.Number);
        Assert.Equal("SHAFT_OP2", state.Program.Name);
    }

    // VM 2.1: PROGRAM=END sets ended, also when reached by JUMP=END.
    [Fact]
    public void Ended_ChangedAfterASnapshot_SnapshotKeepsTrue()
    {
        var state = new ChannelState(StateMachines.MillTurn());
        state.Program.Ended = true;

        ChannelSnapshot snapshot = state.Snapshot();
        state.Program.Ended = false;

        Assert.True(snapshot.Program.Ended);
        Assert.False(state.Program.Ended);
    }

    private static Section ProgramSection(string name, int number)
    {
        return new Section { Kind = SectionKind.Program, Name = name, Number = number, FirstBlock = 1, LastBlock = 9 };
    }
}
