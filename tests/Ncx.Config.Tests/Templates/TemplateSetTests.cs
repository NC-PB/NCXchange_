using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config.Tests.Templates;

/// <summary>
/// The templates of one machine, parsed once from the template text of its records and served to the readers and
/// the compilers: For finds the parsed template of a text, FindFunctionByCode maps a native code back to the state
/// of a function table, codes compared by number (architecture 6, D105, D107).
/// </summary>
public sealed class TemplateSetTests
{
    // The machine file the machines of these tests stand in.
    private const string MachineFile = "machine.toml";

    // One template in every table of the machine file that holds templates, and beside them the values that are no
    // templates: NCX text of an expansion rule, the values of placeholders, a [func_meta] rule, the codes of [raw] and
    // the prefixes of [variables] map (machine-config 2 to 7).
    private const string EveryTable = """
        [machine]
        name = "Every table"
        controller = "fanuc"

        [format]
        program_end = "M30"
        sub_end = "M99"

        [tool_change]
        change = "T{tool} M6"
        change_preloaded = "M6"
        preload = "T{tool}"
        unload = "T0 M6"
        kind_map = { ROTARY = "0.", TURNING = "1." }
        pre = ["HOME Z"]

        [home]
        template = "G28 {axes}"
        point = "G30 P{point} {axes}"

        [setpos]
        template = "G50 {axes}"

        [[axis]]
        id = "C1"
        ncx = "C"
        kind = "rotary"
        clamp = { ON = "M10", OFF = "M11" }

        [diameter]
        ON = "DIAMON"
        OFF = "DIAMOF"

        [spindle.MAIN]
        CW = "M3"
        CCW = "M4"
        OFF = "M5"
        ORIENT = "M19 S{angle}"
        RPM = "S{rpm}"
        VC = "G96 S{value}"
        CSS_OFF = "G97"
        RPM_MAX = "G50 S{value}"

        [spindle.TOOL]
        CW = "M88"

        [spindle_mode.MAIN]
        AXIS = "M91"
        SPINDLE = "M41"

        [spindle_sync]
        ON = "M96"
        OFF = "M97"
        PHASE = "M92"
        channel = 2

        [workpiece]
        MAIN = "G54 M428"
        SUB = "G59 M427"
        SUB_frame = "mirror"
        SUB_mirror = { ON = "G360", OFF = "G361" }

        [sync]
        wait = "M{mark} P{paths}"
        start_channel = "START({channel})"
        wait_channel = "WAITE({channel})"

        [coolant]
        STANDARD = { ON = "M8", OFF = "M9" }
        HIGH_PRESSURE = { ON = "H7={value} M7", OFF = "M9" }

        [func]
        SUB_CHUCK = { OPEN = "M68", CLOSE = "M69" }

        [func_meta]
        SUB_CHUCK.CLOSE = "workpiece_transfer_to = SUB"

        [transform]
        CYLINDER_ON = "G7.1 C{r}"
        CYLINDER_OFF = "G7.1 C0"
        POLAR_ON = "G12.1"
        POLAR_OFF = "G13.1"
        TCPM_ON = "G43.4 H{offset}"
        TCPM_OFF = "G49"
        TILT_ON = "G68.2 X{x} Y{y} Z{z} I{a} J{b} K{c}"
        TILT_OFF = "G69"
        TILT_AXIS_ON = ""
        TILT_TURN = "G53.1"
        ROTARY_PATH_SHORTEST = "M126"
        ROTARY_PATH_FULL = "M127"
        ROTARY_FEED_MM_MIN = "M116"
        ROTARY_FEED_DEG_MIN = "M117"
        move = { TURN = "TURN FMAX", STAY = "STAY" }

        [retract]
        MAX = "M140 MB MAX"
        BY = "M140 MB{distance}"

        [tolerance]
        ON = "G5.1 Q1"
        OFF = "G5.1 Q0"
        mode = { FINISH = 0, ROUGH = 1 }

        [raw]
        known = ["G411"]

        [variables]
        map = { Q = "#1" }

        [system_variables]
        SYS_POS_X = "#5041"
        SYS_WEAR_Z = "#11{index:03}"
        """;

