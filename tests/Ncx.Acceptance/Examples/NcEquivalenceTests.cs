using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Examples;

/// <summary>
/// The comparison rules of phase 3 on hand-written pairs of Klartext
/// (docs/implementation/13-phase-3-readers-compilers.md, "Comparison rules"), with the [format] of
/// machines/heidenhain-itnc530.toml: the comma, three decimals on X Y Z and F.
/// </summary>
public sealed class NcEquivalenceTests
{
    private static readonly NcEquivalence s_comparison = new(Mill());

    // Comparison rules: two equal programs are equivalent.
    [Fact]
    public void Compare_EqualPrograms_AreEquivalent()
    {
        const string Program = "0 BEGIN PGM T MM\n1 L X+10 FMAX\n2 END PGM T MM\n";

        Assert.Null(s_comparison.Compare(Program, Program, []));
    }

    // Comparison rules: block numbers, comments, the * - structuring blocks, blank lines and whitespace do not count.
    [Fact]
    public void Compare_BlockNumbersCommentsAndBlankLines_DoNotCount()
    {
        string source = "0 BEGIN PGM T MM\r\n4 * - SIDE MILL\r\n\r\n5 L X+10  FMAX ; to the start\r\n"
            + "9 END PGM T MM\r\n";
        string compiled = "0 BEGIN PGM T MM\n1 L X+10 FMAX\n2 ; a comment of its own\n3 END PGM T MM\n";

        Assert.Null(s_comparison.Compare(source, compiled, []));
    }

    // Comparison rules: every number is formatted with the decimals of its address, so 70. and 70,0 and +70 are equal,
    // -.534 equals -0,534, and 10,0004 is 10 with the three decimals of Z.
    [Fact]
    public void Compare_NumbersOfOneValue_AreEqual()
    {
        Assert.Null(s_comparison.Compare("1 L X70. Y-.534 Z10,0004 FMAX", "1 L X+70,0 Y-0,534 Z+10 FMAX", []));
    }

    // Controllers heidenhain.md 1: the lines a ~ continues are one block, their comments removed.
    [Fact]
    public void Compare_ContinuedLines_AreOneBlock()
    {
        string source = "1 CYCL DEF 247 INIT. REF.PKT ~\n    Q339=1 ;REF.PUNKTNUMMER\n";
        string compiled = "1 CYCL DEF 247 INIT. REF.PKT Q339=+1\n";

        Assert.Null(s_comparison.Compare(source, compiled, []));
    }

    // Comparison rules: a TOOL CALL line is compared as a set of words, a motion block word by word in order.
    [Fact]
    public void Compare_ToolCall_IsASetOfWordsAndAMotionBlockIsNot()
    {
        const string Header = "0 BEGIN PGM T MM\n";

        Assert.Null(s_comparison.Compare(Header + "1 TOOL CALL 1 Z S1592", Header + "1 TOOL CALL 1 S1592 Z", []));
        Assert.NotNull(s_comparison.Compare(Header + "1 L X+1 Y+2", Header + "1 L Y+2 X+1", []));
    }

    // Comparison rules (P3-07): a real difference is shown as a diff of the two programs.
    [Fact]
    public void Compare_RealDifference_IsShownAsADiff()
    {
        string? difference = s_comparison.Compare(
            "0 BEGIN PGM T MM\n1 L X+10 FMAX\n", "0 BEGIN PGM T MM\n1 L X+11 FMAX\n", []);

        Assert.Equal("@@ block 2 of the source, block 2 of the compiled program @@\n  BEGIN PGM T MM\n- L X10 FMAX\n"
            + "+ L X11 FMAX\n", difference);
    }

    // Phase 3: a difference the documents cannot settle is an exception grounded in its question, and an exception
    // that matches no difference is a difference of its own, never a silent one.
    [Fact]
    public void Compare_Exceptions_AreUsedOnceAndNeverSilently()
    {
        var exception = new NcException { Grounds = "wave-1 question #12", Compiled = ["M30"] };

        Assert.Null(s_comparison.Compare("1 M5\n2 END PGM T MM", "1 M5\n2 M30\n3 END PGM T MM", [exception]));
        Assert.Equal("The exception grounded in wave-1 question #12 matches no difference of the two programs.",
            s_comparison.Compare("1 M5\n2 END PGM T MM", "1 M5\n2 END PGM T MM", [exception]));
    }

    private static MachineConfig Mill()
    {
        var diagnostics = new Diagnostics("heidenhain-itnc530.toml");
        MachineConfig? machine = MachineConfigLoader.Load(
            Path.Combine(Fixture.RepositoryRoot(), "machines", "heidenhain-itnc530.toml"), diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        return machine;
    }
}
