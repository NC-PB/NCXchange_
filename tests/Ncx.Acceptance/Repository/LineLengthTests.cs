using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Repository;

/// <summary>
/// The 120-character line of code-guidelines 3.3 over every C# file of src/ and tests/. dotnet format has no rule for
/// the length, so this test holds it (wave-1 question #2, D109). A test that writes files works in a temporary folder
/// of its own (implementation 00-method 4).
/// </summary>
public sealed class LineLengthTests : IDisposable
{
    // This project uses 120 characters per line (code-guidelines 3.3 and 9).
    private const int MaxLineLength = 120;

    // The folders of the repository whose C# files keep the limit.
    private static readonly string[] s_sourceFolders = ["src", "tests"];

    // What the build writes into bin/ and obj/ of a project is no source file of the repository.
    private static readonly string[] s_buildOutputFolders = ["bin", "obj"];

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("ncx-line-length-");

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    // Code-guidelines 3.3: this project uses 120-character lines. Every longer line is named by its file and its line,
    // so that the failure says where to wrap.
    [Fact]
    public void LineLength_EveryCSharpFileOfSrcAndTests_HasNoLineLongerThan120Characters()
    {
        string root = Fixture.RepositoryRoot();
        List<string> files = SourceFiles(root);
        var longLines = new List<string>();
        foreach (string file in files)
        {
            longLines.AddRange(LongLines(file, File.ReadAllText(Path.Combine(root, file))));
        }

        Assert.NotEmpty(files);
        Assert.True(longLines.Count == 0,
            $"{longLines.Count} lines are longer than {MaxLineLength} characters (code-guidelines 3.3):\n"
            + string.Join('\n', longLines));
    }

    // A line of 120 characters keeps the limit; a line of 121 is named by its file, its line and its length.
    [Fact]
    public void LongLines_LineOf121Characters_NamesFileAndLine()
    {
        string text = new string('x', 120) + "\n" + new string('y', 121) + "\nz\n";

        Assert.Equal(["src/Long.cs(2): 121 characters"], LongLines("src/Long.cs", text));
    }

    // The line ending belongs to no line: a line of 120 characters ended by CR LF keeps the limit.
    [Fact]
    public void LongLines_LineOf120CharactersEndedByCrLf_KeepsTheLimit()
    {
        string text = new string('x', 120) + "\r\n" + new string('x', 120) + "\r\n";

        Assert.Empty(LongLines("src/Crlf.cs", text));
    }

    // The files of the rule are the C# files of src/ and tests/ and of every folder in them but bin/ and obj/, which
    // the build writes; each is named from the repository root with forward slashes.
    [Fact]
    public void SourceFiles_BuildOutputAndOtherFiles_AreLeftOut()
    {
        string root = _folder.FullName;
        string[] written =
        [
            "src/Ncx.Core/Model/Block.cs",
            "src/Ncx.Core/README.md",
            "src/Ncx.Core/bin/Debug/Copied.cs",
            "src/Ncx.Core/obj/Debug/Ncx.Core.AssemblyInfo.cs",
            "tests/Ncx.Core.Tests/BlockTests.cs",
        ];
        foreach (string path in written)
        {
            string file = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file) ?? root);
            File.WriteAllText(file, "");
        }

        Assert.Equal(["src/Ncx.Core/Model/Block.cs", "tests/Ncx.Core.Tests/BlockTests.cs"], SourceFiles(root));
    }

    // Every C# file of src/ and tests/, named from the repository root with forward slashes, in ordinal order.
    private static List<string> SourceFiles(string root)
    {
        var files = new List<string>();
        foreach (string folder in s_sourceFolders)
        {
            string fullPath = Path.Combine(root, folder);
            if (Directory.Exists(fullPath))
            {
                AddSourceFiles(root, fullPath, files);
            }
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    // The C# files of a folder and of its subfolders, passing over the subfolders the build writes.
    private static void AddSourceFiles(string root, string folder, List<string> files)
    {
        foreach (string file in Directory.GetFiles(folder, "*.cs"))
        {
            files.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
        }

        foreach (string subfolder in Directory.GetDirectories(folder))
        {
            if (!s_buildOutputFolders.Contains(Path.GetFileName(subfolder)))
            {
                AddSourceFiles(root, subfolder, files);
            }
        }
    }

    // The lines of a text longer than the limit, each as "file(line): n characters", the way a diagnostic names its
    // place (code-guidelines 6). The line ending counts to no line.
    private static List<string> LongLines(string file, string text)
    {
        var longLines = new List<string>();
        string[] lines = text.ReplaceLineEndings("\n").Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            if (lines[index].Length > MaxLineLength)
            {
                longLines.Add($"{file}({index + 1}): {lines[index].Length} characters");
            }
        }

        return longLines;
    }
}