    // A Heidenhain file carries the comma as the decimal separator (controllers heidenhain.md 1, machine-config 2).
    private const string CommaMachine = """
        [machine]
        name = "Heidenhain mill"
        controller = "heidenhain"

        [format]
        decimal_separator = ","

        [retract]
        BY = "M140 MB{distance}"

        [tolerance]
        ON = "CYCL DEF 32.0 TOLERANZ\nCYCL DEF 32.1 T{tol}"
        """;

    // The same retract on a Heidenhain machine whose file has no [format].
    private const string HeidenhainWithoutFormat = """
        [machine]
        name = "Heidenhain mill without format"
        controller = "heidenhain"

        [retract]
        BY = "M140 MB{distance}"
        """;

    // The same retract on a Heidenhain machine whose [format] writes the point.
    private const string HeidenhainWritingThePoint = """
        [machine]
        name = "Heidenhain mill writing the point"
        controller = "heidenhain"

        [format]
        decimal_separator = "."

        [retract]
        BY = "M140 MB{distance}"
        """;

    // A Siemens machine with the tolerance call of the Siemens files, whose arguments the comma separates.
    private const string SiemensMachine = """
        [machine]
        name = "Siemens mill"
        controller = "siemens"

        [tolerance]
        ON = "CYCLE832({tol},{mode},{rotary})"
        """;

    // A Fanuc machine whose file has no [format].
    private const string FanucWithoutFormat = """
        [machine]
        name = "Fanuc mill without format"
        controller = "fanuc"

        [retract]
        BY = "M140 MB{distance}"
        """;

    // A Fanuc machine whose [format] writes the comma, as no example file does.
    private const string FanucWritingTheComma = """
        [machine]
        name = "Fanuc lathe writing the comma"
        controller = "fanuc"

        [format]
        decimal_separator = ","

        [spindle.MAIN]
        VC = "G96 S{value}"
        """;

    // Every template text of EveryTable, each once: M9 stands in two coolant channels and is one template.
    private static readonly string[] s_templatesOfEveryTable =
    [
        "M30", "M99",
        "T{tool} M6", "M6", "T{tool}", "T0 M6",
        "G28 {axes}", "G30 P{point} {axes}", "G50 {axes}",
        "M10", "M11",
        "DIAMON", "DIAMOF",
        "M3", "M4", "M5", "M19 S{angle}", "S{rpm}", "G96 S{value}", "G97", "G50 S{value}", "M88",
        "M91", "M41",
        "M96", "M97", "M92",
        "G54 M428", "G59 M427", "G360", "G361",
        "M{mark} P{paths}", "START({channel})", "WAITE({channel})",
        "M8", "M9", "H7={value} M7",
        "M68", "M69",
        "G7.1 C{r}", "G7.1 C0", "G12.1", "G13.1", "G43.4 H{offset}", "G49", "G68.2 X{x} Y{y} Z{z} I{a} J{b} K{c}",
        "G69", "", "G53.1", "M126", "M127", "M116", "M117",
        "M140 MB MAX", "M140 MB{distance}",
        "G5.1 Q1", "G5.1 Q0",
        "#5041", "#11{index:03}",
    ];

    /// <summary>
    /// The template texts of EveryTable for the theories.
    /// </summary>
    public static TheoryData<string> TemplatesOfEveryTable => new(s_templatesOfEveryTable);

    // The set holds the parsed templates of one machine, built from the template text of its records (architecture 6,
    // D107): every table of machine-config 2 to 7 that holds templates is in it.
    [Theory]
    [MemberData(nameof(TemplatesOfEveryTable))]
    public void For_TemplateOfEveryTable_IsTheParsedTemplateOfThatText(string text)
    {
        TemplateSet templates = Templates(EveryTable);

        Template? template = templates.For(text);

        Assert.NotNull(template);
        Assert.Equal(text, template.Text);
    }

