using Ncx.Core.Machine;

namespace Ncx.Readers.Tests;

/// <summary>
/// The small source-side virtual machine of a reader: one code per modal group is active until another code of the
/// same group appears, codes compared by number (architecture 7; controllers fanuc.md 3; D105).
/// </summary>
public sealed class SourceStateTests
{
    private static readonly Dictionary<string, int> s_groups = new() { ["G0"] = 1, ["G1"] = 1, ["G91"] = 3 };

    [Fact]
    public void Apply_CodeOfAGroupWithLeadingZero_ReplacesTheActiveCodeOfItsGroup()
    {
        var state = new SourceState(null, s_groups);

        state.Apply(Block(G("00")));
        state.Apply(Block(G("01"), G("91")));

        Assert.Equal("G1", state.ActiveCode(1));
        Assert.Equal("G91", state.ActiveCode(3));
    }

    [Fact]
    public void Apply_CodeOfNoGroup_ChangesNothing()
    {
        var state = new SourceState(null, s_groups);

        state.Apply(Block(G("28")));

        Assert.Empty(state.ModalGroups);
        Assert.Null(state.ActiveCode(1));
    }

    // The state of a new file: nothing active, absolute, nothing preloaded, no position known; the G-code system is
    // the machine's (machine-config 1).
    [Fact]
    public void NewState_OfAFile_KnowsOnlyTheGcodeSystem()
    {
        var state = new SourceState(GcodeSystem.A, s_groups);

        Assert.Equal(GcodeSystem.A, state.GcodeSystem);
        Assert.False(state.Incremental);
        Assert.Null(state.Preloaded);
        Assert.Null(state.ActiveCycle);
        Assert.Null(state.LastSpindle);
        Assert.Empty(state.Positions);
    }

    [Fact]
    public void Positions_SetAndForgotten_AreKnownInBetween()
    {
        var state = new SourceState(null, s_groups);

        state.SetPosition("X", 50.4m);
        Assert.Equal(50.4m, state.Positions["X"]);
        state.ForgetPosition("X");

        Assert.Empty(state.Positions);
    }

    private static SourceWord G(string number)
    {
        return new SourceWord
        {
            Address = "G",
            Text = number,
            Number = decimal.Parse(number, System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    private static SourceBlock Block(params SourceWord[] words)
    {
        return new SourceBlock { Line = 1, Text = "", Words = words };
    }
}
