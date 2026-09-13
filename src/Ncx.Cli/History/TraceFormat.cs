namespace Ncx.Cli.History;

/// <summary>
/// How ncx trace writes its table: plain text in aligned columns or as comma-separated values (virtual machine 8).
/// </summary>
internal enum TraceFormat
{
    /// <summary>
    /// Aligned columns, two spaces apart.
    /// </summary>
    Text,

    /// <summary>
    /// Comma-separated values.
    /// </summary>
    Csv,
}
