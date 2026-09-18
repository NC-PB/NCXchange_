using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Ncx.Acceptance.Cli;
using Ncx.Compilers;
using Ncx.Compilers.Fanuc;
using Ncx.Config;
using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.Parsing;
using Ncx.Core.Writing;
using Ncx.Readers;
using Ncx.Readers.Fanuc;
using Ncx.Tests.Fixtures;

namespace Ncx.Acceptance.Examples;

/// <summary>
/// The acceptance of the Fanuc compiler (phase 3, P3-06; milestone M6): the round trips Fanuc to NCX to Fanuc of
/// 2.5D_FRAESEN, BOHREN and 3D_FRAESEN reproduce their sources under the comparison rules of implementation 13,
/// 2.5D_FRAESEN.ncx compiles to its Fanuc source except the blocks its notes give the Fanuc reading otherwise (D217),
/// ncx compile writes the program, and the whole chain over 3D_FRAESEN takes well under a second.
/// </summary>
public sealed partial class FanucCompilerTests : IDisposable
{
    private readonly CliHarness _cli = new();

    public void Dispose()
    {
        _cli.Dispose();
    }

    // M6, P3-06 done when: the round trip Fanuc to NCX to Fanuc reproduces the source under the comparison rules.
    [Theory]
    [InlineData("2.5D_FRAESEN.fanuc.nc")]
    [InlineData("BOHREN.fanuc.nc")]
    [InlineData("3D_FRAESEN.fanuc.nc")]
    public void RoundTrip_FanucSource_ReproducesTheSource(string source)
    {
        MachineConfig mill = Mill();

        string compiled = RoundTrip(source, mill);

        AssertEquivalent(Fixture.ReadText("sources/" + source), compiled, mill);
    }

    // P3-06 done when: 2.5D_FRAESEN.ncx compiles to a program equivalent to 2.5D_FRAESEN.fanuc.nc. The example gives
    // the Fanuc reading two blocks otherwise (its note 4: RAPID Z=0 FRAME=MACHINE where the Fanuc source writes G91 G28
    // Z0), which compile to G53; D217 recommends the round trip above as the Fanuc check, and every other line equals.
    // TODO(question): D217: what the two readings of 2.5D_FRAESEN must share is open; the two machine-frame moves of
    // note 4 are the listed difference, until D217 is answered.
    [Fact]
    public void Compile_25DFraesenExample_EqualsTheSourceExceptTheMovesOfNote4()
    {
        MachineConfig mill = Mill();
        NcxProgram example = Parser.Parse(Fixture.ReadText("2.5D_FRAESEN.ncx"), "2.5D_FRAESEN.ncx",
            new ParserOptions());

        string compiled = Compile(example, mill);

        string expected = Fixture.ReadText("sources/2.5D_FRAESEN.fanuc.nc")
            .Replace("N380 G91 G28 Z0", "N380 G53 Z0.", StringComparison.Ordinal)
            .Replace("N390 G28 X0 Y0", "N390 G53 X0. Y0.", StringComparison.Ordinal);
        AssertEquivalent(expected, compiled, mill);
    }

    // Architecture 10: ncx compile 2.5D_FRAESEN.ncx --machine fanuc-mill-30i writes the program into the folder of
    // --output, named after the NCX file, without a diagnostic.
    [Fact]
    public void CommandLine_Compile25DFraesen_WritesTheFanucProgram()
    {
        string file = _cli.CopyExample("2.5D_FRAESEN.ncx");
        string folder = _cli.PathOf("nc");

        int exitCode = _cli.Run("compile", file, "--machine", "fanuc-mill-30i", "--output", folder);

        Assert.True(exitCode == 0, _cli.Error);
        Assert.Equal("", _cli.Error);
        string written = File.ReadAllText(Path.Combine(folder, "2.5D_FRAESEN.nc"));
        Assert.StartsWith("%\r\nO0001 (2.5D FRAESEN)\r\nN10 G0 G40\r\n", written, StringComparison.Ordinal);
    }

    // Implementation 13, Risks: 3D_FRAESEN is the performance gate of the whole chain, reader, writer, parser, virtual
    // machine with snapshots and compiler; after a first run that loads the code, the chain takes well under a second.
    [Fact]
    public void Chain_3DFraesen_TakesWellUnderASecond()
    {
        MachineConfig mill = Mill();
        RoundTrip("3D_FRAESEN.fanuc.nc", mill);

        var watch = Stopwatch.StartNew();
        string compiled = RoundTrip("3D_FRAESEN.fanuc.nc", mill);
        watch.Stop();

        Assert.True(watch.ElapsedMilliseconds < 1000,
            $"The chain over 3D_FRAESEN took {watch.ElapsedMilliseconds} ms.");
        Assert.NotEmpty(compiled);
    }

