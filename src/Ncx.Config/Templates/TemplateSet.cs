using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Config.Templates;

/// <summary>
/// The templates of one machine: every template text of its records parsed once into a <see cref="Template"/>, with
/// the decimal separator its [format] writes and the comma its controller reads. The compilers render them, the readers
/// match them and map a native code back to the state of a function table, and all of them ask the one set of the
/// machine (architecture 6, D105, D107).
/// </summary>
public sealed class TemplateSet
{
    // Every diagnostic carries its line and a bad machine file reports the line (code-guidelines 6, D98; phase 2): the
    // loader parses each template of a machine file on the line of its key, and a file with a template that cannot be
    // parsed gives no machine (MachineConfigLoader; wave-1 question #61). The records keep a template as text without
    // its line (D107), so a machine built in code, whose templates never passed the loader, has a template that cannot
    // be parsed reported on line 1, the line of a mistake of the file as a whole, with the template quoted as it is
    // written, so that the user finds it by its text.
    private const int MachineFileLine = 1;

    private readonly Dictionary<string, Template> _byText = new(StringComparer.Ordinal);
    private readonly List<Template> _templates = [];

    // The states of the function tables in the order of machine-config 5 and of the file, each named by the word of
    // its table with the key of the state as its value: SPINDLE:TOOL=CW for the M88 of [spindle.TOOL].
    private readonly List<KeyValuePair<string, Template>> _functionStates = [];

    /// <summary>
    /// Parses every template text of the machine once, numbers written with the decimal separator of its [format] and
    /// read with the point and, on a Heidenhain machine or one whose [format] writes the comma, with the comma. A
    /// template that cannot be parsed leaves it unusable, is an ERROR of the machine file and stays in the set with its
    /// braces as literal text (machine-config introduction).
    /// </summary>
    /// <param name="machine">The machine as its file was loaded, its templates as text (D107).</param>
    /// <param name="diagnostics">The diagnostics of the machine file.</param>
    public TemplateSet(MachineConfig machine, Diagnostics diagnostics)
    {
        string decimalSeparator = DecimalSeparator(machine.Format);
        bool readsComma = ReadsComma(machine.Machine, decimalSeparator);
        List<KeyValuePair<string, string>> functionStates = FunctionStates(machine);

        // Each template text is parsed once, however many tables write it (code-guidelines 7): the M9 that switches
        // off every coolant channel of the Nakamura is one template.
        foreach (string text in TemplateTexts(machine, functionStates))
        {
            if (_byText.ContainsKey(text))
            {
                continue;
            }

            var template = new Template(text, MachineFileLine, decimalSeparator, readsComma, diagnostics);
            _byText.Add(text, template);
            _templates.Add(template);
        }

        foreach (KeyValuePair<string, string> state in functionStates)
        {
            _functionStates.Add(new KeyValuePair<string, Template>(state.Key, _byText[state.Value]));
        }
    }

    /// <summary>
    /// Every template of the machine, each text once: the ends of programs, the tool change, home and setpos, the
    /// clamps of the axes, the diameter switch, the function tables, the workpiece selection, the waits, the
    /// transformations, retract, tolerance and the system variables (machine-config 2 to 7).
    /// </summary>
    public IReadOnlyList<Template> Templates => _templates;

    /// <summary>
    /// The parsed template of a template text of the machine's records, so that a compiler renders the record it
    /// writes: For(machine.ToolChange.Change) for TOOL=n (machine-config 3).
    /// </summary>
    /// <param name="text">The template text as the record keeps it, its M and G codes normalized (D105).</param>
    /// <returns>The template; null when no record of the machine has this text.</returns>
    public Template? For(string text)
    {
        return _byText.TryGetValue(text, out Template? template) ? template : null;
    }

