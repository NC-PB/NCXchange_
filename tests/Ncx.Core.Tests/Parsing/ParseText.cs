using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Core.Tests.Parsing;

/// <summary>
/// Parses NCX text the way the tests of this folder need it: a whole file as a user file, or one line as a block, and
/// the codes of what the parser reported.
/// </summary>
internal static class ParseText
{
    /// <summary>
    /// Parses a whole file as a user file, without pseudo-words (D95).
    /// </summary>
    public static NcxProgram File(string text)
    {
        return Parser.Parse(text, "test.ncx", new ParserOptions());
    }

    /// <summary>
    /// Parses one line as the block of line 1 of a user file; null for trivia.
    /// </summary>
    public static Block? Block(string text, Diagnostics diagnostics)
    {
        return Parser.ParseBlock(text, 1, new ParserOptions(), diagnostics);
    }

    /// <summary>
    /// Parses one line that must be a block and must give no diagnostic.
    /// </summary>
    public static Block CleanBlock(string text)
    {
        var diagnostics = new Diagnostics("test.ncx");
        Block? block = Block(text, diagnostics);
        Assert.True(diagnostics.Items.Count == 0, diagnostics.ToText());
        Assert.NotNull(block);
        return block;
    }

    /// <summary>
    /// The codes of the diagnostics in the order they were reported.
    /// </summary>
    public static List<string> Codes(Diagnostics diagnostics)
    {
        var codes = new List<string>();
        foreach (Diagnostic diagnostic in diagnostics.Items)
        {
            codes.Add(diagnostic.Code);
        }

        return codes;
    }

    /// <summary>
    /// The one diagnostic with this code; the test fails with the whole list when there is none or more than one.
    /// </summary>
    public static Diagnostic Single(Diagnostics diagnostics, string code)
    {
        var found = new List<Diagnostic>();
        foreach (Diagnostic diagnostic in diagnostics.Items)
        {
            if (diagnostic.Code == code)
            {
                found.Add(diagnostic);
            }
        }

        Assert.True(found.Count == 1, $"Expected one {code}, got:\n{diagnostics.ToText()}");
        return found[0];
    }
}