    // Every template text is parsed once (code-guidelines 7): M9 of two coolant channels is one template, and what is
    // no template is not in the set.
    [Fact]
    public void Templates_EveryTable_AreItsTemplateTextsEachOnce()
    {
        var texts = new List<string>();
        foreach (Template template in Templates(EveryTable).Templates)
        {
            texts.Add(template.Text);
        }

        var expected = new List<string>(s_templatesOfEveryTable);
        expected.Sort(StringComparer.Ordinal);
        texts.Sort(StringComparer.Ordinal);
        Assert.Equal(expected, texts);
    }

    // NCX text of an expansion rule is executed by the expander and may carry expressions in braces (machine-config
    // 5a, language 3); the values of placeholders, the rules of [func_meta], the codes of [raw] and the prefixes of
    // [variables] map are no templates either (machine-config 3, 5, 7).
    [Theory]
    [InlineData("HOME Z")]
    [InlineData("0.")]
    [InlineData("TURN FMAX")]
    [InlineData("workpiece_transfer_to = SUB")]
    [InlineData("G411")]
    [InlineData("#1")]
    public void For_TextThatIsNoTemplateOfTheMachine_IsNull(string text)
    {
        Assert.Null(Templates(EveryTable).For(text));
    }

    // A compiler asks for the template of the record it writes, [tool_change] change for TOOL=n (machine-config 3),
    // and gets the one parsed template of that text each time.
    [Fact]
    public void For_ToolChangeOfTheMachine_IsItsParsedTemplate()
    {
        MachineConfig machine = Load(EveryTable);
        var templates = new TemplateSet(machine, new Diagnostics(MachineFile));
        string? change = machine.ToolChange?.Change;
        Assert.NotNull(change);

        Template? template = templates.For(change);

        Assert.NotNull(template);
        Assert.Same(template, templates.For(change));
        Assert.Equal("tool", Assert.Single(template.Placeholders).Name);
    }

    // Readers map M codes back to names (machine-config 5): the state is named by the word of its table, SPINDLE:role
    // for [spindle.ROLE], SPINDLE_MODE:role, SPINDLE_SYNC, COOLANT:channel, FUNC:name, with the key of the state as its
    // value, so that M88 is SPINDLE:TOOL=CW (architecture 7).
    [Theory]
    [InlineData("M3", "SPINDLE:MAIN=CW")]
    [InlineData("M4", "SPINDLE:MAIN=CCW")]
    [InlineData("M88", "SPINDLE:TOOL=CW")]
    [InlineData("M19 S90", "SPINDLE:MAIN=ORIENT")]
    [InlineData("G97", "SPINDLE:MAIN=CSS_OFF")]
    [InlineData("M91", "SPINDLE_MODE:MAIN=AXIS")]
    [InlineData("M41", "SPINDLE_MODE:MAIN=SPINDLE")]
    [InlineData("M96", "SPINDLE_SYNC=ON")]
    [InlineData("M92", "SPINDLE_SYNC=PHASE")]
    [InlineData("M8", "COOLANT:STANDARD=ON")]
    [InlineData("H7=80 M7", "COOLANT:HIGH_PRESSURE=ON")]
    [InlineData("M68", "FUNC:SUB_CHUCK=OPEN")]
    [InlineData("M69", "FUNC:SUB_CHUCK=CLOSE")]
    public void FindFunctionByCode_CodeOfAFunctionTable_IsTheWordOfItsTableAndState(string code, string word)
    {
        Assert.Equal(word, Templates(EveryTable).FindFunctionByCode(code));
    }

