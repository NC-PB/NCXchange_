namespace Ncx.Core.Writing;

/// <summary>
/// How the canonical writer writes a program (code-guidelines 5, Options).
/// </summary>
public sealed record WriterOptions
{
    /// <summary>
    /// Write the generated blocks that the expander inserted as well. False by default: ncx format never writes them
    /// (language 4.15, architecture 4.1).
    /// </summary>
    public bool IncludeGenerated { get; init; }
}
