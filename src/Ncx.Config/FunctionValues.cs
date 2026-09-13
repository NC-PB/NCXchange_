using System.Globalization;
using System.Text.RegularExpressions;

namespace Ncx.Config;

/// <summary>
/// The spelling of M and G codes in the values of a machine file (D105): a bare integer means M followed by the
/// number, and every M or G code is written without leading zeros, so that the compiler writes one form and readers
/// compare codes by number (machine-config 5).
/// </summary>
internal static partial class FunctionValues
{
    /// <summary>
    /// The template with every M and G code token written without leading zeros: "M03 P11" is "M3 P11", "G01 X{x}" is
    /// "G1 X{x}", "G05.1" is "G5.1"; text that is not an M or G code stays as written (machine-config 5, D105).
    /// </summary>
    /// <param name="template">The template as the file writes it.</param>
    public static string Normalize(string template)
    {
        // The zeros in front of the number of an M or G code go; the letter, the number and everything after it stay.
        return CodeWithLeadingZeros().Replace(
            template, code => code.Groups["letter"].Value + code.Groups["number"].Value);
    }

    /// <summary>
    /// A bare integer as a function value: M followed by the number, 8 is "M8" (D105).
    /// </summary>
    /// <param name="number">The integer, 0 or more.</param>
    public static string FromNumber(long number)
    {
        return "M" + number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A function value written as a string: digits alone mean M followed by the number, "08" is "M8"; any other text
    /// keeps its words and loses the leading zeros of its codes, "M08" is "M8" (D105).
    /// </summary>
    /// <param name="text">The string as the file writes it.</param>
    public static string FromText(string text)
    {
        if (text.Length == 0 || !IsDigits(text))
        {
            return Normalize(text);
        }

        string number = text.TrimStart('0');
        return "M" + (number.Length == 0 ? "0" : number);
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

    // An M or G code token: the letter at the start of the value or after a character that is not a letter, a digit
    // or an underscore (M140 MB MAX, WAITM(...) and LIMS= are no codes), then at least one zero before the number.
    [GeneratedRegex("(?<![A-Za-z0-9_])(?<letter>[MG])0+(?<number>[0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex CodeWithLeadingZeros();
}
