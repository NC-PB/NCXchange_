using System.Globalization;
using System.Text;
using Ncx.Core.Model;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// What the Siemens compiler knows of the whole NCX file while it writes one block of it (controllers siemens.md 1, 8,
/// 12 rule 1): the name of the unit of every program and subprogram, the parameters of every subprogram from the
/// arguments its calls give, the labels of every section and the label before the end of a program that a JUMP=END
/// reaches.
/// </summary>
internal sealed class SiemensFile
{
    // The label a JUMP=END goes to, before the program end (controller-mapping 1, JUMP=END).
    private const string EndLabel = "PROGRAM_END";

    // A label of an integer, or of a name whose first two characters are not letters (controllers siemens.md 8).
    private const string LabelPrefix = "LABEL_";

    // The statements of the language of controllers siemens.md 8 and 9, which no label may be.
    private static readonly HashSet<string> s_keywords = new(StringComparer.Ordinal)
    {
        "IF", "ELSE", "ENDIF", "LOOP", "ENDLOOP", "FOR", "TO", "ENDFOR", "WHILE", "ENDWHILE", "REPEAT", "REPEATB",
        "UNTIL", "CASE", "OF", "DEFAULT", "GOTO", "GOTOF", "GOTOB", "GOTOC", "GOTOS", "PROC", "RET", "DEF", "EXTERN",
        "CALL", "MCALL", "PCALL", "EXTCALL", "DEFINE", "AS", "SAVE", "STOPRE", "MSG", "INIT", "START", "WAITE", "WAITM",
        "WAITMC", "SETM", "CLEARM", "AND", "OR", "NOT", "XOR", "DIV", "MOD",
    };

    private readonly IReadOnlyList<BlockStep> _steps;
    private readonly Dictionary<Section, string> _unitNames = [];
    private readonly Dictionary<Section, string> _endLabels = [];
    private readonly Dictionary<string, List<string>> _parameters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Section> _subs = new(StringComparer.Ordinal);

    // The jumps over a conditional call: the target state of the program or walk and what the control has active at
    // the jump, which the block after the call meets once the subprogram has returned (SiemensFlow).
    private readonly List<(TargetState Target, Dictionary<string, string> AtJump)> _jumpsOverCalls = [];

    private SiemensFile(IReadOnlyList<BlockStep> steps)
    {
        _steps = steps;
        foreach (BlockStep step in steps)
        {
            if (step.Section is Section section && section.Kind == SectionKind.Sub && section.Name is string name)
            {
                _subs.TryAdd(name, section);
            }
        }

        CollectParameters();
    }

    /// <summary>
    /// The file of a run: every step of the STATIC walk (architecture 8).
    /// </summary>
    public static SiemensFile Of(IReadOnlyList<BlockStep> steps)
    {
        return new SiemensFile(steps);
    }

    /// <summary>
    /// The subprogram of the file a CALL names by its NAME; null for an external program (language 4.9, CALL).
    /// </summary>
    public Section? SubNamed(Value call)
    {
        if (call is StringValue)
        {
            return null;
        }

        return _subs.GetValueOrDefault(call.ToCanonical());
    }

