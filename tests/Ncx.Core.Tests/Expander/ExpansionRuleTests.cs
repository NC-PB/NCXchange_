using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Tests.VirtualMachine.Validation;
using Ncx.Core.VirtualMachine;

namespace Ncx.Core.Tests.Expander;

/// <summary>
/// The expansion rules of machine-config 5a on the tool change, a function state and a catalog cycle: pre, post,
/// requires and restore become generated NCX blocks around the triggering block, {position:NAME} the axis words of
/// [positions] (architecture 5.5, D63, D100).
/// </summary>
public sealed class ExpansionRuleTests
{
    // [tool_change] pre = ["HOME Z"] inserts HOME Z before every TOOL block: TOOL=n, a bare TOOL, TOOL=0
    // (machine-config 5a, the retract before the change).
    [Fact]
    public void MachineConfig5a_ToolChangePreHomeZ_InsertsHomeZBeforeEveryToolBlock()
    {
        MachineConfig machine = ExpanderMachines.ToolChange(new ExpansionRule { Pre = ["HOME Z"] });
        string program = ExpanderHarness.File("UNITS=MM", "TOOL=1", "RAPID X=10", "PRELOAD=2", "TOOL", "TOOL=0");

        NcxProgram expanded = ExpanderHarness.Expand(program, machine);

        Assert.Equal(
            ExpanderHarness.File(
                "UNITS=MM", "HOME Z", "TOOL=1", "RAPID X=10", "PRELOAD=2", "HOME Z", "TOOL", "HOME Z", "TOOL=0"),
            ExpanderHarness.WriteWithGenerated(expanded));
        Diagnostics diagnostics = ExpanderHarness.Run(expanded, machine, out _);
        Assert.True(diagnostics.Items.Count == 0, diagnostics.ToText());
    }

    // pre = ["RAPID {position:tool_change} FRAME=MACHINE"] with tool_change = { X = 0, Z = -120 } inserts
    // RAPID X=0 Z=-120 FRAME=MACHINE (machine-config 5a, D100).
    [Fact]
    public void D100_PositionPlaceholder_InsertsTheAxisWordsOfThePosition()
    {
        MachineConfig machine = ExpanderMachines.ToolChange(
            new ExpansionRule { Pre = ["RAPID {position:tool_change} FRAME=MACHINE"] });

        NcxProgram expanded = ExpanderHarness.Expand(ExpanderHarness.File("UNITS=MM", "TOOL=1"), machine);

        Assert.Equal(
            ExpanderHarness.File("UNITS=MM", "RAPID X=0 Z=-120 FRAME=MACHINE", "TOOL=1"),
            ExpanderHarness.WriteWithGenerated(expanded));
        Diagnostics diagnostics = ExpanderHarness.Run(expanded, machine, out _);
        Assert.True(diagnostics.Items.Count == 0, diagnostics.ToText());
    }

    // An unknown name in {position:NAME} is an ERROR on the rule, reported once for the rule, and the run stops
    // (architecture 5.5, D100, virtual machine 2.9).
    [Fact]
    public void D100_UnknownPosition_IsAnErrorOnTheRule()
    {
        MachineConfig machine = ExpanderMachines.ToolChange(
            new ExpansionRule { Pre = ["RAPID {position:tool_chnage} FRAME=MACHINE"] });
        string program = ExpanderHarness.File("UNITS=MM", "TOOL=1", "TOOL=2");

        NcxProgram expanded = ExpanderHarness.Expand(program, machine);

        Diagnostic error = RuleAssert.Only(expanded.Diagnostics, DiagnosticCodes.UnknownPosition);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(4, error.Line);
        Assert.Equal(4, error.OriginLine);
        Assert.Contains("tool_chnage", error.Message, StringComparison.Ordinal);
        Assert.Contains("[tool_change]", error.Message, StringComparison.Ordinal);
        Assert.Equal(program, ExpanderHarness.WriteWithGenerated(expanded));
        ExpanderHarness.Run(expanded, machine, out RunResult result);
        Assert.True(result.Stopped);
    }

