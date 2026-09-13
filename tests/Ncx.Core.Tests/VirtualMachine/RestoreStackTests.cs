using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.Tests.VirtualMachine.Validation;
using Ncx.Core.VirtualMachine.State;
using Ncx.Core.Writing;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// The pseudo-words of generated blocks: @SAVE pushes the current value of the state variable its state key names on
/// a restore stack, @RESTORE pops it and re-applies it as if the program had written the words again, one stack per
/// state variable (virtual machine 3.10, D95).
/// </summary>
public sealed class RestoreStackTests
{
    // A saved SPINDLE:MAIN=CW RPM:MAIN=1500 comes back as those two words (virtual machine 3.10).
    [Fact]
    public void VirtualMachine310_RestoredSpindle_ComesBackAsTheWordsThatSetIt()
    {
        var harness = new VmHarness(VmMachines.MillTurn());
        harness.Execute("SPINDLE:MAIN=CW RPM:MAIN=1500");
        harness.Vm.Execute(Generated("@SAVE=SPINDLE:MAIN"));
        harness.Vm.Execute(Generated("SPINDLE:MAIN=OFF RPM:MAIN=200"));

        Block restored = harness.Vm.ExecutePseudoWords(Generated("@RESTORE=SPINDLE:MAIN"));

        Assert.Equal("SPINDLE:MAIN=CW RPM:MAIN=1500", NcxWriter.WriteBlock(restored));
        Assert.True(restored.IsGenerated);
        Assert.Empty(harness.State.Flow.RestoreStack);
    }

    // @RESTORE re-applies the saved value as if the program had written the words again; the rule never knew the speed
    // (virtual machine 3.10).
    [Fact]
    public void VirtualMachine310_Restore_ReappliesTheSavedDirectionAndSpeed()
    {
        var harness = new VmHarness(VmMachines.MillTurn());
        harness.Execute("SPINDLE:MAIN=CW RPM:MAIN=1500");
        harness.Vm.Execute(Generated("@SAVE=SPINDLE:MAIN"));
        harness.Vm.Execute(Generated("SPINDLE:MAIN=OFF RPM:MAIN=200"));

        harness.Vm.Execute(Generated("@RESTORE=SPINDLE:MAIN"));

        harness.AssertNoDiagnostics();
        SpindleState main = harness.State.Spindles["S1"];
        Assert.Equal(SpindleDirection.Clockwise, main.Direction);
        Assert.Equal(1500m, main.Rpm);
        Assert.Empty(harness.State.Flow.RestoreStack);
    }

    // Each state variable the restore stack keeps comes back as it was saved (virtual machine 3.10).
    [Theory]
    [InlineData("COOLANT=ON", "COOLANT", "COOLANT=OFF")]
    [InlineData("COOLANT:THROUGH=ON", "COOLANT:THROUGH", "COOLANT:THROUGH=OFF")]
    [InlineData("RPM:MAIN=1500", "RPM:MAIN", "RPM:MAIN=100")]
    [InlineData("SPINDLE_MODE:MAIN=AXIS", "SPINDLE_MODE:MAIN", "SPINDLE_MODE:MAIN=SPINDLE")]
    [InlineData("CSS:MAIN=ON", "CSS:MAIN", "CSS:MAIN=OFF")]
    [InlineData("VC:MAIN=140", "VC:MAIN", "VC:MAIN=200")]
    [InlineData("RPM_MAX:MAIN=3000", "RPM_MAX:MAIN", "RPM_MAX:MAIN=2000")]
    [InlineData("FUNC:SUB_CHUCK=OPEN", "FUNC:SUB_CHUCK", "FUNC:SUB_CHUCK=CLOSE")]
    [InlineData("F=200", "F", "F=300")]
    [InlineData("FEED_MODE=PER_REV", "FEED_MODE", "FEED_MODE=PER_MIN")]
    [InlineData("COMP=LEFT", "COMP", "COMP=OFF")]
    [InlineData("DIAMETER=ON", "DIAMETER", "DIAMETER=OFF")]
    public void VirtualMachine310_SaveAndRestore_PutTheVariableBack(string set, string stateKey, string change)
    {
        var harness = new VmHarness(VmMachines.MillTurn());
        harness.Execute(set);
        ChannelSnapshot saved = harness.Vm.Snapshot();
        harness.Vm.Execute(Generated("@SAVE=" + stateKey));
        harness.Execute(change);

        harness.Vm.Execute(Generated("@RESTORE=" + stateKey));

        harness.AssertNoDiagnostics();
        StateAssert.Same(saved, harness.Vm.Snapshot());
    }