    /// <summary>
    /// Maps a native code back to the state of the function table that writes it, so that a reader writes the word
    /// of the machine and not MFUNC: "M88" is "SPINDLE:TOOL=CW" on the Nakamura, and "M08" is "COOLANT:STANDARD=ON"
    /// wherever the table writes M8, because codes compare by number (machine-config 5, architecture 7, D105).
    /// </summary>
    /// <param name="nativeCode">The native text of the function, as many words as its template has: "M88",
    /// "M03 P11".</param>
    /// <returns>The state, named by the word of its table with the key of the state as its value: SPINDLE:role=CW for
    /// [spindle.ROLE], SPINDLE_MODE:role=AXIS, SPINDLE_SYNC=ON, COOLANT:channel=ON, FUNC:name=OPEN; null when no state
    /// of the machine writes this code, which the reader writes as MFUNC with a WARNING (machine-config 5).</returns>
    public string? FindFunctionByCode(string nativeCode)
    {
        // Readers map M codes back to names through the tables of the machine and compare codes by number, so that a
        // source M08 matches the M8 of the table (machine-config 5, D105): the template of the state matches the whole
        // native text, blanks between its words tolerated. An empty template writes nothing and names no code.
        // TODO(question): a code that several states write, M9 of every coolant channel of the Nakamura, M3 of MAIN
        // and TOOL on the Mori Seiki, M3 P11 of MAIN and SUB on the Doosan (whose M34 and M134 tell them apart),
        // names the first state in the order of machine-config 5 and of the file; how the reader chooses among them
        // with its source state (architecture 7) is open.
        foreach (KeyValuePair<string, Template> state in _functionStates)
        {
            Template template = state.Value;
            if (template.Text.Length > 0 && template.Matches(nativeCode, out _))
            {
                return state.Key;
            }
        }

        return null;
    }

    // Numbers are written with the decimal separator of [format], the comma on Heidenhain (machine-config 2,
    // controllers heidenhain.md 1 and 8 rule 2).
    // TODO(question): machine-config 2 gives no default for decimal_separator. A file that leaves it out is written
    // with the point, the dot of Fanuc and Siemens (controllers differences.md, Numbers), while the writer of Klartext
    // produces the comma (controllers heidenhain.md 1, 8 rule 2); whether a Heidenhain file without decimal_separator
    // writes the comma is open.
    private static string DecimalSeparator(OutputFormat? format)
    {
        return format?.DecimalSeparator == Template.DecimalComma ? Template.DecimalComma : Template.DecimalPoint;
    }

    // The comma is the decimal separator of Klartext and its reader accepts both (controllers heidenhain.md 7 rule 8;
    // differences.md, Numbers: the comma in the files, the dot accepted by newer controls). The comma belongs to the
    // controller family, so a Heidenhain machine reads it whatever its [format] writes. A machine whose [format]
    // writes the comma reads it as well, so that each of its templates matches back what it renders (architecture 6).
    // Fanuc and Siemens write the dot, and there the comma separates the arguments of a call (differences.md, Numbers).
    private static bool ReadsComma(MachineIdentity identity, string decimalSeparator)
    {
        return identity.Controller == Controller.Heidenhain || decimalSeparator == Template.DecimalComma;
    }

    // The function tables of machine-config 5 whose states are function values (D105), in the order of the section:
    // [spindle.ROLE], [spindle_mode.ROLE], [spindle_sync], [coolant] and [func]. Each state is named by the word of
    // its table (language 4.5, 4.6) with the key of the state as its value, because the states of a table are the
    // values of its word (machine-config 5).
    // TODO(question): the architecture answers FindFunctionByCode("M88") with SPINDLE:TOOL=CW (architecture 7), and
    // machine-config 5 calls the keys of a table the values of its word, while the language writes ORIENT, RPM, VC,
    // CSS and RPM_MAX as words of their own (language 4.5, 4.11), SPINDLE_SYNC with two roles or OFF (4.5) and the
    // default coolant channel as COOLANT without an address (4.6). A state is named as the word of its table with the
    // key as its value, SPINDLE:MAIN=ORIENT, SPINDLE_SYNC=ON, COOLANT:STANDARD=ON; making the word of the language
    // from it is left to the reader.
    private static List<KeyValuePair<string, string>> FunctionStates(MachineConfig machine)
    {
        var states = new List<KeyValuePair<string, string>>();
        foreach (KeyValuePair<string, FunctionTable> spindle in machine.SpindleTables)
        {
            AddStates(states, "SPINDLE:" + spindle.Key, spindle.Value);
        }

        foreach (KeyValuePair<string, FunctionTable> spindleMode in machine.SpindleModeTables)
        {
            AddStates(states, "SPINDLE_MODE:" + spindleMode.Key, spindleMode.Value);
        }

        if (machine.SpindleSync is FunctionTable spindleSync)
        {
            AddStates(states, "SPINDLE_SYNC", spindleSync);
        }

        foreach (KeyValuePair<string, FunctionTable> coolant in machine.Coolant)
        {
            AddStates(states, "COOLANT:" + coolant.Key, coolant.Value);
        }

        foreach (KeyValuePair<string, FunctionTable> function in machine.Functions)
        {
            AddStates(states, "FUNC:" + function.Key, function.Value);
        }

        return states;
    }

