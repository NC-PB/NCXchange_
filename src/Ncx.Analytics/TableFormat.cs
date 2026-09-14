namespace Ncx.Analytics;

/// <summary>
/// How a report writes its tables: plain text in aligned columns or comma-separated values (virtual machine 8).
/// </summary>
public enum TableFormat
{
    /// <summary>
    /// Aligned columns, two spaces apart, the default.
    /// </summary>
    Text,

    /// <summary>
    /// Comma-separated values.
    /// </summary>
    Csv,
}
