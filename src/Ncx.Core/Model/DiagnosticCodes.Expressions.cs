namespace Ncx.Core.Model;

// The codes of the expression parser, PAR100-PAR149 (the ranges in DiagnosticCodes.cs, D98): one ERROR for each way
// the text between the braces of a word value can break the grammar of language 4.12. The errors of evaluation, a
// division by zero or an unassigned variable, are VM codes of the evaluator (P4-01).
public static partial class DiagnosticCodes
{
    /// <summary>
    /// A character that begins no part of an expression, such as # or a single = (language 4.12).
    /// </summary>
    public const string ExpressionUnexpectedCharacter = "PAR100";

    /// <summary>
    /// A number without a digit before or after its decimal point, such as .5 or 1. (language 2 rule 5, language 3,
    /// decimal).
    /// </summary>
    public const string ExpressionMalformedNumber = "PAR101";

    /// <summary>
    /// A $ without a variable name after it (language 4.9, 4.12, variable).
    /// </summary>
    public const string ExpressionMissingVariableName = "PAR102";

    /// <summary>
    /// Nothing between the braces (language 3, expression).
    /// </summary>
    public const string ExpressionEmpty = "PAR103";

    /// <summary>
    /// An operator without its operand, or something else where an operand belongs (language 4.12, primary).
    /// </summary>
    public const string ExpressionMissingOperand = "PAR104";

    /// <summary>
    /// A parenthesis that is opened and not closed, or closed without being opened (language 4.12, primary).
    /// </summary>
    public const string ExpressionUnbalancedParenthesis = "PAR105";

    /// <summary>
    /// A bracket of a variable index that is opened and not closed, or closed without being opened (language 4.12,
    /// variable).
    /// </summary>
    public const string ExpressionUnbalancedBracket = "PAR106";

    /// <summary>
    /// A call of a name that is none of the seventeen functions of language 4.12.
    /// </summary>
    public const string ExpressionUnknownFunction = "PAR107";

    /// <summary>
    /// A name without $ and without parentheses: a variable without its $ or a function without its arguments
    /// (language 4.9, 4.12, primary).
    /// </summary>
    public const string ExpressionBareName = "PAR108";

    /// <summary>
    /// A part after a complete expression, where only an operator or the end may follow (language 4.12).
    /// </summary>
    public const string ExpressionUnexpectedToken = "PAR109";

    /// <summary>
    /// A second comparison behind a comparison, 1 &lt; $A &lt; 3: a comparison takes two sums (language 4.12,
    /// cmpexpr).
    /// </summary>
    public const string ExpressionChainedComparison = "PAR110";
}
