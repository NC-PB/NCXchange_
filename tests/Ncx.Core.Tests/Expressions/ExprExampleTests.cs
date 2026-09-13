using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.Expressions;

/// <summary>
/// The expressions of the specification: every one parses without a diagnostic and prints back identically (P0-05,
/// done when).
/// </summary>
public sealed class ExprExampleTests
{
    // The complete programs of language 6 are the .ncx files of docs/spec/examples.
    private const string ExampleExtension = ".ncx";

    // PATTERN_LOOP.ncx writes a Heidenhain programmer's loop with four expressions (language 6).
    [Fact]
    public void PatternLoop_EveryExpression_ParsesAndPrintsBackIdentically()
    {
        string[] expected = ["$Q1", "$Q1 + 20", "$Q3 + 1", "$Q3 < $Q2"];

        List<string> expressions = ExpressionTexts.InFile(Fixture.ReadText("PATTERN_LOOP.ncx"));

        Assert.Equal(expected, expressions);
        foreach (string expression in expressions)
        {
            AssertPrintsBackIdentically(expression);
        }
    }

    // Every complete program language 6 names: each expression in it parses and prints back identically.
    [Fact]
    public void ExampleFiles_EveryExpression_ParsesAndPrintsBackIdentically()
    {
        int files = 0;
        foreach (string example in Fixture.List())
        {
            if (!example.EndsWith(ExampleExtension, StringComparison.Ordinal))
            {
                continue;
            }

            files++;
            foreach (string expression in ExpressionTexts.InFile(Fixture.ReadText(example)))
            {
                AssertPrintsBackIdentically(expression);
            }
        }

        Assert.True(files >= 5, "Language 6 names five complete programs.");
    }

    // The expressions the language document writes in its text: language 3 (value types, expression), 4.9 (IF) and
    // 4.12 (system variables, D51).
    [Theory]
    [InlineData("$Q1 + 20")]
    [InlineData("$Q3 < $Q2")]
    [InlineData("$SYS_POS_X")]
    [InlineData("$SYS_MPOS_B")]
    [InlineData("$SYS_TOOL_LEN")]
    [InlineData("$SYS_WEAR_Z[99]")]
    [InlineData("$SYS_TOOL")]
    [InlineData("$SYS_PART_MAIN")]
    public void LanguageDocument_ExpressionInTheText_ParsesAndPrintsBackIdentically(string expression)
    {
        AssertPrintsBackIdentically(expression);
    }

    // The snippets of language 6, read from the document itself so that an expression added to them is covered.
    [Fact]
    public void LanguageSection6_EveryExpressionOfTheSnippets_ParsesAndPrintsBackIdentically()
    {
        string document = File.ReadAllText(
            Path.Combine(Fixture.RepositoryRoot(), "docs", "spec", "ncx-language.md"));

        List<string> codeLines = ExpressionTexts.Section6CodeLines(document);

        Assert.NotEmpty(codeLines);
        foreach (string line in codeLines)
        {
            foreach (string expression in ExpressionTexts.InLine(line))
            {
                AssertPrintsBackIdentically(expression);
            }
        }
    }

    // The cases of P0-05, written canonically, print back as written.
    [Theory]
    [InlineData("1 + 2 * 3")]
    [InlineData("-2 ^ 2")]
    [InlineData("2 ^ 3 ^ 2")]
    [InlineData("$Q1 < $Q2 AND NOT $Q3")]
    [InlineData("$SYS_WEAR_Z[99]")]
    [InlineData("MAX($A, 2) * SIN(30)")]
    public void TaskCases_WrittenCanonically_PrintBackIdentically(string expression)
    {
        AssertPrintsBackIdentically(expression);
    }

    // Parses without a diagnostic and writes the same text again.
    private static void AssertPrintsBackIdentically(string expression)
    {
        Assert.Equal(expression, ExprStructure.ParseValid(expression).ToString());
    }
}
