namespace Ncx.Readers;

/// <summary>
/// How a reader reads one file (code-guidelines 5, Options): passed explicitly, so a test builds the options it needs.
/// </summary>
public sealed record ReadOptions
{
    /// <summary>
    /// The reader rules of the plugins, asked in this order for what the configuration tables of the machine leave
    /// undecided (D40, D66, D106); empty by default.
    /// </summary>
    public IReadOnlyList<ISourceRule> Rules { get; init; } = [];
}
