namespace Ncx.Readers;

/// <summary>
/// Cuts the text of a controller file into source blocks, one per line of the controller's syntax (architecture 7):
/// G01 X10. Z-5. is not L X+10 Z-5 F200 is not G1 X=10 Z=-5. One tokenizer per controller family.
/// </summary>
public interface ISourceTokenizer
{
    /// <summary>
    /// Cuts a whole file into blocks. Every line of the file is one block in file order, a blank or comment-only line
    /// a block without words, so that the reader keeps it in its place as trivia (D92); a block that the controller
    /// continues over several lines is one block with its continuation lines.
    /// </summary>
    /// <param name="text">The whole text, line endings as in the file.</param>
    IEnumerable<SourceBlock> Tokenize(string text);
}
