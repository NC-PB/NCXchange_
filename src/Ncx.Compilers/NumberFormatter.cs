using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// Writes numbers as the machine takes them, per [format] (machine-config 2): the decimals per address, trailing
/// zeros, the decimal separator, always in the invariant culture (code-guidelines 3.4). NCX never rounds or formats a
/// number and the compiler applies the number format of the target machine (language 2 rule 5): what fits is written
/// as it is, and a value with more decimals than the machine takes is rounded to them with a WARNING.
/// </summary>
public sealed class NumberFormatter
{
    // The decimal point, and the comma of Klartext files (machine-config 2; controllers heidenhain.md 1).
    private const string Point = ".";
    private const string Comma = ",";

    // A decimal keeps at most 28 decimals.
    private const int MostDecimals = 28;

    private readonly OutputFormat? _format;
    private readonly Diagnostics _diagnostics;
    private readonly bool _pointOnWholeNumbers;

    /// <summary>
    /// The formatter of one machine.
    /// </summary>
    /// <param name="machine">The machine, its [format] and its controller.</param>
    /// <param name="diagnostics">Where the WARNING about a rounded value goes.</param>
    /// <param name="pointOnWholeNumbers">True for a controller that writes the decimal point on every real number,
    /// X70. on Fanuc (controllers fanuc.md 2, 10 rule 5).</param>
    public NumberFormatter(MachineConfig machine, Diagnostics diagnostics, bool pointOnWholeNumbers)
    {
        _format = machine.Format;
        _diagnostics = diagnostics;
        _pointOnWholeNumbers = pointOnWholeNumbers;
        DecimalSeparator = DecimalSeparatorOf(machine);
    }

    /// <summary>
    /// The decimal separator the machine writes, "." or ",".
    /// </summary>
    public string DecimalSeparator { get; }

    /// <summary>
    /// The decimal separator of a machine as the configuration resolves it: decimal_separator of [format] when the
    /// file writes it, else the separator of the controller family (wave-1 question #64).
    /// </summary>
    /// <param name="machine">The machine.</param>
    public static string DecimalSeparatorOf(MachineConfig machine)
    {
        // A written decimal_separator wins; without it a Heidenhain machine writes the comma of Klartext files and
        // the others the point (machine-config 2; controllers heidenhain.md 1 and 8 rule 2, differences.md, Numbers;
        // wave-1 question #64).
        if (machine.Format?.DecimalSeparator is string written)
        {
            return written == Comma ? Comma : Point;
        }

        return machine.Machine.Controller == Controller.Heidenhain ? Comma : Point;
    }

    /// <summary>
    /// The decimals [format] gives an address; null when it gives none.
    /// </summary>
    /// <param name="address">The address as decimals = { X = 3, F = 1, S = 0 } writes it.</param>
    public int? DecimalsOf(string address)
    {
        if (_format is null || !_format.Decimals.TryGetValue(address, out int decimals))
        {
            return null;
        }

        return Math.Clamp(decimals, 0, MostDecimals);
    }

    /// <summary>
    /// Writes a number of an address: X, F, S, or the address whose decimals a native address takes, X for I.
    /// </summary>
    /// <param name="address">The address of [format] decimals whose decimals apply.</param>
    /// <param name="value">The value, as the virtual machine or the block has it.</param>
    /// <param name="block">The block it is written for, which the WARNING cites (D98).</param>
    /// <returns>The digits with the decimal separator of the machine, without the address.</returns>
    public string Format(string address, decimal value, Block block)
    {
        // What fits is never rounded. A value with more decimals than the machine takes for the address is rounded to
        // them, half away from zero as language 4.12 rounds, with a WARNING (phase 3, P3-03; language 2 rule 5).
        int? decimals = DecimalsOf(address);
        decimal written = value;
        if (decimals is int places && Math.Round(value, places) != value)
        {
            written = Math.Round(value, places, MidpointRounding.AwayFromZero);
            _diagnostics.Warning(block, DiagnosticCodes.MoreDecimalsThanTheMachineTakes,
                $"{address}{Invariant(value)} has more decimals than the machine takes for {address}, "
                + $"{places.ToString(CultureInfo.InvariantCulture)} ([format] decimals), so it is written as "
                + $"{address}{Invariant(written)} (machine-config 2).");
        }

        return Digits(written, decimals).Replace(Point, DecimalSeparator, StringComparison.Ordinal);
    }

    /// <summary>
    /// Writes a feed, the address F of [format] decimals.
    /// </summary>
    public string FormatFeed(decimal value, Block block)
    {
        return Format("F", value, block);
    }

    /// <summary>
    /// Writes a spindle speed, the address S of [format] decimals.
    /// </summary>
    public string FormatSpeed(decimal value, Block block)
    {
        return Format("S", value, block);
    }

    // The digits of a number that fits: under trailing_zeros = true padded with zeros to the decimals of its address,
    // otherwise without trailing zeros (machine-config 2); a whole number of an address that takes decimals keeps its
    // point on a controller that writes one on every real number (controllers fanuc.md 10 rule 5); zero never gets a
    // minus sign.
    private string Digits(decimal value, int? decimals)
    {
        decimal number = value == 0m ? 0m : value;
        string text = _format is { TrailingZeros: true } && decimals is int places
            ? number.ToString("F" + places.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            : WithoutTrailingZeros(number.ToString(CultureInfo.InvariantCulture));
        if (_pointOnWholeNumbers && decimals > 0 && !text.Contains(Point, StringComparison.Ordinal))
        {
            text += Point;
        }

        return text;
    }

    // 70.500 is 70.5 and 70.000 is 70.
    private static string WithoutTrailingZeros(string text)
    {
        if (!text.Contains(Point, StringComparison.Ordinal))
        {
            return text;
        }

        return text.TrimEnd('0').TrimEnd('.');
    }

    private static string Invariant(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
