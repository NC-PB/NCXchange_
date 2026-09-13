using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config.Tests;

/// <summary>
/// The job manifest, &lt;name&gt;.ncxjob.toml (machine-config 8, D15, D20, D48, F24).
/// </summary>
public sealed class JobManifestLoaderTests
{
    // Machine-config 8, D48: two channels of one file, the second naming its program; the shared spindles and axes
    // (D20, F24).
    [Fact]
    public void JobManifest_TwoChannelsWithAProgram_Loads()
    {
        var diagnostics = new Diagnostics("shaft.ncxjob.toml");

        JobManifest? job = JobManifestLoader.LoadText("""
            [job]
            name = "SHAFT"
            machine = "nakamura-ntjx.toml"

            [[channel]]
            id = 1
            file = "shaft.ncx"

            [[channel]]
            id = 2
            file = "shaft.ncx"
            program = "SHAFT_CH2"

            [shared]
            spindles = ["S1", "S2"]
            axes = ["B1"]
            """, diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(job);
        Assert.Equal("SHAFT", job.Name);
        Assert.Equal("nakamura-ntjx.toml", job.Machine);
        Assert.Equal(2, job.Channels.Count);
        Assert.Equal(new ChannelProgram { Id = 1, File = "shaft.ncx" }, job.Channels[0]);
        Assert.Equal("SHAFT_CH2", job.Channels[1].Program);
        Assert.Equal(["S1", "S2"], job.SharedSpindles);
        Assert.Equal(["B1"], job.SharedAxes);
    }

    // Machine-config 8: a channel runs the program of a file; without the file it is an ERROR that names the table.
    [Fact]
    public void JobManifest_ChannelWithoutFile_ReportsTheMissingKey()
    {
        var diagnostics = new Diagnostics("shaft.ncxjob.toml");

        JobManifest? job = JobManifestLoader.LoadText("""
            [job]
            machine = "nakamura-ntjx.toml"

            [[channel]]
            id = 1
            """, diagnostics);

        Assert.Null(job);
        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.MissingKey, error.Code);
        Assert.Equal(4, error.Line);
        Assert.Contains("[[channel]]", error.Message, StringComparison.Ordinal);
        Assert.Contains("file", error.Message, StringComparison.Ordinal);
    }

    // Machine-config 8: [job] names the machine.
    [Fact]
    public void JobManifest_WithoutMachine_ReportsTheMissingKey()
    {
        var diagnostics = new Diagnostics("shaft.ncxjob.toml");

        JobManifestLoader.LoadText("""
            [job]
            name = "SHAFT"
            """, diagnostics);

        Diagnostic error = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.MissingKey, error.Code);
        Assert.Contains("machine", error.Message, StringComparison.Ordinal);
    }

    // P2-01: an unknown key is a WARNING that names the nearest known key.
    [Fact]
    public void JobManifest_TypoInShared_WarnsWithTheNearestKey()
    {
        var diagnostics = new Diagnostics("shaft.ncxjob.toml");

        JobManifest? job = JobManifestLoader.LoadText("""
            [job]
            machine = "nakamura-ntjx.toml"

            [shared]
            spindle = ["S1", "S2"]
            """, diagnostics);

        Assert.NotNull(job);
        Diagnostic warning = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.UnknownKey, warning.Code);
        Assert.Equal(5, warning.Line);
        Assert.Contains("spindles", warning.Message, StringComparison.Ordinal);
    }
}
