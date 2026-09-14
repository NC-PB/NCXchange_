namespace Ncx.Cli.Commands;

/// <summary>
/// What a run of dotnet of the .NET SDK gave: its exit code and what it wrote, or why it could not be started
/// (implementation 17, P7-02).
/// </summary>
internal sealed record DotnetResult
{
    /// <summary>
    /// The exit code of dotnet; null when it could not be started.
    /// </summary>
    public int? ExitCode { get; init; }

    /// <summary>
    /// What dotnet wrote to its standard output and its standard error, line by line in the order it came.
    /// </summary>
    public string Output { get; init; } = "";

    /// <summary>
    /// Why dotnet could not be started; null when it ran.
    /// </summary>
    public string? Failure { get; init; }
}
