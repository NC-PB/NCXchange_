namespace Ncx.Core.Machine;

/// <summary>
/// [raw]: the builder codes the reader keeps as RAW with the builder's name, RAW:NAKAMURA (machine-config 5, F23).
/// </summary>
public sealed record RawTable
{
    /// <summary>
    /// known: the native codes, "G411", "G300", as the file writes them.
    /// </summary>
    public IReadOnlyList<string> Known { get; init; } = [];
}
