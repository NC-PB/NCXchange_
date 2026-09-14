using Ncx.Acceptance.Examples;
using Ncx.Cli;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// ncx format &lt;file&gt; [--check] [--output &lt;file&gt;]: the parser and the canonical writer and nothing else, the
/// exit codes of D97, the diagnostics on the standard error in the form of D98 (architecture 10; D91; phase 0, P0-06).
/// Every test works in a temporary folder of its own (implementation 00-method 4).
/// </summary>
public sealed class FormatCommandTests : IDisposable
{
    // A hand-written file in free order and its canonical form (language 5 rules 6 and 7).
    private static readonly string s_freeOrderFile = Lines(
        "FILE=BEGIN NCX=1",
        "PROGRAM=BEGIN NAME=\"FREE\"",
        "UNITS=MM WORKPLANE=XY FEED_MODE=PER_MIN",
        "Y=2 LINE COMP=LEFT X=7 F=200 ; note",
        "PROGRAM=END",
        "FILE=END");

    private static readonly string s_freeOrderCanonical = Lines(
        "FILE=BEGIN NCX=1",
        "PROGRAM=BEGIN NAME=\"FREE\"",
        "FEED_MODE=PER_MIN UNITS=MM WORKPLANE=XY",
        "LINE X=7 Y=2 F=200 COMP=LEFT                            ; note",
        "PROGRAM=END",
        "FILE=END");

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("ncx-format-");
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
        _folder.Delete(recursive: true);
    }

    // Every example is canonical: --check exits 0 and writes nothing (phase 0, P0-06, acceptance run; D97).
    [Theory]
    [MemberData(nameof(ExampleFormatTests.Examples), MemberType = typeof(ExampleFormatTests))]
    public void FormatCheck_Example_ExitsZeroAndWritesNothing(string example)
    {
        string file = CopyExample(example);

        int exitCode = Run("format", file, "--check");

        Assert.True(exitCode == 0, _error.ToString());
        Assert.Equal("", _output.ToString());
        Assert.Equal("", _error.ToString());
    }

    // Without --check and --output the canonical text goes to the standard output (architecture 10). A bare TOOL
    // stays bare: format runs no virtual machine (D91; INCREMENTAL_SUB.ncx line 16).
    [Fact]
    public void Format_Example_WritesTheCanonicalTextToTheStandardOutput()
    {
        string file = CopyExample("INCREMENTAL_SUB.ncx");

        int exitCode = Run("format", file);

        Assert.Equal(0, exitCode);
        Assert.Equal(Fixture.ReadText("INCREMENTAL_SUB.ncx"), _output.ToString());
        Assert.Equal("", _error.ToString());
    }

    // --output writes the canonical text into the file, byte for byte, and nothing to the standard output
    // (architecture 10).
    [Fact]
    public void FormatOutput_Example_WritesTheFileByteForByte()
    {
        string file = CopyExample("POLAR_FACE.ncx");
        string outputFile = PathOf("out.ncx");

        int exitCode = Run("format", file, "--output", outputFile);

        Assert.Equal(0, exitCode);
        Assert.Equal(Fixture.ReadBytes("POLAR_FACE.ncx"), File.ReadAllBytes(outputFile));
        Assert.Equal("", _output.ToString());
    }

    // A hand-written block in free order is written in canonical form (language 5 rules 6 and 7; phase 0, P0-06).
    [Fact]
    public void Format_FreeOrderFile_WritesItInCanonicalForm()
    {
        string file = WriteFile("free.ncx", s_freeOrderFile);

        int exitCode = Run("format", file);

        Assert.Equal(0, exitCode);
        Assert.Equal(s_freeOrderCanonical, _output.ToString());
    }

    // --check writes nothing and exits 1 when the canonical text differs from the file; a note names the first line
    // that differs (D97, architecture 10).
    [Fact]
    public void FormatCheck_FreeOrderFile_ExitsOneAndNamesTheFirstLineThatDiffers()
    {
        string file = WriteFile("free.ncx", s_freeOrderFile);

        int exitCode = Run("format", file, "--check");

        Assert.Equal(1, exitCode);
        Assert.Equal("", _output.ToString());
        Assert.StartsWith($"{file}(3): INFO CLI003: ", _error.ToString(), StringComparison.Ordinal);
    }

    // The canonical text is a fixed point: the file that format wrote passes --check (language 2 rule 7).
    [Fact]
    public void FormatCheck_FileThatFormatWrote_ExitsZero()
    {
        string file = WriteFile("free.ncx", s_freeOrderFile);
        string outputFile = PathOf("canonical.ncx");

        int firstExitCode = Run("format", file, "--output", outputFile);
        int secondExitCode = Run("format", outputFile, "--check");

        Assert.Equal(0, firstExitCode);
        Assert.Equal(0, secondExitCode);
        Assert.Equal(s_freeOrderCanonical, File.ReadAllText(outputFile));
    }

    // An ERROR stops the run: its diagnostic goes to the standard error in the form of D98, nothing is written, and
    // the exit code is 1 (code-guidelines 5 and 6; D97, D98).
    [Fact]
    public void Format_UnknownKey_ExitsOneWithTheDiagnosticAndWritesNothing()
    {
        string file = WriteFile(
            "unknown.ncx",
            Lines("FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"U\"", "LINE X=1 SPEED=5", "PROGRAM=END", "FILE=END"));
        string outputFile = PathOf("out.ncx");

        int exitCode = Run("format", file, "--output", outputFile);

        Assert.Equal(1, exitCode);
        Assert.StartsWith($"{file}(3): ERROR PAR010: ", _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(outputFile));
        Assert.Equal("", _output.ToString());
    }

    // A WARNING is reported and the run continues, exit 0 without --strict (D97); the file keeps the line ending of
    // its first line break (language 3, Encoding).
    [Fact]
    public void Format_MixedLineEndings_WarnsAndWritesTheFirstLineEnding()
    {
        string file = WriteFile("mixed.ncx",
            "FILE=BEGIN NCX=1\r\nPROGRAM=BEGIN NAME=\"M\"\nPROGRAM=END\r\nFILE=END\r\n");

        int exitCode = Run("format", file);

        Assert.Equal(0, exitCode);
        Assert.Equal("FILE=BEGIN NCX=1\r\nPROGRAM=BEGIN NAME=\"M\"\r\nPROGRAM=END\r\nFILE=END\r\n", _output.ToString());
        Assert.StartsWith($"{file}(2): WARNING PAR001: ", _error.ToString(), StringComparison.Ordinal);
    }

    // The same file under --check differs from its canonical text on line 2 (D97).
    [Fact]
    public void FormatCheck_MixedLineEndings_ExitsOne()
    {
        string file = WriteFile("mixed.ncx",
            "FILE=BEGIN NCX=1\r\nPROGRAM=BEGIN NAME=\"M\"\nPROGRAM=END\r\nFILE=END\r\n");

        int exitCode = Run("format", file, "--check");

        Assert.Equal(1, exitCode);
        Assert.Contains($"{file}(2): INFO CLI003: ", _error.ToString(), StringComparison.Ordinal);
    }

    // The last line of a canonical file ends with a line break; --check finds the one that is missing (P0-06, D97).
    [Fact]
    public void FormatCheck_LastLineWithoutLineBreak_ExitsOne()
    {
        string file = WriteFile("open.ncx", "FILE=BEGIN NCX=1\nPROGRAM=BEGIN NAME=\"O\"\nPROGRAM=END\nFILE=END");

        int exitCode = Run("format", file, "--check");

        Assert.Equal(1, exitCode);
        Assert.StartsWith($"{file}(4): INFO CLI003: ", _error.ToString(), StringComparison.Ordinal);
    }

    // A byte order mark is kept where it was: a canonical file with one passes --check and is written with it.
    [Fact]
    public void Format_CanonicalFileWithByteOrderMark_KeepsIt()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Fixture.ReadBytes("PATTERN_LOOP.ncx")];
        string file = PathOf("bom.ncx");
        File.WriteAllBytes(file, bytes);
        string outputFile = PathOf("out.ncx");

        int checkExitCode = Run("format", file, "--check");
        int outputExitCode = Run("format", file, "--output", outputFile);

        Assert.Equal(0, checkExitCode);
        Assert.Equal(0, outputExitCode);
        Assert.Equal(bytes, File.ReadAllBytes(outputFile));
    }

    // An input that cannot be read decides exit code 2 before the run starts; the I/O error is reported as a
    // diagnostic with the file name (D97; code-guidelines 6).
    [Fact]
    public void Format_MissingFile_ExitsTwoWithTheFileName()
    {
        string file = PathOf("missing.ncx");

        int exitCode = Run("format", file);

        Assert.Equal(2, exitCode);
        Assert.StartsWith($"{file}(1): ERROR CLI002: ", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal("", _output.ToString());
    }

    // An NCX file is UTF-8 text; bytes that are no UTF-8 are an input that cannot be read (language 3, Encoding; D97).
    [Fact]
    public void Format_FileThatIsNoUtf8_ExitsTwo()
    {
        string file = PathOf("latin1.ncx");
        File.WriteAllBytes(file, [0x3B, 0x20, 0xE4, 0x0A]);

        int exitCode = Run("format", file);

        Assert.Equal(2, exitCode);
        Assert.StartsWith($"{file}(1): ERROR CLI002: ", _error.ToString(), StringComparison.Ordinal);
    }

    // An output file that cannot be written is an ERROR of the run, which has started: exit 1 (D97, architecture 10).
    [Fact]
    public void FormatOutput_FolderThatDoesNotExist_ExitsOne()
    {
        string file = CopyExample("PATTERN_LOOP.ncx");
        string outputFile = Path.Combine(_folder.FullName, "no-such-folder", "out.ncx");

        int exitCode = Run("format", file, "--output", outputFile);

        Assert.Equal(1, exitCode);
        Assert.StartsWith($"{outputFile}(1): ERROR CLI004: ", _error.ToString(), StringComparison.Ordinal);
    }

    // format takes no machine file, so --machine is a usage error: exit 2, reported before anything is read (D91,
    // D97).
    [Fact]
    public void Format_MachineOption_IsAUsageError()
    {
        string file = CopyExample("2.5D_FRAESEN.ncx");

        int exitCode = Run("format", file, "--machine", "fanuc-mill-30i");

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx(1): ERROR CLI001: ", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal("", _output.ToString());
    }

    // --check writes nothing, so it takes no --output: a usage error, exit 2 (architecture 10, D97).
    [Fact]
    public void FormatCheck_WithOutput_IsAUsageError()
    {
        string file = CopyExample("2.5D_FRAESEN.ncx");
        string outputFile = PathOf("out.ncx");

        int exitCode = Run("format", file, "--check", "--output", outputFile);

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx(1): ERROR CLI001: ", _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(outputFile));
    }

    // A command line that names no command, or no file for format, is a usage error: exit 2 (D97).
    [Theory]
    [InlineData]
    [InlineData("format")]
    [InlineData("frobnicate")]
    public void Ncx_IncompleteCommandLine_IsAUsageError(params string[] args)
    {
        int exitCode = Run(args);

        Assert.Equal(2, exitCode);
        Assert.StartsWith("ncx(1): ERROR CLI001: ", _error.ToString(), StringComparison.Ordinal);
    }

    // The help of format names its options and ends with exit code 0.
    [Fact]
    public void Format_Help_ExitsZeroAndNamesTheOptions()
    {
        int exitCode = Run("format", "--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--check", _output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--output", _output.ToString(), StringComparison.Ordinal);
    }

    private int Run(params string[] args)
    {
        return Program.Run(args, _output, _error);
    }

    // A copy of an embedded example in the temporary folder, byte for byte.
    private string CopyExample(string example)
    {
        string file = PathOf(example);
        File.WriteAllBytes(file, Fixture.ReadBytes(example));
        return file;
    }

    private string WriteFile(string name, string text)
    {
        string file = PathOf(name);
        File.WriteAllText(file, text);
        return file;
    }

    private string PathOf(string name)
    {
        return Path.Combine(_folder.FullName, name);
    }

    // The lines of a file, each ended with LF.
    private static string Lines(params string[] lines)
    {
        return string.Join('\n', lines) + "\n";
    }
}
