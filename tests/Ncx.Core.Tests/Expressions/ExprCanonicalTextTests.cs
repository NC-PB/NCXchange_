using Ncx.Core.Expressions;

namespace Ncx.Core.Tests.Expressions;

/// <summary>
/// The canonical text of an expression, what ToString and ToCanonical write between the braces (P0-05 in
/// implementation 10-phase-0-foundations): one space around binary operators, AND, OR and MOD included; the unary
/// minus directly before its operand; ", " between arguments; no spaces inside parentheses and brackets; numbers as
/// written.
/// </summary>
public sealed class ExprCanonicalTextTests
{
    // One space on each side of every binary operator, the words AND, OR and MOD included.
    [Theory]
    [InlineData("$A+1", "$A + 1")]
    [InlineData("$A*2-1/$B^2", "$A * 2 - 1 / $B ^ 2")]
    [InlineData("7   MOD   3", "7 MOD 3")]
    [InlineData("$A<$B AND $C>=1 OR $D!=0", "$A < $B AND $C >= 1 OR $D != 0")]
    [InlineData("$A==$B", "$A == $B")]
    [InlineData("$A<=$B", "$A <= $B")]
    [InlineData("$A>$B", "$A > $B")]
    public void BinaryOperator_AnySpacing_OneSpaceOnEachSide(string text, string canonical)
    {
        Assert.Equal(canonical, ExprStructure.ParseValid(text).ToString());
    }

    // The unary minus stands directly before its operand.
    [Theory]
    [InlineData("- $A", "-$A")]
    [InlineData("-  (1 + 2)", "-(1 + 2)")]
    [InlineData("2 ^ - 3", "2 ^ -3")]
    [InlineData("2 -  - 3", "2 - -3")]
    public void UnaryMinus_AnySpacing_NoSpaceBeforeTheOperand(string text, string canonical)
    {
        Assert.Equal(canonical, ExprStructure.ParseValid(text).ToString());
    }

    // NOT is followed by one space (see TODO(question) in UnaryNode): written without one, NOT and a function name
    // would read back as one name.
    [Theory]
    [InlineData("NOT   $Q3", "NOT $Q3")]
    [InlineData("NOT(1)", "NOT (1)")]
    [InlineData("NOT SIN($A) > 0", "NOT SIN($A) > 0")]
    public void Not_AnySpacing_OneSpaceBeforeTheOperand(string text, string canonical)
    {
        Assert.Equal(canonical, ExprStructure.ParseValid(text).ToString());
    }

    // ", " between arguments, no space between a function name and its parenthesis, no spaces inside parentheses and
    // brackets.
    [Theory]
    [InlineData("MAX($A,2)", "MAX($A, 2)")]
    [InlineData("MAX( $A , 2 )", "MAX($A, 2)")]
    [InlineData("SIN (30)", "SIN(30)")]
    [InlineData("( $A + 1 ) * 2", "($A + 1) * 2")]
    [InlineData("$SYS_WEAR_Z[ 99 ]", "$SYS_WEAR_Z[99]")]
    [InlineData("$ Q1", "$Q1")]
    public void Delimiters_AnySpacing_WrittenWithTheCanonicalSpacing(string text, string canonical)
    {
        Assert.Equal(canonical, ExprStructure.ParseValid(text).ToString());
    }

    // Numbers are written as they were read, never rounded or reformatted (language 2 rule 5).
    [Theory]
    [InlineData("007.50")]
    [InlineData("0.05")]
    [InlineData("1592")]
    public void Number_AsWritten_KeepsItsDigits(string number)
    {
        Assert.Equal(number, ExprStructure.ParseValid(number).ToString());
    }

    // Parentheses are kept as written, also where the grammar would not need them (see TODO(question) in
    // ParenthesesNode).
    [Theory]
    [InlineData("((1))")]
    [InlineData("($Q1 < $Q2) AND ($Q3 > 0)")]
    [InlineData("1 + (2 * 3)")]
    public void Parentheses_AsWritten_AreKept(string canonical)
    {
        Assert.Equal(canonical, ExprStructure.ParseValid(canonical).ToString());
    }

    // The canonical text reads back into the same tree and writes the same text again.
    [Theory]
    [InlineData("-2 ^ 2")]
    [InlineData("2 ^ -3 ^ 2")]
    [InlineData("2 - -3")]
    [InlineData("$Q1 < $Q2 AND NOT $Q3")]
    [InlineData("NOT -$A == -1 OR $B")]
    [InlineData("$SYS_WEAR_Z[$Q1 * 2 + 1] MOD 3")]
    [InlineData("ATAN2(-$Y, ABS($X - 1)) / (2 ^ 0.5)")]
    [InlineData("MIN($A, $B, 3) >= -(1 - $C)")]
    public void CanonicalText_ParsedAgain_GivesTheSameTreeAndText(string canonical)
    {
        ExprNode tree = ExprStructure.ParseValid(canonical);
        ExprNode again = ExprStructure.ParseValid(tree.ToString());

        Assert.Equal(canonical, tree.ToString());
        Assert.Equal(tree, again);
    }

    // ToString is the canonical text, so a tree reads as NCX in test output and in the debugger.
    [Fact]
    public void ToString_AnyTree_IsTheCanonicalText()
    {
        ExprNode tree = ExprStructure.ParseValid("MAX($A,2)*SIN(30)");

        Assert.Equal(tree.ToCanonical(), tree.ToString());
    }

    // A tree built in code, as a reader may build one, writes the same canonical text.
    [Fact]
    public void ToCanonical_TreeBuiltInCode_WritesTheCanonicalText()
    {
        ExprNode tree = new BinaryNode(
            BinaryOperator.Less,
            new VariableNode("Q3", null),
            new CallNode(ExprFunction.Max, [new VariableNode("Q2", null), new NumberNode("1")]));

        Assert.Equal("$Q3 < MAX($Q2, 1)", tree.ToCanonical());
    }

    // Two calls with equal arguments are equal trees, as two equal lists of words are equal values (ListValue).
    [Fact]
    public void CallNode_EqualArguments_IsEqual()
    {
        var first = new CallNode(ExprFunction.Min, [new NumberNode("1"), new VariableNode("A", null)]);
        var second = new CallNode(ExprFunction.Min, [new NumberNode("1"), new VariableNode("A", null)]);
        var third = new CallNode(ExprFunction.Min, [new NumberNode("1")]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, third);
    }
}
