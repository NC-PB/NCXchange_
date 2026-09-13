using Ncx.Core.Model;

namespace Ncx.Config.Tests;

/// <summary>
/// The start values of the variables, &lt;name&gt;.vars.toml (machine-config 8, virtual machine 2.7, 3.6).
/// </summary>
public sealed class VarsFileTests
{
    // Machine-config 8: Q1 = 10 seeds the variable Q1 with 10.
    [Fact]
    public void VarsFile_Q1Equals10_SeedsQ1WithTen()
    {
        var diagnostics = new Diagnostics("pattern.vars.toml");

        VarsFile? vars = VarsFile.LoadText("""
            Q1 = 10
            """, diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(vars);
        Assert.Equal(new IntegerValue(10, "10"), vars.Variables["Q1"]);
    }

    // Machine-config 8: numbers and strings, as the file writes them.
    [Fact]
    public void VarsFile_DecimalAndString_SeedAsWritten()
    {
        var diagnostics = new Diagnostics("pattern.vars.toml");

        VarsFile? vars = VarsFile.LoadText("""
            Q2 = 5.5
            QS1 = "TEXT"
            """, diagnostics);

        Assert.NotNull(vars);
        Assert.Equal(new DecimalValue(5.5m, "5.5"), vars.Variables["Q2"]);
        Assert.Equal(new StringValue("TEXT"), vars.Variables["QS1"]);
        Assert.Equal(["Q2", "QS1"], vars.Variables.Keys);
    }

    // Machine-config 8: a start value is a number or a string; a table is a wrong type.
    [Fact]
    public void VarsFile_Table_ReportsWrongType()
    {
        var diagnostics = new Diagnostics("pattern.vars.toml");

        VarsFile? vars = VarsFile.LoadText("""
            Q1 = 10
            Q2 = { X = 1 }
            """, diagnostics);

        Assert.Null(vars);
        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.WrongType, error.Code);
        Assert.Equal(2, error.Line);
    }
}
