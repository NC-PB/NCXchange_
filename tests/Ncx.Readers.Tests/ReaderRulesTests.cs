using Ncx.Core.Model;
using Ncx.Readers.Tests.Fakes;
using static Ncx.Readers.Tests.Fakes.FakeRead;

namespace Ncx.Readers.Tests;

/// <summary>
/// The reader rules of plugins in the skeleton of every reader: the configuration tables of the machine are tried
/// first, a rule decides what they leave undecided, the reader reads the rest (architecture 7 and 9; D40, D66, D105,
/// D106; phase 3, P3-01).
/// </summary>
public sealed class ReaderRulesTests
{
    // Done when of P3-01: a fake reader with three rules reads a three-line input into the expected NCX text. The
    // builder M codes M456 and M457, which no table of the machine names, go to the rules, the first one that claims
    // a block reads it; M08 is decided by the coolant table, whose M8 it matches by number, and no rule is asked
    // (D40, D66, D105).
    [Fact]
    public void FakeReaderWithThreeRules_ThreeLines_ReadsTheExpectedNcx()
    {
        var transfer = new CodeRule(456, "WORKPIECE=SUB");
        var counter = new CodeRule(457, "FUNC:PART_COUNTER=COUNT");
        var throughCoolant = new CodeRule(8, "COOLANT:THROUGH=ON");

        NcxProgram program = Program(Lines("M456", "M08", "M457"), transfer, counter, throughCoolant);

        Assert.Equal(
            Lines(
                "FILE=BEGIN NCX=1",
                "PROGRAM=BEGIN",
                "WORKPIECE=SUB",
                "COOLANT=ON",
                "FUNC:PART_COUNTER=COUNT",
                "PROGRAM=END",
                "FILE=END"),
            Core.Writing.NcxWriter.Write(program));
        Assert.Equal([DiagnosticCodes.ProgramEndMissing], Codes(program));
        Assert.Equal([1, 3], transfer.Offered);
        Assert.Equal([3], counter.Offered);
        Assert.Empty(throughCoolant.Offered);
    }

    // The configuration tables are tried first: a code a table names is read from the table even when a rule would
    // claim it, and a source M08 matches the table's M8 by number (machine-config 5, D40, D105).
    [Fact]
    public void Tables_CodeTheTableNamesInAnotherSpelling_AreTriedBeforeTheRules()
    {
        var throughCoolant = new CodeRule(8, "COOLANT:THROUGH=ON");

        string text = Text(Lines("%", "O0001", "M08", "M009", "M30", "%"), throughCoolant);

        Assert.Contains("\nCOOLANT=ON\nCOOLANT=OFF\n", text, StringComparison.Ordinal);
        Assert.Empty(throughCoolant.Offered);
    }

    // A code that neither the tables nor a rule decide is read by the reader of the family, MFUNC for an M code no
    // table names (architecture 7).
    [Fact]
    public void Rules_NoRuleClaimsTheBlock_TheReaderReadsIt()
    {
        var transfer = new CodeRule(456, "WORKPIECE=SUB");

        string text = Text(Lines("%", "O0001", "M136", "M30", "%"), transfer);

        Assert.Contains("\nMFUNC=136\n", text, StringComparison.Ordinal);
    }

    // A rule sees the source-side state after the modal codes of the block it is offered, since the codes of a block
    // act for the block itself (architecture 7; controllers fanuc.md 3).
    [Fact]
    public void Rules_Offered_SeeTheStateAfterTheModalCodesOfTheBlock()
    {
        var transfer = new CodeRule(456, "WORKPIECE=SUB");

        Program(Lines("%", "O0001", "G0 X0", "G1 M456", "M30", "%"), transfer);

        Assert.Equal(["G1"], transfer.MotionSeen);
    }

    // A claim covers the one block offered and is complete when Read returns: the rule's blocks stand where the
    // claimed block stands, between the blocks read before and after it, and the rule is offered only the block the
    // tables leave undecided (D5, D40, D66). How a rule claims a sequence of blocks is an open question (ISourceRule).
    [Fact]
    public void Rules_ClaimBetweenTwoMotions_RuleBlocksStandInPlaceOfTheBlock()
    {
        var transfer = new CodeRule(456, "WORKPIECE=SUB");

        string text = Text(Lines("%", "O0001", "G0 X0", "M456", "G1 X10", "M30", "%"), transfer);

        Assert.Contains("\nRAPID X=0\nWORKPIECE=SUB\nLINE X=10\n", text, StringComparison.Ordinal);
        Assert.Equal([4], transfer.Offered);
    }

    // Comments are kept (D5): the comment of a block a rule claimed stays as a comment-only line after the rule's
    // blocks (D92).
    [Fact]
    public void Rules_ClaimedBlockWithAComment_KeepsTheCommentAsTrivia()
    {
        var transfer = new CodeRule(456, "WORKPIECE=SUB");

        string text = Text(Lines("%", "O0001", "M456 (TRANSFER)", "M30", "%"), transfer);

        Assert.Contains("\nWORKPIECE=SUB\n; TRANSFER\nPROGRAM=END\n", text, StringComparison.Ordinal);
    }
}
