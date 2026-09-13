using Ncx.Cli;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Cli;

/// <summary>
/// One command line of ncx in a temporary folder of its own, with its standard output and standard error, for the
/// tests of check, trace and annotate (implementation 00-method 4: outputs never go anywhere but a temporary folder).
/// </summary>
internal sealed class CliHarness : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("ncx-cli-");
    private StringWriter _output = new();
    private StringWriter _error = new();

    /// <summary>
    /// What the last command wrote to the standard output.
    /// </summary>
    public string Output => _output.ToString();

    /// <summary>
    /// What the last command wrote to the standard error: the diagnostics (D98).
    /// </summary>
    public string Error => _error.ToString();

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
        _folder.Delete(recursive: true);
    }

    /// <summary>
    /// Runs one command line and returns its exit code; the outputs of the command before are forgotten.
    /// </summary>
    public int Run(params string[] args)
    {
        _output.Dispose();
        _error.Dispose();
        _output = new StringWriter();
        _error = new StringWriter();
        return Program.Run(args, _output, _error);
    }

    /// <summary>
    /// A copy of an embedded example in the temporary folder, byte for byte, named as the example.
    /// </summary>
    public string CopyExample(string example)
    {
        string file = PathOf(Path.GetFileName(example));
        File.WriteAllBytes(file, Fixture.ReadBytes(example));
        return file;
    }

    /// <summary>
    /// A file of the test in the temporary folder.
    /// </summary>
    public string WriteFile(string name, string text)
    {
        string file = PathOf(name);
        File.WriteAllText(file, text);
        return file;
    }

    /// <summary>
    /// A path in the temporary folder.
    /// </summary>
    public string PathOf(string name)
    {
        return Path.Combine(_folder.FullName, name);
    }

    /// <summary>
    /// The lines of a file, each ended with LF.
    /// </summary>
    public static string Lines(params string[] lines)
    {
        return string.Join('\n', lines) + "\n";
    }

    /// <summary>
    /// A file of one program named "T" with the given blocks inside its frame, from line 3 on, every line ended with
    /// LF (language 4.1, 4.13).
    /// </summary>
    public static string OneProgram(params string[] blocks)
    {
        var lines = new List<string> { "FILE=BEGIN NCX=1", "PROGRAM=BEGIN NAME=\"T\"" };
        lines.AddRange(blocks);
        lines.Add("PROGRAM=END");
        lines.Add("FILE=END");
        return Lines(lines.ToArray());
    }

    /// <summary>
    /// Compares an output with its expected file in tests/Ncx.Acceptance/Expected as a whole, both with LF line
    /// endings; on a difference the output is written to the temporary folder ncx-acceptance for a diff, as
    /// ExampleCheckTests does (code-guidelines 8).
    /// </summary>
    /// <param name="expectedName">The name of the expected file: 2.5D_FRAESEN.trace.txt.</param>
    /// <param name="actual">The output.</param>
    public static void AssertExpectedFile(string expectedName, string actual)
    {
        string expectedPath =
            Path.Combine(Fixture.RepositoryRoot(), "tests", "Ncx.Acceptance", "Expected", expectedName);
        string expected = File.ReadAllText(expectedPath).ReplaceLineEndings("\n");
        string normalized = actual.ReplaceLineEndings("\n");
        if (normalized != expected)
        {
            string folder = Path.Combine(Path.GetTempPath(), "ncx-acceptance");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, expectedName), normalized);
        }

        Assert.True(normalized == expected,
            $"The output differs from {expectedPath} (actual in the temporary folder ncx-acceptance).\n"
            + $"Expected:\n{expected}\nActual:\n{normalized}");
    }
}
