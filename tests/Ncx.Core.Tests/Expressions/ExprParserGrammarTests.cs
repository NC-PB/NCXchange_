using Ncx.Core.Expressions;

namespace Ncx.Core.Tests.Expressions;

/// <summary>
/// The grammar of language 4.12, production by production: the tree each expression becomes, written with every
/// operator node in parentheses (ExprStructure).
/// </summary>
public sealed class ExprParserGrammarTests
{
    // orexpr = andexpr { "OR" andexpr }: OR binds weakest, and ORs in a row group from the left (language 4.12).
    [Fact]
    public void OrExpr_ThreeOperands_GroupFromTheLeft()
    {
        Assert.Equal("(($A OR $B) OR $C)", ExprStructure.Parse("$A OR $B OR $C"));
    }

    // andexpr = notexpr { "AND" notexpr }: AND binds tighter than OR (language 4.12).
    [Fact]
    public void AndExpr_BetweenOrOperands_BindsTighterThanOr()
    {
        Assert.Equal("($A OR ($B AND $C))", ExprStructure.Parse("$A OR $B AND $C"));
    }

    // notexpr = [ "NOT" ] cmpexpr: NOT negates the whole comparison behind it (language 4.12).
    [Fact]
    public void NotExpr_BeforeComparison_NegatesTheWholeComparison()
    {
        Assert.Equal("(NOT ($A < $B))", ExprStructure.Parse("NOT $A < $B"));
    }

    // The precedence case of P0-05: the comparison is the left operand of AND, and NOT negates only the right one
    // (language 4.12, andexpr and notexpr).
    [Fact]
    public void NotExpr_AfterAnd_NegatesOnlyTheRightOperandOfAnd()
    {
        Assert.Equal("(($Q1 < $Q2) AND (NOT $Q3))", ExprStructure.Parse("$Q1 < $Q2 AND NOT $Q3"));
    }

    // cmpexpr = sum [ ( "==" | "!=" | "<" | "<=" | ">" | ">=" ) sum ]: a comparison compares two sums
    // (language 4.12).
    [Theory]
    [InlineData("==")]
    [InlineData("!=")]
    [InlineData("<")]
    [InlineData("<=")]
    [InlineData(">")]
    [InlineData(">=")]
    public void CmpExpr_EachComparison_ComparesTwoSums(string comparison)
    {
        Assert.Equal($"(($A + 1) {comparison} ($B - 1))", ExprStructure.Parse($"$A + 1 {comparison} $B - 1"));
    }

    // sum = product { ( "+" | "-" ) product }: plus and minus group from the left, so 1 - 2 + 3 is (1 - 2) + 3
    // (language 4.12).
    [Fact]
    public void Sum_MinusThenPlus_GroupsFromTheLeft()
    {
        Assert.Equal("((1 - 2) + 3)", ExprStructure.Parse("1 - 2 + 3"));
    }

    // The precedence case of P0-05: 1 + 2 * 3 is the sum of 1 and the product (language 4.12, sum and product).
    [Fact]
    public void Product_InsideSum_IsTheRightOperandOfThePlus()
    {
        Assert.Equal("(1 + (2 * 3))", ExprStructure.Parse("1 + 2 * 3"));
    }

    // product = unary { ( "*" | "/" | "MOD" ) unary }: the three group from the left (language 4.12).
    [Fact]
    public void Product_ModThenDivideThenTimes_GroupFromTheLeft()
    {
        Assert.Equal("(((7 MOD 3) / 2) * $A)", ExprStructure.Parse("7 MOD 3 / 2 * $A"));
    }

    // The precedence case of P0-05: unary = [ "-" ] power, so -2 ^ 2 is the unary minus of the power, not the square
    // of -2 (language 4.12).
    [Fact]
    public void Unary_MinusBeforePower_NegatesThePower()
    {
        Assert.Equal("(-(2 ^ 2))", ExprStructure.Parse("-2 ^ 2"));
    }

    // The unary is the operand of a product, so -2 * 3 is (-2) * 3 (language 4.12, product).
    [Fact]
    public void Unary_MinusBeforeProduct_NegatesOnlyTheFirstOperand()
    {
        Assert.Equal("((-2) * 3)", ExprStructure.Parse("-2 * 3"));
    }

    // Each product of a sum starts with its own optional minus: 2 - -3 (language 4.12, sum and unary).
    [Fact]
    public void Unary_AfterBinaryMinus_IsTheRightOperand()
    {
        Assert.Equal("(2 - (-3))", ExprStructure.Parse("2 - -3"));
    }

    // The precedence case of P0-05: power = primary [ "^" unary ], the exponent is a unary that holds the next power,
    // so ^ groups from the right (language 4.12).
    [Fact]
    public void Power_TwoCarets_GroupFromTheRight()
    {
        Assert.Equal("(2 ^ (3 ^ 2))", ExprStructure.Parse("2 ^ 3 ^ 2"));
    }

    // The exponent is a unary, so it may carry its own minus (language 4.12, power).
    [Fact]
    public void Power_NegativeExponent_IsTheUnaryMinusOfTheExponent()
    {
        Assert.Equal("(2 ^ (-3))", ExprStructure.Parse("2 ^ -3"));
    }

    // primary = number: an integer or a decimal of language 3, kept as written (language 2 rule 5).
    [Theory]
    [InlineData("20")]
    [InlineData("007")]
    [InlineData("0.05")]
    [InlineData("10.50")]
    public void Primary_Number_KeepsTheTextAsWritten(string number)
    {
        NumberNode node = Assert.IsType<NumberNode>(ExprStructure.ParseValid(number));

        Assert.Equal(number, node.Text);
    }

