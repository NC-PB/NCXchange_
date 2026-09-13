using Ncx.Core.Model;
using Ncx.Core.Writing;
using static Ncx.Readers.Tests.Fanuc.FanucRead;

namespace Ncx.Readers.Tests.Fanuc;

/// <summary>
/// The state a Fanuc subprogram runs with and leaves (virtual machine 3.9; controller-mapping 6, SUB and CALL): a
/// subprogram runs with the state of its caller, the caller continues with the state its subprogram left, and a program
/// starts from the initial state its header writes (D34). What the reader does not know there keeps the blocks that
/// depend on it RAW (D5).
/// </summary>
public sealed class FanucSubprogramTests
{
    // Under G81 every following block with a position calls the cycle again (controllers fanuc.md 6), also the blocks of
    // a subprogram the caller calls while the cycle is active: they are CYCLE_CALL, and check walks them at the CALL
    // with the caller's CYCLE (virtual machine 3.9).
    [Fact]
    public void SubprogramCalledUnderG81_PositionBlocksAreCycleCalls()
    {
        string text = Text("%\nO0001\nG0 X0 Y0 Z10.\nG81 G99 Z-5. R2. F100\nM98 P100\nG80\nM30\nO0100\nX10.\nX20.\nM99\n%\n");

        Assert.Contains("\nSUB=BEGIN NAME=100\nCYCLE_CALL X=10\nCYCLE_CALL X=20\nSUB=END\n", text,
            StringComparison.Ordinal);
        AssertFormatsToItself(text);
        Assert.False(Check(text, Mill()).HasErrors, Check(text, Mill()).ToText());
    }

    // The first motion of a subprogram takes its verb from the code of group 01 its caller left, G0, not from the G1
    // of a block after the call (virtual machine 3.9).
    [Fact]
    public void SubprogramCalledAfterG0_MovesAtG0()
    {
        string text = Text("%\nO0001\nG0 X0 Y0 Z10.\nM98 P100\nG1 X30. F100\nM30\nO0100\nX10.\nM99\n%\n");

        Assert.Contains("\nSUB=BEGIN NAME=100\nRAPID X=10\nSUB=END\n", text, StringComparison.Ordinal);
    }

    // Two calls in different states, G0 and G1: the verb of a block that sets no code of group 01 is not known, and the
    // block stays RAW; a block that sets its code reads as usual.
    [Fact]
    public void SubprogramOfCallersInDifferentStates_KeepsTheBlocksThatDependOnThemRaw()
    {
        NcxProgram program = Program(
            "%\nO0001\nG0 X0 Y0\nM98 P100\nG1 X5. F100\nM98 P100\nM30\nO0100\nX10.\nG1 Y5.\nM99\n%\n", Mill());
        string text = NcxWriter.Write(program);

        Assert.Contains("\nSUB=BEGIN NAME=100\nRAW:FANUC=\"X10.\"\nLINE Y=5\nSUB=END\n", text, StringComparison.Ordinal);
        Assert.Contains(program.Diagnostics.Items, diagnostic => diagnostic.Message.Contains("group 01",
            StringComparison.Ordinal));
    }

    // G90 in one call and G91 in the other: whether X10. is absolute or incremental is not known. The F of the RAW
    // block reaches the control, so the next line carries it.
    [Fact]
    public void SubprogramOfCallersUnderG90AndG91_KeepsItsAbsoluteWordsRaw()
    {
        string text = Text(
            "%\nO0001\nG90 G0 X0\nM98 P100\nG91\nM98 P100\nG90\nM30\nO0100\nG1 X10. F100\nG91 G1 X5.\nM99\n%\n");

        Assert.Contains("\nSUB=BEGIN NAME=100\nRAW:FANUC=\"G1 X10. F100\"\nLINE IX=5 F=100\nSUB=END\n", text,
            StringComparison.Ordinal);
    }

    // The caller continues with the state its subprogram left (virtual machine 3.9): after a subprogram that switches
    // to G1, the verb of X10. is not known to the reader, which reads the subprogram after its caller.
    [Fact]
    public void BlockAfterACall_OfASubprogramThatSetsGroup01_IsKeptAsRaw()
    {
        Assert.Contains(
            "\nRAPID X=0 Y=0\nCALL=100\nRAW:FANUC=\"X10.\"\nLINE X=20\n",
            Text("%\nO0001\nG0 X0 Y0\nM98 P100\nX10.\nG1 X20.\nM30\nO0100\nG1 X5. F100\nM99\n%\n"),
            StringComparison.Ordinal);
    }

    // A subprogram that no program of the file calls runs with the chain of a caller the reader does not know: G69 and
    // G52 act on entries of that chain and stay RAW, G68 is appended to it (language 4.2, virtual machine 3.9).
    [Fact]
    public void SubprogramWithoutACallerInTheFile_KeepsItsFrameResetsRaw()
    {
        string text = Text("%\nO0001\nG0 X0\nM30\nO0100\nG69\nG52 X0\nG68 R30.\nM99\n%\n");

        Assert.Contains("\nSUB=BEGIN NAME=100\nRAW:FANUC=\"G69\"\nRAW:FANUC=\"G52 X0\"\nROTATE=30\nSUB=END\n", text,
            StringComparison.Ordinal);
    }

    // With the chain its one caller left, a subprogram's G69 resets the caller's rotation (controllers fanuc.md 4).
    [Fact]
    public void SubprogramOfACallerWithARotation_ResetsItWithG69()
    {
        string text = Text("%\nO0001\nG68 R30.\nM98 P100\nM30\nO0100\nG69\nM99\n%\n");

        Assert.Contains("\nSUB=BEGIN NAME=100\nROTATE=RESET\nSUB=END\n", text, StringComparison.Ordinal);
    }

    // Every program of the file starts from the initial state its header writes (D34), not from the end of the
    // program before it: X2. of O0002 moves at the code of group 01 before the source writes one.
    [Fact]
    public void SecondProgramOfTheFile_StartsFromTheInitialState()
    {
        string text = Text("%\nO0001\nG1 X1. F100\nM30\nO0002\nX2.\nM30\n%\n");

        Assert.Contains(
            "\nPROGRAM=BEGIN NUMBER=2\nFEED_MODE=PER_MIN COMP=OFF UNITS=MM WORKPLANE=XY CYCLE=OFF\nRAPID X=2\nPROGRAM=END\n",
            text, StringComparison.Ordinal);
    }
}
