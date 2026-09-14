namespace Ncx.Compilers;

/// <summary>
/// One output file of a compile: its name and its text in the syntax of the controller (architecture 8).
/// </summary>
public sealed record CompiledFile
{
    /// <summary>
    /// The file name with the extension of the controller family and without a folder: "part-123.nc", "2.5D
    /// FRAESEN.h" (machine-config 10).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The text, block numbers and line endings as [format] writes them (machine-config 2).
    /// </summary>
    public required string Text { get; init; }
}
