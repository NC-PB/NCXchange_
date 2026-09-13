using System.Reflection;
using Ncx.Core.Expressions;
using Ncx.Core.Model;

namespace Ncx.Core.Tests.Expressions;

/// <summary>
/// A text between braces that is not an expression of language 4.12 is one PAR ERROR on the line of its block, with
/// the position inside the expression, and gives no tree (P0-05, code-guidelines 6).
/// </summary>
public sealed class ExprParserDiagnosticsTests
{
    // The error case of P0-05: a name that is not one of the seventeen functions of the grammar (language 4.12,
    // function).
    [Fact]
    public void Function_UnknownName_IsErrorAtTheName()
    {
        Diagnostic diagnostic = SingleError("$A + FOO(1)", DiagnosticCodes.ExpressionUnknownFunction);

        Assert.Equal(
            "FOO at position 6 of {$A + FOO(1)} is not a function of language 4.12, which knows SIN, COS, TAN, ASIN, "
            + "ACOS, ATAN, ATAN2, SQRT, ABS, INT, FRAC, ROUND, SGN, LN, EXP, MIN and MAX.",
            diagnostic.Message);
    }

    // The error case of P0-05: a parenthesis that is opened and not closed (language 4.12, primary).
    [Fact]
    public void Parentheses_NotClosed_IsErrorNamingTheOpeningParenthesis()
    {
        Diagnostic diagnostic = SingleError("($A + 1) * (2", DiagnosticCodes.ExpressionUnbalancedParenthesis);

        Assert.Equal(
            "The parenthesis at position 12 of {($A + 1) * (2} is not closed: ) is expected at the end "
            + "(language 4.12, primary).",
            diagnostic.Message);
    }

    // A parenthesis that closes where none is open (language 4.12, primary).
    [Fact]
    public void Parentheses_ClosedWithoutOpening_IsErrorAtTheParenthesis()
    {
        Diagnostic diagnostic = SingleError("$A + 1)", DiagnosticCodes.ExpressionUnbalancedParenthesis);

        Assert.Equal(
            "The parenthesis at position 7 of {$A + 1)} closes nothing (language 4.12, primary).",
            diagnostic.Message);
    }

    // The arguments of a call end with a parenthesis (language 4.12, primary).
    [Fact]
    public void Function_ArgumentsNotClosed_IsErrorNamingTheOpeningParenthesis()
    {
        Diagnostic diagnostic = SingleError("MAX($A, 2", DiagnosticCodes.ExpressionUnbalancedParenthesis);

        Assert.Equal(
            "The parenthesis at position 4 of {MAX($A, 2} is not closed: , or ) is expected at the end "
            + "(language 4.12, primary).",
            diagnostic.Message);
    }

    // The arguments of a call are separated by commas (language 4.12, primary).
    [Fact]
    public void Function_ArgumentsWithoutComma_IsErrorWhereTheCommaIsExpected()
    {
        Diagnostic diagnostic = SingleError("MAX($A 2)", DiagnosticCodes.ExpressionUnbalancedParenthesis);

        Assert.Equal(
            "The parenthesis at position 4 of {MAX($A 2)} is not closed: , or ) is expected at position 8 "
            + "(language 4.12, primary).",
            diagnostic.Message);
    }

    // The error case of P0-05: an operator without its operand, or an operand position that holds something else
    // (language 4.12, primary).
    [Theory]
    [InlineData("$A +", "at the end")]
    [InlineData("* 2", "at position 1")]
    [InlineData("$A + * 2", "at position 6")]
    [InlineData("NOT", "at the end")]
    [InlineData("MAX($A, )", "at position 9")]
    [InlineData("--2", "at position 2")]
    [InlineData("$A AND OR $B", "at position 8")]
    public void Operand_Missing_IsErrorWithThePosition(string text, string place)
    {
        Diagnostic diagnostic = SingleError(text, DiagnosticCodes.ExpressionMissingOperand);

        Assert.Equal(
            $"An operand is missing {place} of {{{text}}}: a number, a $variable, a function call or an expression "
            + "in parentheses (language 4.12, primary).",
            diagnostic.Message);
    }

    // An expression holds at least one primary (language 3, expression = "{" expr "}").
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Expression_Empty_IsError(string text)
    {
        Diagnostic diagnostic = SingleError(text, DiagnosticCodes.ExpressionEmpty);

        Assert.Equal($"The expression {{{text}}} is empty (language 3, expression; 4.12).", diagnostic.Message);
    }

