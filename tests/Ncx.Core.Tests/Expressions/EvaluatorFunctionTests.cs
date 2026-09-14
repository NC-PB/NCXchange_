using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Core.Tests.Expressions;

/// <summary>
/// The seventeen functions of language 4.12 by name: angles in degrees, INT truncating toward zero, ROUND rounding half
/// away from zero, and the ERRORs of a value that does not exist and of a wrong number of arguments.
/// </summary>
public sealed class EvaluatorFunctionTests
{
    // Angles in degrees (language 4.12): SIN, COS and TAN take degrees, and on the axes and at 30, 45 and 60 degrees
    // the value is the exact one, whatever the number of turns.
    [Theory]
    [InlineData("SIN(0)", "0")]
    [InlineData("SIN(30)", "0.5")]
    [InlineData("SIN(90)", "1")]
    [InlineData("SIN(180)", "0")]
    [InlineData("SIN(270)", "-1")]
    [InlineData("SIN(-90)", "-1")]
    [InlineData("SIN(450)", "1")]
    [InlineData("SIN(-330)", "0.5")]
    [InlineData("COS(0)", "1")]
    [InlineData("COS(60)", "0.5")]
    [InlineData("COS(90)", "0")]
    [InlineData("COS(180)", "-1")]
    [InlineData("COS(-270)", "0")]
    [InlineData("TAN(0)", "0")]
    [InlineData("TAN(45)", "1")]
    [InlineData("TAN(-45)", "-1")]
    [InlineData("TAN(180)", "0")]
    public void SinCosTan_AngleInDegrees_IsTheExactValue(string expression, string expected)
    {
        Assert.Equal(EvaluationChannel.DecimalOf(expected), new EvaluationChannel().NumberOf(expression));
    }

    // TAN has no value at 90 and 270 degrees, where the cosine is 0 (language 4.12).
    [Theory]
    [InlineData("TAN(90)")]
    [InlineData("TAN(-90)")]
    [InlineData("TAN(270)")]
    public void Tan_QuarterTurn_IsUndefined(string expression)
    {
        Assert.Equal(DiagnosticCodes.ResultUndefined, new EvaluationChannel().ErrorOf(expression).Code);
    }

    // Angles in degrees (language 4.12): ASIN, ACOS, ATAN and ATAN2 give degrees, ATAN2(y, x) the angle of the point x,
    // y from -180 to 180 (the reading of the open question in Evaluator.ArcTangent2).
    [Theory]
    [InlineData("ASIN(0.5)", "30")]
    [InlineData("ASIN(1)", "90")]
    [InlineData("ASIN(-1)", "-90")]
    [InlineData("ACOS(0.5)", "60")]
    [InlineData("ACOS(1)", "0")]
    [InlineData("ACOS(-1)", "180")]
    [InlineData("ATAN(1)", "45")]
    [InlineData("ATAN(-1)", "-45")]
    [InlineData("ATAN2(1, 1)", "45")]
    [InlineData("ATAN2(1, -1)", "135")]
    [InlineData("ATAN2(-1, -1)", "-135")]
    [InlineData("ATAN2(0, -1)", "180")]
    [InlineData("ATAN2(1, 0)", "90")]
    [InlineData("ATAN2(0, 1)", "0")]
    public void InverseFunctions_Value_IsAnAngleInDegrees(string expression, string expected)
    {
        Assert.Equal(EvaluationChannel.DecimalOf(expected), new EvaluationChannel().NumberOf(expression));
    }

    // Off the axes a function is computed in double and comes back to 15 significant digits (P4-01): right to a
    // relative 1E-14 of the exact value.
    [Theory]
    [InlineData("SIN(45)", "0.70710678118654752440")]
    [InlineData("COS(30)", "0.86602540378443864676")]
    [InlineData("TAN(30)", "0.57735026918962576451")]
    [InlineData("ATAN(0.5)", "26.565051177077989351")]
    [InlineData("SQRT(2)", "1.41421356237309504880")]
    [InlineData("EXP(1)", "2.71828182845904523536")]
    [InlineData("LN(2)", "0.69314718055994530942")]
    [InlineData("LN(EXP(2))", "2")]
    [InlineData("3 ^ 0.5", "1.73205080756887729353")]
    public void Function_IrrationalValue_IsRightToFifteenSignificantDigits(string expression, string exact)
    {
        decimal expected = EvaluationChannel.DecimalOf(exact);

        decimal value = new EvaluationChannel().NumberOf(expression);

        Assert.True(
            Math.Abs(value - expected) <= Math.Abs(expected) * 0.00000000000001m,
            $"{{{expression}}} is {value.ToString(CultureInfo.InvariantCulture)}, exactly {exact}.");
    }

