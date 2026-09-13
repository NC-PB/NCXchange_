namespace Ncx.Core.Machine;

/// <summary>
/// The first and the last wait mark a compiler may use, mark_range = [100, 199] (machine-config 5).
/// </summary>
/// <param name="First">The first mark, 100.</param>
/// <param name="Last">The last mark, 199.</param>
public sealed record MarkRange(int First, int Last);
