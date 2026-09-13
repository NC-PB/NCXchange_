using System.Text;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.Writing;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Examples;

/// <summary>
/// The examples of the specification format to themselves byte for byte. They were rewritten into the rank order and
/// the comment of PATTERN_LOOP.ncx line 15 was realigned when D90 and D92 were applied, so ncx format produces an empty
/// diff over docs/spec/examples/*.ncx, and a difference is a bug of the writer, not of the example (phase 0, P0-06).
/// </summary>
public sealed class ExampleFormatTests
{
    /// <summary>
    /// Every .ncx example of docs/spec/examples, so that an example added later is covered as well.
    /// </summary>
    public static TheoryData<string> Examples()
    {
        return [.. ExampleNames()];
    }

    // The five complete programs that language 6 names are among the examples formatted here.
    [Fact]
    public void Examples_TheFiveProgramsOfLanguage6_AreFormatted()
    {
        Assert.Equal(
            ["2.5D_FRAESEN.ncx", "INCREMENTAL_SUB.ncx", "MILLTURN_TRANSFER.ncx", "PATTERN_LOOP.ncx", "POLAR_FACE.ncx"],
            ExampleNames());
    }

    // The example parses without a diagnostic and the canonical writer gives it back byte for byte, line endings, the
    // comment column and the trivia included (language 2 rule 7, 5 rules 6 and 7; D90, D91, D92, D93).
    [Theory]
    [MemberData(nameof(Examples))]
    public void Format_Example_ReproducesItByteForByte(string example)
    {
        string text = Fixture.ReadText(example);
        NcxProgram program = Parser.Parse(text, example, new ParserOptions());

        string written = NcxWriter.Write(program);

        Assert.True(program.Diagnostics.Items.Count == 0, program.Diagnostics.ToText());
        Assert.Equal(text, written);
        Assert.Equal(Fixture.ReadBytes(example), Encoding.UTF8.GetBytes(written));
    }

    // The .ncx files directly in docs/spec/examples; the sources and machines folders hold no NCX.
    private static List<string> ExampleNames()
    {
        var names = new List<string>();
        foreach (string example in Fixture.List())
        {
            if (example.EndsWith(".ncx", StringComparison.Ordinal) && !example.Contains('/', StringComparison.Ordinal))
            {
                names.Add(example);
            }
        }

        return names;
    }
}
