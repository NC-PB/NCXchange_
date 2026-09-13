namespace Ncx.Core.Model;

/// <summary>
/// A comment-only or blank line. It is not a block: it is kept in place and written back as read, and it may stand
/// on any line, also before FILE=BEGIN and after FILE=END (language 3, Block and Comment; D92).
/// </summary>
/// <param name="Line">The 1-based line number in the file.</param>
/// <param name="Text">The line as read, without its line ending; empty or whitespace for a blank line.</param>
public sealed record Trivia(int Line, string Text);
