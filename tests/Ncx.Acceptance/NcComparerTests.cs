using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance;

/// <summary>
/// The comparison rules of phase 3 on hand-written pairs (implementation 13, P3-07, "Comparison rules"; code-guidelines
/// 8): Fanuc text with the [format] of machines/fanuc-mill-30i.toml (the dot, three decimals on X Y Z and F) and
/// Klartext with the [format] of machines/heidenhain-itnc530.toml (the comma, the same decimals).
/// </summary>
public sealed class NcComparerTests
{
    private static readonly NcComparer s_fanuc = new(Machine("fanuc-mill-30i"));
    private static readonly NcComparer s_klartext = new(Machine("heidenhain-itnc530"));

    // Comparison rules: two equal programs are equivalent.
    [Fact]
    public void Compare_EqualPrograms_AreEquivalent()
    {
        const string Program = "%\nO0001\nN10 G0 G40\nN20 G0 X10. Y5.\nN30 M2\n%\n";

        Assert.Null(s_fanuc.Compare(Program, Program, []));
    }

    // Comparison rules: block numbers do not count, N10 against N20 and 5 against 7, nor whether a block has one.
    [Fact]
    public void Compare_BlockNumbersThatDiffer_DoNotCount()
    {
        Assert.Null(s_fanuc.Compare("N10 G0 X10.\nN20 G1 Y5. F100.\n", "N20 G0 X10.\nG1 Y5. F100.\n", []));
        Assert.Null(s_klartext.Compare("5 L X+10 FMAX\n6 L Y+5 F100\n", "7 L X+10 FMAX\n12 L Y+5 F100\n", []));
    }

    // Comparison rules: comments and blank lines do not count: ( ) on Fanuc, ; and the * - structuring blocks of
    // Klartext (controllers fanuc.md 1, heidenhain.md 1), comment-only lines, CR LF against LF, runs of blanks.
    [Fact]
    public void Compare_CommentsAndBlankLines_DoNotCount()
    {
        string fanucSource = "N10 G0 X10. (TO THE START)\r\n\r\n(SIDE MILL D10)\r\nN20 G1  Y5. F100.\r\n";
        string klartextSource = "0 BEGIN PGM T MM\r\n4 * - SIDE MILL\r\n\r\n5 L X+10  FMAX ; to the start\r\n";

        Assert.Null(s_fanuc.Compare(fanucSource, "N10 G0 X10.\nN20 G1 Y5. F100.\n", []));
        Assert.Null(s_klartext.Compare(klartextSource, "0 BEGIN PGM T MM\n1 ; a comment of its own\n2 L X+10 FMAX\n",
            []));
    }

    // Comparison rules: every number is formatted with the decimals of its address in [format], so 70. and 70.0 and
    // 70 are equal, and -.534 equals -0.534; a number with more decimals than the machine takes is its rounded value.
    [Fact]
    public void Compare_NumbersOfOneValueWithinTheDecimalsOfTheMachine_AreEqual()
    {
        Assert.Null(s_fanuc.Compare("N10 G1 X70. Y-.534 Z10.0004 F100.", "N10 G1 X70.0 Y-0.534 Z10 F100", []));
        Assert.Null(s_klartext.Compare("1 L X70. Y-.534 Z10,0004 FMAX", "1 L X+70,0 Y-0,534 Z+10 FMAX", []));
        Assert.NotNull(s_fanuc.Compare("N10 G1 Z10.0006", "N10 G1 Z10.", []));
    }

    // Controllers heidenhain.md 1: the lines a ~ continues are one block, their comments removed.
    [Fact]
    public void Compare_ContinuedLines_AreOneBlock()
    {
        string source = "1 CYCL DEF 247 INIT. REF.PKT ~\n    Q339=1 ;REF.PUNKTNUMMER\n";
        string compiled = "1 CYCL DEF 247 INIT. REF.PKT Q339=+1\n";

        Assert.Null(s_klartext.Compare(source, compiled, []));
    }

    // Comparison rules: the header block of the source may carry its modal words in another order than the compiled
    // one, so a header line, a line of G codes at the start of the program, is compared as a set of words.
    [Fact]
    public void Compare_HeaderLinesOfModalCodes_AreSetsOfWords()
    {
        string source = "%\nO0001 (PART)\nN10 G0 G40\nN20 G80 G90 G94 G98\nN30 G0 X1.\n";
        string compiled = "%\nO0001 (PART)\nN10 G40 G0\nN20 G98 G94 G90 G80\nN30 G0 X1.\n";

        Assert.Null(s_fanuc.Compare(source, compiled, []));
    }

