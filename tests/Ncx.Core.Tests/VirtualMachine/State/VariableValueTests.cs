using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.Tests.VirtualMachine.State;

/// <summary>
/// The value of a variable (virtual machine 2.7): a number or a string (language 4.9), or UNKNOWN (virtual machine 1).
/// </summary>
public sealed class VariableValueTests
{
    // VM 1: UNKNOWN is a value with no number or string behind it.
    [Fact]
    public void Unknown_IsUnknown_AndHoldsNothing()
    {
        Assert.True(VariableValue.Unknown.IsUnknown);
        Assert.Null(VariableValue.Unknown.Value);
    }

    // Language 4.9: a number or a string is a known value, kept as written.
    [Fact]
    public void Of_NumberOrString_IsKnownAndKeptAsWritten()
    {
        VariableValue number = VariableValue.Of(new DecimalValue(10.50m, "10.50"));
        VariableValue text = VariableValue.Of(new StringValue("TEXT"));

        Assert.False(number.IsUnknown);
        Assert.Equal("10.50", number.ToString());
        Assert.Equal("\"TEXT\"", text.ToString());
    }

    // Language 4.9: an identifier or an unevaluated expression is no value of a variable.
    [Fact]
    public void Of_IdentifierOrExpression_Throws()
    {
        Assert.Throws<ArgumentException>(() => VariableValue.Of(new IdentValue("CW")));
        Assert.Throws<ArgumentException>(() => VariableValue.Of(new ExprValue("$Q1 + 20", null)));
    }

    // Two values are equal when they hold the same number as written; UNKNOWN equals UNKNOWN.
    [Fact]
    public void Equality_SameValue_IsEqual()
    {
        Assert.Equal(VariableValue.Of(new IntegerValue(10, "10")), VariableValue.Of(new IntegerValue(10, "10")));
        Assert.NotEqual(VariableValue.Of(new IntegerValue(10, "10")), VariableValue.Unknown);
        Assert.Equal("UNKNOWN", VariableValue.Unknown.ToString());
    }
}
