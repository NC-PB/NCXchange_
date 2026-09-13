using System.Globalization;

namespace Ncx.Readers;

/// <summary>
/// The M and G codes of a source compared by number, so that a source M08 matches the M8 of the machine's tables and
/// G01 is the G1 of a modal group (machine-config 5, D105).
/// </summary>
internal static class NativeCode
{
    /// <summary>
    /// The code of a word in the spelling of D105, the address and its number without leading zeros: "G1" for G01,
    /// "G5.1" for G05.1, "M8" for M08; null for a word without a number.
    /// </summary>
    /// <param name="word">The source word.</param>
    public static string? Of(SourceWord word)
    {
        if (word.Number is null || word.Expression is not null)
        {
            return null;
        }

        string text = word.Text.Replace(',', '.');
        int dot = text.IndexOf('.');
        string whole = (dot < 0 ? text : text.Substring(0, dot)).TrimStart('0');
        string fraction = dot < 0 ? "" : text.Substring(dot);
        return word.Address + (whole.Length == 0 ? "0" : whole) + fraction;
    }

    /// <summary>
    /// Tells whether two codes are the same code: the same letters and the same number, "M08" and "M8", "G05.1" and
    /// "G5.1" (D105); codes without a number, "RL_POS", compare as written.
    /// </summary>
    /// <param name="first">One code as written.</param>
    /// <param name="second">The other code as written.</param>
    public static bool SameCode(string first, string second)
    {
        // Codes compare by number, so that the spelling of the source and of the machine file need not agree
        // (machine-config 5, D105).
        if (Split(first, out string firstLetters, out decimal firstNumber)
            && Split(second, out string secondLetters, out decimal secondNumber))
        {
            return firstLetters == secondLetters && firstNumber == secondNumber;
        }

        return first == second;
    }

    // A code is letters followed by a number with an optional decimal point: M08, G5.1, L770.
    private static bool Split(string code, out string letters, out decimal number)
    {
        int firstDigit = 0;
        while (firstDigit < code.Length && !char.IsAsciiDigit(code[firstDigit]))
        {
            firstDigit++;
        }

        letters = code.Substring(0, firstDigit);
        number = 0;
        string digits = code.Substring(firstDigit);
        if (letters.Length == 0 || digits.Length == 0)
        {
            return false;
        }

        return decimal.TryParse(digits, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);
    }
}
