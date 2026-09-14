using Ncx.Core.Model;

namespace Ncx.Config.Tests;

/// <summary>
/// The tool table of D10: the tool kind, length and radius per tool, which [machine] tool_table names (machine-config
/// 1), read in the form of the TODO(question) of ToolTable.
/// </summary>
public sealed class ToolTableTests
{
    // Machine-config 1, D10, language 4.4: a tool is found by its number, or by its name for a tool called by name.
    [Fact]
    public void LoadText_ToolsByNumberAndByName_AreFoundByTheirTool()
    {
        var diagnostics = new Diagnostics("mill.tools.toml");

        ToolTable? table = ToolTable.LoadText("""
            [[tool]]
            number = 4
            kind = "TURNING"
            length = 35.5
            radius = 5

            [[tool]]
            name = "DRILL_D8"
            kind = "ROTARY"
            """, diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(table);
        ToolData? four = table.Find(new ToolRef(4));
        Assert.Equal("TURNING", four?.Kind);
        Assert.Equal(35.5m, four?.Length);
        Assert.Equal(5m, four?.Radius);
        Assert.Equal("ROTARY", table.Find(new ToolRef("DRILL_D8"))?.Kind);
        Assert.Null(table.Find(new ToolRef(5)));
    }

    // P2-01: a tool without number and name cannot be found, an ERROR on its table; the file gives no table.
    [Fact]
    public void LoadText_ToolWithoutNumberAndName_IsAnError()
    {
        var diagnostics = new Diagnostics("mill.tools.toml");

        ToolTable? table = ToolTable.LoadText("""
            [[tool]]
            kind = "TURNING"
            """, diagnostics);

        Assert.Null(table);
        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Error, error.Severity);
        Assert.Equal(1, error.Line);
    }

    // P2-01: an unknown key is a WARNING that names the nearest known key, and the table still loads.
    [Fact]
    public void LoadText_UnknownKey_IsAWarningAndTheTableLoads()
    {
        var diagnostics = new Diagnostics("mill.tools.toml");

        ToolTable? table = ToolTable.LoadText("""
            [[tool]]
            number = 1
            knd = "ROTARY"
            """, diagnostics);

        Assert.NotNull(table);
        Diagnostic warning = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Warning, warning.Severity);
        Assert.Contains("kind", warning.Message, StringComparison.Ordinal);
    }
}