    // A function without a value for the argument is an ERROR, never a number that is not one (language 4.12).
    [Theory]
    [InlineData("ASIN(1.5)")]
    [InlineData("ACOS(-2)")]
    [InlineData("ATAN2(0, 0)")]
    [InlineData("SQRT(-1)")]
    [InlineData("LN(0)")]
    [InlineData("LN(-1)")]
    public void Function_ArgumentWithoutAValue_IsUndefined(string expression)
    {
        Assert.Equal(DiagnosticCodes.ResultUndefined, new EvaluationChannel().ErrorOf(expression).Code);
    }

    // Language 4.12: SQRT, ABS, SGN, LN and EXP.
    [Theory]
    [InlineData("SQRT(4)", "2")]
    [InlineData("SQRT(2.25)", "1.5")]
    [InlineData("SQRT(0)", "0")]
    [InlineData("ABS(-2.5)", "2.5")]
    [InlineData("ABS(3)", "3")]
    [InlineData("SGN(-3)", "-1")]
    [InlineData("SGN(0)", "0")]
    [InlineData("SGN(2.5)", "1")]
    [InlineData("LN(1)", "0")]
    [InlineData("EXP(0)", "1")]
    public void Function_ExactArgument_IsTheExactValue(string expression, string expected)
    {
        Assert.Equal(EvaluationChannel.DecimalOf(expected), new EvaluationChannel().NumberOf(expression));
    }

    // Language 4.12: INT truncates toward zero; the P4-01 case {INT(-2.7)} is -2.
    [Theory]
    [InlineData("INT(-2.7)", "-2")]
    [InlineData("INT(2.7)", "2")]
    [InlineData("INT(-2)", "-2")]
    [InlineData("INT(0.5)", "0")]
    public void Int_AnyNumber_TruncatesTowardZero(string expression, string expected)
    {
        Assert.Equal(EvaluationChannel.DecimalOf(expected), new EvaluationChannel().NumberOf(expression));
    }

    // FRAC is the part after the decimal point with the sign of the number, so that INT(x) + FRAC(x) is x (the reading
    // of the open question in Evaluator.EvaluateCall).
    [Theory]
    [InlineData("FRAC(2.75)", "0.75")]
    [InlineData("FRAC(-2.7)", "-0.7")]
    [InlineData("FRAC(3)", "0")]
    [InlineData("INT(-2.7) + FRAC(-2.7)", "-2.7")]
    public void Frac_AnyNumber_IsThePartAfterTheDecimalPoint(string expression, string expected)
    {
        Assert.Equal(EvaluationChannel.DecimalOf(expected), new EvaluationChannel().NumberOf(expression));
    }

    // Language 4.12: ROUND rounds half away from zero; the P4-01 cases {ROUND(2.5)} is 3 and {ROUND(-2.5)} is -3.
    [Theory]
    [InlineData("ROUND(2.5)", "3")]
    [InlineData("ROUND(-2.5)", "-3")]
    [InlineData("ROUND(0.5)", "1")]
    [InlineData("ROUND(3.5)", "4")]
    [InlineData("ROUND(2.4)", "2")]
    [InlineData("ROUND(-2.6)", "-3")]
    public void Round_Half_RoundsAwayFromZero(string expression, string expected)
    {
        Assert.Equal(EvaluationChannel.DecimalOf(expected), new EvaluationChannel().NumberOf(expression));
    }

    // Language 4.12: MIN and MAX give the smallest and the largest of their arguments.
    [Theory]
    [InlineData("MIN(3, 1, 2)", "1")]
    [InlineData("MAX(3, 1, 2)", "3")]
    [InlineData("MIN(-1.5, -1)", "-1.5")]
    [InlineData("MAX(4)", "4")]
    public void MinMax_Arguments_IsTheSmallestOrTheLargest(string expression, string expected)
    {
        Assert.Equal(EvaluationChannel.DecimalOf(expected), new EvaluationChannel().NumberOf(expression));
    }

    // The case of P0-05 evaluated: MAX($A, 2) * SIN(30) with A = 4 is 4 * 0.5.
    [Fact]
    public void Function_InAProduct_IsAnOperandLikeAnyOther()
    {
        var channel = new EvaluationChannel();
        channel.Set("A", 4m);

        Assert.Equal(2m, channel.NumberOf("MAX($A, 2) * SIN(30)"));
    }

    // ATAN2 takes two arguments, MIN and MAX one or more, every other function one (the reading of the open question
    // in Evaluator.ArgumentCountFits).
    [Theory]
    [InlineData("SIN(1, 2)")]
    [InlineData("ROUND(2.5, 1)")]
    [InlineData("ATAN2(1)")]
    [InlineData("ATAN2(1, 2, 3)")]
    public void Function_WrongNumberOfArguments_IsAnError(string expression)
    {
        Diagnostic error = new EvaluationChannel().ErrorOf(expression);

        Assert.Equal(DiagnosticCodes.FunctionArgumentCount, error.Code);
        Assert.Contains(expression, error.Message, StringComparison.Ordinal);
    }
}
