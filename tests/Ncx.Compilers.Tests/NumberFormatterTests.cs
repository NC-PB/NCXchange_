using Ncx.Compilers.Tests.Fakes;
using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Tests;

/// <summary>
/// Numbers per [format] (machine-config 2): decimals per address, trailing zeros, the decimal separator, never rounds
/// what fits, a WARNING when a value has more decimals than the machine takes (phase 3, P3-03).
/// </summary>
public sealed class NumberFormatterTests
{
    // The block a WARNING cites.
    private static readonly Block s_block = new() { Line = 7, Words = [] };

    // Phase 3, P3-03: what fits is written as it is, never rounded.
    [Fact]
    public void Format_ValueThatFits_IsWrittenAsItIs()
    {
        var diagnostics = new Diagnostics("T.ncx");
        NumberFormatter numbers = Formatter(FakeMachines.PointFormat, diagnostics);

        Assert.Equal("10.12", numbers.Format("X", 10.12m, s_block));
        Assert.Empty(diagnostics.Items);
    }

    // Phase 3, P3-03; language 4.12: a value with more decimals than the machine takes is rounded half away from zero
    // to its decimals, with a WARNING on the block.
    [Theory]
    [InlineData("10.1235", "10.124")]
    [InlineData("-10.1235", "-10.124")]
    [InlineData("0.0005", "0.001")]
    public void Format_MoreDecimalsThanTheMachineTakes_IsRoundedWithCmp020(string value, string written)
    {
        var diagnostics = new Diagnostics("T.ncx");
        NumberFormatter numbers = Formatter(FakeMachines.PointFormat, diagnostics);

        string text = numbers.Format("X", decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
            s_block);

        Assert.Equal(written, text);
        Diagnostic warning = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.MoreDecimalsThanTheMachineTakes, warning.Code);
        Assert.Equal(7, warning.Line);
    }

    // Machine-config 2: a value rounded to zero is written without a minus sign.
    [Fact]
    public void Format_NegativeValueRoundedToZero_HasNoMinusSign()
    {
        NumberFormatter numbers = Formatter(FakeMachines.PointFormat, new Diagnostics("T.ncx"));

        Assert.Equal("0", numbers.Format("X", -0.0004m, s_block));
    }

    // Machine-config 2: trailing_zeros = true pads to the decimals of the address, with the comma of the machine.
    [Fact]
    public void Format_TrailingZerosAndComma_PadsToTheDecimalsWithTheComma()
    {
        NumberFormatter numbers = Formatter(FakeMachines.CommaFormat, new Diagnostics("T.ncx"));

        Assert.Equal("10,5000", numbers.Format("X", 10.5m, s_block));
        Assert.Equal("-7,0250", numbers.Format("Y", -7.025m, s_block));
    }

    // Controllers fanuc.md 2, 10 rule 5: a controller that writes the point on every real number writes X70., and the
    // speed of S = 0 without it.
    [Fact]
    public void Format_PointOnWholeNumbers_WritesThePointWhereTheAddressTakesDecimals()
    {
        NumberFormatter numbers =
            Formatter(FakeMachines.PointFormat, new Diagnostics("T.ncx"), pointOnWholeNumbers: true);

        Assert.Equal("70.", numbers.Format("X", 70m, s_block));
        Assert.Equal("70.", numbers.Format("X", 70.000m, s_block));
        Assert.Equal("1592", numbers.FormatSpeed(1592m, s_block));
        Assert.Equal("565.", numbers.FormatFeed(565m, s_block));
    }

    // Language 2 rule 5: an address that [format] gives no decimals keeps the digits of its value.
    [Fact]
    public void Format_AddressWithoutDecimals_KeepsTheDigitsOfItsValue()
    {
        var diagnostics = new Diagnostics("T.ncx");
        NumberFormatter numbers = Formatter(FakeMachines.PointFormat, diagnostics);

        Assert.Equal("1.23456", numbers.Format("Q", 1.23456m, s_block));
        Assert.Equal("2.5", numbers.Format("Q", 2.500m, s_block));
        Assert.Empty(diagnostics.Items);
    }

    // Wave-1 question #64: without decimal_separator a Heidenhain machine writes the comma of its files, the others the
    // point; a written decimal_separator wins.
    [Theory]
    [InlineData("heidenhain", null, ",")]
    [InlineData("fanuc", null, ".")]
    [InlineData("siemens", null, ".")]
    [InlineData("heidenhain", ".", ".")]
    [InlineData("fanuc", ",", ",")]
    public void DecimalSeparatorOf_ControllerAndKey_IsTheWrittenKeyElseTheControllersOwn(string controller,
        string? written, string expected)
    {
        string format = written is null
            ? "[format]\ndecimals = { X = 3 }"
            : $"[format]\ndecimal_separator = \"{written}\"\ndecimals = {{ X = 3 }}";

        MachineConfig machine = Machine(FakeMachines.Mill(controller, format));

        Assert.Equal(expected, NumberFormatter.DecimalSeparatorOf(machine));
    }

    private static NumberFormatter Formatter(string format, Diagnostics diagnostics, bool pointOnWholeNumbers = false)
    {
        return new NumberFormatter(Machine(FakeMachines.Mill(format: format)), diagnostics, pointOnWholeNumbers);
    }

    private static MachineConfig Machine(string toml)
    {
        var diagnostics = new Diagnostics("fake.toml");
        return MachineConfigLoader.LoadText(toml, diagnostics)
            ?? throw new InvalidOperationException(diagnostics.ToText());
    }
}