    // Readers compare codes by number, so a source M08 matches the table's M8 (machine-config 5, D105).
    [Theory]
    [InlineData("M08", "COOLANT:STANDARD=ON")]
    [InlineData("M008", "COOLANT:STANDARD=ON")]
    [InlineData("M03", "SPINDLE:MAIN=CW")]
    [InlineData("M088", "SPINDLE:TOOL=CW")]
    [InlineData("M068", "FUNC:SUB_CHUCK=OPEN")]
    [InlineData("G097", "SPINDLE:MAIN=CSS_OFF")]
    [InlineData("G0097", "SPINDLE:MAIN=CSS_OFF")]
    public void FindFunctionByCode_SourceCodeWithLeadingZeros_ComparesByNumber(string code, string word)
    {
        Assert.Equal(word, Templates(EveryTable).FindFunctionByCode(code));
    }

    // 8, 08, M8 and M08 in the file are the same code (D105): whatever the file writes, every spelling of the source
    // finds it.
    [Theory]
    [InlineData("M8", "COOLANT:STANDARD=ON")]
    [InlineData("M08", "COOLANT:STANDARD=ON")]
    [InlineData("M9", "COOLANT:STANDARD=OFF")]
    [InlineData("M009", "COOLANT:STANDARD=OFF")]
    [InlineData("M13", "COOLANT:AIR=ON")]
    [InlineData("M14", "COOLANT:AIR=OFF")]
    public void FindFunctionByCode_TableWrittenWithLeadingZerosOrAsNumbers_FindsEverySpelling(string code, string word)
    {
        const string ZerosAndNumbers = """
            [machine]
            name = "Codes as the files write them"
            controller = "fanuc"

            [coolant]
            STANDARD = { ON = "M08", OFF = 9 }
            AIR = { ON = "013", OFF = "14" }
            """;

        Assert.Equal(word, Templates(ZerosAndNumbers).FindFunctionByCode(code));
    }

    // An M code that matches no entry becomes MFUNC=n with a WARNING in the reader (machine-config 5): no state of a
    // function table names it. By number means the whole number, and the program end, the clamps of an axis, the
    // workpiece selection, the builder codes of [raw] and the transformations are templates, not function tables.
    [Theory]
    [InlineData("M136")]
    [InlineData("M80")]
    [InlineData("M18")]
    [InlineData("M")]
    [InlineData("M30")]
    [InlineData("M10")]
    [InlineData("G54 M428")]
    [InlineData("G411")]
    [InlineData("G12.1")]
    [InlineData("M6")]
    public void FindFunctionByCode_CodeNoFunctionTableNames_IsNull(string code)
    {
        Assert.Null(Templates(EveryTable).FindFunctionByCode(code));
    }

    // A code that several entries give names the first in the order of machine-config 5 and of the file: M5 is OFF
    // of the spindle and SPINDLE of the spindle mode on millturn1.toml, M9 switches off every coolant channel, and the
    // DOOR and the SUB_CHUCK of machine-config 5 share M68.
    [Theory]
    [InlineData("M5", "SPINDLE:MAIN=OFF")]
    [InlineData("M9", "COOLANT:STANDARD=OFF")]
    [InlineData("M68", "FUNC:DOOR=OPEN")]
    public void FindFunctionByCode_CodeOfTwoEntries_IsTheFirstInTheOrderOfMachineConfig5(string code, string word)
    {
        const string SharedCodes = """
            [machine]
            name = "Shared codes"
            controller = "siemens"

            [spindle.MAIN]
            OFF = "M5"

            [spindle_mode.MAIN]
            SPINDLE = "M5"

            [coolant]
            STANDARD = { ON = "M8", OFF = "M9" }
            LOW_PRESSURE = { ON = "M7", OFF = "M9" }

            [func]
            DOOR = { OPEN = "M68", CLOSE = "M69" }
            SUB_CHUCK = { OPEN = "M68", CLOSE = "M69" }
            """;

        Assert.Equal(word, Templates(SharedCodes).FindFunctionByCode(code));
    }

