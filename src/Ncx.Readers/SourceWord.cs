using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers;

/// <summary>
/// One word of a source block: its address, its value as written, the value as a number when it is one, and the
/// expression when the controller wrote one (architecture 7). A reader rule of a plugin sees the words in this form
/// (D106).
/// </summary>
public sealed record SourceWord
{
    /// <summary>
    /// The address, uppercase: "X", "G", "M", "#", "Q".
    /// </summary>
    public required string Address { get; init; }

    /// <summary>
    /// The value as written after the address, without the equals sign of an extended address: "70.", "-.534",
    /// "+10", "-6,964"; empty for a word without a value.
    /// </summary>
    public string Text { get; init; } = "";

    /// <summary>
    /// The value as a number; null when the word carries an expression or no number.
    /// </summary>
    public decimal? Number { get; init; }

    /// <summary>
    /// The expression as the controller writes it, "[#1+2]" or "R1*2"; null for a word without one. A reader
    /// converts it to an NCX expression with its own rules (language 4.12).
    /// </summary>
    public string? Expression { get; init; }

    /// <summary>
    /// The value as an NCX number, the digits as written in the lexical form of language 3: no plus sign, no trailing
    /// dot, a zero in front of a leading dot, the point as the decimal separator; "70." is 70, "-.534" is -0.534,
    /// "+10" is 10, "-6,964" is -6.964 (language 2 rule 5, language 3; controllers fanuc.md 2, heidenhain.md 7 rule 8).
    /// </summary>
    /// <returns>An IntegerValue or a DecimalValue; null when the text is no number.</returns>
    public Value? ToNcxNumber()
    {
        // NCX never rounds or formats a number: the digits stay as written, only the forms that language 3 does not
        // take are changed, the plus sign, the trailing and the leading dot, and the comma of Klartext (language 2
        // rule 5).
        string text = Text.Replace(',', '.');
        if (text.StartsWith('+'))
        {
            text = text.Substring(1);
        }

        string sign = text.StartsWith('-') ? "-" : "";
        string digits = text.Substring(sign.Length);
        int dot = digits.IndexOf('.');
        string whole = dot < 0 ? digits : digits.Substring(0, dot);
        string fraction = dot < 0 ? "" : digits.Substring(dot + 1);
        if ((whole.Length == 0 && fraction.Length == 0) || !IsDigits(whole) || !IsDigits(fraction))
        {
            return null;
        }

        string written = sign + (whole.Length == 0 ? "0" : whole);
        if (fraction.Length == 0)
        {
            return long.TryParse(written, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long integer)
                ? new IntegerValue(integer, written)
                : null;
        }

        written += "." + fraction;
        return decimal.TryParse(written, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out decimal number)
            ? new DecimalValue(number, written)
            : null;
    }

    private static bool IsDigits(string text)
    {
        foreach (char character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
