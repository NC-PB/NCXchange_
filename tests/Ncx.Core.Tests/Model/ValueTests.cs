using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Core.Tests.Model;

/// <summary>
/// The value types of language 3 and the text canonical NCX writes for each.
/// </summary>
public sealed class ValueTests
{
    // NCX never rounds or reformats a number: the writer emits the text as read (language 2 rule 5).
    [Theory]
    [InlineData("-7.025")]
    [InlineData("0.05")]
    [InlineData("007.50")]
    public void DecimalValue_ReadFromText_KeepsTheTextAsWritten(string text)
    {
        var value = new DecimalValue(decimal.Parse(text, CultureInfo.InvariantCulture), text);

        Assert.Equal(text, value.Text);
        Assert.Equal(text, value.ToCanonical());
    }

    [Fact]
    public void DecimalValue_MinusSevenPointZeroTwoFive_HoldsThatNumber()
    {
        var value = new DecimalValue(-7.025m, "-7.025");

        Assert.Equal(-7.025m, value.Number);
    }

    [Theory]
    [InlineData("1592")]
    [InlineData("-5")]
    [InlineData("007")]
    public void IntegerValue_ReadFromText_KeepsTheTextAsWritten(string text)
    {
        var value = new IntegerValue(long.Parse(text, CultureInfo.InvariantCulture), text);

        Assert.Equal(text, value.Text);
        Assert.Equal(text, value.ToCanonical());
    }

    // A quote inside a string is written with a backslash in front (language 3, string).
    [Fact]
    public void StringValue_ContentWithQuotes_WritesEscapedQuotes()
    {
        var value = new StringValue("""SIDE "MILL" D10""");

        Assert.Equal(
            """
            "SIDE \"MILL\" D10"
            """,
            value.ToCanonical());
    }

    // A backslash inside a string is written twice (language 3, string).
    [Fact]
    public void StringValue_ContentWithBackslash_WritesTheBackslashTwice()
    {
        var value = new StringValue("""C:\NC\PARTS""");

        Assert.Equal(
            """
            "C:\\NC\\PARTS"
            """,
            value.ToCanonical());
    }

    // The backslash of an escaped quote is itself escaped, so the text reads back as the same content.
    [Fact]
    public void StringValue_BackslashBeforeQuote_EscapesBoth()
    {
        var value = new StringValue("""A\"B""");

        Assert.Equal(
            """
            "A\\\"B"
            """,
            value.ToCanonical());
    }

    // A string may contain spaces and semicolons as they are (language 3, string).
    [Fact]
    public void StringValue_ContentWithSpacesAndSemicolon_WritesThemAsTheyAre()
    {
        var value = new StringValue("SLOT; ROW");

        Assert.Equal(
            """
            "SLOT; ROW"
            """,
            value.ToCanonical());
    }

    // A list is written with commas and without spaces (language 3, list).
    [Fact]
    public void ListValue_TwoRoles_WritesThemSeparatedByACommaWithoutSpaces()
    {
        var value = new ListValue(["MAIN", "SUB"]);

        Assert.Equal("MAIN,SUB", value.ToCanonical());
    }

    // Two lists with the same items in the same order are the same value.
    [Fact]
    public void ListValue_SameItems_IsTheSameValue()
    {
        var first = new ListValue(["X", "Y"]);
        var second = new ListValue(["X", "Y"]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, new ListValue(["Y", "X"]));
    }

    [Fact]
    public void IdentValue_Clockwise_WritesTheIdentifier()
    {
        Assert.Equal("CW", new IdentValue("CW").ToCanonical());
    }

    [Fact]
    public void NoValue_ToCanonical_IsEmpty()
    {
        Assert.Equal("", NoValue.Instance.ToCanonical());
    }

    // An expression stands in braces (language 3, expression); the tree follows from the expression parser.
    [Fact]
    public void ExprValue_Text_IsWrittenInBraces()
    {
        var value = new ExprValue("$Q1 + 20", null);

        Assert.Equal("$Q1 + 20", value.Text);
        Assert.Null(value.Tree);
        Assert.Equal("{$Q1 + 20}", value.ToCanonical());
    }

    // A state key is KEY[:ADDR], the value of @SAVE and @RESTORE (language 3, state key; D95).
    [Theory]
    [InlineData("SPINDLE", "MAIN", "SPINDLE:MAIN")]
    [InlineData("COOLANT", null, "COOLANT")]
    public void StateKeyValue_KeyAndAddress_WritesKeyColonAddress(string key, string? addr, string expected)
    {
        var value = new StateKeyValue(key, addr);

        Assert.Equal(expected, value.ToCanonical());
    }
}
