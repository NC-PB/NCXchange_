using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Tests.Fixtures;

namespace Ncx.Config.Tests.Templates;

/// <summary>
/// The templates of the eight machine files, the five examples of machine-config 11 (with millturn1.toml, D104) and
/// the three mills of machines/ (P2-04): every template renders from a sample value for each placeholder and matches
/// back to the same values, a table over the loaded configurations (phase 2, P2-02; architecture 6).
/// </summary>
public sealed class TemplateSetMachinesTests
{
    // The examples are embedded from docs/spec/examples; the mills are read from machines/ of the repository, found
    // from the test assembly (tests/README.md).
    private const string ExamplesFolder = "docs/spec/examples/";

    private const string Nakamura = "docs/spec/examples/machines/nakamura-ntjx.toml";
    private const string Doosan = "docs/spec/examples/machines/doosan-puma-2600sy.toml";
    private const string HeidenhainMill = "machines/heidenhain-itnc530.toml";

    private static readonly string[] s_machineFiles =
    [
        Nakamura,
        Doosan,
        "docs/spec/examples/machines/mori-ntx1000-mapps.toml",
        "docs/spec/examples/machines/dmg-ctx-840d.toml",
        "docs/spec/examples/machines/millturn1.toml",
        "machines/fanuc-mill-30i.toml",
        HeidenhainMill,
        "machines/siemens-840dsl-mill.toml",
    ];

    // The template set of every machine file, built once for all the rows of the table.
    private static readonly Dictionary<string, TemplateSet> s_templateSets = TemplateSets();

    /// <summary>
    /// The eight machine files for the theories.
    /// </summary>
    public static TheoryData<string> MachineFiles => new(s_machineFiles);

    /// <summary>
    /// Every template of every machine file: one row per machine file and template text.
    /// </summary>
    public static TheoryData<string, string> TemplatesOfTheMachineFiles
    {
        get
        {
            var rows = new TheoryData<string, string>();
            foreach (string machineFile in s_machineFiles)
            {
                foreach (Template template in s_templateSets[machineFile].Templates)
                {
                    rows.Add(machineFile, template.Text);
                }
            }

            return rows;
        }
    }

    // P2-02 done when: every template in the five example files (with millturn1.toml, D104) renders from a sample
    // word set and matches back to it; the three mills of machines/ as well.
    [Theory]
    [MemberData(nameof(TemplatesOfTheMachineFiles))]
    public void RoundTrip_EveryTemplateOfTheMachineFiles_MatchesBackToItsSampleValues(string machineFile, string text)
    {
        Template? template = s_templateSets[machineFile].For(text);

        Assert.NotNull(template);
        TemplateSamples.AssertRoundTrip(template);
    }