    // pre and post of a function state: PALLET_CHANGE = { RUN = "M60", pre = [...], post = ["ORIGIN=1"] }
    // (machine-config 5a).
    [Fact]
    public void MachineConfig5a_FunctionPreAndPost_StandBeforeAndAfterTheBlock()
    {
        MachineConfig machine = ExpanderMachines.PalletChange(
            new ExpansionRule { Pre = ["HOME Z", "HOME X"], Post = ["ORIGIN=1"] });

        NcxProgram expanded = ExpanderHarness.Expand(
            ExpanderHarness.File("UNITS=MM", "FUNC:PALLET_CHANGE=RUN"), machine);

        Assert.Equal(
            ExpanderHarness.File("UNITS=MM", "HOME Z", "HOME X", "FUNC:PALLET_CHANGE=RUN", "ORIGIN=1"),
            ExpanderHarness.WriteWithGenerated(expanded));
    }

    // A catalog cycle may carry pre (machine-config 5a, the Doosan M291 before G83). Which block triggers the rule of a
    // catalog cycle is open (TODO(question) in ExpansionRules): the rule fires on the block that names the cycle, so
    // the mode function stands before that block and not before its CYCLE_CALL blocks.
    [Fact]
    public void MachineConfig5a_CatalogCyclePre_StandsBeforeTheBlockThatNamesTheCycle()
    {
        MachineConfig machine = ExpanderMachines.Peck(new ExpansionRule { Pre = ["FUNC:PECK_MODE=RETRACT"] });
        string program = ExpanderHarness.File(
            "UNITS=MM",
            "CYCLE=PECK CLEARANCE=5 DEPTH=-20 PECK=2 CYCLE_F=100",
            "CYCLE_CALL X=10 Y=10",
            "CYCLE_CALL X=20 Y=10",
            "CYCLE=OFF");

        NcxProgram expanded = ExpanderHarness.Expand(program, machine);

        Assert.Equal(
            ExpanderHarness.File(
                "UNITS=MM",
                "FUNC:PECK_MODE=RETRACT",
                "CYCLE=PECK CLEARANCE=5 DEPTH=-20 PECK=2 CYCLE_F=100",
                "CYCLE_CALL X=10 Y=10",
                "CYCLE_CALL X=20 Y=10",
                "CYCLE=OFF"),
            ExpanderHarness.WriteWithGenerated(expanded));
    }

    // The same reading for post (the open question in ExpansionRules): the post block stands directly after the block
    // that names the cycle, before its CYCLE_CALL blocks, so it switches the mode back before the holes are drilled.
    [Fact]
    public void MachineConfig5a_CatalogCyclePost_StandsDirectlyAfterTheBlockThatNamesTheCycle()
    {
        MachineConfig machine = ExpanderMachines.Peck(
            new ExpansionRule { Pre = ["FUNC:PECK_MODE=RETRACT"], Post = ["FUNC:PECK_MODE=CHIP_BREAK"] });
        string program = ExpanderHarness.File(
            "UNITS=MM",
            "CYCLE=PECK CLEARANCE=5 DEPTH=-20 PECK=2 CYCLE_F=100",
            "CYCLE_CALL X=10 Y=10",
            "CYCLE=OFF");

        NcxProgram expanded = ExpanderHarness.Expand(program, machine);

        Assert.Equal(
            ExpanderHarness.File(
                "UNITS=MM",
                "FUNC:PECK_MODE=RETRACT",
                "CYCLE=PECK CLEARANCE=5 DEPTH=-20 PECK=2 CYCLE_F=100",
                "FUNC:PECK_MODE=CHIP_BREAK",
                "CYCLE_CALL X=10 Y=10",
                "CYCLE=OFF"),
            ExpanderHarness.WriteWithGenerated(expanded));
    }