    // A value that was none, a feed never set or a function without a state, is none again: no word sets none
    // (virtual machine 2.2, 2.5, 3.10).
    [Theory]
    [InlineData("F", "F=100")]
    [InlineData("FUNC:SUB_CHUCK", "FUNC:SUB_CHUCK=OPEN")]
    [InlineData("VC:MAIN", "VC:MAIN=140")]
    public void VirtualMachine310_SavedNone_IsRestoredToNone(string stateKey, string change)
    {
        // The first word on the spindle resolves it into the state; the snapshot is taken after that, so that both
        // states hold the same resources.
        var harness = new VmHarness(VmMachines.MillTurn());
        harness.Execute("SPINDLE:MAIN=OFF");
        ChannelSnapshot saved = harness.Vm.Snapshot();
        harness.Vm.Execute(Generated("@SAVE=" + stateKey));
        harness.Execute(change);

        harness.Vm.Execute(Generated("@RESTORE=" + stateKey));

        harness.AssertNoDiagnostics();
        StateAssert.Same(saved, harness.Vm.Snapshot());
        Assert.Null(harness.State.Motion.Feed);
    }

    // A speed set from an expression is UNKNOWN in STATIC mode and comes back UNKNOWN (virtual machine 1, 3.10).
    [Fact]
    public void VirtualMachine1_UnknownSpeed_IsRestoredUnknown()
    {
        var harness = new VmHarness(VmMachines.MillTurn());
        harness.Execute("SPINDLE:MAIN=CW RPM:MAIN={$Q1 * 2}");
        harness.Vm.Execute(Generated("@SAVE=SPINDLE:MAIN"));
        harness.Vm.Execute(Generated("SPINDLE:MAIN=OFF RPM:MAIN=100"));
        Assert.DoesNotContain("RPM:S1", harness.State.Unknown);

        harness.Vm.Execute(Generated("@RESTORE=SPINDLE:MAIN"));

        Assert.False(harness.Diagnostics.HasErrors, harness.Diagnostics.ToText());
        Assert.Equal(SpindleDirection.Clockwise, harness.State.Spindles["S1"].Direction);
        Assert.Contains("RPM:S1", harness.State.Unknown);
    }

    // The restore stack is one stack per state variable: @RESTORE pops the value of its own variable, whatever was
    // saved after it (virtual machine 3.10).
    [Fact]
    public void VirtualMachine310_Restore_PopsTheValueOfItsOwnVariable()
    {
        var harness = new VmHarness(VmMachines.MillTurn());
        harness.Execute("SPINDLE:MAIN=CW RPM:MAIN=1500", "COOLANT:THROUGH=ON");
        harness.Vm.Execute(Generated("@SAVE=SPINDLE:MAIN"));
        harness.Vm.Execute(Generated("@SAVE=COOLANT:THROUGH"));
        harness.Execute("SPINDLE:MAIN=OFF", "COOLANT:THROUGH=OFF");

        harness.Vm.Execute(Generated("@RESTORE=SPINDLE:MAIN"));

        Assert.Equal(SpindleDirection.Clockwise, harness.State.Spindles["S1"].Direction);
        Assert.False(harness.State.Coolant["THROUGH"]);
        Assert.Equal("COOLANT:THROUGH", Assert.Single(harness.State.Flow.RestoreStack).Key.ToCanonical());
        harness.Vm.Execute(Generated("@RESTORE=COOLANT:THROUGH"));
        Assert.True(harness.State.Coolant["THROUGH"]);
        harness.AssertNoDiagnostics();
    }

    // Two saves of one variable come back newest first (virtual machine 3.10, a stack).
    [Fact]
    public void VirtualMachine310_TwoSavesOfOneVariable_ComeBackNewestFirst()
    {
        var harness = new VmHarness(VmMachines.MillTurn());
        harness.Execute("RPM:MAIN=1000");
        harness.Vm.Execute(Generated("@SAVE=RPM:MAIN"));
        harness.Execute("RPM:MAIN=2000");
        harness.Vm.Execute(Generated("@SAVE=RPM:MAIN"));
        harness.Execute("RPM:MAIN=3000");

        harness.Vm.Execute(Generated("@RESTORE=RPM:MAIN"));
        Assert.Equal(2000m, harness.State.Spindles["S1"].Rpm);
        harness.Vm.Execute(Generated("@RESTORE=RPM:MAIN"));
        Assert.Equal(1000m, harness.State.Spindles["S1"].Rpm);
        harness.AssertNoDiagnostics();
    }

