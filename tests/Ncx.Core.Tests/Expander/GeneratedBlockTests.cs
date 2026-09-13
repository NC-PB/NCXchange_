using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Tests.VirtualMachine.Validation;
using Ncx.Core.VirtualMachine;
using Ncx.Core.Writing;

namespace Ncx.Core.Tests.Expander;

/// <summary>
/// Generated blocks carry their origin, a diagnostic on one names the originating line, ncx format never writes them,
/// and only generated blocks may carry pseudo-words (language 4.15, virtual machine 3.10; D95, D98).
/// </summary>
public sealed class GeneratedBlockTests
{
    private static readonly string s_coolant = ExpanderHarness.File(
        "UNITS=MM",
        "SPINDLE:MAIN=CW RPM:MAIN=1500",
        "COOLANT:THROUGH=ON");

    // A generated block carries its origin: the block it was generated for, the rule that made it and why (virtual
    // machine 3.10).
    [Fact]
    public void VirtualMachine310_GeneratedBlock_CarriesTheBlockTheRuleAndTheReason()
    {
        NcxProgram original = ExpanderHarness.Parse(s_coolant);
        NcxProgram expanded = Ncx.Core.Expander.Expander.Expand(original, ExpanderMachines.CoolantClutch(), []);
        Block coolant = ExpanderHarness.Find(original, "COOLANT:THROUGH=ON");

        Block stop = ExpanderHarness.Find(expanded, "SPINDLE:MAIN=OFF");
        Block save = ExpanderHarness.Find(expanded, "@SAVE=SPINDLE:MAIN");
        Block restore = ExpanderHarness.Find(expanded, "@RESTORE=SPINDLE:MAIN");

        Assert.True(stop.IsGenerated);
        Assert.Equal(5, stop.OriginLine);
        Assert.Same(coolant, stop.Generated?.Origin);
        Assert.Equal("[coolant] THROUGH", stop.Generated?.Source);
        Assert.Equal("requires", stop.Generated?.Reason);
        Assert.Equal(GeneratedPlacement.Before, stop.Generated?.Placement);
        Assert.Equal("restore", save.Generated?.Reason);
        Assert.Equal(GeneratedPlacement.Before, save.Generated?.Placement);
        Assert.Equal(GeneratedPlacement.After, restore.Generated?.Placement);
        Assert.Same(coolant, ExpanderHarness.Find(expanded, "COOLANT:THROUGH=ON"));
    }

    // ncx format never writes generated blocks: format of the expanded program equals format of the original, also
    // where a rewriter or limits = "clamp" rewrote a block in place (language 4.15, D64, D91).
    [Fact]
    public void Language415_FormatOfTheExpandedProgram_EqualsFormatOfTheOriginal()
    {
        MachineConfig machine = ExpanderMachines.CoolantClutch() with
        {
            Limits = LimitPolicy.Clamp,
            ToolChange = new ToolChangeConfig { Rule = new ExpansionRule { Pre = ["HOME Z"] } },
        };
        string text = ExpanderHarness.File(
            "; setup",
            "UNITS=MM",
            "SPINDLE:MAIN=CW RPM:MAIN=8000 ; fast",
            "",
            "TOOL=1",
            "COOLANT:THROUGH=ON",
            "LINE X=10 F=100");
        var slower = new ReplacingRewriter("LINE X=10 F=100", "LINE X=10 F=50", "a slower feed");

        NcxProgram expanded = ExpanderHarness.Expand(text, machine, slower);

        Assert.Equal(NcxWriter.Write(ExpanderHarness.Parse(text)), NcxWriter.Write(expanded));
        Assert.NotEqual(NcxWriter.Write(expanded), ExpanderHarness.WriteWithGenerated(expanded));
    }