    // Comparison rules: a line of G codes after the first line that does more is no header line, so its order counts.
    [Fact]
    public void Compare_ModalCodesAfterTheHeader_AreComparedInOrder()
    {
        string source = "%\nO0001\nN10 G0 X1.\nN20 G80 G90\n";
        string compiled = "%\nO0001\nN10 G0 X1.\nN20 G90 G80\n";

        Assert.NotNull(s_fanuc.Compare(source, compiled, []));
    }

    // Comparison rules: a TOOL CALL line or a T M6 line is compared as a set of words, a motion block word by word in
    // order.
    [Fact]
    public void Compare_ToolChange_IsASetOfWordsAndAMotionBlockIsNot()
    {
        const string KlartextHeader = "0 BEGIN PGM T MM\n";

        Assert.Null(s_klartext.Compare(KlartextHeader + "1 TOOL CALL 1 Z S1592",
            KlartextHeader + "1 TOOL CALL 1 S1592 Z", []));
        Assert.Null(s_fanuc.Compare("N10 G0 X1.\nN20 T1 M06", "N10 G0 X1.\nN20 M6 T1", []));
        Assert.NotNull(s_klartext.Compare(KlartextHeader + "1 L X+1 Y+2", KlartextHeader + "1 L Y+2 X+1", []));
        Assert.NotNull(s_fanuc.Compare("N10 G0 X1.\nN20 G0 X1. Y2.", "N10 G0 X1.\nN20 G0 Y2. X1.", []));
    }

    // Controller-mapping 1, SKIP: the / of a skipped block is no block number; a skipped block against an unskipped one
    // is a difference, and the / before or after the block number is the same skip.
    [Fact]
    public void Compare_SkippedBlock_KeepsItsSlash()
    {
        Assert.Null(s_fanuc.Compare("N10 G0 X1.\n/N20 G0 X2.", "N10 G0 X1.\nN20 /G0 X2.", []));
        Assert.NotNull(s_fanuc.Compare("N10 G0 X1.\n/N20 G0 X2.", "N10 G0 X1.\nN20 G0 X2.", []));
    }

    // Controllers fanuc.md 1; controller-mapping 1, SKIP: / or /n at the block start is the optional block skip, and
    // the n of its switch is no block number: /1 against /2 is a difference, /2 against / as well, and /2 before a
    // blank, before the words or before the N number is the same skip.
    [Fact]
    public void Compare_SkipSwitchOnFanuc_IsNoBlockNumber()
    {
        Assert.NotNull(s_fanuc.Compare("N10 G0 X1.\n/1 G0 X2.", "N10 G0 X1.\n/2 G0 X2.", []));
        Assert.NotNull(s_fanuc.Compare("N10 G0 X1.\n/2 G0 X2.", "N10 G0 X1.\n/G0 X2.", []));
        Assert.Null(s_fanuc.Compare("N10 G0 X1.\n/2 G0 X2.", "N10 G0 X1.\n/2G0 X2.", []));
        Assert.Null(s_fanuc.Compare("N10 G0 X1.\n/2 N20 G0 X2.", "N10 G0 X1.\n/2G0 X2.", []));
    }

    // Controllers heidenhain.md 1; controller-mapping 1, SKIP: the / of Klartext at the block start takes no digit of
    // the block number, before it or after it.
    [Fact]
    public void Compare_SkipOnKlartext_TakesNoDigitOfTheBlockNumber()
    {
        const string KlartextHeader = "0 BEGIN PGM T MM\n";

        Assert.Null(s_klartext.Compare(KlartextHeader + "/12 L X+1 FMAX", KlartextHeader + "1 /L X+1 FMAX", []));
        Assert.NotNull(s_klartext.Compare(KlartextHeader + "/12 L X+1 FMAX", KlartextHeader + "1 L X+1 FMAX", []));
    }

    // Controllers fanuc.md 1: ( ... ) is a comment, anywhere in the block, and nothing else is: a ; inside a comment
    // ends nothing, so the words after the comment count, and a ; outside one is part of the block.
    [Fact]
    public void Compare_SemicolonOnFanuc_IsNoComment()
    {
        Assert.NotNull(s_fanuc.Compare("N10 G1 X1. (T1;D10) Y2.", "N10 G1 X1. (T1;D10) Y3.", []));
        Assert.Null(s_fanuc.Compare("N10 G1 X1. (T1;D10) Y2.", "N10 G1 X1. Y2.", []));
        Assert.NotNull(s_fanuc.Compare("N10 G1 X1. ; Y2.", "N10 G1 X1.", []));
    }