    // An empty template names no code: an empty text is found by no state.
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void FindFunctionByCode_EmptyTemplate_NamesNoCode(string code)
    {
        const string EmptyState = """
            [machine]
            name = "Empty state"
            controller = "fanuc"

            [func]
            LAMP = { ON = "", OFF = "M9" }
            """;

        Assert.Null(Templates(EmptyState).FindFunctionByCode(code));
        Assert.Equal("FUNC:LAMP=OFF", Templates(EmptyState).FindFunctionByCode("M9"));
    }

    // The channel binding stays data of the machine for the job compiler (D56): the state of a bound table is found
    // like any other, and the table keeps its channel.
    [Fact]
    public void FindFunctionByCode_ChannelBoundTable_FindsTheStateAndTheMachineKeepsTheBinding()
    {
        MachineConfig machine = Load(EveryTable);

        var templates = new TemplateSet(machine, new Diagnostics(MachineFile));

        Assert.Equal("SPINDLE_SYNC=OFF", templates.FindFunctionByCode("M97"));
        Assert.Equal(2, machine.SpindleSync?.Channel);
    }

    // The compiler writes numbers with the decimal separator of [format], the comma on Heidenhain (machine-config 2,
    // controllers heidenhain.md 1 and 8 rule 2).
    [Fact]
    public void Render_MachineWritingTheComma_WritesNumbersWithTheComma()
    {
        var values = new TemplateValues();
        values.Set("distance", 2.5m);

        Assert.Equal("M140 MB2,5", Render(Templates(CommaMachine), "M140 MB{distance}", values));
    }

    // The comma belongs to the numbers of the placeholders; the literal text keeps what it writes.
    [Fact]
    public void Render_MachineWritingTheComma_KeepsThePointOfTheLiteralText()
    {
        var values = new TemplateValues();
        values.Set("tol", 0.05m);

        string? text = Render(Templates(CommaMachine), """
            CYCL DEF 32.0 TOLERANZ
            CYCL DEF 32.1 T{tol}
            """, values);

        Assert.NotNull(text);
        string[] lines = ["CYCL DEF 32.0 TOLERANZ", "CYCL DEF 32.1 T0,05"];
        Assert.Equal(lines, text.Split('\n'));
    }

    // The comma is the decimal separator, and the reader accepts both (controllers heidenhain.md 7 rule 8).
    [Theory]
    [InlineData("M140 MB2,5", "2.5")]
    [InlineData("M140 MB2.5", "2.5")]
    [InlineData("M140 MB-10,25", "-10.25")]
    [InlineData("M140 MB50", "50")]
    public void Matches_MachineWritingTheComma_ReadsTheCommaAndThePoint(string text, string distance)
    {
        Template? template = Templates(CommaMachine).For("M140 MB{distance}");
        Assert.NotNull(template);

        Assert.True(template.Matches(text, out TemplateValues captured));
        Assert.True(captured.TryGetNumber("distance", out decimal found));
        Assert.Equal(decimal.Parse(distance, CultureInfo.InvariantCulture), found);
    }

    // The comma is the decimal separator of Klartext and its reader accepts both (controllers heidenhain.md 7 rule 8):
    // the comma belongs to the controller family, so a Heidenhain machine reads it whatever its [format] writes, and a
    // file without [format] as well.
    [Theory]
    [InlineData(HeidenhainWithoutFormat, "M140 MB2,5")]
    [InlineData(HeidenhainWithoutFormat, "M140 MB2.5")]
    [InlineData(HeidenhainWritingThePoint, "M140 MB2,5")]
    [InlineData(HeidenhainWritingThePoint, "M140 MB2.5")]
    public void Matches_HeidenhainMachineWhateverItsFormat_ReadsTheCommaAndThePoint(string toml, string text)
    {
        Template? template = Templates(toml).For("M140 MB{distance}");
        Assert.NotNull(template);

        Assert.True(template.Matches(text, out TemplateValues captured));
        Assert.True(captured.TryGetNumber("distance", out decimal distance));
        Assert.Equal(2.5m, distance);
    }

