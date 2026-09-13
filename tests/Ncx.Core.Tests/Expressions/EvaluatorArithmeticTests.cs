using Ncx.Core.Model;

namespace Ncx.Core.Tests.Expressions;

/// <summary>
/// The arithmetic of language 4.12 in decimal: numbers, + - * /, MOD, the power and the unary minus, with the ERRORs of
/// a division by zero and of a value beyond the range of decimal.
/// </summary>
public sealed class EvaluatorArithmeticTests
{
    // Language 3, 4.12: a number in an expression is its value as written.
    [Theory]
    [InlineData("20", "20")]
    [InlineData("0.05", "0.05")]
    [InlineData("007.50", "7.5")]
    public void Number_AsWritten_IsItsValue(string expression, string expected)
    {
        Assert.Equal(EvaluationChannel.DecimalOf(expected), new EvaluationChannel().NumberOf(expression));
    }

    // Language 4.12, sum and product: + - * and / in decimal, * and / before + and -, grouped from the left.
    [Theory]
    [InlineData("1 + 2", "3")]
    [InlineData("1 - 2 + 3", "2")]
    [InlineData("2.5 * 4", "10")]
    [InlineData("1 / 4", "0.25")]
    [InlineData("12 / 4 / 3", "1")]
    [InlineData("1 + 2 * 3", "7")]
    [InlineData("(1 + 2) * 3", "9")]
    public void SumAndProduct_Operands_ComputeInDecimal(string expression, string expected)
    {
        Assert.Equal(EvaluationChannel.DecimalOf(expected), new EvaluationChannel().NumberOf(expression));
    }

    // Decimal arithmetic (P4-01): 0.1 + 0.2 is 0.3 exactly, so the comparison a program writes holds.
    [Fact]
    public void Add_DecimalFractions_IsExact()
    {
        var channel = new EvaluationChannel();

        Assert.Equal(0.3m, channel.NumberOf("0.1 + 0.2"));
        Assert.Equal(1m, channel.NumberOf("0.1 + 0.2 == 0.3"));
    }

    // Language 4.12: division by zero is an ERROR, on the line of the block (D98); the P4-01 case {1 / 0}.
    [Fact]
    public void Divide_ByZero_IsAnError()
    {
        Diagnostic error = new EvaluationChannel().ErrorOf("1 / 0");

        Assert.Equal(DiagnosticCodes.DivisionByZero, error.Code);
        Assert.Equal(EvaluationChannel.BlockLine, error.Line);
        Assert.Contains("1 / 0", error.Message, StringComparison.Ordinal);
    }

    // Language 4.12: the divisor may be a variable that holds 0.
    [Fact]
    public void Divide_ByAVariableThatHoldsZero_IsAnError()
    {
        var channel = new EvaluationChannel();
        channel.Set("Q1", 0m);

        Assert.Equal(DiagnosticCodes.DivisionByZero, channel.ErrorOf("10 / $Q1").Code);
    }

    // Language 4.12: MOD keeps the sign of the dividend; the P4-01 case {-7 MOD 3} is -1.
    [Theory]
    [InlineData("-7 MOD 3", "-1")]
    [InlineData("7 MOD -3", "1")]
    [InlineData("-7 MOD -3", "-1")]
    [InlineData("7 MOD 3", "1")]
    [InlineData("7.5 MOD 2", "1.5")]
    [InlineData("-7.5 MOD 2", "-1.5")]
    public void Mod_AnySigns_KeepsTheSignOfTheDividend(string expression, string expected)
    {
        Assert.Equal(EvaluationChannel.DecimalOf(expected), new EvaluationChannel().NumberOf(expression));
    }

    // MOD is the remainder of a division, so a divisor of 0 is a division by zero (language 4.12).
    [Fact]
    public void Mod_ByZero_IsDivisionByZero()
    {
        Assert.Equal(DiagnosticCodes.DivisionByZero, new EvaluationChannel().ErrorOf("5 MOD 0").Code);
    }

    // Language 4.12, power = primary [ "^" unary ]: 2 ^ 3 ^ 2 is 2 ^ 9, the unary minus before a power negates the
    // power, and the exponent may carry its own minus.
    [Theory]
    [InlineData("2 ^ 10", "1024")]
    [InlineData("1.1 ^ 2", "1.21")]
    [InlineData("2 ^ 3 ^ 2", "512")]
    [InlineData("-2 ^ 2", "-4")]
    [InlineData("(-2) ^ 3", "-8")]
    [InlineData("2 ^ -2", "0.25")]
    [InlineData("5 ^ 0", "1")]
    [InlineData("4 ^ 0.5", "2")]
    [InlineData("1 ^ 1000000000", "1")]
    public void Power_WholeAndFractionalExponents_IsThePower(string expression, string expected)
    {
        Assert.Equal(EvaluationChannel.DecimalOf(expected), new EvaluationChannel().NumberOf(expression));
    }

    // A whole-number power is the repeated product in decimal, so $A ^ 2 is $A * $A to the last digit (P4-01, decimal
    // arithmetic).
    [Fact]
    public void Power_WholeExponent_IsTheRepeatedProduct()
    {
        var channel = new EvaluationChannel();
        channel.Set("A", 1.23456789m);

        Assert.Equal(1.5241578750190521m, channel.NumberOf("$A ^ 2"));
        Assert.Equal(channel.NumberOf("$A * $A * $A"), channel.NumberOf("$A ^ 3"));
    }

    // 0 to a negative power is 1 / 0, a division by zero (language 4.12).
    [Fact]
    public void Power_ZeroToANegativePower_IsDivisionByZero()
    {
        Assert.Equal(DiagnosticCodes.DivisionByZero, new EvaluationChannel().ErrorOf("0 ^ -1").Code);
    }

    // A negative number to a fractional power has no real value (language 4.12).
    [Fact]
    public void Power_NegativeNumberToAFractionalPower_IsUndefined()
    {
        Assert.Equal(DiagnosticCodes.ResultUndefined, new EvaluationChannel().ErrorOf("(-8) ^ 0.5").Code);
    }

    // Language 4.12, unary = [ "-" ] power: the minus negates its operand.
    [Fact]
    public void UnaryMinus_BeforeAnOperand_Negates()
    {
        var channel = new EvaluationChannel();
        channel.Set("Q1", 10m);

        Assert.Equal(-10m, channel.NumberOf("-$Q1"));
        Assert.Equal(2m, channel.NumberOf("-(1 - 3)"));
    }

    // The evaluator computes in decimal, about 7.9E28 either way: a number or a value beyond that is an ERROR
    // (language 4.12).
    [Theory]
    [InlineData("79228162514264337593543950336")]
    [InlineData("79228162514264337593543950335 + 1")]
    [InlineData("79228162514264337593543950335 * 2")]
    [InlineData("10 ^ 30")]
    [InlineData("0.1 ^ -30")]
    [InlineData("2 ^ 100.5")]
    [InlineData("EXP(100)")]
    public void Value_BeyondTheRangeOfDecimal_IsAnError(string expression)
    {
        Assert.Equal(DiagnosticCodes.ResultOutOfRange, new EvaluationChannel().ErrorOf(expression).Code);
    }
}
