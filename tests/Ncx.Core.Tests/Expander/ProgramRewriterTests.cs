using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Tests.VirtualMachine;
using Ncx.Core.VirtualMachine.State;
using Ncx.Core.Writing;

namespace Ncx.Core.Tests.Expander;

/// <summary>
/// The program rewriters of the plugins in the expander: every block passes once, and Unchanged, Replace and Surround
/// are the three answers, given as NCX text the expander parses (architecture 5.5, 9; code-guidelines 11; D61, D106).
/// </summary>
public sealed class ProgramRewriterTests
{
    private static readonly string s_coolant = ExpanderHarness.File(
        "UNITS=MM",
        "SPINDLE:MAIN=CW RPM:MAIN=1500",
        "COOLANT:THROUGH=ON");

    // Every block of the program passes through a rewriter once, with the machine name, the channel and the line
    // and no VM (code-guidelines 11, D80, D106); Unchanged changes nothing.
    [Fact]
    public void IProgramRewriter_Unchanged_SeesEveryBlockOnceAndChangesNothing()
    {
        var recorder = new ContextRecorder();

        NcxProgram expanded = ExpanderHarness.Expand(s_coolant, ExpanderMachines.Mill(), recorder);

        Assert.Equal(
            [
                "Test mill 1 1 FILE=BEGIN NCX=1 0",
                "Test mill 1 2 PROGRAM=BEGIN NAME=\"TEST\" 0",
                "Test mill 1 3 UNITS=MM 0",
                "Test mill 1 4 SPINDLE:MAIN=CW RPM:MAIN=1500 0",
                "Test mill 1 5 COOLANT:THROUGH=ON 0",
                "Test mill 1 6 PROGRAM=END 0",
                "Test mill 1 7 FILE=END 0",
            ],
            recorder.Calls);
        Assert.Equal(s_coolant, ExpanderHarness.WriteWithGenerated(expanded));
    }

    // The CoolantClutchRule of the plugin template produces the same generated blocks as the configuration rule and
    // the same state afterwards (code-guidelines 11, machine-config 5a).
    [Fact]
    public void IProgramRewriter_CoolantClutchRule_GivesTheBlocksAndTheStateOfTheConfigurationRule()
    {
        NcxProgram byRewriter = ExpanderHarness.Expand(s_coolant, ExpanderMachines.Mill(), new CoolantClutchRule());
        NcxProgram byRule = ExpanderHarness.Expand(s_coolant, ExpanderMachines.CoolantClutch());

        Assert.Equal(ExpanderHarness.WriteWithGenerated(byRule), ExpanderHarness.WriteWithGenerated(byRewriter));
        StateAssert.Same(
            ExpanderHarness.StateBeforeProgramEnd(byRule, ExpanderMachines.CoolantClutch()),
            ExpanderHarness.StateBeforeProgramEnd(byRewriter, ExpanderMachines.Mill()));
        Block stop = ExpanderHarness.Find(byRewriter, "SPINDLE:MAIN=OFF");
        Assert.Equal("CoolantClutchRule", stop.Generated?.Source);
        Assert.Equal("the coolant clutch needs a standing spindle", stop.Generated?.Reason);
    }

    // Replace rewrites the words of the block; the VM executes the changed block and ncx format still writes the
    // original (architecture 9, language 4.15).
    [Fact]
    public void IProgramRewriter_Replace_RewritesTheBlockInPlace()
    {
        var slower = new ReplacingRewriter(
            "SPINDLE:MAIN=CW RPM:MAIN=1500", "SPINDLE:MAIN=CW RPM:MAIN=1200", "the tool is long");
        NcxProgram original = ExpanderHarness.Parse(s_coolant);

        NcxProgram expanded = Ncx.Core.Expander.Expander.Expand(original, ExpanderMachines.Mill(), [slower]);

        Block replaced = ExpanderHarness.Find(expanded, "SPINDLE:MAIN=CW RPM:MAIN=1200");
        Assert.Equal(GeneratedPlacement.InPlace, replaced.Generated?.Placement);
        Assert.Same(ExpanderHarness.Find(original, "SPINDLE:MAIN=CW RPM:MAIN=1500"), replaced.Generated?.Origin);
        Assert.Equal(4, replaced.OriginLine);
        Assert.Equal(original.Blocks.Count, expanded.Blocks.Count);
        Assert.Equal(1200m, ExpanderHarness.StateBeforeProgramEnd(expanded, ExpanderMachines.Mill()).Spindles["S1"].Rpm);
        Assert.Equal(NcxWriter.Write(original), NcxWriter.Write(expanded));
    }

