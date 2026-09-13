namespace Ncx.Tests.Fixtures;

/// <summary>
/// Runs in every test project, because tests/Fixtures.props links it into each: the fixture mechanism of
/// tests/README.md is in place in this test project.
/// </summary>
public sealed class EmbeddedExamplesTests
{
    // Every test project embeds every file of docs/spec/examples, so that its tests read the specification's own
    // examples and the specification folder stays the single source of truth (implementation 00-method 4).
    [Fact]
    public void EmbeddedExamples_ThisTestProject_HoldsEveryFileOfTheExamplesFolder()
    {
        string examplesFolder = Path.Combine(Fixture.RepositoryRoot(), "docs", "spec", "examples");
        var originals = new List<string>();
        foreach (string file in Directory.EnumerateFiles(examplesFolder, "*", SearchOption.AllDirectories))
        {
            // Hidden files such as .DS_Store are not examples and are not embedded (tests/Fixtures.props).
            if (!Path.GetFileName(file).StartsWith('.'))
            {
                originals.Add(Path.GetRelativePath(examplesFolder, file).Replace('\\', '/'));
            }
        }

        originals.Sort(StringComparer.Ordinal);
        Assert.Equal(originals, Fixture.List());
    }
}
