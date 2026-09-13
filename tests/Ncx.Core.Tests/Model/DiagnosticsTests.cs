using Ncx.Core.Model;

namespace Ncx.Core.Tests.Model;

/// <summary>
/// Diagnostics are collected as a result and rendered as the user reads them (code-guidelines 6, D98).
/// </summary>
public sealed class DiagnosticsTests
{
    // A diagnostic renders as file(line): ERROR PAR003: message (D98).
    [Fact]
    public void ToText_ErrorOnABlockOfTheFile_RendersFileLineSeverityCodeAndMessage()
    {
        var diagnostics = new Diagnostics("2.5D_FRAESEN.ncx");

        diagnostics.Error(12, "PAR003", "message");

        Assert.Equal("2.5D_FRAESEN.ncx(12): ERROR PAR003: message\n", diagnostics.ToText());
    }

    // A diagnostic on a generated block names the line of the block it was generated for: file(line, from 12)
    // (D98).
    [Fact]
    public void ToText_WarningOnAGeneratedBlock_RendersTheLineItWasGeneratedFor()
    {
        var diagnostics = new Diagnostics("2.5D_FRAESEN.ncx");

        diagnostics.Warning(GeneratedBlock(line: 13, originLine: 12), "VM042", "message");

        Assert.Equal("2.5D_FRAESEN.ncx(13, from 12): WARNING VM042: message\n", diagnostics.ToText());
    }

    // The OriginLine of a generated block is copied into Diagnostic.OriginLine (D98).
    [Fact]
    public void Error_OnAGeneratedBlock_CarriesItsOriginLine()
    {
        var diagnostics = new Diagnostics("POLAR_FACE.ncx");

        diagnostics.Error(GeneratedBlock(line: 21, originLine: 20), "VM042", "message");

        Diagnostic diagnostic = Assert.Single(diagnostics.Items);
        Assert.Equal(21, diagnostic.Line);
        Assert.Equal(20, diagnostic.OriginLine);
        Assert.Equal("POLAR_FACE.ncx(21, from 20): ERROR VM042: message", diagnostic.ToText());
    }

    [Fact]
    public void Error_OnABlockOfTheFile_HasNoOriginLine()
    {
        var diagnostics = new Diagnostics("POLAR_FACE.ncx");
        var block = new Block { Line = 20, Words = [new Word { Key = "RAPID" }] };

        diagnostics.Error(block, "VM042", "message");

        Diagnostic diagnostic = Assert.Single(diagnostics.Items);
        Assert.Equal(20, diagnostic.Line);
        Assert.Null(diagnostic.OriginLine);
        Assert.Equal("POLAR_FACE.ncx(20): ERROR VM042: message", diagnostic.ToText());
    }

    // INFO carries notes that are neither an ERROR nor a WARNING (D98).
    [Fact]
    public void ToText_Info_RendersInfo()
    {
        var diagnostics = new Diagnostics("PATTERN_LOOP.ncx");

        diagnostics.Info(12, "PLG001", "plugin MyShopRules: inserted 2 blocks at line 12");

        Assert.Equal(
            "PATTERN_LOOP.ncx(12): INFO PLG001: plugin MyShopRules: inserted 2 blocks at line 12\n",
            diagnostics.ToText());
    }

    // A reported diagnostic keeps what it was reported with, and the file of its list (D98).
    [Fact]
    public void Items_ReportedWarning_KeepsSeverityFileLineCodeAndMessage()
    {
        var diagnostics = new Diagnostics("INCREMENTAL_SUB.ncx");

        diagnostics.Warning(17, "VM042", "message");

        Diagnostic diagnostic = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Warning, diagnostic.Severity);
        Assert.Equal("INCREMENTAL_SUB.ncx", diagnostic.File);
        Assert.Equal(17, diagnostic.Line);
        Assert.Null(diagnostic.OriginLine);
        Assert.Equal("VM042", diagnostic.Code);
        Assert.Equal("message", diagnostic.Message);
    }

    // An ERROR stops the run; a WARNING and an INFO do not (virtual machine 2.9).
    [Fact]
    public void HasErrors_WarningsAndInfosOnly_IsFalse()
    {
        var diagnostics = new Diagnostics("PATTERN_LOOP.ncx");

        diagnostics.Warning(3, "VM042", "message");
        diagnostics.Info(4, "PLG001", "note");

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void HasErrors_OneError_IsTrue()
    {
        var diagnostics = new Diagnostics("PATTERN_LOOP.ncx");

        diagnostics.Warning(3, "VM042", "message");
        diagnostics.Error(5, "PAR003", "message");

        Assert.True(diagnostics.HasErrors);
    }

    // The user reads a list, one line per diagnostic in the order the stage reported them (code-guidelines 5).
    [Fact]
    public void ToText_ThreeDiagnostics_WritesOneLineEachInTheOrderReported()
    {
        var diagnostics = new Diagnostics("MILLTURN_TRANSFER.ncx");

        diagnostics.Warning(30, "VM042", "second in the file, first reported");
        diagnostics.Error(8, "PAR003", "first in the file");
        diagnostics.Info(40, "PLG001", "a note");

        Assert.Equal(
            """
            MILLTURN_TRANSFER.ncx(30): WARNING VM042: second in the file, first reported
            MILLTURN_TRANSFER.ncx(8): ERROR PAR003: first in the file
            MILLTURN_TRANSFER.ncx(40): INFO PLG001: a note

            """,
            diagnostics.ToText());
    }

    [Fact]
    public void ToText_NoDiagnostics_IsEmpty()
    {
        var diagnostics = new Diagnostics("PATTERN_LOOP.ncx");

        Assert.Empty(diagnostics.Items);
        Assert.False(diagnostics.HasErrors);
        Assert.Equal("", diagnostics.ToText());
    }

    // A diagnostic about another file, a called program, keeps its own file (virtual machine 2.9).
    [Fact]
    public void Add_DiagnosticOfAnotherFile_KeepsItsFile()
    {
        var diagnostics = new Diagnostics("MAIN.ncx");

        diagnostics.Add(new Diagnostic
        {
            Severity = Severity.Error,
            File = "O9010.ncx",
            Line = 4,
            Code = "VM042",
            Message = "message",
        });

        Assert.Equal("O9010.ncx(4): ERROR VM042: message\n", diagnostics.ToText());
        Assert.True(diagnostics.HasErrors);
    }

    // A block the expander made for the block of the origin line (language 4.15).
    private static Block GeneratedBlock(int line, int originLine)
    {
        var spindleOff = new Word { Key = "SPINDLE", Addr = "MAIN", Value = new IdentValue("OFF") };
        return new Block { Line = line, Words = [spindleOff], IsGenerated = true, OriginLine = originLine };
    }
}
