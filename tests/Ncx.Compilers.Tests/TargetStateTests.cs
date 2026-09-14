namespace Ncx.Compilers.Tests;

/// <summary>
/// What the target control has active, so that a modal word is written only on change (phase 3, P3-03; virtual machine
/// 3.9, D99).
/// </summary>
public sealed class TargetStateTests
{
    // Phase 3, P3-03: a value is written when the key is unknown or held another value, not while it stays active.
    [Fact]
    public void Changes_SameValueAndAnother_IsTrueOnlyOnChange()
    {
        var target = new TargetState();

        Assert.True(target.Changes("G01", "G1"));
        Assert.False(target.Changes("G01", "G1"));
        Assert.True(target.Changes("G01", "G0"));
        Assert.Equal("G0", target.ActiveOf("G01"));
    }

    // Phase 3, P3-03: a forgotten key is unknown, and its next value is written whatever it is.
    [Fact]
    public void Forget_Key_MakesTheNextValueAChange()
    {
        var target = new TargetState();
        target.Set("F", "100.");

        target.Forget("F");

        Assert.Null(target.ActiveOf("F"));
        Assert.True(target.Changes("F", "100."));
    }

    // Virtual machine 3.9, D99: after a subprogram returns, the control has active what its walk wrote, and what it
    // left unknown is as the caller left it.
    [Fact]
    public void TakeOver_WalkOfASubprogram_OverridesWhatTheWalkWrote()
    {
        var caller = new TargetState();
        caller.Set("G01", "G1");
        caller.Set("F", "100.");
        var walk = new TargetState();
        walk.Set("G01", "G0");

        caller.TakeOver(walk);

        Assert.Equal("G0", caller.ActiveOf("G01"));
        Assert.Equal("100.", caller.ActiveOf("F"));
    }
}