    // The rewriters run in their order, and each sees the block as the one before left it (code-guidelines 5, chain of
    // rewriters).
    [Fact]
    public void IProgramRewriter_TwoRewriters_TheSecondSeesTheReplacementOfTheFirst()
    {
        var first = new ReplacingRewriter(
            "SPINDLE:MAIN=CW RPM:MAIN=1500", "SPINDLE:MAIN=CW RPM:MAIN=1200", "the tool is long");
        var second = new ReplacingRewriter(
            "SPINDLE:MAIN=CW RPM:MAIN=1200", "SPINDLE:MAIN=CW RPM:MAIN=1000", "the part is thin");
        NcxProgram original = ExpanderHarness.Parse(s_coolant);

        NcxProgram expanded = Ncx.Core.Expander.Expander.Expand(original, ExpanderMachines.Mill(), [first, second]);

        Block replaced = ExpanderHarness.Find(expanded, "SPINDLE:MAIN=CW RPM:MAIN=1000");
        Assert.Same(ExpanderHarness.Find(original, "SPINDLE:MAIN=CW RPM:MAIN=1500"), replaced.Generated?.Origin);
        Assert.Equal("the tool is long; the part is thin", replaced.Generated?.Reason);
    }

    // The blocks of a rewriter stand inside the blocks of the expansion rules, next to the block (architecture 5.5).
    [Fact]
    public void IProgramRewriter_Surround_NestsInsideTheBlocksOfTheRules()
    {
        MachineConfig machine = ExpanderMachines.ToolChange(new ExpansionRule { Pre = ["HOME Z"] });
        var coolant = new SurroundingRewriter("TOOL", ["COOLANT=OFF"], ["COOLANT=ON"]);

        NcxProgram expanded = ExpanderHarness.Expand(ExpanderHarness.File("UNITS=MM", "TOOL=1"), machine, coolant);

        Assert.Equal(
            ExpanderHarness.File("UNITS=MM", "HOME Z", "COOLANT=OFF", "TOOL=1", "COOLANT=ON"),
            ExpanderHarness.WriteWithGenerated(expanded));
        Assert.Equal("SurroundingRewriter", ExpanderHarness.Find(expanded, "COOLANT=OFF").Generated?.Source);
    }

    // A rewriter's text that does not parse is reported on the originating line with the rewriter's name
    // (code-guidelines 10.3, D98).
    [Fact]
    public void IProgramRewriter_TextThatDoesNotParse_IsReportedOnTheOriginatingLineWithTheRewriter()
    {
        var broken = new SurroundingRewriter("TOOL", ["FOO=1"], []);

        NcxProgram expanded = ExpanderHarness.Expand(
            ExpanderHarness.File("UNITS=MM", "TOOL=1"), ExpanderMachines.Mill(), broken);

        Diagnostic error = Assert.Single(expanded.Diagnostics.Items);
        Assert.Equal(DiagnosticCodes.UnknownKey, error.Code);
        Assert.Equal(4, error.OriginLine);
        Assert.StartsWith("SurroundingRewriter \"FOO=1\": ", error.Message, StringComparison.Ordinal);
    }

    // A rewriter may write the pseudo-words; the expander parses its text with the option of generated text (D95).
    [Fact]
    public void D95_RewriterText_MayCarryPseudoWords()
    {
        NcxProgram expanded = ExpanderHarness.Expand(s_coolant, ExpanderMachines.Mill(), new CoolantClutchRule());

        Assert.Empty(expanded.Diagnostics.Items);
        Assert.True(ExpanderHarness.Find(expanded, "@SAVE=SPINDLE:MAIN").IsGenerated);
        Assert.Equal(
            SpindleDirection.Off,
            ExpanderHarness.StateAfter(expanded, ExpanderMachines.Mill(), "COOLANT:THROUGH=ON").Spindles["S1"].Direction);
    }
}
