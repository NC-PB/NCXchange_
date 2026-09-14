using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The numbers of a SINUMERIK source as NCX writes them: taken as written where the source gives them (language 2 rule
/// 5, controllers siemens.md 11 rule 9), and in the shortest form of language 3 where the reader computes one, the
/// center of an arc, a point of a pattern, the corner of a chamfer.
/// </summary>
internal static class SiemensNumbers
{
    /// <summary>
    /// Computed coordinates keep the decimals of the source words they come from, at least these (wave-1 question #46,
    /// as the Fanuc and Heidenhain readers keep them).
    /// </summary>
    public const int LeastDecimals = 3;

    /// <summary>
    /// A computed number as an NCX number: an integer when it has no fraction, a decimal without trailing zeros
    /// otherwise, 50.000 is 50 and -0.5340 is -0.534 (language 3).
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

    /// <summary>
    /// A number of the geometry, computed in double, rounded to a decimal half away from zero, the one rounding NCX
    /// defines (language 4.12, ROUND).
    /// </summary>
    /// <param name="number">The computed number.</param>
    /// <param name="decimals">The decimals to keep.</param>
    public static decimal Round(double number, int decimals)
    {
        return Math.Round((decimal)number, decimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The digits a number text has after its decimal point: 3 for 50.534, 0 for 70. and 70.
    /// </summary>
    /// <param name="text">The number as written.</param>
    public static int DecimalsOf(string text)
    {
        int dot = text.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? 0 : text.Length - dot - 1;
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
    /// A number as written in the source as an NCX number, the digits kept: "70." is 70, "-.534" is -0.534, "+10" is
    /// 10 (language 2 rule 5, language 3); null when the text is no number.
    /// </summary>
    /// <param name="text">The number as written.</param>
    public static Value? Parse(string text)
    {
        return new SourceWord { Address = "", Text = text.Trim() }.ToNcxNumber();
    }

    /// <summary>
    /// A whole number of a text, for counts, spindle numbers and modes; null when the text is no whole number.
    /// </summary>
    /// <param name="text">The number as written.</param>
    public static int? WholeNumber(string text)
    {
        return Parse(text) is IntegerValue integer && integer.Number is >= int.MinValue and <= int.MaxValue
            ? (int)integer.Number
            : null;
    }
}