    // variable = "$" addr [ "[" expr "]" ]: the name after the dollar sign (language 4.9, 4.12).
    [Fact]
    public void Variable_WithoutIndex_HasItsNameAndNoIndex()
    {
        VariableNode variable = Assert.IsType<VariableNode>(ExprStructure.ParseValid("$Q1"));

        Assert.Equal("Q1", variable.Name);
        Assert.Null(variable.Index);
    }

    // The case of P0-05: a system variable with an index selects a register, here wear register 99 (language 4.12,
    // D51).
    [Fact]
    public void Variable_SystemVariableWithIndex_HasTheIndex()
    {
        VariableNode variable = Assert.IsType<VariableNode>(ExprStructure.ParseValid("$SYS_WEAR_Z[99]"));

        Assert.Equal("SYS_WEAR_Z", variable.Name);
        Assert.Equal(new NumberNode("99"), variable.Index);
    }

    // The index may itself be an expression (language 4.12, system variables).
    [Fact]
    public void Variable_IndexExpression_IsAWholeExpression()
    {
        Assert.Equal("$SYS_WEAR_Z[($Q1 + 1)]", ExprStructure.Parse("$SYS_WEAR_Z[$Q1 + 1]"));
    }

    // The precedence case of P0-05: primary = function "(" expr { "," expr } ")", and two calls are the operands of
    // the product (language 4.12).
    [Fact]
    public void Function_CallsInProduct_AreTheOperandsOfTheProduct()
    {
        Assert.Equal("(MAX($A, 2) * SIN(30))", ExprStructure.Parse("MAX($A, 2) * SIN(30)"));
    }

    // Every argument is a whole expr (language 4.12, primary).
    [Fact]
    public void Function_ArgumentsWithOperators_EachArgumentIsItsOwnTree()
    {
        Assert.Equal("ATAN2(($Y - 1), ($X * 2))", ExprStructure.Parse("ATAN2($Y - 1, $X * 2)"));
    }

    // function = "SIN" | "COS" | ... | "MAX": each of the seventeen names of the grammar is its function
    // (language 4.12).
    [Theory]
    [InlineData("SIN", ExprFunction.Sin)]
    [InlineData("COS", ExprFunction.Cos)]
    [InlineData("TAN", ExprFunction.Tan)]
    [InlineData("ASIN", ExprFunction.Asin)]
    [InlineData("ACOS", ExprFunction.Acos)]
    [InlineData("ATAN", ExprFunction.Atan)]
    [InlineData("ATAN2", ExprFunction.Atan2)]
    [InlineData("SQRT", ExprFunction.Sqrt)]
    [InlineData("ABS", ExprFunction.Abs)]
    [InlineData("INT", ExprFunction.Int)]
    [InlineData("FRAC", ExprFunction.Frac)]
    [InlineData("ROUND", ExprFunction.Round)]
    [InlineData("SGN", ExprFunction.Sgn)]
    [InlineData("LN", ExprFunction.Ln)]
    [InlineData("EXP", ExprFunction.Exp)]
    [InlineData("MIN", ExprFunction.Min)]
    [InlineData("MAX", ExprFunction.Max)]
    public void Function_EachNameOfTheGrammar_IsItsFunction(string name, ExprFunction function)
    {
        CallNode call = Assert.IsType<CallNode>(ExprStructure.ParseValid(name + "($A)"));

        Assert.Equal(function, call.Function);
        Assert.Equal(name, ExprSymbols.Of(function));
    }

    // The function names are exactly the seventeen of the grammar (language 4.12, function).
    [Fact]
    public void ExprFunction_Members_AreTheSeventeenNamesOfTheGrammar()
    {
        Assert.Equal(17, Enum.GetValues<ExprFunction>().Length);
    }

    // primary = "(" expr ")": parentheses make a sum the operand of a product, and the tree keeps them as written
    // (language 4.12).
    [Fact]
    public void Parentheses_AroundSum_MakeTheSumTheOperandOfTheProduct()
    {
        Assert.Equal("(paren((1 + 2)) * 3)", ExprStructure.Parse("(1 + 2) * 3"));
    }

    // Whitespace inside {} is free: the same tree with any spacing, or none, between the parts of the grammar
    // (language 4.12).
    [Theory]
    [InlineData("$Q1+20")]
    [InlineData("  $Q1   +   20  ")]
    [InlineData("\t$Q1\t+\t20")]
    [InlineData("$ Q1 + 20")]
    public void Whitespace_AnySpacing_GivesTheSameTree(string text)
    {
        Assert.Equal("($Q1 + 20)", ExprStructure.Parse(text));
    }

    // Keys, addresses and identifiers are uppercase, and a parser may accept lowercase and normalize (language 3,
    // Case); a variable name is an address (language 4.9).
    [Fact]
    public void Case_LowercaseVariableNames_AreNormalizedToUppercase()
    {
        Assert.Equal("($Q1 + $SYS_WEAR_Z[99])", ExprStructure.Parse("$q1 + $sys_wear_z[99]"));
    }

    // Function names and the words AND, OR, NOT and MOD are read like the rest of the language, lowercase
    // normalized (see TODO(question) in ExprTokenizer).
    [Fact]
    public void Case_LowercaseFunctionsAndOperatorWords_AreNormalizedToUppercase()
    {
        Assert.Equal("((SIN($A) MOD 2) AND (NOT $B))", ExprStructure.Parse("sin($a) mod 2 and not $b"));
    }
}
