namespace Ncx.Readers.Tests;

/// <summary>
/// Readers compare M and G codes by number, so that a source M08 matches the table's M8 (machine-config 5, D105).
/// </summary>
public sealed class NativeCodeTests
{
    [Theory]
    [InlineData("M08", "M8")]
    [InlineData("G05.1", "G5.1")]
    [InlineData("G0411", "G411")]
    [InlineData("RL_POS", "RL_POS")]
    public void SameCode_SameLettersAndNumber_IsTheSameCode(string first, string second)
    {
        Assert.True(NativeCode.SameCode(first, second));
    }

    [Theory]
    [InlineData("M8", "M9")]
    [InlineData("G411", "M411")]
    [InlineData("L770", "L77")]
    public void SameCode_OtherLettersOrNumber_IsAnotherCode(string first, string second)
    {
        Assert.False(NativeCode.SameCode(first, second));
    }

    [Theory]
    [InlineData("G", "01", "G1")]
    [InlineData("G", "00", "G0")]
    [InlineData("G", "54.1", "G54.1")]
    [InlineData("M", "030", "M30")]
    public void Of_WordWithLeadingZeros_IsTheCodeWithout(string address, string text, string code)
    {
        var word = new SourceWord
        {
            Address = address,
            Text = text,
            Number = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture),
        };

        Assert.Equal(code, NativeCode.Of(word));
    }
}