    // A diagnostic on a generated block names the block it was generated for: file(line, from 12) (language 4.15,
    // D98). HOME on an axis without a reference point is the WARNING of D100.
    [Fact]
    public void D98_DiagnosticOnAGeneratedBlock_NamesTheOriginatingLine()
    {
        MachineConfig machine = ExpanderMachines.ToolChange(new ExpansionRule { Pre = ["HOME Y"] });
        NcxProgram expanded = ExpanderHarness.Expand(ExpanderHarness.File("UNITS=MM", "TOOL=1"), machine);

        Diagnostics diagnostics = ExpanderHarness.Run(expanded, machine, out RunResult result);

        Assert.False(result.Stopped);
        Diagnostic warning = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.HomeWithoutReferencePoint, warning.Code);
        Assert.Equal(4, warning.OriginLine);
        Assert.StartsWith("test.ncx(4, from 4): WARNING VM060: ", warning.ToText(), StringComparison.Ordinal);
    }

    // Pseudo-words in a user file are an ERROR (D95): the parser reports it, the expander leaves the program as it is,
    // and the run stops before the first block (virtual machine 2.9).
    [Fact]
    public void D95_PseudoWordInAUserFile_IsAnErrorAndStopsTheRun()
    {
        MachineConfig machine = ExpanderMachines.CoolantClutch();
        string text = ExpanderHarness.File("UNITS=MM", "@SAVE=SPINDLE:MAIN", "COOLANT:THROUGH=ON");
        NcxProgram original = ExpanderHarness.Parse(text);

        NcxProgram expanded = Ncx.Core.Expander.Expander.Expand(original, machine, []);

        Assert.Equal(DiagnosticCodes.PseudoWordInUserFile, Assert.Single(expanded.Diagnostics.Items).Code);
        Assert.Equal(original.Blocks, expanded.Blocks);
        ExpanderHarness.Run(expanded, machine, out RunResult result);
        Assert.True(result.Stopped);
    }

    // The expander never changes the program it is given; it returns a new one (code-guidelines 7).
    [Fact]
    public void Architecture55_Expand_LeavesTheOriginalProgramAsItIs()
    {
        NcxProgram original = ExpanderHarness.Parse(s_coolant);
        int blocks = original.Blocks.Count;

        NcxProgram expanded = Ncx.Core.Expander.Expander.Expand(original, ExpanderMachines.CoolantClutch(), []);

        Assert.Equal(blocks, original.Blocks.Count);
        Assert.Equal(blocks + 3, expanded.Blocks.Count);
        Assert.NotSame(original.Diagnostics, expanded.Diagnostics);
        Assert.Equal(original.Sections[0].FirstBlock, expanded.Sections[0].FirstBlock);
        Assert.Equal(original.Sections[0].LastBlock + 3, expanded.Sections[0].LastBlock);
    }

    // The text of a rule is NCX: what does not parse is reported on the originating line and names the rule
    // (machine-config 5a, D98).
    [Fact]
    public void MachineConfig5a_RuleTextThatDoesNotParse_IsReportedOnTheOriginatingLine()
    {
        MachineConfig machine = ExpanderMachines.ToolChange(new ExpansionRule { Pre = ["FOO=1"] });

        NcxProgram expanded = ExpanderHarness.Expand(ExpanderHarness.File("UNITS=MM", "TOOL=1"), machine);

        Diagnostic error = Assert.Single(expanded.Diagnostics.Items);
        Assert.Equal(DiagnosticCodes.UnknownKey, error.Code);
        Assert.Equal(4, error.OriginLine);
        Assert.StartsWith("[tool_change] pre \"FOO=1\": ", error.Message, StringComparison.Ordinal);
    }

    // A rule text must be a block: a comment-only line is none (language 3, Block; machine-config 5a).
    [Fact]
    public void MachineConfig5a_RuleTextWithoutABlock_IsAnError()
    {
        MachineConfig machine = ExpanderMachines.ToolChange(new ExpansionRule { Pre = ["; a note"] });

        NcxProgram expanded = ExpanderHarness.Expand(ExpanderHarness.File("UNITS=MM", "TOOL=1"), machine);

        Diagnostic error = RuleAssert.Only(expanded.Diagnostics, DiagnosticCodes.GeneratedTextWithoutBlock);
        Assert.Equal(4, error.OriginLine);
    }

    // A generated block never opens or closes the file, a program or a subprogram: PROGRAM=END stands exactly once
    // (language 4.1, 4.13).
    [Fact]
    public void Language413_RuleTextWithAFrameWord_IsAnError()
    {
        MachineConfig machine = ExpanderMachines.ToolChange(new ExpansionRule { Pre = ["PROGRAM=END"] });

        NcxProgram expanded = ExpanderHarness.Expand(ExpanderHarness.File("UNITS=MM", "TOOL=1"), machine);

        RuleAssert.Only(expanded.Diagnostics, DiagnosticCodes.GeneratedFrameWord);
        Assert.Equal(
            ExpanderHarness.File("UNITS=MM", "TOOL=1"), ExpanderHarness.WriteWithGenerated(expanded));
    }

    // Nothing stands before a BEGIN block or after an END block: a block there would stand outside every program and
    // subprogram (language 4.13).
    [Fact]
    public void Language413_GeneratedBlockBeforeProgramBegin_IsAnError()
    {
        var rewriter = new SurroundingRewriter("PROGRAM", ["COOLANT=ON"], []);

        NcxProgram expanded = ExpanderHarness.Expand(ExpanderHarness.File("UNITS=MM"), ExpanderMachines.Mill(), rewriter);

        Diagnostic error = RuleAssert.Only(expanded.Diagnostics, DiagnosticCodes.GeneratedBlockOutsideSection);
        Assert.Equal(2, error.OriginLine);
        Assert.Equal(
            ExpanderHarness.File("UNITS=MM", "COOLANT=ON"), ExpanderHarness.WriteWithGenerated(expanded));
    }
}