    // Read the source with the Fanuc reader, write it as canonical NCX, parse that text, compile it for the machine:
    // the chain of ncx convert and ncx compile (architecture 7, 8).
    private static string RoundTrip(string source, MachineConfig machine)
    {
        NcxProgram read = new FanucReader().Read(new SourceFile(source, Fixture.ReadText("sources/" + source)),
            machine, new ReadOptions());
        Assert.False(read.Diagnostics.HasErrors, read.Diagnostics.ToText());
        NcxProgram parsed = Parser.Parse(NcxWriter.Write(read), source, new ParserOptions());
        return Compile(parsed, machine);
    }

    private static string Compile(NcxProgram program, MachineConfig machine)
    {
        CompileResult result = new FanucCompiler().Compile(program, machine, new CompileOptions());
        Assert.True(result.Diagnostics.Items.Count == 0, result.Diagnostics.ToText());
        return Assert.Single(result.Files).Text;
    }

    // The comparison rules of implementation 13 (P3-07, made precise there): block numbers, comments and blank lines
    // removed, whitespace trimmed, every number of both sides formatted with the decimals of the machine's [format], so
    // that 70. and 70 are equal and -.534 equals -0.534; a header line and a T M6 line compared as a set of words;
    // every other line word for word, in order. On a difference both texts are shown as normalized.
    private static void AssertEquivalent(string expected, string actual, MachineConfig machine)
    {
        string expectedLines = string.Join('\n', Normalized(expected, machine));
        string actualLines = string.Join('\n', Normalized(actual, machine));
        Assert.Equal(expectedLines, actualLines);
    }

    private static List<string> Normalized(string text, MachineConfig machine)
    {
        var lines = new List<string>();
        bool inHeader = false;
        foreach (string raw in text.ReplaceLineEndings("\n").Split('\n'))
        {
            string line = BlockNumber().Replace(Comment().Replace(raw, ""), "").Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var words = new List<string>();
            foreach (string word in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                words.Add(NumberOf(word, machine));
            }

            bool onlyCodes = words.TrueForAll(word => word.StartsWith('G'));
            inHeader = (inHeader || words[0].StartsWith('O')) && (words[0].StartsWith('O') || onlyCodes);
            if ((inHeader && onlyCodes) || words.Contains("M6"))
            {
                words.Sort(StringComparer.Ordinal);
            }

            lines.Add(string.Join(' ', words));
        }

        return lines;
    }

    // A word of an address and a number with the decimals of the address in [format], without trailing zeros.
    private static string NumberOf(string word, MachineConfig machine)
    {
        Match match = AddressAndNumber().Match(word);
        if (!match.Success)
        {
            return word;
        }

        string address = match.Groups[1].Value;
        decimal number = decimal.Parse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (machine.Format?.Decimals.TryGetValue(address, out int decimals) == true)
        {
            number = Math.Round(number, decimals, MidpointRounding.AwayFromZero);
        }

        string text = number.ToString(CultureInfo.InvariantCulture);
        text = text.Contains('.', StringComparison.Ordinal) ? text.TrimEnd('0').TrimEnd('.') : text;
        return address + (text == "-0" ? "0" : text);
    }

    // The mill of the Fanuc sources with the cycle catalog of its [cycles] (machine-config 6).
    private static MachineConfig Mill()
    {
        string root = Fixture.RepositoryRoot();
        var diagnostics = new Diagnostics("fanuc-mill-30i.toml");
        MachineConfig? machine = MachineConfigLoader.Load(Path.Combine(root, "machines", "fanuc-mill-30i.toml"),
            diagnostics);
        Assert.True(machine is not null && !diagnostics.HasErrors, diagnostics.ToText());
        CycleCatalog? catalog = CycleCatalogLoader.Load(Path.Combine(root, "cycles", "fanuc.toml"), Controller.Fanuc,
            diagnostics);
        Assert.True(catalog is not null, diagnostics.ToText());
        return CycleCatalogLoader.WithCatalog(machine, catalog);
    }

    [GeneratedRegex(@"\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex Comment();

    [GeneratedRegex(@"^N[0-9]+\s*", RegexOptions.CultureInvariant)]
    private static partial Regex BlockNumber();

    [GeneratedRegex(@"^([A-Z]+)([-+]?(?:[0-9]+\.?[0-9]*|\.[0-9]+))$", RegexOptions.CultureInvariant)]
    private static partial Regex AddressAndNumber();
}
