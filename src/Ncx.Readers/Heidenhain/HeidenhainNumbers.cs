using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The numbers of a Klartext source as NCX writes them: taken as written where the source gives them, the comma or the
/// dot as the decimal separator (language 2 rule 5; controllers heidenhain.md 7 rule 8), and in the shortest form of
/// language 3 where the reader computes one, an absolute coordinate from a surface and a depth, a point from a pole.
/// </summary>
internal static class HeidenhainNumbers
{
    // Computed numbers keep the decimals of the source numbers they come from, at least these.
    public const int LeastDecimals = 3;

    /// <summary>
    /// A computed number as an NCX number: an integer when it has no fraction, a decimal without trailing zeros
    /// otherwise, 5.000 is 5 and -21.7320 is -21.732 (language 3).
    /// </summary>
    /// <param name="number">The number.</param>
    public static Value Of(decimal number)
    {
        string text = number.ToString("0.############################", CultureInfo.InvariantCulture);
        if (text == "-0")
        {
            text = "0";
        }

        return text.Contains('.', StringComparison.Ordinal)
            ? new DecimalValue(decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture), text)
            : new IntegerValue(long.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture), text);
    }

    // TODO(question): no document says how many decimals a number the reader computes with trigonometry keeps, a point
    // of a polar coordinate or the centre of a tangential arc (wave-1 question #46); the caller keeps as many as the
    // source words it was computed from, at least three.

    /// <summary>
    /// A number of the geometry, computed in double, rounded to a decimal: half away from zero, the one rounding NCX
    /// defines (language 4.12, ROUND).
    /// </summary>
    /// <param name="number">The computed number.</param>
    /// <param name="decimals">The decimals to keep.</param>
    public static decimal Round(double number, int decimals)
    {
        return Math.Round((decimal)number, decimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The digits a source number has after its decimal separator: 3 for X-6,964, 0 for X+10.
    /// </summary>
    /// <param name="word">The source word with a number.</param>
    public static int DecimalsOf(SourceWord word)
    {
        string text = word.Text.Replace(',', '.');
        int separator = text.IndexOf('.', StringComparison.Ordinal);
        return separator < 0 ? 0 : text.Length - separator - 1;
    }

    /// <summary>
    /// The number of an NCX number value; null for any other value, an expression among them.
    /// </summary>
    /// <param name="value">The value of a word.</param>
    public static decimal? NumberOf(Value? value)
    {
        return value switch
        {
            IntegerValue integer => integer.Number,
            DecimalValue number => number.Number,
            _ => null,
        };
    }

    /// <summary>
    /// A number as Klartext writes it, a sign in front and the comma or the dot as the decimal separator: "+10",
    /// "-6,964", "1.5"; null for any other text (controllers heidenhain.md 1, 7 rule 8).
    /// </summary>
    /// <param name="text">The text after the address.</param>
    public static decimal? Parse(string text)
    {
        string number = text.Replace(',', '.');
        bool hasDigit = false;
        foreach (char character in number)
        {
            hasDigit |= char.IsAsciiDigit(character);
        }

        return hasDigit && decimal.TryParse(number, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : null;
    }
}
