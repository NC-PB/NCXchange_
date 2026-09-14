using Ncx.Analytics;

namespace Ncx.Cli.Commands;

/// <summary>
/// What the command line asks of ncx analyze beyond the run: the analytics of --analytic, the block range of --from
/// and --to (D67) and the table format of --format (virtual machine 8).
/// </summary>
internal sealed record AnalyzeSettings
{
    /// <summary>
    /// The names of the analytics to run, in the order their reports are written.
    /// </summary>
    public required IReadOnlyList<string> Analytics { get; init; }

    /// <summary>
    /// --from and --to: NCX line numbers of the file (virtual machine 8, D67); the whole file by default.
    /// </summary>
    public BlockRange Range { get; init; } = BlockRange.Whole;

    /// <summary>
    /// --format: aligned text, the default, or CSV (virtual machine 8).
    /// </summary>
    public TableFormat Format { get; init; } = TableFormat.Text;
}
