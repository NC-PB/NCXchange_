using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Tests.VirtualMachine.Validation;
using Ncx.Core.Writing;

namespace Ncx.Core.Tests.Expander;

/// <summary>
/// limits = "clamp": the expander rewrites RPM, F and a target beyond the machine limits to the limit, and the WARNING
/// says so; under "warn" it rewrites nothing and the virtual machine reports them (machine-config 1, virtual machine
/// 5, D64).
/// </summary>
public sealed class LimitClampTests
{
    // RPM above the rpm_max of its spindle is rewritten to rpm_max, with a WARNING on the originating line.
    [Fact]
    public void D64_RpmAboveRpmMax_IsClampedWithAWarning()
    {
        NcxProgram expanded = ExpanderHarness.Expand(
            ExpanderHarness.File("SPINDLE:MAIN=CW RPM:MAIN=8000"), ExpanderMachines.Clamp());

        Assert.Equal(
            ExpanderHarness.File("SPINDLE:MAIN=CW RPM:MAIN=6000"), ExpanderHarness.WriteWithGenerated(expanded));
        Diagnostic warning = RuleAssert.Only(expanded.Diagnostics, DiagnosticCodes.LimitClamped);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Equal(3, warning.OriginLine);
        Assert.Contains("rpm_max 6000", warning.Message, StringComparison.Ordinal);
        Assert.Contains("RPM:MAIN=6000", warning.Message, StringComparison.Ordinal);
        Block clamped = ExpanderHarness.Find(expanded, "SPINDLE:MAIN=CW RPM:MAIN=6000");
        Assert.Equal(GeneratedPlacement.InPlace, clamped.Generated?.Placement);
        Assert.Equal("[machine] limits", clamped.Generated?.Source);
    }

    // RPM below rpm_min is rewritten to rpm_min.
    [Fact]
    public void D64_RpmBelowRpmMin_IsClampedToRpmMin()
    {
        NcxProgram expanded = ExpanderHarness.Expand(
            ExpanderHarness.File("SPINDLE:MAIN=CW RPM:MAIN=20"), ExpanderMachines.Clamp());

        Assert.Equal(
            ExpanderHarness.File("SPINDLE:MAIN=CW RPM:MAIN=45"), ExpanderHarness.WriteWithGenerated(expanded));
    }

    // RPM without a role address is the speed of the default spindle and is clamped to its limits (virtual machine
    // 3.8 rule 2).
    [Fact]
    public void D64_RpmWithoutRole_IsClampedToTheLimitsOfTheDefaultSpindle()
    {
        NcxProgram expanded = ExpanderHarness.Expand(
            ExpanderHarness.File("SPINDLE=CW RPM=7000.5"), ExpanderMachines.Clamp());

        Assert.Equal(ExpanderHarness.File("SPINDLE=CW RPM=6000"), ExpanderHarness.WriteWithGenerated(expanded));
    }

    // Under limits = "warn", the default, the expander rewrites nothing; the virtual machine reports the limits
    // (virtual machine 5, D64).
    [Fact]
    public void D64_UnderWarn_NothingIsRewritten()
    {
        string text = ExpanderHarness.File(
            "UNITS=MM", "SPINDLE:MAIN=CW RPM:MAIN=8000", "LINE X=10 Z=-5 F=9000", "RAPID X=700 FRAME=MACHINE");

        NcxProgram expanded = ExpanderHarness.Expand(text, ExpanderMachines.Mill());

        Assert.Equal(text, ExpanderHarness.WriteWithGenerated(expanded));
        Assert.Empty(expanded.Diagnostics.Items);
    }

    // F above the max_feed of an axis the block moves is rewritten to the smallest max_feed of those axes.
    [Fact]
    public void D64_FeedAboveTheMaxFeedOfAMovingAxis_IsClamped()
    {
        NcxProgram expanded = ExpanderHarness.Expand(
            ExpanderHarness.File("UNITS=MM", "LINE X=10 Z=-5 F=9000", "LINE X=20 F=9000"), ExpanderMachines.Clamp());

        Assert.Equal(
            ExpanderHarness.File("UNITS=MM", "LINE X=10 Z=-5 F=8000", "LINE X=20 F=9000"),
            ExpanderHarness.WriteWithGenerated(expanded));
        Assert.Contains("max_feed 8000", Assert.Single(expanded.Diagnostics.Items).Message, StringComparison.Ordinal);
    }

    // A target of a FRAME=MACHINE block is a machine coordinate and is clamped to the limits of its linear axis
    // (virtual machine 5, D100).
    [Fact]
    public void D64_MachineFrameTargetBeyondTheLimits_IsClamped()
    {
        NcxProgram expanded = ExpanderHarness.Expand(
            ExpanderHarness.File("UNITS=MM", "RAPID X=700 Z=-300 FRAME=MACHINE"), ExpanderMachines.Clamp());

        Assert.Equal(
            ExpanderHarness.File("UNITS=MM", "RAPID X=650 Z=-200 FRAME=MACHINE"),
            ExpanderHarness.WriteWithGenerated(expanded));
        Assert.Equal(2, expanded.Diagnostics.Items.Count);
    }

    // The expander never sees the machine position of a workpiece-frame target, and the limits of a rotary axis may be
    // a display range; neither is rewritten (architecture 5.5, D100).
    [Fact]
    public void D64_WorkpieceTargetAndRotaryTarget_AreNotRewritten()
    {
        string text = ExpanderHarness.File("UNITS=MM", "RAPID X=700", "RAPID C=400 FRAME=MACHINE");

        NcxProgram expanded = ExpanderHarness.Expand(text, ExpanderMachines.Clamp());

        Assert.Equal(text, ExpanderHarness.WriteWithGenerated(expanded));
        Assert.Empty(expanded.Diagnostics.Items);
    }

    // The clamp applies to every block the VM executes for a block, a generated one as well, which keeps its place and
    // names both sources.
    [Fact]
    public void D64_GeneratedBlockBeyondTheLimits_IsClampedInItsPlace()
    {
        MachineConfig machine = ExpanderMachines.Clamp() with
        {
            ToolChange = new ToolChangeConfig { Rule = new ExpansionRule { Pre = ["RAPID X=900 FRAME=MACHINE"] } },
        };

        NcxProgram expanded = ExpanderHarness.Expand(ExpanderHarness.File("UNITS=MM", "TOOL=1"), machine);

        Assert.Equal(
            ExpanderHarness.File("UNITS=MM", "RAPID X=650 FRAME=MACHINE", "TOOL=1"),
            ExpanderHarness.WriteWithGenerated(expanded));
        Block clamped = ExpanderHarness.Find(expanded, "RAPID X=650 FRAME=MACHINE");
        Assert.Equal(GeneratedPlacement.Before, clamped.Generated?.Placement);
        Assert.Equal("[tool_change], [machine] limits", clamped.Generated?.Source);
    }

    // ncx format writes the block of the file as read, not the clamped value (language 4.15).
    [Fact]
    public void D64_ClampedBlock_IsWrittenByFormatAsRead()
    {
        string text = ExpanderHarness.File("SPINDLE:MAIN=CW RPM:MAIN=8000");

        NcxProgram expanded = ExpanderHarness.Expand(text, ExpanderMachines.Clamp());

        Assert.Equal(text, NcxWriter.Write(expanded));
    }
}
