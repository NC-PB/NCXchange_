using Ncx.Acceptance.Cli;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Examples;

/// <summary>
/// The five examples of the specification checked with their machine files, found by name in machines/: no ERROR,
/// the second reading of the phase 1 acceptance, and the diagnostics of each pair compared as a whole file with
/// tests/Ncx.Acceptance/Expected/&lt;name&gt;.&lt;machine&gt;.check.txt (implementation 12, P2-04; D103, D104).
/// </summary>
public sealed class ExampleMachineCheckTests : IDisposable
{
    // The tool's own folder of these checks is the repository, whose machines/ and cycles/ hold the shipped machine
    // files and cycle catalogs (implementation 12, P2-04); the working directory is a temporary folder of its own.
    private readonly ProjectHarness _project = new(Fixture.RepositoryRoot());

    /// <summary>
    /// Each example with a machine it checks against: the three mill examples with the Heidenhain and the Fanuc mill,
    /// MILLTURN_TRANSFER with millturn1 (D104), POLAR_FACE with the Nakamura (its note, D103).
    /// </summary>
    public static TheoryData<string, string> ExamplesWithTheirMachines()
    {
        // TODO(question): VM 5 checks the spindle of the current tool holder before a LINE, the default spindle when
        // the holder has none. millturn1.toml gives its turret H1 the driven-tool spindle S3 (TOOL), so the turning
        // LINEs 13 and 14 of MILLTURN_TRANSFER, which cut while MAIN turns, report the WARNING VM500 twice against it;
        // whether a turning tool in a turret with driven tools needs the work spindle instead is open. The expected
        // file records the two WARNINGs as VM 5 has them.
        return new TheoryData<string, string>
        {
            { "2.5D_FRAESEN.ncx", "heidenhain-itnc530" },
            { "2.5D_FRAESEN.ncx", "fanuc-mill-30i" },
            { "PATTERN_LOOP.ncx", "heidenhain-itnc530" },
            { "PATTERN_LOOP.ncx", "fanuc-mill-30i" },
            { "INCREMENTAL_SUB.ncx", "heidenhain-itnc530" },
            { "INCREMENTAL_SUB.ncx", "fanuc-mill-30i" },
            { "MILLTURN_TRANSFER.ncx", "millturn1" },
            { "POLAR_FACE.ncx", "nakamura-ntjx" },
        };
    }

    public void Dispose()
    {
        _project.Dispose();
    }

    // P2-04, D103, D104: the five examples check with no ERROR with their machine files, so ncx check exits 0.
    [Theory]
    [MemberData(nameof(ExamplesWithTheirMachines))]
    public void Check_ExampleWithItsMachineByName_RaisesNoError(string example, string machine)
    {
        string file = CopyExample(example);

        int exitCode = _project.Check(file, machine);

        Assert.True(exitCode == 0, _project.Error);
        Assert.DoesNotContain(": ERROR ", _project.Error, StringComparison.Ordinal);
    }

    // VM 5, D100: what each example reports against its machine, compared as a whole file (D98).
    [Theory]
    [MemberData(nameof(ExamplesWithTheirMachines))]
    public void Check_ExampleWithItsMachineByName_GivesTheExpectedDiagnostics(string example, string machine)
    {
        string file = CopyExample(example);

        _project.Check(file, machine);

        CliHarness.AssertExpectedFile($"{Path.GetFileNameWithoutExtension(example)}.{machine}.check.txt",
            _project.Error.Replace(file, example, StringComparison.Ordinal));
    }

    // P2-04, the acceptance run: ncx check with the machine by name on the command line exits 0. The name is found in
    // machines/ of the working directory of the process or of the tool's own folder, which hold the same shipped files.
    [Theory]
    [InlineData("2.5D_FRAESEN.ncx", "heidenhain-itnc530")]
    [InlineData("POLAR_FACE.ncx", "nakamura-ntjx")]
    public void CommandLine_ExampleWithItsMachineByName_ExitsZero(string example, string machine)
    {
        using var cli = new CliHarness();
        string file = cli.CopyExample(example);

        int exitCode = cli.Run("check", file, "--machine", machine);

        Assert.True(exitCode == 0, cli.Error);
    }

    // A copy of an embedded example in the working directory, byte for byte (tests/README.md).
    private string CopyExample(string example)
    {
        return _project.WriteBytesInWorkingDirectory(example, Fixture.ReadBytes(example));
    }
}
