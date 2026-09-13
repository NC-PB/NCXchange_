namespace Ncx.Readers;

/// <summary>
/// One controller file as a reader gets it: its name, which every diagnostic about it carries, and its text
/// (architecture 7, D98).
/// </summary>
/// <param name="Name">The name of the file, "2.5D_FRAESEN.fanuc.nc".</param>
/// <param name="Text">The whole text, line endings as in the file.</param>
public sealed record SourceFile(string Name, string Text);
