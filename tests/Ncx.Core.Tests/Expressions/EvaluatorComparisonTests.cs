using Ncx.Core.Model;

namespace Ncx.Core.Tests.Expressions;

/// <summary>
/// The comparisons of language 4.12, and AND, OR and NOT: each yields 1 or 0, and a value that is not 0 counts as
/// true, as IF reads it (language 4.9).
/// </summary>
public sealed class EvaluatorComparisonTests
{
    // Language 4.12, cmpexpr: comparisons yield 1 or 0.
    [Theory]
    [InlineData("1 == 1", "1")]
    [InlineData("1 == 2", "0")]
    [InlineData("1 != 2", "1")]
    [InlineData("1 != 1", "0")]
    [InlineData("1 < 2", "1")]
    [InlineData("2 < 1", "0")]
    [InlineData("2 <= 2", "1")]
    [InlineData("3 <= 2", "0")]
    [InlineData("2 > 1", "1")]
    [InlineData("1 > 2", "0")]
    [InlineData("2 >= 2", "1")]
    [InlineData("1 >= 2", "0")]
    public void Comparison_EachOperator_YieldsOneOrZero(string expression, string expected)
    {
        Assert.Equal(EvaluationChannel.DecimalOf(expected), new EvaluationChannel().NumberOf(expression));
    }

    // A comparison compares values, not the text of the numbers: 2.50 is 2.5 (language 2 rule 5, 4.12).
    [Fact]
    public void Equal_SameValueWrittenDifferently_YieldsOne()
    {
        Assert.Equal(1m, new EvaluationChannel().NumberOf("2.50 == 2.5"));
    }

    // AND, OR and NOT yield 1 or 0 (P4-01); a value that is not 0 counts as true, as IF reads it (language 4.9).
    [Theory]
    [InlineData("1 AND 1", "1")]
    [InlineData("2 AND -3", "1")]
    [InlineData("1 AND 0", "0")]
    [InlineData("0 AND 0", "0")]
    [InlineData("0 OR 0", "0")]
    [InlineData("0 OR 0.5", "1")]
    [InlineData("-1 OR 0", "1")]
    [InlineData("NOT 0", "1")]
    [InlineData("NOT 5", "0")]
    [InlineData("NOT 1 > 2", "1")]
    public void Logic_EachOperator_YieldsOneOrZero(string expression, string expected)
    {
        Assert.Equal(EvaluationChannel.DecimalOf(expected), new EvaluationChannel().NumberOf(expression));
    }

    // The precedence case of P0-05 evaluated: the comparison is the left operand of AND, and NOT negates the right one
    // (language 4.12, andexpr and notexpr).
    [Theory]
    [InlineData("1", "2", "0", "1")]
    [InlineData("1", "2", "7", "0")]
    [InlineData("3", "2", "0", "0")]
    public void AndNot_PrecedenceCaseOfP005_ComparesThenNegatesTheRightOperand(
        string q1, string q2, string q3, string expected)
    {
        var channel = new EvaluationChannel();
        channel.Set("Q1", EvaluationChannel.DecimalOf(q1));
        channel.Set("Q2", EvaluationChannel.DecimalOf(q2));
        channel.Set("Q3", EvaluationChannel.DecimalOf(q3));

        Assert.Equal(EvaluationChannel.DecimalOf(expected), channel.NumberOf("$Q1 < $Q2 AND NOT $Q3"));
    }

    // Both operands of AND are evaluated, as for every other operator, so a left operand of 0 does not hide the
    // division by zero on the right (the reading of the open question in Evaluator.EvaluateBinary).
    [Fact]
    public void And_LeftOperandZero_StillEvaluatesTheRightOperand()
    {
        Assert.Equal(DiagnosticCodes.DivisionByZero, new EvaluationChannel().ErrorOf("0 AND 1 / 0").Code);
    }
}