    // The writer of Klartext must produce the comma (controllers heidenhain.md 1, 8 rule 2; differences.md, Numbers;
    // the machine-config 2 comment: "," for Heidenhain), so a Heidenhain file without decimal_separator writes the
    // comma (wave-1 question #64).
    [Fact]
    public void DecimalSeparator_HeidenhainFileWithoutTheKey_IsTheComma()
    {
        TemplateSet templates = Templates(HeidenhainWithoutFormat);
        var values = new TemplateValues();
        values.Set("distance", 2.5m);

        Assert.Equal(",", templates.DecimalSeparator);
        Assert.Equal("M140 MB2,5", Render(templates, "M140 MB{distance}", values));
    }

    // Fanuc and Siemens write the dot (controllers differences.md, Numbers): a file without decimal_separator writes
    // the point there (wave-1 question #64).
    [Theory]
    [InlineData(SiemensMachine)]
    [InlineData(FanucWithoutFormat)]
    public void DecimalSeparator_FanucOrSiemensFileWithoutTheKey_IsThePoint(string toml)
    {
        Assert.Equal(".", Templates(toml).DecimalSeparator);
    }

    // The default machine of D103 names no controller and writes the point, as Fanuc and Siemens do.
    [Fact]
    public void DecimalSeparator_DefaultMachine_IsThePoint()
    {
        var templates = new TemplateSet(DefaultMachine.Create(), new Diagnostics("default machine"));

        Assert.Equal(".", templates.DecimalSeparator);
    }

    // decimal_separator of [format], where the file writes it, wins over the controller's default (machine-config 2;
    // wave-1 question #64).
    [Theory]
    [InlineData(HeidenhainWritingThePoint, ".")]
    [InlineData(CommaMachine, ",")]
    [InlineData(FanucWritingTheComma, ",")]
    public void DecimalSeparator_WrittenKey_WinsOverTheController(string toml, string separator)
    {
        Assert.Equal(separator, Templates(toml).DecimalSeparator);
    }

    // [format] decides what the compiler writes (machine-config 2): a Heidenhain machine whose file says the point
    // writes the point, while it reads the comma as well.
    [Fact]
    public void Render_HeidenhainMachineWritingThePoint_WritesThePoint()
    {
        var values = new TemplateValues();
        values.Set("distance", 2.5m);

        Assert.Equal("M140 MB2.5", Render(Templates(HeidenhainWritingThePoint), "M140 MB{distance}", values));
    }

    // Siemens writes the dot (controllers differences.md, Numbers), and the comma separates the arguments of a call:
    // CYCLE832(0.01,1,0.1) is the tolerance 0.01 in mode 1 with the rotary tolerance 0.1, while a comma inside a number
    // makes another argument, so that CYCLE832(0,01,1,0,1) and CYCLE832(0,01,1,1) are no call of this template.
    [Fact]
    public void Matches_SiemensMachine_TheCommaSeparatesTheArguments()
    {
        const string Cycle832 = "CYCLE832({tol},{mode},{rotary})";
        TemplateSet templates = Templates(SiemensMachine);
        Template? template = templates.For(Cycle832);
        Assert.NotNull(template);
        var values = new TemplateValues();
        values.Set("tol", 0.01m);
        values.Set("mode", 1);
        values.Set("rotary", 0.1m);

        Assert.Equal("CYCLE832(0.01,1,0.1)", Render(templates, Cycle832, values));
        Assert.True(template.Matches("CYCLE832(0.01,1,0.1)", out TemplateValues captured));
        Assert.True(captured.TryGetNumber("tol", out decimal tol));
        Assert.Equal(0.01m, tol);
        Assert.True(captured.TryGetNumber("rotary", out decimal rotary));
        Assert.Equal(0.1m, rotary);
        Assert.False(template.Matches("CYCLE832(0,01,1,0,1)", out _));
        Assert.False(template.Matches("CYCLE832(0,01,1,1)", out _));
    }

