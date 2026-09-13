using Ncx.Core.Model;
using Ncx.Core.Parsing;

namespace Ncx.Core.Tests.Parsing;

/// <summary>
/// The pseudo-words @SAVE and @RESTORE of generated blocks are lexed only under the option the expander uses for
/// generated text, with a state key KEY[:ADDR] as their value; in a user file a word starting with @ is the ERROR
/// "pseudo-word in a user file" (language 3, KEY; 4.15; virtual machine 3.10; D95).
/// </summary>
public sealed class PseudoWordTests
{
    // In a user file @SAVE=SPINDLE:MAIN is the ERROR "pseudo-word in a user file", not an unknown key (D95).
    [Fact]
    public void D95_SaveWithoutTheOption_IsPseudoWordInAUserFile()
    {
        var diagnostics = new Diagnostics("test.ncx");
        Block? block = Parser.ParseBlock("@SAVE=SPINDLE:MAIN", 1, new ParserOptions(), diagnostics);

        Diagnostic diagnostic = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.PseudoWordInUserFile, diagnostic.Code);
        Assert.Equal(Severity.Error, diagnostic.Severity);
        Assert.Contains("pseudo-word in a user file", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal("@SAVE=SPINDLE:MAIN", block?.SourceText);
    }

    // Any word starting with @ in a user file is that ERROR (language 3, KEY; D95).
    [Theory]
    [InlineData("@RESTORE=SPINDLE:MAIN")]
    [InlineData("@FOO=1")]
    [InlineData("@")]
    public void D95_WordStartingWithAtInAUserFile_IsPseudoWordInAUserFile(string text)
    {
        var diagnostics = new Diagnostics("test.ncx");
        Parser.ParseBlock(text, 1, new ParserOptions(), diagnostics);

        Assert.Equal(DiagnosticCodes.PseudoWordInUserFile, Assert.Single(diagnostics.Items).Code);
    }

    // Under the option the value of @SAVE is a state key, KEY:ADDR (D95).
    [Fact]
    public void D95_SaveWithTheOption_HasAStateKey()
    {
        var diagnostics = new Diagnostics("generated");
        Block? block = Parser.ParseBlock(
            "@SAVE=spindle:main", 1, new ParserOptions { AllowPseudoWords = true }, diagnostics);

        Assert.Empty(diagnostics.Items);
        Word word = Assert.Single(block!.Words);
        Assert.Equal("@SAVE", word.Key);
        Assert.Equal(new StateKeyValue("SPINDLE", "MAIN"), word.Value);
        Assert.True(word.Definition?.IsInternal);
    }

    // A state key without an address names the variable of a key alone: COOLANT, F (virtual machine 3.10, D95).
    [Fact]
    public void D95_RestoreWithTheOption_TakesAKeyWithoutAddress()
    {
        var diagnostics = new Diagnostics("generated");
        Block? block = Parser.ParseBlock(
            "@RESTORE=COOLANT", 1, new ParserOptions { AllowPseudoWords = true }, diagnostics);

        Assert.Empty(diagnostics.Items);
        Assert.Equal(new StateKeyValue("COOLANT", null), Assert.Single(block!.Words).Value);
    }

    // Under the option the value of a pseudo-word is a state key and nothing else (D95).
    [Theory]
    [InlineData("@SAVE=5")]
    [InlineData("@SAVE=\"SPINDLE\"")]
    [InlineData("@SAVE=SPINDLE:")]
    public void D95_PseudoWordWithAnotherValue_IsMalformedValue(string text)
    {
        var diagnostics = new Diagnostics("generated");
        Parser.ParseBlock(text, 1, new ParserOptions { AllowPseudoWords = true }, diagnostics);

        Assert.Equal(DiagnosticCodes.MalformedValue, Assert.Single(diagnostics.Items).Code);
    }

    // A state key is the value of the pseudo-words only: in a user word KEY:ADDR is no value at all (language 3,
    // value types; D95).
    [Fact]
    public void D95_StateKeyOnAUserWord_IsMalformedValue()
    {
        var diagnostics = new Diagnostics("generated");
        Parser.ParseBlock("SPINDLE=SPINDLE:MAIN", 1, new ParserOptions { AllowPseudoWords = true }, diagnostics);

        Assert.Equal(DiagnosticCodes.MalformedValue, Assert.Single(diagnostics.Items).Code);
    }
}
