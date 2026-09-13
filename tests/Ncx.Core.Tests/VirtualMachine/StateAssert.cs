using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine;

/// <summary>
/// Compares two states of a channel by what the program set: spindles, holders, coolant, functions, feed, feed mode,
/// compensation, positions, units, diameter and the UNKNOWN variables. The flow is left out, since an expanded program
/// holds more blocks than its original and its block indices differ.
/// </summary>
internal static class StateAssert
{
    /// <summary>
    /// Asserts that both snapshots hold the same state, printing both texts on a difference.
    /// </summary>
    public static void Same(ChannelSnapshot expected, ChannelSnapshot actual)
    {
        Assert.Equal(Text(expected), Text(actual));
    }

    // One line per state variable, sorted, so that the order of a dictionary does not count.
    private static string Text(ChannelSnapshot state)
    {
        var lines = new List<string>();
        foreach (KeyValuePair<string, SpindleSnapshot> spindle in state.Spindles)
        {
            lines.Add("spindle " + spindle.Key + ": " + spindle.Value);
        }

        foreach (KeyValuePair<string, HolderSnapshot> holder in state.Holders)
        {
            lines.Add("holder " + holder.Key + ": " + holder.Value);
        }

        foreach (KeyValuePair<string, bool> coolant in state.Coolant)
        {
            lines.Add("coolant " + coolant.Key + ": " + coolant.Value);
        }

        foreach (KeyValuePair<string, string?> function in state.Functions)
        {
            lines.Add("function " + function.Key + ": " + function.Value);
        }

        foreach (KeyValuePair<string, AxisPosition> position in state.Motion.Position)
        {
            lines.Add("position " + position.Key + ": " + position.Value);
        }

        foreach (string unknown in state.Unknown)
        {
            lines.Add("unknown " + unknown);
        }

        lines.Add("feed: " + state.Motion.Feed + " " + state.Motion.FeedMode);
        lines.Add("comp: " + state.Motion.Comp);
        lines.Add("units: " + state.Frame.Units + ", diameter: " + state.Frame.Diameter);
        lines.Add("last holder: " + state.LastHolder);
        lines.Sort(StringComparer.Ordinal);
        return string.Join("\n", lines);
    }
}