    // Every state of one table: the word with the key of the state as its value, and the text of its template.
    private static void AddStates(List<KeyValuePair<string, string>> states, string word, FunctionTable table)
    {
        foreach (KeyValuePair<string, string> state in table.States)
        {
            states.Add(new KeyValuePair<string, string>(word + "=" + state.Key, state.Value));
        }
    }

    // Every template text of the records, in the order of machine-config. No template is the NCX text of an expansion
    // rule, which the expander executes and whose braces may be expressions (machine-config 5a, language 3), nor the
    // values of a placeholder (kind_map, move, mode), the rules of [func_meta], the codes of [raw] or the prefixes of
    // [variables] map.
    private static List<string> TemplateTexts(
        MachineConfig machine, List<KeyValuePair<string, string>> functionStates)
    {
        var texts = new List<string>();

        // The ends of programs and subprograms (machine-config 2); the tool change, home and setpos (3).
        AddText(texts, machine.Format?.ProgramEnd);
        AddText(texts, machine.Format?.SubEnd);
        AddText(texts, machine.ToolChange?.Change);
        AddText(texts, machine.ToolChange?.ChangePreloaded);
        AddText(texts, machine.ToolChange?.Preload);
        AddText(texts, machine.ToolChange?.Unload);
        AddText(texts, machine.Home?.Template);
        AddText(texts, machine.Home?.Point);
        AddText(texts, machine.Setpos?.Template);

        // The clamps of the rotary axes and the diameter switch (machine-config 4).
        foreach (AxisDef axis in machine.Axes)
        {
            texts.AddRange(axis.Clamp.Values);
        }

        AddText(texts, machine.Diameter?.On);
        AddText(texts, machine.Diameter?.Off);

        // The function tables, the workpiece selection with its mirror cycles, the waits, the transformations, retract
        // and tolerance (machine-config 5).
        foreach (KeyValuePair<string, string> state in functionStates)
        {
            texts.Add(state.Value);
        }

        if (machine.Workpiece is WorkpieceConfig workpiece)
        {
            texts.AddRange(workpiece.Templates.Values);
            foreach (FunctionTable mirror in workpiece.Mirrors.Values)
            {
                texts.AddRange(mirror.States.Values);
            }
        }

        AddText(texts, machine.Sync?.Wait);
        AddText(texts, machine.Sync?.StartChannel);
        AddText(texts, machine.Sync?.WaitChannel);
        AddText(texts, machine.Transform?.CylinderOn);
        AddText(texts, machine.Transform?.CylinderOff);
        AddText(texts, machine.Transform?.PolarOn);
        AddText(texts, machine.Transform?.PolarOff);
        AddText(texts, machine.Transform?.TcpmOn);
        AddText(texts, machine.Transform?.TcpmOff);
        AddText(texts, machine.Transform?.TiltOn);
        AddText(texts, machine.Transform?.TiltOff);
        AddText(texts, machine.Transform?.TiltAxisOn);
        AddText(texts, machine.Transform?.TiltTurn);
        AddText(texts, machine.Transform?.RotaryPathShortest);
        AddText(texts, machine.Transform?.RotaryPathFull);
        AddText(texts, machine.Transform?.RotaryFeedMmMin);
        AddText(texts, machine.Transform?.RotaryFeedDegMin);
        AddText(texts, machine.Retract?.Max);
        AddText(texts, machine.Retract?.By);
        AddText(texts, machine.Tolerance?.On);
        AddText(texts, machine.Tolerance?.Off);

        // The native system variables, SYS_WEAR_Z = "#11{index:03}" (machine-config 7).
        texts.AddRange(machine.SystemVariables.Entries.Values);
        return texts;
    }

    // A template the file leaves out is not there.
    private static void AddText(List<string> texts, string? text)
    {
        if (text is not null)
        {
            texts.Add(text);
        }
    }
}