    // A character that begins no part of the grammar: not an operator, digit, letter, $, parenthesis, bracket or comma
    // (language 4.12).
    [Theory]
    [InlineData("#101", "at position 1", "#")]
    [InlineData("$A = 1", "at position 4", "=")]
    [InlineData("$A & $B", "at position 4", "&")]
    [InlineData("\"A\"", "at position 1", "\"")]
    [InlineData("$A ! $B", "at position 4", "!")]
    [InlineData("_A", "at position 1", "_")]
    public void Character_OutsideTheGrammar_IsErrorAtTheCharacter(string text, string place, string character)
    {
        Diagnostic diagnostic = SingleError(text, DiagnosticCodes.ExpressionUnexpectedCharacter);

        Assert.Equal(
            $"The character {character} {place} of {{{text}}} is not part of an expression (language 4.12).",
            diagnostic.Message);
    }

    // A decimal has digits on both sides of the point, and the number has no trailing dot (language 2 rule 5,
    // language 3, decimal).
    [Theory]
    [InlineData("1.", "The number at position 1 of {1.} needs a digit after the decimal point")]
    [InlineData(".5", "The number at position 1 of {.5} needs a digit before the decimal point")]
    [InlineData("1.2.3", "The number at position 4 of {1.2.3} needs a digit before the decimal point")]
    public void Number_Malformed_IsErrorAtTheNumber(string text, string message)
    {
        Diagnostic diagnostic = SingleError(text, DiagnosticCodes.ExpressionMalformedNumber);

        Assert.Equal(message + " (language 3, decimal).", diagnostic.Message);
    }

    // variable = "$" addr: the dollar sign is followed by a name that starts with a letter (language 4.9, 4.12).
    [Theory]
    [InlineData("$")]
    [InlineData("$1")]
    [InlineData("$ + 1")]
    public void Variable_DollarWithoutName_IsErrorAtTheDollarSign(string text)
    {
        Diagnostic diagnostic = SingleError(text, DiagnosticCodes.ExpressionMissingVariableName);

        Assert.Equal(
            $"The $ at position 1 of {{{text}}} is not followed by a variable name such as $Q1 "
            + "(language 4.9, 4.12, variable).",
            diagnostic.Message);
    }

    // A name without a dollar sign is a function and takes its arguments in parentheses (language 4.12, primary):
    // the usual slip is the forgotten dollar sign of a variable.
    [Fact]
    public void Name_WithoutDollarOrParentheses_IsErrorSuggestingTheVariable()
    {
        Diagnostic diagnostic = SingleError("Q1 + 1", DiagnosticCodes.ExpressionBareName);

        Assert.Equal(
            "Q1 at position 1 of {Q1 + 1} is neither a variable nor a function call; a variable is read with $, "
            + "as in $Q1 (language 4.9, 4.12).",
            diagnostic.Message);
    }

    // A function name without its parentheses (language 4.12, primary).
    [Fact]
    public void Name_FunctionWithoutParentheses_IsErrorSuggestingTheParentheses()
    {
        Diagnostic diagnostic = SingleError("SIN 30", DiagnosticCodes.ExpressionBareName);

        Assert.Equal(
            "SIN at position 1 of {SIN 30} is a function and takes its arguments in parentheses, as in SIN(...) "
            + "(language 4.12, primary).",
            diagnostic.Message);
    }

    // After a complete expression comes an operator or the end (language 4.12).
    [Theory]
    [InlineData("1 2", "2", "at position 3")]
    [InlineData("$A NOT $B", "NOT", "at position 4")]
    [InlineData("SIN(30) $A", "$", "at position 9")]
    public void Token_AfterCompleteExpression_IsErrorAtTheToken(string text, string token, string place)
    {
        Diagnostic diagnostic = SingleError(text, DiagnosticCodes.ExpressionUnexpectedToken);

        Assert.Equal(
            $"{token} {place} of {{{text}}} does not continue the expression: an operator or the end is expected "
            + "(language 4.12).",
            diagnostic.Message);
    }

