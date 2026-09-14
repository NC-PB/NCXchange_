using Ncx.Analytics;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// The ten worst motions of a report, kept while the run streams by: the ten shortest of the segment length, the ten
/// largest changes of the tool vector change (implementation 14, P4-03 and risks).
/// </summary>
public sealed class WorstMotionsTests
{
    // P4-03, the ten shortest: of twelve motions of 12, 11, ... 1 mm on lines 1 to 12 the ten shortest stay, the
    // shortest first.
    [Fact]
    public void Add_TwelveMotionsSmallestFirst_KeepsTheTenSmallest()
    {
        var shortest = new WorstMotions(largest: false);

        for (int line = 1; line <= 12; line++)
        {
            shortest.Add(Motion(line, 13 - line));
        }

        Assert.Equal([12, 11, 10, 9, 8, 7, 6, 5, 4, 3], Lines(shortest));
    }

    // P4-03, the ten largest changes: of twelve motions of 1, 2, ... 12 degrees the ten largest stay, the largest
    // first.
    [Fact]
    public void Add_TwelveMotionsLargestFirst_KeepsTheTenLargest()
    {
        var largest = new WorstMotions(largest: true);

        for (int line = 1; line <= 12; line++)
        {
            largest.Add(Motion(line, line));
        }

        Assert.Equal([12, 11, 10, 9, 8, 7, 6, 5, 4, 3], Lines(largest));
    }

    // Equal values keep the order of the run: the earlier motion stands first, and a later motion of the same value
    // does not push an earlier one out.
    [Fact]
    public void Add_EqualValues_KeepTheEarlierMotions()
    {
        var shortest = new WorstMotions(largest: false);

        for (int line = 1; line <= 11; line++)
        {
            shortest.Add(Motion(line, 1));
        }

        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], Lines(shortest));
    }

    private static WorstMotion Motion(int line, double value)
    {
        return new WorstMotion { Line = line, Verb = Verb.Line, Value = value };
    }

    private static List<int> Lines(WorstMotions motions)
    {
        var lines = new List<int>();
        foreach (WorstMotion motion in motions.Motions)
        {
            lines.Add(motion.Line);
        }

        return lines;
    }
}
