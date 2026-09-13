using System.Globalization;

namespace Ncx.Core.Model;

/// <summary>
/// The value of a number that NCXchange computed rather than read: a limit the expander clamps to (D64), a speed the
/// virtual machine writes back for @RESTORE (virtual machine 3.10).
/// </summary>
internal static class NumberValues
{
    /// <summary>
    /// An integer when the number has no decimals, a decimal otherwise, each with the text of the number in the
    /// invariant culture, 6000 or 1500.5 (language 3, integer and decimal; code-guidelines 3.4).
    /// </summary>
    public static Value Of(decimal number)
    {
        string text = number.ToString(CultureInfo.InvariantCulture);
        if (text.Contains('.', StringComparison.Ordinal))
        {
            return new DecimalValue(number, text);
        }

        return new IntegerValue(decimal.ToInt64(number), text);
    }
}
