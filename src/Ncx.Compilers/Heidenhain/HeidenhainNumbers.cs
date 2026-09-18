using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The numbers of Klartext (controllers heidenhain.md 1, 2, 6; 8 rule 2): the comma as the decimal separator, which the
/// NumberFormatter writes from decimal_separator of [format], a sign on every coordinate, X+10 Y-7,025 Z+0, and a Q
/// parameter where a number may stand, L X+Q1 Y-Q2 (FQ1 and SQ2 of a feed and a speed, HeidenhainParameters).
/// </summary>
internal static class HeidenhainNumbers
{
    private const string Plus = "+";
    private const string Minus = "-";

    /// <summary>
    /// A number with its sign, the comma and the decimals of its address (heidenhain 8 rule 2; machine-config 2).
    /// </summary>
    /// <param name="writing">The block being written.</param>
    /// <param name="address">The address whose decimals of [format] apply: X, F; any other takes the number as it
    /// is.</param>
    /// <param name="value">The value.</param>
    public static string Signed(HeidenhainBlock writing, string address, decimal value)
    {
        string digits = writing.Numbers.Format(address, value, writing.Block);
        return digits.StartsWith(Minus, StringComparison.Ordinal) ? digits : Plus + digits;
    }

    /// <summary>
    /// The number of a value, an integer or a decimal; null for any other value.
    /// </summary>
    public static decimal? NumberOf(Value value)
    {
        return value switch
        {
            IntegerValue integer => integer.Number,
            DecimalValue number => number.Number,
            _ => null,
        };
    }

    // Expressions may stand wherever a number stands, L X+Q1 Y-Q2 (controllers heidenhain.md 6): a Q parameter with
    // its sign.
    // TODO(question): heidenhain.md 6 shows a Q parameter where a number stands and gives no form for a formula
    // there, {$Q1 + 5} in a coordinate; such a value has no Klartext form in this compiler and is reported (CMP102).

    /// <summary>
    /// The value of a word where Klartext takes a number: the number with its sign, or a Q parameter with its sign,
    /// +Q1 of {$Q1}, -Q2 of {-$Q2}; null for any other value, which the caller reports.
    /// </summary>
    public static string? SignedValue(HeidenhainBlock writing, string address, Value value)
    {
        if (NumberOf(value) is decimal number)
        {
            return Signed(writing, address, number);
        }

        return value is ExprValue { Tree: ExprNode tree } ? HeidenhainFormula.SignedParameter(tree) : null;
    }

    /// <summary>
    /// Reports a word whose value has no Klartext form in this compiler (CMP102): a formula where a number stands, or a
    /// Q parameter in a word the compiler writes from its number.
    /// </summary>
    // TODO: a Q parameter in a word whose number the compiler writes as it stands, the R of CR, the MB of M140, the
    // vector of LN, the time of cycle 9, could be written as the parameter (controllers heidenhain.md 6); these words
    // are written from numbers only, as the values the compiler computes from numbers are, the Q values of CYCL DEF.
    public static void ReportValue(HeidenhainBlock writing, Word word)
    {
        bool parameter = word.Value is ExprValue { Tree: ExprNode tree }
            && HeidenhainFormula.SignedParameter(tree) is not null;
        string reason = parameter
            ? "a Q parameter may stand wherever a number stands in Klartext (controllers heidenhain.md 6), and the "
                + "compiler writes this word from its number, which a Q parameter gives only when the program runs"
            : "Klartext takes a number or a Q parameter where a number stands, L X+Q1 Y-Q2, and the documents give no "
                + "form for a formula there (controllers heidenhain.md 6)";
        writing.Error(DiagnosticCodes.HeidenhainValueWithoutKlartext,
            $"{word.ToCanonical()}: {reason}; nothing is written for it.");
    }
}