    // Every template of the eight files can be parsed: none is reported (machine-config introduction).
    [Theory]
    [MemberData(nameof(MachineFiles))]
    public void Constructor_EveryMachineFile_ParsesItsTemplatesWithoutDiagnostic(string machineFile)
    {
        var diagnostics = new Diagnostics(machineFile);

        var templates = new TemplateSet(Load(machineFile), diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.NotEmpty(templates.Templates);
    }

    // P2-02 done when: G340 T{tool:02}{offset:02}. A{next:02}. renders G340 T0101. A02. and matches it; here the
    // tool change of the Nakamura file itself (machine-config 3).
    [Fact]
    public void For_NakamuraToolChange_RendersG340T0101A02AndMatchesIt()
    {
        MachineConfig machine = Load(Nakamura);
        string? change = machine.ToolChange?.Change;
        Assert.NotNull(change);
        Template? template = new TemplateSet(machine, new Diagnostics(Nakamura)).For(change);
        Assert.NotNull(template);
        var values = new TemplateValues();
        values.Set("tool", 1);
        values.Set("offset", 1);
        values.Set("next", 2);
        var diagnostics = new Diagnostics("PART.ncx");

        string? text = template.Render(values, ToolBlock(), diagnostics);

        Assert.Empty(diagnostics.Items);
        Assert.Equal("G340 T{tool:02}{offset:02}. A{next:02}.", template.Text);
        Assert.Equal("G340 T0101. A02.", text);
        Assert.True(template.Matches("G340 T0101. A02.", out TemplateValues captured));
        Assert.True(captured.TryGetNumber("next", out decimal next));
        Assert.Equal(2m, next);
    }

    // The reader asks the machine for M88 and gets SPINDLE:TOOL=CW (architecture 7), however the source writes the
    // number (D105).
    [Theory]
    [InlineData("M88", "SPINDLE:TOOL=CW")]
    [InlineData("M088", "SPINDLE:TOOL=CW")]
    [InlineData("M96", "SPINDLE_SYNC=ON")]
    [InlineData("M50", "FUNC:SUB_CHUCK=CLOSE")]
    public void FindFunctionByCode_Nakamura_NamesTheStateOfItsTable(string code, string word)
    {
        Assert.Equal(word, s_templateSets[Nakamura].FindFunctionByCode(code));
    }

    // The Doosan file writes its codes with leading zeros and selects the spindle with a P suffix; they load
    // normalized, M03 P11 as M3 P11, and every spelling of the source finds them (D105).
    [Theory]
    [InlineData("M03 P11", "SPINDLE:MAIN=CW")]
    [InlineData("M3 P12", "SPINDLE:TOOL=CW")]
    [InlineData("M05P12", "SPINDLE:TOOL=OFF")]
    [InlineData("M08", "COOLANT:STANDARD=ON")]
    [InlineData("M291", "FUNC:PECK_MODE=RETRACT")]
    public void FindFunctionByCode_Doosan_ComparesItsCodesByNumber(string code, string word)
    {
        Assert.Equal(word, s_templateSets[Doosan].FindFunctionByCode(code));
    }

    // The Heidenhain mill writes the comma (controllers heidenhain.md 1 and 8 rule 2, machine-config 2): the numbers
    // of its tolerance cycle are written with it, the cycle numbers of the literal text keep their point.
    [Fact]
    public void Render_HeidenhainTolerance_WritesTheCommaOfItsFormat()
    {
        MachineConfig machine = Load(HeidenhainMill);
        string? tolerance = machine.Tolerance?.On;
        Assert.NotNull(tolerance);
        Template? template = new TemplateSet(machine, new Diagnostics(HeidenhainMill)).For(tolerance);
        Assert.NotNull(template);
        var values = new TemplateValues();
        values.Set("tol", 0.02m);
        values.Set("mode", 0);
        values.Set("rotary", 0.1m);
        var diagnostics = new Diagnostics("PART.ncx");

        string? text = template.Render(values, ToolBlock(), diagnostics);

        Assert.Empty(diagnostics.Items);
        Assert.NotNull(text);
        string[] lines = ["CYCL DEF 32.0 TOLERANZ", "CYCL DEF 32.1 T0,02", "CYCL DEF 32.2 HSC-MODE:0 TA0,1"];
        Assert.Equal(lines, text.Split('\n'));
    }

    // The template set of every machine file of the table.
    private static Dictionary<string, TemplateSet> TemplateSets()
    {
        var templateSets = new Dictionary<string, TemplateSet>();
        foreach (string machineFile in s_machineFiles)
        {
            templateSets[machineFile] = new TemplateSet(Load(machineFile), new Diagnostics(machineFile));
        }

        return templateSets;
    }

    // A machine file of the table, loaded without an ERROR (P2-01): an example through its embedded copy, a mill
    // through its path in the repository (tests/README.md).
    private static MachineConfig Load(string machineFile)
    {
        var diagnostics = new Diagnostics(machineFile);
        MachineConfig? machine;
        if (machineFile.StartsWith(ExamplesFolder, StringComparison.Ordinal))
        {
            string examplePath = machineFile.Substring(ExamplesFolder.Length);
            machine = MachineConfigLoader.LoadText(Fixture.ReadText(examplePath), diagnostics);
        }
        else
        {
            machine = MachineConfigLoader.Load(Path.Combine(Fixture.RepositoryRoot(), machineFile), diagnostics);
        }

        Assert.NotNull(machine);
        return machine;
    }

    // The block a template is rendered for.
    private static Block ToolBlock()
    {
        return new Block { Line = 12, Words = [new Word { Key = "TOOL", Value = new IntegerValue(1, "1") }] };
    }
}
