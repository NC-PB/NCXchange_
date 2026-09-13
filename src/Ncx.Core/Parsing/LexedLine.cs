namespace Ncx.Core.Parsing;

/// <summary>
/// One line as the lexer reads it: trivia when it holds no word, otherwise the words of a block and its trailing
/// comment (language 3, Block, Comment; D92).
/// </summary>
/// <param name="Line">The 1-based line number in the file.</param>
/// <param name="Text">The line as read, without its line ending.</param>
/// <param name="Words">The words the lexer could read, in their order; a malformed word is reported and left
/// out.</param>
/// <param name="Comment">The comment from the first semicolon outside strings and expressions to the end of the line,
/// the semicolon included, as read; null when the line has none.</param>
/// <param name="IsTrivia">True for a blank, whitespace-only or comment-only line, which is no block (D92).</param>
internal sealed record LexedLine(int Line, string Text, IReadOnlyList<LexedWord> Words, string? Comment, bool IsTrivia);
