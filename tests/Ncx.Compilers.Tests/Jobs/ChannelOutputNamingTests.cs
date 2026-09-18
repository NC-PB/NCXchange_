using Ncx.Core.Machine;

namespace Ncx.Compilers.Tests.Jobs;

/// <summary>
/// The names of the files of a job, one per channel (implementation 16, P6-02; controller-mapping 7, CHANNEL): the
/// Nakamura memory card convention O1000 and O1000.P-2, the channel suffix _C1 and _C2 of the STAMA, the program NAME
/// of the DMG units, by [format] channel_files (the TODO(question) of OutputFormat.ChannelFiles).
/// </summary>
public sealed class ChannelOutputNamingTests
{
    // Controller-mapping 7: Nakamura names the files of one job by path suffix, O1000 for path 1 and O1000.P-2 for
    // path 2.
    [Fact]
    public void PathSuffix_TwoChannels_AreO1000AndO1000P2()
    {
        Assert.Equal(["O1000", "O1000.P-2"], NamesOf("channel_files = \"path_suffix\"", 1000, 1000));
    }

    // Controller-mapping 7: the STAMA names the files of a job TEST 5_C1.MPF and TEST 5_C2.MPF, the job name with the
    // channel.
    [Fact]
    public void ChannelSuffix_TwoChannels_AreTheJobNameWithC1AndC2()
    {
        Assert.Equal(["JOB_C1.nc", "JOB_C2.nc"], NamesOf("channel_files = \"channel_suffix\"", 1000, 1000));
    }

    // Controller-mapping 7: the DMG templates hold the units %_N_1000_MPF and %_N_2000_MPF, each channel program named
    // after its NAME.
    [Fact]
    public void ProgramName_TwoChannels_AreTheNamesOfTheirPrograms()
    {
        Assert.Equal(["1000.nc", "2000.nc"], NamesOf("channel_files = \"program_name\"", 1000, 2000));
    }

    // Without channel_files each channel keeps the name the compiler gives the file of its program.
    [Fact]
    public void WithoutChannelFiles_TwoFiles_KeepTheNamesOfTheirFiles()
    {
        Assert.Equal(["C1.nc", "C2.nc"], NamesOf("", 1000, 2000));
    }

    // The TODO(question) of ChannelOutputNaming: two channels that would write one file are told apart by _C1, _C2.
    [Fact]
    public void ProgramName_TwoProgramsOfOneName_AreToldApartByTheChannel()
    {
        Assert.Equal(["1000_C1.nc", "1000_C2.nc"], NamesOf("channel_files = \"program_name\"", 1000, 1000));
    }

    // The names of the two files of a job whose channels run the programs NAME="1000" and NAME="2000" with these
    // numbers.
    private static List<string> NamesOf(string format, int first, int second)
    {
        CompileResult result = JobCompile.Run(JobCompile.Machine(format: format),
            new JobManifest
            {
                Name = "JOB",
                Machine = "test.toml",
                Channels =
                [
                    new ChannelProgram { Id = 1, File = "C1.ncx" },
                    new ChannelProgram { Id = 2, File = "C2.ncx" },
                ],
            },
            new Dictionary<string, string>
            {
                ["C1.ncx"] = Named(first),
                ["C2.ncx"] = Named(second),
            });
        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        var names = new List<string>();
        foreach (CompiledFile file in result.Files)
        {
            names.Add(file.Name);
        }

        return names;
    }

    // A program whose NAME is its number.
    private static string Named(int number)
    {
        string text = JobCompile.Program(number, "SYNC=110");
        return text.Replace("NAME=\"T\"", "NAME=\"" + number.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "\"", StringComparison.Ordinal);
    }
}
