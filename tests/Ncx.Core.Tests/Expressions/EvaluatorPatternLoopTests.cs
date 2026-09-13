using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.Expressions;

/// <summary>
/// The expressions of PATTERN_LOOP.ncx evaluated with the loop variables set by hand, the way the loop of the example
/// sets them (language 6, P4-01). The INTERPRETED run that follows the jump by itself is P4-01 part two.
/// </summary>
public sealed class EvaluatorPatternLoopTests
{
    // PATTERN_LOOP note 1: INTERPRETED mode runs the loop five times with the CYCLE_CALL at X = 10, 30, 50, 70, 90,
    // and Q1 and Q3 change with every iteration.
    [Fact]
    public void PatternLoop_LoopVariablesSetByHand_CallsAtXTenToNinetyFiveTimes()
    {
        // The four expressions of the file in the order written: CYCLE_CALL X={$Q1}, VAR:Q1={$Q1 + 20},
        // VAR:Q3={$Q3 + 1} and JUMP=1 IF={$Q3 < $Q2}.
        List<string> expressions = ExpressionTexts.InFile(Fixture.ReadText("PATTERN_LOOP.ncx"));
        string[] written = ["$Q1", "$Q1 + 20", "$Q3 + 1", "$Q3 < $Q2"];
        Assert.Equal(written, expressions);

        // VAR:Q1=10, VAR:Q2=5 and VAR:Q3=0 stand before LABEL=1.
        var channel = new EvaluationChannel();
        channel.Set("Q1", 10m);
        channel.Set("Q2", 5m);
        channel.Set("Q3", 0m);

        var callX = new List<decimal>();
        var q1 = new List<decimal>();
        var q3 = new List<decimal>();
        bool jumpBack = true;

        // From LABEL=1 to the JUMP, at most ten times, so that a wrong condition fails the test instead of looping.
        while (jumpBack && callX.Count < 10)
        {
            callX.Add(channel.NumberOf(expressions[0]));
            channel.Set("Q1", channel.NumberOf(expressions[1]));
            channel.Set("Q3", channel.NumberOf(expressions[2]));
            q1.Add(channel.NumberOf("$Q1"));
            q3.Add(channel.NumberOf("$Q3"));

            // JUMP=1 IF={$Q3 < $Q2}: the jump back to LABEL=1 is taken when the condition is not 0 (language 4.9).
            jumpBack = channel.NumberOf(expressions[3]) != 0;
        }

        decimal[] expectedCallX = [10m, 30m, 50m, 70m, 90m];
        decimal[] expectedQ1 = [30m, 50m, 70m, 90m, 110m];
        decimal[] expectedQ3 = [1m, 2m, 3m, 4m, 5m];
        Assert.Equal(expectedCallX, callX);
        Assert.Equal(expectedQ1, q1);
        Assert.Equal(expectedQ3, q3);
    }
}