    /// <summary>
    /// The name of the unit of a program or subprogram: %_N_NAME_MPF, PROC NAME (controllers siemens.md 1, 8, 12 rule
    /// 1): the NAME of the section in capitals; a subprogram named by a number is L and the number, the form of L100;
    /// a character a SINUMERIK name does not hold is an underscore, with a WARNING once per section; a name another
    /// section of the same kind has takes _2 behind it, with a WARNING.
    /// </summary>
    /// <param name="section">The program or subprogram.</param>
    /// <param name="fileStem">The name of the NCX file, for a program without NAME and NUMBER.</param>
    /// <param name="write">The block being written, where the WARNING stands.</param>
    public string UnitNameOf(Section section, string fileStem, SiemensBlock write)
    {
        if (_unitNames.TryGetValue(section, out string? known))
        {
            return known;
        }

        // A subprogram named by a number is called as L100, where the leading zeros count (controllers siemens.md 8).
        string given = section.Name
            ?? section.Number?.ToString(CultureInfo.InvariantCulture)
            ?? fileStem;
        string written = Written(given);
        string name = section.Kind == SectionKind.Sub && IsDigits(given) ? "L" + given : written;
        if (!string.Equals(written, given, StringComparison.OrdinalIgnoreCase))
        {
            write.Warning(DiagnosticCodes.SiemensNameChanged,
                $"The {(section.Kind == SectionKind.Sub ? "subprogram" : "program")} {given} is written as {name}: a "
                + "SINUMERIK name holds letters, digits and underscores (controllers siemens.md 1).");
        }

        // One file per unit and one unit per name (controllers siemens.md 1, 12 rule 1): a name another program or
        // subprogram of the file already has takes _2 behind it, as the framework names the files of two programs of
        // one name, so that no unit replaces another (language 2 rule 8).
        string unique = name;
        for (int count = 2; IsTaken(section.Kind, unique); count++)
        {
            unique = name + "_" + count.ToString(CultureInfo.InvariantCulture);
        }

        if (unique != name)
        {
            write.Warning(DiagnosticCodes.SiemensUnitNameTaken,
                $"The {(section.Kind == SectionKind.Sub ? "subprogram" : "program")} {given} is written as {unique}: "
                + $"another {(section.Kind == SectionKind.Sub ? "subprogram" : "program")} of the file is the unit "
                + $"{name}, and a SINUMERIK file holds one unit per name (controllers siemens.md 1, 12 rule 1).");
        }

        _unitNames.Add(section, unique);
        return unique;
    }

    /// <summary>
    /// The parameters of the PROC of a subprogram, the names of the ARG words its calls give, in the order they first
    /// stand in the file (controllers siemens.md 8; controller-mapping 6, CALL + ARG).
    /// </summary>
    public IReadOnlyList<string> ParametersOf(Section sub)
    {
        return sub.Name is string name && _parameters.TryGetValue(name, out List<string>? parameters)
            ? parameters
            : [];
    }

    /// <summary>
    /// The subprograms with parameters that the blocks of a program or subprogram call, which EXTERN announces in it
    /// (controllers siemens.md 8).
    /// </summary>
    public List<Section> CalledWithParameters(Section unit)
    {
        var called = new List<Section>();
        foreach (BlockStep step in _steps)
        {
            if (step.Section != unit || step.Block.Find("CALL")?.Value is not Value target)
            {
                continue;
            }

            if (SubNamed(target) is Section sub && ParametersOf(sub).Count > 0 && !called.Contains(sub))
            {
                called.Add(sub);
            }
        }

        return called;
    }

    /// <summary>
    /// The SINUMERIK label of an NCX label, NAME: at the block start (controllers siemens.md 8): an identifier whose
    /// first two characters are letters or underscores as it is, any other label, and a word of the language of siemens
    /// 8 and 9 such as LOOP, with LABEL_ in front.
    /// </summary>
    public static string LabelOf(Value label)
    {
        string text = label.ToCanonical();
        bool letters = text.Length >= 2 && IsLabelStart(text[0]) && IsLabelStart(text[1]);
        return label is IdentValue && letters && !s_keywords.Contains(text) ? text : LabelPrefix + text;
    }

    /// <summary>
    /// The label before the end of a program that JUMP=END goes to: PROGRAM_END, or PROGRAM_END_2 where the program has
    /// a label of that name (controller-mapping 1, JUMP=END).
    /// </summary>
    public string EndLabelOf(Section program)
    {
        if (_endLabels.TryGetValue(program, out string? known))
        {
            return known;
        }

        var labels = new List<string>();
        foreach (BlockStep step in _steps)
        {
            if (step.Section == program && step.Block.Find("LABEL")?.Value is Value label)
            {
                labels.Add(LabelOf(label));
            }
        }

        string name = EndLabel;
        for (int count = 2; labels.Contains(name); count++)
        {
            name = EndLabel + "_" + count.ToString(CultureInfo.InvariantCulture);
        }

        _endLabels.Add(program, name);
        return name;
    }

