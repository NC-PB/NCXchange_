using System.Text;

namespace Ncx.Analytics;

/// <summary>
/// One table of a report: a header row of column names and one row per entry, written as plain text in aligned columns
/// or as comma-separated values (virtual machine 8: "All output is plain text (CSV or aligned columns)").
/// </summary>
// TODO(question): virtual machine 8 says "CSV or aligned columns" and nothing more, the same gap as for the table of
// ncx trace (wave-2 question #40): whether a table has a header row, which format is the default, how a CSV field is
// quoted, and, for a report of several tables with lines of text between them, how CSV writes those lines. As trace
// does until that is answered: a header row of the column names first, aligned text the default, a CSV field with a
// comma, a quote or a line break in quotes with its quotes doubled (RFC 4180), every line ended with LF; a line of
// text of a report is written as it is, in CSV as a row of one field.
public sealed class TextTable
{
    // Aligned columns stand two spaces apart.
    private const string ColumnGap = "  ";

    private readonly string[] _columns;
    private readonly List<string[]> _rows = [];

    /// <summary>
    /// A table with its column names.
    /// </summary>
    /// <param name="columns">The column names, the header row.</param>
    public TextTable(params string[] columns)
    {
        _columns = columns;
    }

    /// <summary>
    /// The rows added so far, without the header row.
    /// </summary>
    public int RowCount => _rows.Count;

    /// <summary>
    /// Adds a row, one cell per column.
    /// </summary>
    /// <param name="cells">The cells in the order of the columns.</param>
    public void Add(params string[] cells)
    {
        // A row with another number of cells is a mistake of the analytic that writes it (code-guidelines 6).
        if (cells.Length != _columns.Length)
        {
            throw new ArgumentException(
                $"A row of {cells.Length} cells does not fit a table of {_columns.Length} columns.", nameof(cells));
        }

        _rows.Add(cells);
    }

    /// <summary>
    /// Writes the table, the header row first, every line ended with LF.
    /// </summary>
    /// <param name="format">Aligned text or CSV.</param>
    public string Write(TableFormat format)
    {
        var table = new List<string[]> { _columns };
        table.AddRange(_rows);
        return format == TableFormat.Csv ? Csv(table) : Aligned(table);
    }

    /// <summary>
    /// A line of text of a report, a title or a note, ended with LF: as it is in aligned text, a row of one field in
    /// CSV.
    /// </summary>
    /// <param name="text">The line without its line ending.</param>
    /// <param name="format">Aligned text or CSV.</param>
    public static string Line(string text, TableFormat format)
    {
        return (format == TableFormat.Csv ? CsvField(text) : text) + "\n";
    }

    // Comma-separated values, one line per row.
    private static string Csv(List<string[]> table)
    {
        var text = new StringBuilder();
        foreach (string[] cells in table)
        {
            for (int column = 0; column < cells.Length; column++)
            {
                if (column > 0)
                {
                    text.Append(',');
                }

                text.Append(CsvField(cells[column]));
            }

            text.Append('\n');
        }

        return text.ToString();
    }

    // A field with a comma, a quote or a line break stands in quotes, and its quotes are doubled (RFC 4180).
    private static string CsvField(string cell)
    {
        if (cell.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return cell;
        }

        return "\"" + cell.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    // Every column as wide as its widest cell, the columns two spaces apart, no space at the end of a line.
    private string Aligned(List<string[]> table)
    {
        var widths = new int[_columns.Length];
        foreach (string[] cells in table)
        {
            for (int column = 0; column < cells.Length; column++)
            {
                widths[column] = Math.Max(widths[column], cells[column].Length);
            }
        }

        var text = new StringBuilder();
        foreach (string[] cells in table)
        {
            var line = new StringBuilder();
            for (int column = 0; column < cells.Length; column++)
            {
                if (column > 0)
                {
                    line.Append(ColumnGap);
                }

                line.Append(cells[column].PadRight(widths[column]));
            }

            text.Append(line.ToString().TrimEnd()).Append('\n');
        }

        return text.ToString();
    }
}
