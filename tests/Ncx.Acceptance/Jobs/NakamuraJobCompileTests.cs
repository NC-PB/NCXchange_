using System.Globalization;
using System.Text.RegularExpressions;
using Ncx.Acceptance.Examples;
using Ncx.Compilers;
using Ncx.Compilers.Fanuc;
using Ncx.Config;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.Writing;
using Ncx.Readers;
using Ncx.Readers.Fanuc;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Jobs;

/// <summary>
/// The Nakamura WY-250L pair compiled back as one job (implementation 16, P6-02; milestone M9): both paths read by the
/// Fanuc reader with nakamura-ntjx.toml, written as NCX and parsed again, compiled by the job compiler with the Fanuc
/// compiler and the manifest of Fixtures/, and compared with their sources under the comparison rules of phase 3: the
/// wait codes in the same order, the spindle synchronization in the program of path 2, which [spindle_sync] channel = 2
/// binds it to, and the builder lines kept as RAW verbatim.
/// </summary>
// TODO(question): the Fanuc compiler (P3-06) writes G97 of the sub spindle from [spindle.SUB] CSS_OFF, which
// nakamura-ntjx.toml does not give (the TODO(question) of FanucToolWords.WriteSpindleLines), and G28 B0 only with an
// incremental letter of B, which it does not give either (D243). The job is compiled for the machine with the two
// filled in, CSS_OFF = "G97" and B as the incremental letter of B, until those are answered.
public sealed partial class NakamuraJobCompileTests
{
    // The wait marks of the Nakamura, M100 to M199 ([sync] mark_range), and its spindle synchronization, M96, M97 and
    // M92 ([spindle_sync]).
    private const int FirstMark = 100;
    private const int LastMark = 199;
    private static readonly string[] s_spindleSync = ["M96", "M97", "M92"];

    // M9, P6-02 done when: the Nakamura pair compiles back with its wait codes in the same order.
    [Theory]
    [InlineData(0, "NAKAMURA_WY250L_O1000.path1.nc")]
    [InlineData(1, "NAKAMURA_WY250L_O1000.path2.nc")]
    public void NakamuraJob_CompiledBack_WritesTheWaitCodesInTheOrderOfTheSource(int channel, string source)
    {
        MachineConfig machine = Machine();
        CompileResult result = Compile(machine);

        List<string> compiled = WaitCodes(Blocks(result.Files[channel].Text, machine));

        Assert.Equal(WaitCodes(Blocks(Fixture.ReadText("sources/" + source), machine)), compiled);
        Assert.Equal(["M199", "M110", "M112", "M106", "M190", "M191", "M192", "M193", "M194", "M195"], compiled);
    }

    // M9, P6-02 done when: the spindle synchronization words stand in the path that owns them, path 2 (virtual machine
    // 3.8 rule 2a, D56), in the order of the source, and nowhere in path 1.
    [Fact]
    public void NakamuraJob_CompiledBack_WritesTheSpindleSynchronizationInPath2Alone()
    {
        MachineConfig machine = Machine();
        CompileResult result = Compile(machine);

        List<string> path2 = SpindleSyncCodes(Blocks(result.Files[1].Text, machine));

        Assert.Empty(SpindleSyncCodes(Blocks(result.Files[0].Text, machine)));
        Assert.Equal(SpindleSyncCodes(Blocks(Fixture.ReadText("sources/NAKAMURA_WY250L_O1000.path2.nc"), machine)),
            path2);
        Assert.Equal(["M97", "M96", "M97"], path2);
    }

    // Implementation 16, P6-02: the builder RAW lines of each path stand verbatim in its compiled program, in their
    // order (language 4.1, RAW; D5).
    [Theory]
    [InlineData(0, "NAKAMURA_WY250L_O1000.path1.nc")]
    [InlineData(1, "NAKAMURA_WY250L_O1000.path2.nc")]
    public void NakamuraJob_CompiledBack_WritesTheRawLinesVerbatim(int channel, string source)
    {
        MachineConfig machine = Machine();
        CompileResult result = Compile(machine);

        List<string> lines = [.. result.Files[channel].Text.Split("\r\n")];
        int from = 0;
        foreach (string raw in RawTexts(Converted(source, machine)))
        {
            int at = lines.IndexOf(raw, from);
            Assert.True(at >= 0, $"The RAW line \"{raw}\" is not in the compiled path after line {from}.");
            from = at + 1;
        }

        Assert.True(from > 0, "The path has no RAW line.");
    }