    // SPINDLE without a role and SPINDLE:MAIN name one state variable when MAIN is the default spindle (virtual
    // machine 3.8 rule 2, 3.10).
    [Fact]
    public void VirtualMachine310_KeyWithAndWithoutTheRoleOfTheDefaultSpindle_NameOneVariable()
    {
        var harness = new VmHarness(VmMachines.MillTurn());
        harness.Execute("SPINDLE=CW RPM=800");
        harness.Vm.Execute(Generated("@SAVE=SPINDLE"));
        harness.Execute("SPINDLE=OFF");

        harness.Vm.Execute(Generated("@RESTORE=SPINDLE:MAIN"));

        harness.AssertNoDiagnostics();
        Assert.Equal(SpindleDirection.Clockwise, harness.State.Spindles["S1"].Direction);
    }

    // @RESTORE without a saved value of its variable is an ERROR on the generated block, which names its origin
    // (virtual machine 3.10, D98).
    [Fact]
    public void VirtualMachine310_RestoreWithNothingSaved_IsAnError()
    {
        var harness = new VmHarness(VmMachines.MillTurn());
        harness.Vm.Execute(Generated("@SAVE=COOLANT"));

        harness.Vm.Execute(Generated("@RESTORE=SPINDLE:MAIN", line: 12));

        Diagnostic error = RuleAssert.Only(harness, DiagnosticCodes.NothingSaved);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(12, error.OriginLine);
    }

    // A state key that names no state variable the restore stack keeps is an ERROR (virtual machine 3.10).
    [Theory]
    [InlineData("@SAVE=TOOL")]
    [InlineData("@SAVE=X")]
    [InlineData("@SAVE=CYCLE")]
    [InlineData("@SAVE=FUNC")]
    [InlineData("@SAVE=F:X")]
    [InlineData("@RESTORE=SHIFT")]
    public void VirtualMachine310_StateKeyTheStackDoesNotKeep_IsAnError(string text)
    {
        var harness = new VmHarness(VmMachines.MillTurn());

        harness.Vm.Execute(Generated(text));

        RuleAssert.Only(harness, DiagnosticCodes.StateKeyNotKept);
    }

    // The snapshot keeps the restore stack with the state key as written, the variable it names and the words that
    // set it again (virtual machine 3.10, architecture 5.3).
    [Fact]
    public void VirtualMachine310_Snapshot_CarriesTheSavedEntry()
    {
        var harness = new VmHarness(VmMachines.MillTurn());
        harness.Execute("SPINDLE:MAIN=CCW RPM:MAIN=900");
        harness.Vm.Execute(Generated("@SAVE=SPINDLE:MAIN"));

        RestoreEntry entry = Assert.Single(harness.Vm.Snapshot().Flow.RestoreStack);

        Assert.Equal("SPINDLE:MAIN", entry.Key.ToCanonical());
        Assert.Equal("SPINDLE:S1", entry.Variable);
        Assert.Equal(["SPINDLE:MAIN=CCW", "RPM:MAIN=900"], Canonical(entry.Words));
        Assert.Empty(entry.UnknownKeys);
    }

    // A pseudo-word is accepted only in a block the expander generated; in a block of the file it is the ERROR
    // "pseudo-word in a user file" and does nothing (virtual machine 3 step 1, D95).
    [Fact]
    public void D95_PseudoWordInABlockOfTheFile_IsPseudoWordInAUserFile()
    {
        var harness = new VmHarness(VmMachines.MillTurn());
        harness.Execute("SPINDLE:MAIN=CW RPM:MAIN=1500");
        Block ofTheFile = Generated("@SAVE=SPINDLE:MAIN") with { IsGenerated = false, OriginLine = null };

        harness.Vm.Execute(ofTheFile);

        Diagnostic error = Assert.Single(harness.Diagnostics.Items);
        Assert.Equal(DiagnosticCodes.PseudoWordInUserFile, error.Code);
        Assert.Contains("pseudo-word in a user file", error.Message, StringComparison.Ordinal);
        Assert.Empty(harness.State.Flow.RestoreStack);
    }

    // A block of generated text, parsed with the option the expander uses (D95), as the expander marks it.
    private static Block Generated(string text, int line = 90)
    {
        var diagnostics = new Diagnostics("test.ncx");
        Block? block = Parser.ParseBlock(text, line, new ParserOptions { AllowPseudoWords = true }, diagnostics);
        Assert.True(diagnostics.Items.Count == 0, diagnostics.ToText());
        Assert.NotNull(block);
        return block with { IsGenerated = true, OriginLine = line };
    }

    private static List<string> Canonical(IReadOnlyList<Word> words)
    {
        var texts = new List<string>();
        foreach (Word word in words)
        {
            texts.Add(word.ToCanonical());
        }

        return texts;
    }
}