    // restore puts a variable back through @RESTORE from what @SAVE kept, so a restore list is saved before the pre
    // blocks also when the rule has no requires (machine-config 5a, virtual machine 3.10).
    [Fact]
    public void MachineConfig5a_RestoreWithoutRequires_SavesBeforeThePreBlocks()
    {
        MachineConfig machine = ExpanderMachines.PalletChange(
            new ExpansionRule { Pre = ["SPINDLE:MAIN=OFF"], Restore = ["SPINDLE"] });

        NcxProgram expanded = ExpanderHarness.Expand(
            ExpanderHarness.File("UNITS=MM", "FUNC:PALLET_CHANGE=RUN"), machine);

        Assert.Equal(
            ExpanderHarness.File(
                "UNITS=MM",
                "@SAVE=SPINDLE:MAIN",
                "SPINDLE:MAIN=OFF",
                "FUNC:PALLET_CHANGE=RUN",
                "@RESTORE=SPINDLE:MAIN"),
            ExpanderHarness.WriteWithGenerated(expanded));
    }

    // Two rules on one block nest, the rule of the first word in canonical order outermost: the tool change around the
    // coolant clutch (architecture 5.5; code-guidelines 5, chain of rewriters).
    [Fact]
    public void Architecture55_TwoRulesOnOneBlock_NestInTheCanonicalOrderOfTheirWords()
    {
        MachineConfig machine = ExpanderMachines.CoolantClutch() with
        {
            ToolChange = new ToolChangeConfig { Rule = new ExpansionRule { Pre = ["HOME Z"], Post = ["COOLANT=ON"] } },
        };
        string program = ExpanderHarness.File(
            "UNITS=MM", "SPINDLE:MAIN=CW RPM:MAIN=1500", "COOLANT:THROUGH=ON TOOL=2");

        NcxProgram expanded = ExpanderHarness.Expand(program, machine);

        Assert.Equal(
            ExpanderHarness.File(
                "UNITS=MM",
                "SPINDLE:MAIN=CW RPM:MAIN=1500",
                "HOME Z",
                "@SAVE=SPINDLE:MAIN",
                "SPINDLE:MAIN=OFF",
                "TOOL=2 COOLANT:THROUGH=ON",
                "@RESTORE=SPINDLE:MAIN",
                "COOLANT=ON"),
            ExpanderHarness.WriteWithGenerated(expanded));
    }

    // A generated block carries the SKIP of the block it was generated for, so that skip_blocks skips them together
    // (language 4.1, D53).
    [Fact]
    public void D53_GeneratedBlocks_CarryTheSkipOfTheirOrigin()
    {
        MachineConfig machine = ExpanderMachines.ToolChange(new ExpansionRule { Pre = ["HOME Z"] });

        NcxProgram expanded = ExpanderHarness.Expand(ExpanderHarness.File("UNITS=MM", "SKIP=2 TOOL=1"), machine);

        Assert.Equal(
            ExpanderHarness.File("UNITS=MM", "SKIP=2 HOME Z", "SKIP=2 TOOL=1"),
            ExpanderHarness.WriteWithGenerated(expanded));
    }

    // A rule of a spindle table applies to the spindle words of its role; a bare SPINDLE addresses the default
    // spindle and its table (machine-config 5, 5a; virtual machine 3.8 rule 2).
    [Fact]
    public void MachineConfig5a_SpindleTableRule_AppliesToTheWordsOfItsSpindle()
    {
        MachineConfig mill = ExpanderMachines.Mill();
        FunctionTable main = mill.SpindleTables["MAIN"] with
        {
            Rule = new ExpansionRule { Requires = new Dictionary<string, string> { ["COOLANT"] = "OFF" } },
        };
        MachineConfig machine = mill with { SpindleTables = new Dictionary<string, FunctionTable> { ["MAIN"] = main } };

        NcxProgram expanded = ExpanderHarness.Expand(
            ExpanderHarness.File("UNITS=MM", "SPINDLE=CW", "COOLANT=ON"), machine);

        Assert.Equal(
            ExpanderHarness.File("UNITS=MM", "COOLANT=OFF", "SPINDLE=CW", "COOLANT=ON"),
            ExpanderHarness.WriteWithGenerated(expanded));
    }
}
