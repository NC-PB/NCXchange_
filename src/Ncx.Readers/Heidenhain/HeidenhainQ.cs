using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The Q parameters and their assignments (controllers heidenhain.md 6; controller-mapping 6, VAR; language 4.9): the
/// reader keeps the names Q5, QL5 and QR5 as NCX variable names; FN 0 assigns, FN 1 adds, FN 2 subtracts, FN 3
/// multiplies, FN 4 divides, FN 5 takes the square root, and a formula Q1 = Q2 + 3 * SIN Q3 is an NCX expression, each
/// written as VAR:Qn=... The string parameters QS have their own formulas and stay RAW.
/// </summary>
internal static class HeidenhainQ
{
    /// <summary>
    /// Tells whether an address names a Q parameter, Q5, QL5, QR5 or QS5 (controllers heidenhain.md 6).
    /// </summary>
    /// <param name="address">The address of a word.</param>
    public static bool IsParameter(string address)
    {
        int letters = 0;
        while (letters < address.Length && char.IsAsciiLetter(address[letters]))
        {
            letters++;
        }

        string prefix = address.Substring(0, letters);
        if (prefix is not ("Q" or "QL" or "QR" or "QS") || letters == address.Length)
        {
            return false;
        }

        for (int index = letters; index < address.Length; index++)
        {
            if (!char.IsAsciiDigit(address[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Reads a formula, Q1 = Q2 + 3 * SIN Q3, into VAR:Q1={$Q2 + 3 * SIN($Q3)} (controllers heidenhain.md 6).
    /// </summary>
    /// <param name="block">The block being read, whose first word is the assignment.</param>
    public static void ReadFormula(HeidenhainBlock block)
    {
        SourceWord assignment = block.Source.Words[0];
        block.MarkRead(assignment);
        Assign(block, assignment.Address, assignment.Text);
    }

    /// <summary>
    /// Reads FN 0 to FN 5: FN 0: Q5 = +10 assigns, FN 1: Q1 = +Q2 + +5 adds, FN 2 subtracts, FN 3 multiplies, FN 4:
    /// Q1 = +Q2 DIV +3 divides, FN 5: Q1 = SQRT +Q2 takes the square root (controllers heidenhain.md 6).
    /// </summary>
    /// <param name="block">The block being read, whose first word is FN.</param>
    /// <param name="function">The number of the FN function, 0 to 5.</param>
    public static void ReadFunction(HeidenhainBlock block, int function)
    {
        block.MarkAllRead();
        SourceWord? assignment = block.Source.Words.Count == 2 ? block.Source.Words[1] : null;
        if (assignment is null || !IsParameter(assignment.Address))
        {
            block.Draft.KeepAsRaw($"FN {function} assigns no Q parameter");
            return;
        }

        if (function == 0)
        {
            Assign(block, assignment.Address, assignment.Text);
            return;
        }

        string[] parts = assignment.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string? text = function == 5 ? SquareRoot(parts, out string? problem) : Arithmetic(function, parts, out problem);
        Value? value = text is null
            ? null
            : HeidenhainExpression.Parse(text, block.Line, block.Diagnostics.File, out problem);
        if (value is null)
        {
            block.Draft.KeepAsRaw(problem ?? $"FN {function}: {assignment.Text} has no NCX form");
            return;
        }

        block.Draft.Main.Add("VAR", assignment.Address, value);
    }

    // A value or a formula assigned to a parameter; the string parameters QS have their own formulas (controllers
    // heidenhain.md 6).
    private static void Assign(HeidenhainBlock block, string parameter, string right)
    {
        if (parameter.StartsWith("QS", StringComparison.Ordinal))
        {
            block.Draft.KeepAsRaw($"{parameter} is a string parameter, whose formulas NCX has no form for");
            return;
        }

        Value? value = HeidenhainExpression.ValueOf(right, block.Line, block.Diagnostics.File, out string? problem);
        if (value is null)
        {
            block.Draft.KeepAsRaw(problem!);
            return;
        }

        block.Draft.Main.Add("VAR", parameter, value);
    }

    // FN 1 to FN 4: two operands and the operator of the function between them, + - * and DIV.
    private static string? Arithmetic(int function, string[] parts, out string? problem)
    {
        string[] operators = ["+", "-", "*", "DIV"];
        string[] ncx = ["+", "-", "*", "/"];
        problem = null;
        if (function > 4 || parts.Length != 3
            || (!string.Equals(parts[1], operators[function - 1], StringComparison.OrdinalIgnoreCase)
                && !(function == 4 && parts[1] == "/")))
        {
            problem = $"FN {function} needs two operands and the operator {operators[Math.Min(function, 4) - 1]}";
            return null;
        }

        string? first = HeidenhainExpression.ToText(parts[0], out problem);
        string? second = first is null ? null : HeidenhainExpression.ToText(parts[2], out problem);
        return second is null ? null : first + " " + ncx[function - 1] + " " + second;
    }

    // FN 5: SQRT and its operand.
    private static string? SquareRoot(string[] parts, out string? problem)
    {
        problem = null;
        if (parts.Length != 2 || !string.Equals(parts[0], "SQRT", StringComparison.OrdinalIgnoreCase))
        {
            problem = "FN 5 needs SQRT and its operand";
            return null;
        }

        string? operand = HeidenhainExpression.ToText(parts[1], out problem);
        return operand is null ? null : "SQRT(" + operand + ")";
    }
}