    /// <summary>
    /// True when a JUMP=END, or a RETURN outside a subprogram, stands in the program, so that its end carries the label
    /// (controller-mapping 1, JUMP=END; language 4.9, RETURN).
    /// </summary>
    public bool JumpsToEnd(Section program)
    {
        foreach (BlockStep step in _steps)
        {
            if (step.Section == program && (step.Block.Has("JUMP", null, "END") || step.Block.Has("RETURN")))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The block of a label in a section, the target of a JUMP or REPEAT; null when the section has none.
    /// </summary>
    public Block? LabelBlock(Section section, string label)
    {
        foreach (BlockStep step in _steps)
        {
            if (step.Section == section && step.Block.Find("LABEL")?.Value.ToCanonical() == label)
            {
                return step.Block;
            }
        }

        return null;
    }

    /// <summary>
    /// Records what the control has active at the jump over a conditional call, IF NOT (cond) GOTOF past the call
    /// (controller-mapping 6, structured loops).
    /// </summary>
    public void JumpOverCall(TargetState target)
    {
        _jumpsOverCalls.Add((target, new Dictionary<string, string>(target.Active, StringComparer.Ordinal)));
    }

    /// <summary>
    /// The first block written into a target state after a conditional call, once the subprogram has returned: the
    /// control arrives at the label after the call from the call, with what its walk wrote (virtual machine 3.9, D99),
    /// or from the jump over it, with what was active at the jump, so every value the call changed is unknown.
    /// </summary>
    public void MeetJumpsOverCalls(SiemensBlock write)
    {
        for (int index = _jumpsOverCalls.Count - 1; index >= 0; index--)
        {
            (TargetState target, Dictionary<string, string> atJump) = _jumpsOverCalls[index];
            if (!ReferenceEquals(target, write.Target))
            {
                continue;
            }

            _jumpsOverCalls.RemoveAt(index);
            foreach (string key in target.Active.Keys.ToList())
            {
                if (!atJump.TryGetValue(key, out string? value) || target.ActiveOf(key) != value)
                {
                    write.MakeUnknown(key);
                }
            }
        }
    }

    // The names of the ARG words of every CALL of a subprogram of the file, in file order (language 4.9, ARG).
    private void CollectParameters()
    {
        foreach (BlockStep step in _steps)
        {
            if (step.Block.Find("CALL")?.Value is not Value target || SubNamed(target)?.Name is not string name)
            {
                continue;
            }

            if (!_parameters.TryGetValue(name, out List<string>? parameters))
            {
                parameters = [];
                _parameters.Add(name, parameters);
            }

            foreach (Word word in step.Block.Words)
            {
                if (word.Key == "ARG" && word.Addr is string argument && !parameters.Contains(argument))
                {
                    parameters.Add(argument);
                }
            }
        }
    }

    // Letters, digits and underscores in capitals (controllers siemens.md 1).
    private static string Written(string name)
    {
        var text = new StringBuilder();
        foreach (char character in name.ToUpperInvariant())
        {
            text.Append(char.IsAsciiLetterOrDigit(character) || character == '_' ? character : '_');
        }

        return text.ToString();
    }

    // A unit name that a section of the same kind already has; names are case-insensitive (controllers siemens.md 1),
    // and every name is written in capitals.
    private bool IsTaken(SectionKind kind, string name)
    {
        foreach (KeyValuePair<Section, string> unit in _unitNames)
        {
            if (unit.Key.Kind == kind && unit.Value == name)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDigits(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (char character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLabelStart(char character)
    {
        return char.IsAsciiLetter(character) || character == '_';
    }
}
