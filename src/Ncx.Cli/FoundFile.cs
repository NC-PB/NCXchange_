namespace Ncx.Cli;

/// <summary>
/// A file that ncx found for the run, a machine file or a cycle catalog: where it is read, and how the diagnostics name
/// it (D98).
/// </summary>
internal sealed record FoundFile
{
    /// <summary>
    /// The full path the file is read from.
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    /// The file as the diagnostics name it: a path as the command line or ncx.toml gives it, a file found by name by
    /// its path from the working directory, machines/nakamura-ntjx.toml.
    /// </summary>
    public required string Name { get; init; }
}
