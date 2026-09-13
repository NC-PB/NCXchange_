using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Repository;

/// <summary>
/// The embedded examples against their originals in docs/spec/examples (tests/README.md).
/// </summary>
public sealed class FixtureTests
{
    // The example machines, as paths relative to docs/spec/examples (tests/README.md).
    private const string ExampleMachinesFolder = "machines/";

    // The copy a test reads is the specification's example byte for byte; the specification folder stays the single
    // source of truth (implementation 00-method 4, phase 0 P0-01).
    [Fact]
    public void EmbeddedExamples_EveryCopy_EqualsItsOriginalByteForByte()
    {
        string examplesFolder = Path.Combine(Fixture.RepositoryRoot(), "docs", "spec", "examples");
        IReadOnlyList<string> examples = Fixture.List();
        var differing = new List<string>();
        foreach (string examplePath in examples)
        {
            byte[] original = File.ReadAllBytes(Path.Combine(examplesFolder, examplePath));
            if (!original.SequenceEqual(Fixture.ReadBytes(examplePath)))
            {
                differing.Add(examplePath);
            }
        }

        Assert.NotEmpty(examples);
        Assert.Empty(differing);
    }

    // The five example machines are the shipped machine files: machines/ holds a copy of each, kept identical to the
    // example byte for byte, so that the specification folder stays the single source of truth and a fix is made in
    // docs/spec/examples/machines and copied (implementation 12, P2-04; machine-config 11).
    [Fact]
    public void ExampleMachines_EveryCopyInMachines_EqualsItsExampleByteForByte()
    {
        string machinesFolder = Path.Combine(Fixture.RepositoryRoot(), "machines");
        var exampleMachines = new List<string>();
        var differing = new List<string>();
        foreach (string examplePath in Fixture.List())
        {
            if (!examplePath.StartsWith(ExampleMachinesFolder, StringComparison.Ordinal))
            {
                continue;
            }

            exampleMachines.Add(examplePath);
            string copy = Path.Combine(machinesFolder, examplePath.Substring(ExampleMachinesFolder.Length));
            if (!File.Exists(copy) || !File.ReadAllBytes(copy).SequenceEqual(Fixture.ReadBytes(examplePath)))
            {
                differing.Add(examplePath);
            }
        }

        Assert.Equal(5, exampleMachines.Count);
        Assert.Empty(differing);
    }

    // The CAM sources are kept as written, line endings and all (docs/spec/examples/sources/README.md), and the
    // .ncx examples use LF; the text a test reads keeps both, so a reader test sees what the reader will see.
    [Fact]
    public void ReadText_ExamplesWithCrlfAndLf_KeepTheirLineEndings()
    {
        Assert.Contains("\r\n", Fixture.ReadText("sources/BOHREN.fanuc.nc"), StringComparison.Ordinal);
        Assert.DoesNotContain("\r", Fixture.ReadText("2.5D_FRAESEN.ncx"), StringComparison.Ordinal);
    }

    // A path that names no embedded example is a mistake in the test and says which path it was.
    [Fact]
    public void ReadBytes_UnknownExample_NamesThePath()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => Fixture.ReadBytes("NO_SUCH_EXAMPLE.ncx"));
        Assert.Contains("NO_SUCH_EXAMPLE.ncx", error.Message, StringComparison.Ordinal);
    }
}
