namespace Ncx.Config.Tests;

/// <summary>
/// The spelling of M and G codes in the templates of a machine file (D105, machine-config 5).
/// </summary>
public sealed class FunctionValuesTests
{
    // D105: every M or G code token of a value loses its leading zeros, also inside a multi-word template with
    // parameters or placeholders; text that is not an M or G code is kept as written (machine-config 5).
    [Theory]
    [InlineData("M08", "M8")]
    [InlineData("M8", "M8")]
    [InlineData("M00", "M0")]
    [InlineData("M030", "M30")]
    [InlineData("G01", "G1")]
    [InlineData("G00", "G0")]
    [InlineData("M03 P11", "M3 P11")]
    [InlineData("G01 X{x}", "G1 X{x}")]
    [InlineData("G05.1 Q1", "G5.1 Q1")]
    [InlineData("H7={value} M07", "H7={value} M7")]
    [InlineData("T0\nG0361", "T0\nG361")]
    [InlineData("M04=3", "M4=3")]
    public void Normalize_CodeWithLeadingZeros_IsWrittenWithoutThem(string written, string normalized)
    {
        Assert.Equal(normalized, FunctionValues.Normalize(written));
    }

    // D105, machine-config 5: builder cycles, placeholders, Siemens words and Heidenhain text carry no M or G code
    // token and stay exactly as written.
    [Theory]
    [InlineData("L707({angle})")]
    [InlineData("WAITM({mark},{channels})")]
    [InlineData("M{mark} P{paths}")]
    [InlineData("M4=3")]
    [InlineData("SPOS[4]={angle}")]
    [InlineData("G340 T{tool:02}{offset:02}. A{next:02}.")]
    [InlineData("M140 MB MAX")]
    [InlineData("DIAMON")]
    [InlineData("G96 S4={value}")]
    [InlineData("T=\"{name}\"\nTC({offset},,,{kind},{b},{c})")]
    [InlineData("LIMS={value}")]
    [InlineData("TRACYL_S4({d})")]
    [InlineData("CYCL DEF 32.0 TOLERANZ")]
    [InlineData("")]
    public void Normalize_TextWithoutPaddedCodes_StaysAsWritten(string written)
    {
        Assert.Equal(written, FunctionValues.Normalize(written));
    }

    // D105: a bare integer, or a string of digits, means M followed by the number.
    [Theory]
    [InlineData(8L, "M8")]
    [InlineData(0L, "M0")]
    [InlineData(106L, "M106")]
    public void FromNumber_BareInteger_IsTheMCode(long number, string code)
    {
        Assert.Equal(code, FunctionValues.FromNumber(number));
    }
}