    // cmpexpr = sum [ comparison sum ]: one comparison, no chain, also inside parentheses (language 4.12).
    [Theory]
    [InlineData("1 < $A < 3", "at position 8")]
    [InlineData("($A == 1 == $B)", "at position 10")]
    public void Comparison_Chained_IsErrorAtTheSecondComparison(string text, string place)
    {
        Diagnostic diagnostic = SingleError(text, DiagnosticCodes.ExpressionChainedComparison);

        Assert.Equal(
            $"The comparison {place} of {{{text}}} follows another comparison; comparisons do not chain, join two "
            + "of them with AND (language 4.12, cmpexpr).",
            diagnostic.Message);
    }

    // variable = "$" addr [ "[" expr "]" ]: the index ends with a bracket (language 4.12).
    [Fact]
    public void Bracket_NotClosed_IsErrorNamingTheOpeningBracket()
    {
        Diagnostic diagnostic = SingleError("$A[1", DiagnosticCodes.ExpressionUnbalancedBracket);

        Assert.Equal(
            "The bracket at position 3 of {$A[1} is not closed: ] is expected at the end (language 4.12, variable).",
            diagnostic.Message);
    }

    // A bracket that closes where none is open (language 4.12, variable).
    [Fact]
    public void Bracket_ClosedWithoutOpening_IsErrorAtTheBracket()
    {
        Diagnostic diagnostic = SingleError("$A]", DiagnosticCodes.ExpressionUnbalancedBracket);

        Assert.Equal(
            "The bracket at position 3 of {$A]} closes nothing (language 4.12, variable).",
            diagnostic.Message);
    }

    // Every diagnostic carries the file and the line of the block (D98, code-guidelines 6); the position inside the
    // expression is in the message, and an expression with an error gives no tree.
    [Fact]
    public void Error_InBlockOfLine17_IsReportedOnLine17WithoutTree()
    {
        var diagnostics = new Diagnostics("PATTERN_LOOP.ncx");

        ExprNode? tree = ExprParser.Parse("$Q1 +", 17, diagnostics);

        Assert.Null(tree);
        Diagnostic diagnostic = Assert.Single(diagnostics.Items);
        Assert.Equal("PATTERN_LOOP.ncx", diagnostic.File);
        Assert.Equal(17, diagnostic.Line);
        Assert.Null(diagnostic.OriginLine);
        Assert.Equal(
            "PATTERN_LOOP.ncx(17): ERROR PAR104: An operand is missing at the end of {$Q1 +}: a number, a $variable, "
            + "a function call or an expression in parentheses (language 4.12, primary).",
            diagnostic.ToText());
    }

    // The parser stops at the first problem of an expression, so one text between braces gives one ERROR.
    [Fact]
    public void Error_SeveralProblems_OnlyTheFirstIsReported()
    {
        Diagnostic diagnostic = SingleError("(FOO(1) + ", DiagnosticCodes.ExpressionUnknownFunction);

        Assert.StartsWith("FOO at position 2 of", diagnostic.Message, StringComparison.Ordinal);
    }

    // The diagnostics list of the file keeps what was reported before the expression.
    [Fact]
    public void Error_ListWithEarlierDiagnostic_AddsOneMore()
    {
        var diagnostics = new Diagnostics("test.ncx");
        diagnostics.Warning(3, "PAR001", "an earlier finding");

        ExprParser.Parse("1 +", 4, diagnostics);

        Assert.Equal(2, diagnostics.Items.Count);
        Assert.Equal(DiagnosticCodes.ExpressionMissingOperand, diagnostics.Items[1].Code);
    }

    // The expression parser takes its codes from PAR100-PAR149, the range of the header of DiagnosticCodes (D98).
    [Fact]
    public void DiagnosticCodes_ExpressionCodes_AreInTheRangePar100ToPar149()
    {
        int count = 0;
        foreach (FieldInfo field in typeof(DiagnosticCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!field.IsLiteral || !field.Name.StartsWith("Expression", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.Matches("^PAR1[0-4][0-9]$", (string)field.GetRawConstantValue()!);
            count++;
        }

        Assert.Equal(11, count);
    }

    // Parses a text that must fail with exactly one ERROR of the given code, and no tree.
    private static Diagnostic SingleError(string text, string code)
    {
        var diagnostics = new Diagnostics("test.ncx");

        ExprNode? tree = ExprParser.Parse(text, 1, diagnostics);

        Assert.Null(tree);
        Diagnostic diagnostic = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Error, diagnostic.Severity);
        Assert.Equal(code, diagnostic.Code);
        return diagnostic;
    }
}
