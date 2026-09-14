using Ncx.Analytics;

namespace Ncx.Acceptance.Analytics;

/// <summary>
/// The tables of the reports: plain text in aligned columns or CSV (virtual machine 8), with a header row and the
/// quoting of the table of ncx trace (wave-2 question #40).
/// </summary>
public sealed class TextTableTests
{
    // VM 8, aligned columns: every column as wide as its widest cell, two spaces apart, under the header row, no space
    // at the end of a line.
    [Fact]
    public void Write_Text_AlignsTheColumnsTwoSpacesApartUnderAHeaderRow()
    {
        var table = new TextTable("tool", "seconds", "time");
        table.Add("1", "191.5", "0:03:12");
        table.Add("12", "4.0", "");

        Assert.Equal("tool  seconds  time\n1     191.5    0:03:12\n12    4.0\n", table.Write(TableFormat.Text));
    }

    // VM 8, CSV: a field with a comma or a quote stands in quotes, its quotes doubled.
    [Fact]
    public void Write_Csv_QuotesAFieldWithACommaOrAQuote()
    {
        var table = new TextTable("feeds", "section");
        table.Add("2387 PER_MIN, 1.5 PER_REV", "\"KREISTASCHE\"");

        Assert.Equal("feeds,section\n\"2387 PER_MIN, 1.5 PER_REV\",\"\"\"KREISTASCHE\"\"\"\n",
            table.Write(TableFormat.Csv));
    }

    // A line of text of a report stands as it is in aligned text and as a row of one field in CSV.
    [Fact]
    public void Line_TextAndCsv_IsTheLineAndARowOfOneField()
    {
        const string Note = "Not timed: 4 motions of unknown length, 0 dwells of unknown seconds.";

        Assert.Equal(Note + "\n", TextTable.Line(Note, TableFormat.Text));
        Assert.Equal("\"" + Note + "\"\n", TextTable.Line(Note, TableFormat.Csv));
    }

    // A row that does not fit the columns is a mistake of the analytic that writes it (code-guidelines 6).
    [Fact]
    public void Add_RowWithAnotherNumberOfCells_Throws()
    {
        var table = new TextTable("tool", "seconds");

        Assert.Throws<ArgumentException>(() => table.Add("1"));
    }
}
