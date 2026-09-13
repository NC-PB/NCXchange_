using Ncx.Core.Model;
using Ncx.Core.Tests.VirtualMachine;
using Ncx.Core.VirtualMachine;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.Expander;

/// <summary>
/// The shop-floor example of machine-config 5a: the through-spindle coolant engages its clutch only while the spindle
/// stands, so on this machine COOLANT:THROUGH=ON with requires = { SPINDLE = "OFF" } and restore = ["SPINDLE"] is a
/// stop, the coolant and a restart with the speed the spindle had (architecture 5.5, virtual machine 3.10, D63).
/// </summary>
public sealed class CoolantClutchRuleTests
{
    private static readonly string s_program = ExpanderHarness.File(
        "UNITS=MM",
        "SPINDLE:MAIN=CW RPM:MAIN=1500",
        "COOLANT:THROUGH=ON");

    // The expander emits @SAVE=SPINDLE:MAIN, SPINDLE:MAIN=OFF, the coolant block, @RESTORE=SPINDLE:MAIN
    // (architecture 5.5, machine-config 5a).
    [Fact]
    public void MachineConfig5a_CoolantClutch_InsertsSaveStopCoolantAndRestore()
    {
        NcxProgram expanded = ExpanderHarness.Expand(s_program, ExpanderMachines.CoolantClutch());

        Assert.Equal(
            ExpanderHarness.File(
                "UNITS=MM",
                "SPINDLE:MAIN=CW RPM:MAIN=1500",
                "@SAVE=SPINDLE:MAIN",
                "SPINDLE:MAIN=OFF",
                "COOLANT:THROUGH=ON",
                "@RESTORE=SPINDLE:MAIN"),
            ExpanderHarness.WriteWithGenerated(expanded));
        Assert.Empty(expanded.Diagnostics.Items);
    }

    // The VM turns the restore into SPINDLE:MAIN=CW RPM:MAIN=1500 from its own snapshot, so the state after the
    // coolant block equals the state without the rule plus the coolant, RPM:MAIN restored (virtual machine 3.10).
    [Fact]
    public void VirtualMachine310_CoolantClutch_StateAfterEqualsTheStateWithoutTheRulePlusTheCoolant()
    {
        ChannelSnapshot withRule = ExpanderHarness.StateBeforeProgramEnd(
            ExpanderHarness.Expand(s_program, ExpanderMachines.CoolantClutch()), ExpanderMachines.CoolantClutch());
        ChannelSnapshot withoutRule = ExpanderHarness.StateBeforeProgramEnd(
            ExpanderHarness.Parse(s_program), ExpanderMachines.Mill());

        StateAssert.Same(withoutRule, withRule);
        Assert.Equal(SpindleDirection.Clockwise, withRule.Spindles["S1"].Direction);
        Assert.Equal(1500m, withRule.Spindles["S1"].Rpm);
        Assert.True(withRule.Coolant["THROUGH"]);
        Assert.Empty(withRule.Flow.RestoreStack);
    }

    // requires: the condition holds while the function is written, so the spindle stands in the coolant block
    // (machine-config 5a).
    [Fact]
    public void MachineConfig5a_CoolantClutch_SpindleStandsWhileTheCoolantEngages()
    {
        ChannelSnapshot atTheCoolant = ExpanderHarness.StateAfter(
            ExpanderHarness.Expand(s_program, ExpanderMachines.CoolantClutch()), ExpanderMachines.CoolantClutch(),
            "COOLANT:THROUGH=ON");

        Assert.Equal(SpindleDirection.Off, atTheCoolant.Spindles["S1"].Direction);
        Assert.True(atTheCoolant.Coolant["THROUGH"]);
        Assert.Single(atTheCoolant.Flow.RestoreStack);
    }

    // Generated blocks are executed like any other; a STATIC run of the expanded program reports nothing (virtual
    // machine 1, 3.10).
    [Fact]
    public void VirtualMachine310_CoolantClutch_RunsStaticWithoutADiagnostic()
    {
        Diagnostics diagnostics = ExpanderHarness.Run(
            ExpanderHarness.Expand(s_program, ExpanderMachines.CoolantClutch()), ExpanderMachines.CoolantClutch(),
            out RunResult result);

        Assert.False(result.Stopped);
        Assert.True(diagnostics.Items.Count == 0, diagnostics.ToText());
    }

    // A requires key with its role written names the same variable as the bare key of the default spindle
    // (virtual machine 3.8 rule 2, 3.10).
    [Fact]
    public void MachineConfig5a_RequiresWithTheRoleWritten_GivesTheSameBlocks()
    {
        var withRole = new Ncx.Core.Machine.ExpansionRule
        {
            Requires = new Dictionary<string, string> { ["SPINDLE:MAIN"] = "OFF" },
            Restore = ["SPINDLE:MAIN"],
        };

        Assert.Equal(
            ExpanderHarness.WriteWithGenerated(ExpanderHarness.Expand(s_program, ExpanderMachines.CoolantClutch())),
            ExpanderHarness.WriteWithGenerated(
                ExpanderHarness.Expand(s_program, ExpanderMachines.CoolantRule(withRole))));
    }
}
