using Ncx.Core.Model;

namespace Ncx.Readers.Tests;

/// <summary>
/// A source number becomes an NCX number with its digits as written, in the lexical form of language 3 (language 2
/// rule 5; controllers fanuc.md 2, heidenhain.md 7 rule 8).
/// </summary>
public sealed class SourceWordTests
{
    [Theory]
    [InlineData("70.", "70")]
    [InlineData("-.534", "-0.534")]
    [InlineData("+10", "10")]
    [InlineData("-6,964", "-6.964")]
    [InlineData("1.500", "1.500")]
    [InlineData("0", "0")]
    public void ToNcxNumber_SourceForms_AreWrittenInTheFormOfLanguage3(string source, string ncx)
    {
        Value? value = new SourceWord { Address = "X", Text = source }.ToNcxNumber();

        Assert.NotNull(value);
        Assert.Equal(ncx, value.ToCanonical());
    }

    [Fact]
    public void ToNcxNumber_WholeNumberAndFraction_AreIntegerAndDecimal()
    {
        Assert.IsType<IntegerValue>(new SourceWord { Address = "X", Text = "70." }.ToNcxNumber());
        Assert.IsType<DecimalValue>(new SourceWord { Address = "X", Text = "7.5" }.ToNcxNumber());
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("-")]
    [InlineData("#1")]
    [InlineData("1.2.3")]
    public void ToNcxNumber_NoNumber_IsNull(string source)
    {
        Assert.Null(new SourceWord { Address = "X", Text = source }.ToNcxNumber());
    }
}