    // A machine whose [format] writes the comma reads it as well, on a controller other than Heidenhain too, so that
    // each of its templates matches back what it renders (architecture 6, phase 2 P2-02).
    [Fact]
    public void Matches_FanucMachineWritingTheComma_ReadsBackWhatItRenders()
    {
        TemplateSet templates = Templates(FanucWritingTheComma);
        Template? template = templates.For("G96 S{value}");
        Assert.NotNull(template);
        var values = new TemplateValues();
        values.Set("value", 2.5m);

        string? text = Render(templates, "G96 S{value}", values);

        Assert.Equal("G96 S2,5", text);
        Assert.NotNull(text);
        Assert.True(template.Matches(text, out TemplateValues captured));
        Assert.True(captured.TryGetNumber("value", out decimal value));
        Assert.Equal(2.5m, value);
    }

    // A template the set cannot parse leaves it unusable and is an ERROR of the machine file, the template quoted
    // as the file writes it (machine-config introduction). The loader reports a template of a file on the line of its
    // key (MachineConfigLoaderTemplateTests, wave-1 question #61); a machine built in code has no lines, so the set
    // reports its template on line 1, and the template stays in the set with its braces as text.
    [Fact]
    public void Constructor_MalformedTemplateOfAMachineBuiltInCode_IsCfgErrorOnLineOne()
    {
        MachineConfig machine = DefaultMachine.Create() with
        {
            ToolChange = new ToolChangeConfig { Change = "T{tool M6" },
        };
        var diagnostics = new Diagnostics(MachineFile);

        var templates = new TemplateSet(machine, diagnostics);

        Diagnostic diagnostic = Assert.Single(diagnostics.Items);
        Assert.Equal(Severity.Error, diagnostic.Severity);
        Assert.Equal(DiagnosticCodes.TemplatePlaceholderMalformed, diagnostic.Code);
        Assert.Equal(MachineFile, diagnostic.File);
        Assert.Equal(1, diagnostic.Line);
        Assert.Equal(
            """The template "T{tool M6" opens a placeholder that is never closed (machine-config introduction).""",
            diagnostic.Message);
        Assert.NotNull(templates.For("T{tool M6"));
    }

    // The built-in machine of D103 compiles nothing, so its tables hold no templates.
    [Fact]
    public void Constructor_DefaultMachine_HoldsNoTemplate()
    {
        var diagnostics = new Diagnostics("default machine");

        var templates = new TemplateSet(DefaultMachine.Create(), diagnostics);

        Assert.Empty(templates.Templates);
        Assert.Null(templates.FindFunctionByCode("M8"));
        Assert.Empty(diagnostics.Items);
    }

    // A machine file of these tests, loaded without a diagnostic.
    private static MachineConfig Load(string toml)
    {
        var diagnostics = new Diagnostics(MachineFile);
        MachineConfig? machine = MachineConfigLoader.LoadText(toml, diagnostics);
        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(machine);
        return machine;
    }

    // The template set of a machine file of these tests, built without a diagnostic.
    private static TemplateSet Templates(string toml)
    {
        var diagnostics = new Diagnostics(MachineFile);
        var templates = new TemplateSet(Load(toml), diagnostics);
        Assert.Equal("", diagnostics.ToText());
        return templates;
    }

    // Renders a template of the set for a block of the program and asserts that nothing was reported.
    private static string? Render(TemplateSet templates, string text, TemplateValues values)
    {
        Template? template = templates.For(text);
        Assert.NotNull(template);
        var diagnostics = new Diagnostics("PART.ncx");
        var block = new Block { Line = 12, Words = [new Word { Key = "RETRACT", Value = new IntegerValue(2, "2") }] };

        string? rendered = template.Render(values, block, diagnostics);

        Assert.Equal("", diagnostics.ToText());
        return rendered;
    }
}
