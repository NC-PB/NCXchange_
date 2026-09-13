namespace Ncx.Core.Parsing;

/// <summary>
/// How the parser reads a text (code-guidelines 5, Options): as a user file, the default, or as the text of generated
/// blocks, which the expander parses with the pseudo-words of D95.
/// </summary>
public sealed record ParserOptions
{
    /// <summary>
    /// Accept the pseudo-words @SAVE and @RESTORE with a state key KEY[:ADDR] as their value. The expander sets it for
    /// the text of generated blocks; without it a word starting with @ is the ERROR "pseudo-word in a user file"
    /// (language 3, KEY; architecture 4; D95). False by default.
    /// </summary>
    public bool AllowPseudoWords { get; init; }

    /// <summary>
    /// Keep the line every block was read from in Block.SourceText. True by default; without it only a block with an
    /// ERROR keeps its source text, as architecture 4.1 keeps the block of an unknown key.
    /// </summary>
    public bool KeepSourceText { get; init; } = true;
}
