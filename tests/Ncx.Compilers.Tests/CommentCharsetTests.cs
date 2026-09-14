using Ncx.Core.Model;

namespace Ncx.Compilers.Tests;

/// <summary>
/// comment_charset of [format]: the umlauts transliterated before writing (machine-config 2; controllers fanuc.md 10
/// rule 5).
/// </summary>
public sealed class CommentCharsetTests
{
    private static readonly Block s_block = new() { Line = 9, Words = [] };

    // Machine-config 2: under ASCII the umlauts are transliterated.
    [Theory]
    [InlineData("Änderung über Maß", "Aenderung ueber Mass")]
    [InlineData("ÄNDERUNG", "AENDERUNG")]
    [InlineData("MAß UND GRÖSSE", "MAss UND GROESSE")]
    [InlineData("NUT Ö", "NUT Oe")]
    [InlineData("SCHLÜSSEL SCHLÜ", "SCHLUESSEL SCHLUE")]
    public void Transliterate_Ascii_TransliteratesTheUmlauts(string text, string written)
    {
        var diagnostics = new Diagnostics("T.ncx");

        Assert.Equal(written, CommentCharset.Transliterate(text, CommentCharset.Ascii, s_block, diagnostics));
        Assert.Empty(diagnostics.Items);
    }

    // Machine-config 2 and the TODO(question) of CommentCharset: a character outside ASCII that is no umlaut becomes ?
    // with a WARNING that names it.
    [Fact]
    public void Transliterate_CharacterWithoutTransliteration_IsTheWarningCmp022()
    {
        var diagnostics = new Diagnostics("T.ncx");

        string written = CommentCharset.Transliterate("Ø10 90°", CommentCharset.Ascii, s_block, diagnostics);

        Assert.Equal("?10 90?", written);
        Diagnostic warning = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.CommentCharacterOutsideCharset, warning.Code);
        Assert.Contains("Ø, °", warning.Message, StringComparison.Ordinal);
    }

    // The TODO(question) of CommentCharset: without comment_charset the comment is written as it is.
    [Fact]
    public void Transliterate_WithoutCharset_WritesTheTextAsItIs()
    {
        var diagnostics = new Diagnostics("T.ncx");

        Assert.Equal("Änderung Ø", CommentCharset.Transliterate("Änderung Ø", null, s_block, diagnostics));
        Assert.Empty(diagnostics.Items);
    }
}