    // Comparison rules (P3-07): a real difference is shown as a unified diff of the two programs as they are compared,
    // with the lines of the files where they first differ.
    [Fact]
    public void Compare_RealDifference_IsShownAsAUnifiedDiff()
    {
        string source = "0 BEGIN PGM T MM\n1 L X+10 FMAX\n2 ; note\n3 L Y+5 FMAX\n4 L Z+1 FMAX\n5 END PGM T MM\n";
        string compiled = "0 BEGIN PGM T MM\n1 L X+10 FMAX\n2 L Y+6 FMAX\n3 L Z+1 FMAX\n4 END PGM T MM\n";

        string? difference = s_klartext.Compare(source, compiled, []);

        Assert.Equal("""
            --- source
            +++ compiled
            @@ -1,5 +1,5 @@ source line 4, compiled line 3
             BEGIN PGM T MM
             L X10 FMAX
            -L Y5 FMAX
            +L Y6 FMAX
             L Z1 FMAX
             END PGM T MM

            """.ReplaceLineEndings("\n"), difference);
    }

    // Comparison rules (P3-07): a line only one program has shows as that line alone, the two programs agreeing again
    // after it.
    [Fact]
    public void Compare_LineOnlyTheCompiledProgramHas_IsShownAsAnAddedLine()
    {
        string source = "N10 G0 X1.\nN20 G0 X2.\nN30 G0 X3.\nN40 G0 X4.\nN50 G0 X5.\n";
        string compiled = "N10 G0 X1.\nN20 G0 X2.\nN30 M8\nN40 G0 X3.\nN50 G0 X4.\nN60 G0 X5.\n";

        string? difference = s_fanuc.Compare(source, compiled, []);

        Assert.Equal("""
            --- source
            +++ compiled
            @@ -1,5 +1,6 @@ source line 3, compiled line 3
             G0 X1
             G0 X2
            +M8
             G0 X3
             G0 X4
             G0 X5

            """.ReplaceLineEndings("\n"), difference);
    }

    // Wave-1 question #12, answered by D49: the program_end line of the machine is not a difference where the source
    // ends in another form (M2 against the M30 of a Fanuc source) or in none (the Klartext sources end without M30).
    [Fact]
    public void Compare_ProgramEndWhereTheSourceEndsOtherwiseOrNot_IsNoDifference()
    {
        Assert.Null(s_klartext.Compare("0 BEGIN PGM T MM\n1 M5\n2 END PGM T MM",
            "0 BEGIN PGM T MM\n1 M5\n2 M30\n3 END PGM T MM", []));
        Assert.Null(s_fanuc.Compare("%\nO0001\nN10 M5\nN20 M30\n%", "%\nO0001\nN10 M5\nN20 M2\n%", []));
        Assert.Null(s_fanuc.Compare("%\nO0001\nN10 M5\n%", "%\nO0001\nN10 M5\nN20 M2\n%", []));
        Assert.NotNull(s_fanuc.Compare("%\nO0001\nN10 M5\nN20 G0 X1.\n%", "%\nO0001\nN10 M5\nN20 M2\nN30 G0 X1.\n%",
            []));
    }

    // Phase 3: a difference the documents cannot settle is an exception grounded in its question, and an exception
    // that matches no difference is a difference of its own, never a silent one.
    [Fact]
    public void Compare_Exceptions_AreUsedOnceAndNeverSilently()
    {
        var exception = new NcException { Grounds = "the target state at the start of a program", Compiled = ["M137"] };

        Assert.Null(s_klartext.Compare("0 BEGIN PGM T MM\n1 M5", "0 BEGIN PGM T MM\n1 M137\n2 M5", [exception]));
        Assert.Equal("The exception grounded in the target state at the start of a program matches no difference of "
            + "the two programs.", s_klartext.Compare("0 BEGIN PGM T MM\n1 M5", "0 BEGIN PGM T MM\n1 M5", [exception]));
    }

    // Comparison rules: the blocks as they are compared, each with the line of the file it starts on.
    [Fact]
    public void Blocks_FanucProgram_AreTheLinesWithoutNumbersAndCommentsWithEveryNumberFormatted()
    {
        List<NcBlock> blocks = s_fanuc.Blocks("%\r\nO0001 (PART)\r\nN10 G0 G40\r\n\r\n(TOOL)\r\nN20 T01 M06\r\n"
            + "N30 G0 X+50.40 Y-.5\r\n%\r\n");

        Assert.Equal(
        [
            new NcBlock(1, "%", true),
            new NcBlock(2, "O1", true),
            new NcBlock(3, "G0 G40", true),
            new NcBlock(6, "T1 M6", false),
            new NcBlock(7, "G0 X50.4 Y-0.5", false),
            new NcBlock(8, "%", false),
        ], blocks);
    }

    private static MachineConfig Machine(string name)
    {
        var diagnostics = new Diagnostics(name + ".toml");
        MachineConfig? machine = MachineConfigLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "machines", name + ".toml"), diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        return machine;
    }
}
