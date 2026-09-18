using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Repository;

/// <summary>
/// The guided tour docs/reading-the-code.md against the code: every file and folder it names exists, and it runs from
/// Program.cs through one ncx format run and one ncx check run into the virtual machine, then through one ncx convert
/// into the reader and one ncx compile into the compiler (code-guidelines 10.3; phase 0, P0-07; phase 1 and phase 3,
/// exit checklists).
/// </summary>
public sealed class ReadingTheCodeTests
{
    // A fenced block of the tour starts and ends with three backticks; the block of a command line the reader runs is
    // marked sh (docs/reading-the-code.md, About this tour).
    private const string Fence = "```";
    private const string CommandLineFence = "```sh";

    // The endings of the files of the repository, by which a word without a slash is a file name all the same:
    // NCXchange.sln, but also a bare Parser.cs, which the tour must write from the repository root.
    private static readonly string[] s_fileEndings =
        [".cs", ".csproj", ".h", ".json", ".md", ".nc", ".ncx", ".props", ".sln", ".toml", ".txt", ".yml"];

    // The route of the tour, in the order its files are first named (phase 0, P0-07; phase 1, P1-07; phase 3, P3-07).
    private static readonly string[] s_route =
    [
        "src/Ncx.Cli/Program.cs",
        "src/Ncx.Cli/Commands/FormatCommand.cs",
        "src/Ncx.Cli/Commands/CheckCommand.cs",
        "src/Ncx.Core/VirtualMachine/VirtualMachine.cs",
        "src/Ncx.Cli/Commands/ConvertCommand.cs",
        "src/Ncx.Readers/ReaderBase.cs",
        "src/Ncx.Cli/Commands/CompileCommand.cs",
        "src/Ncx.Compilers/CompilerBase.cs",
    ];

    // Code-guidelines 10.3, P0-07 done when: the tour is checked against the code by a test that asserts that the files
    // it names exist.
    [Fact]
    public void Tour_EveryNamedPath_Exists()
    {
        string root = Fixture.RepositoryRoot();
        List<string> paths = NamedPaths(ReadTour());
        var missing = new List<string>();
        foreach (string path in paths)
        {
            if (!Exists(root, path))
            {
                missing.Add(path);
            }
        }

        Assert.NotEmpty(paths);
        Assert.True(missing.Count == 0,
            "docs/reading-the-code.md names what does not exist from the repository root: "
            + string.Join(", ", missing));
    }

    // Phase 0, P0-07: the tour goes from Program.cs through one ncx format run and one ncx check run into the virtual
    // machine, and phase 3, P3-07, on through convert and compile, so it names the files of that route first in that
    // order.
    [Fact]
    public void Tour_FirstNamedFiles_RunFromProgramThroughFormatCheckConvertAndCompile()
    {
        List<string> paths = NamedPaths(ReadTour());

        int previous = -1;
        foreach (string file in s_route)
        {
            int position = paths.IndexOf(file);
            Assert.True(position > previous,
                $"docs/reading-the-code.md does not name {file}, or names it first before the file before it on the "
                + $"route {string.Join(" -> ", s_route)}.");
            previous = position;
        }
    }

    // The rule by which the tour names a path: a word in backticks, or a word of a command line in a block marked sh,
    // that holds a slash or ends like a file of the repository. A call, a word of NCX, the output of a command and a
    // file the reader writes himself, in a block marked text, name no path.
    [Fact]
    public void NamedPaths_CodeSpansCommandLinesAndOutput_GiveThePathsOnly()
    {
        string text = """
            Open `src/Ncx.Cli/Program.cs` and read `Parser.Parse(text, fileName, options)`; a word is `KEY:ADDR=VALUE`.

            | `src/Ncx.Core/Parsing/` | `NCXchange.sln` | `src/Ncx.Cli/Program.cs` |

            ```sh
            dotnet run --project src/Ncx.Cli -- check docs/spec/examples/2.5D_FRAESEN.ncx
            ```

            ```text
            $ dotnet run --project src/Ncx.Cli -- check mistake.ncx
            mistake.ncx(6): ERROR VM201: LINE moves at the active feed, and no F is set (virtual machine 3.1).
            ```
            """;

        List<string> paths = NamedPaths(text);

        Assert.Equal(
            [
                "src/Ncx.Cli/Program.cs",
                "src/Ncx.Core/Parsing/",
                "NCXchange.sln",
                "src/Ncx.Cli",
                "docs/spec/examples/2.5D_FRAESEN.ncx",
            ],
            paths);
    }

    // A path written from the root of the disk is no path from the repository root.
    [Fact]
    public void Exists_PathFromTheRootOfTheDisk_IsNoPathOfTheRepository()
    {
        string root = Fixture.RepositoryRoot();

        Assert.True(Exists(root, "NCXchange.sln"));
        Assert.False(Exists(root, Path.Combine(root, "NCXchange.sln")));
        Assert.False(Exists(root, "src/Ncx.Cli/NoSuchFile.cs"));
    }

    // The tour is a file of the repository, read from the root found from the test assembly (tests/README.md).
    private static string ReadTour()
    {
        return File.ReadAllText(Path.Combine(Fixture.RepositoryRoot(), "docs", "reading-the-code.md"));
    }

    // Every path a text names, each once, in the order first named: the words of the code spans outside the fenced
    // blocks and the words of the blocks marked sh.
    private static List<string> NamedPaths(string text)
    {
        var paths = new List<string>();
        bool inBlock = false;
        bool commandLines = false;
        foreach (string line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith(Fence, StringComparison.Ordinal))
            {
                commandLines = !inBlock && trimmed == CommandLineFence;
                inBlock = !inBlock;
                continue;
            }

            if (inBlock)
            {
                if (commandLines)
                {
                    AddPaths(line, paths);
                }

                continue;
            }

            // A line cut at its backticks: every second part is a code span.
            string[] parts = line.Split('`');
            for (int index = 1; index < parts.Length; index += 2)
            {
                AddPaths(parts[index], paths);
            }
        }

        return paths;
    }

    // The words of a code span or a command line that name a path, each added once.
    private static void AddPaths(string text, List<string> paths)
    {
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (IsPath(word) && !paths.Contains(word))
            {
                paths.Add(word);
            }
        }
    }

    // A word names a file or a folder when it holds a slash or ends like a file of the repository.
    private static bool IsPath(string word)
    {
        if (word.Contains('/', StringComparison.Ordinal))
        {
            return true;
        }

        foreach (string ending in s_fileEndings)
        {
            if (word.EndsWith(ending, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // A path exists when it is written from the repository root, not from the root of the disk, and names a file or a
    // folder there.
    private static bool Exists(string root, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return false;
        }

        string fullPath = Path.Combine(root, path);
        return File.Exists(fullPath) || Directory.Exists(fullPath);
    }
}
