using Ncx.Core.Model;

namespace Ncx.Core.Tests.Model;

/// <summary>
/// A word is a key with an optional address and an optional value (language 3, Word).
/// </summary>
public sealed class WordTests
{
    [Fact]
    public void Word_BuiltWithKeyAddressAndValue_ReadsThemBack()
    {
        var clockwise = new IdentValue("CW");
        var word = new Word { Key = "SPINDLE", Addr = "MAIN", Value = clockwise };

        Assert.Equal("SPINDLE", word.Key);
        Assert.Equal("MAIN", word.Addr);
        Assert.Same(clockwise, word.Value);
    }

    // A machine axis word has no catalog entry; the virtual machine resolves it against the machine (D93).
    [Fact]
    public void Word_MachineAxisWord_HasNoDefinition()
    {
        var word = new Word { Key = "Z2", Value = new IntegerValue(-5, "-5") };

        Assert.Null(word.Definition);
        Assert.Null(word.Addr);
    }

    // KEY:ADDR=VALUE (language 3, Word).
    [Fact]
    public void ToCanonical_KeyAddressAndValue_WritesKeyColonAddressEqualsValue()
    {
        var word = new Word { Key = "OFFSET", Addr = "LEN", Value = new IntegerValue(1, "1") };

        Assert.Equal("OFFSET:LEN=1", word.ToCanonical());
    }

    // KEY alone: a word without a value has no equals sign (language 3, Word).
    [Fact]
    public void ToCanonical_WordWithoutValue_WritesTheKeyAlone()
    {
        var word = new Word { Key = "RAPID" };

        Assert.Same(NoValue.Instance, word.Value);
        Assert.Equal("RAPID", word.ToCanonical());
    }

    // KEY=VALUE with the number as written (language 2 rule 5).
    [Fact]
    public void ToCanonical_KeyAndDecimal_WritesTheDecimalAsWritten()
    {
        var word = new Word { Key = "Y", Value = new DecimalValue(-7.025m, "-7.025") };

        Assert.Equal("Y=-7.025", word.ToCanonical());
    }

    // A string value is written in quotes with its escapes (language 3, string).
    [Fact]
    public void ToCanonical_StringWithQuotes_WritesTheEscapedString()
    {
        var word = new Word { Key = "SECTION", Value = new StringValue("""SIDE "MILL" D10""") };

        Assert.Equal(
            """
            SECTION="SIDE \"MILL\" D10"
            """,
            word.ToCanonical());
    }

    // An expression is written in braces (language 3, expression).
    [Fact]
    public void ToCanonical_VariableWithExpression_WritesTheExpressionInBraces()
    {
        var word = new Word { Key = "VAR", Addr = "Q1", Value = new ExprValue("$Q1 + 20", null) };

        Assert.Equal("VAR:Q1={$Q1 + 20}", word.ToCanonical());
    }
}