    // The job of the fixture over the converted pair, compiled without an ERROR, one file per channel.
    private static CompileResult Compile(MachineConfig machine)
    {
        var jobDiagnostics = new Diagnostics(JobFixture.NakamuraJob);
        JobManifest? job = JobManifestLoader.LoadText(JobFixture.ReadText(JobFixture.NakamuraJob), jobDiagnostics);
        Assert.True(job is not null, jobDiagnostics.ToText());
        var files = new Dictionary<string, NcxProgram>();
        foreach (ChannelProgram channel in job.Channels)
        {
            files[channel.File] = Converted(Path.ChangeExtension(channel.File, ".nc"), machine);
        }

        CompileResult result = new JobCompiler(new FanucCompiler()).Compile(JobFixture.NakamuraJob, job, files,
            machine, new CompileOptions());
        Assert.False(result.Diagnostics.HasErrors, result.Diagnostics.ToText());
        Assert.Equal(2, result.Files.Count);
        return result;
    }

    // A path as ncx convert writes it and ncx compile reads it again: the Fanuc reader with the machine, the canonical
    // writer and the parser (phase 3, P3-02).
    private static NcxProgram Converted(string source, MachineConfig machine)
    {
        NcxProgram read = new FanucReader().Read(new SourceFile(source, Fixture.ReadText("sources/" + source)),
            machine, new ReadOptions());
        NcxProgram program = Parser.Parse(NcxWriter.Write(read), Path.ChangeExtension(source, ".ncx"),
            new ParserOptions());
        Assert.False(program.Diagnostics.HasErrors, program.Diagnostics.ToText());
        return program;
    }

    // nakamura-ntjx.toml with the two templates of the TODO(question) above.
    private static MachineConfig Machine()
    {
        string text = Fixture.ReadText("machines/nakamura-ntjx.toml");
        string filled = text
            .Replace("[spindle.SUB]\n", "[spindle.SUB]\nCSS_OFF = \"G97\"\n", StringComparison.Ordinal)
            .Replace("letter = \"B\"\n", "letter = \"B\"\nincremental_letter = \"B\"\n", StringComparison.Ordinal);
        Assert.Equal(text.Length + "CSS_OFF = \"G97\"\nincremental_letter = \"B\"\n".Length, filled.Length);
        var diagnostics = new Diagnostics("nakamura-ntjx.toml");
        MachineConfig? machine = MachineConfigLoader.LoadText(filled, diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        return machine;
    }

    // The blocks of a program under the comparison rules of phase 3: block numbers, comments and blank lines removed,
    // the numbers formatted with the decimals of the machine (NcComparer).
    private static List<string> Blocks(string text, MachineConfig machine)
    {
        return new NcComparer(machine).Blocks(text).ConvertAll(block => block.Text);
    }

    // The wait codes of a program, M100 to M199 in the order they stand.
    private static List<string> WaitCodes(List<string> blocks)
    {
        var codes = new List<string>();
        foreach (string block in blocks)
        {
            foreach (Match code in MCode().Matches(block))
            {
                int number = int.Parse(code.Groups[1].Value, CultureInfo.InvariantCulture);
                if (number is >= FirstMark and <= LastMark)
                {
                    codes.Add("M" + number.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        return codes;
    }

    // The codes of [spindle_sync] of a program in the order they stand.
    private static List<string> SpindleSyncCodes(List<string> blocks)
    {
        var codes = new List<string>();
        foreach (string block in blocks)
        {
            foreach (Match code in MCode().Matches(block))
            {
                string written = "M" + int.Parse(code.Groups[1].Value, CultureInfo.InvariantCulture)
                    .ToString(CultureInfo.InvariantCulture);
                if (s_spindleSync.Contains(written))
                {
                    codes.Add(written);
                }
            }
        }

        return codes;
    }

    // The source text of every RAW word of a program, in file order.
    private static List<string> RawTexts(NcxProgram program)
    {
        var texts = new List<string>();
        foreach (Block block in program.Blocks)
        {
            if (block.Find("RAW")?.Value is StringValue raw)
            {
                texts.Add(raw.Content);
            }
        }

        return texts;
    }

    [GeneratedRegex(@"M([0-9]+)", RegexOptions.CultureInvariant)]
    private static partial Regex MCode();
}
