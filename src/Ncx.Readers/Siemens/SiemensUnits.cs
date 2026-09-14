using System.Globalization;
using System.Text.RegularExpressions;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The units of a SINUMERIK file and what the structure pass needs of them (controllers siemens.md 1, 8, 11 rules 1
/// and 6; controller-mapping 1 and 6; language 4.13): every %_N_NAME_MPF is a program, every %_N_NAME_SPF and every
/// PROC unit a subprogram, every _INI unit a program of RAW blocks; the labels of each unit, the labels its jumps
/// enter, and the calls of the subprograms of the file.
/// </summary>
internal sealed partial class SiemensUnits
{
    private readonly Dictionary<int, SiemensUnit> _unitOfLine = [];

    /// <summary>
    /// The units in file order.
    /// </summary>
    public List<SiemensUnit> Units { get; } = [];

    /// <summary>
    /// How many blocks of the file call each subprogram of the file, by its name.
    /// </summary>
    public Dictionary<string, int> CallCounts { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Lays out the units of the blocks of a file.
    /// </summary>
    /// <param name="blocks">Every block of the file in file order.</param>
    public static SiemensUnits Of(IReadOnlyList<SourceBlock> blocks)
    {
        var units = new SiemensUnits();
        SiemensUnit? current = null;
        foreach (SourceBlock block in blocks)
        {
            if (block.IsTrivia)
            {
                continue;
            }

            SourceWord first = block.Words[0];
            if (first.Address == "%")
            {
                current = Header(block, first.Text);
            }
            else if (StatementOf(block)?.Address == "PROC")
            {
                SiemensProc proc = ParseProc(StatementOf(block)!.Text);
                if (current is { Kind: SiemensUnitKind.Sub, Proc: null } && current.Blocks.Count == 0)
                {
                    current.Proc = proc;
                    current.DeclarationLine = block.Line;
                    units.Add(current, block);
                    continue;
                }

                current = new SiemensUnit { Kind = SiemensUnitKind.Sub, Name = proc.Name, Begin = block, Proc = proc };
            }
            else if (current is null)
            {
                current = new SiemensUnit { Kind = SiemensUnitKind.Program };
                current.Blocks.Add(block);
            }
            else
            {
                current.Blocks.Add(block);
            }

            if (units.Units.Count == 0 || !ReferenceEquals(units.Units[^1], current))
            {
                units.Units.Add(current);
            }

            units._unitOfLine[block.Line] = current;
        }

        foreach (SiemensUnit unit in units.Units)
        {
            units.FindLabels(unit);
        }

        return units;
    }

    /// <summary>
    /// The unit a block stands in; null for trivia before the first block.
    /// </summary>
    public SiemensUnit? UnitOf(SourceBlock block)
    {
        return _unitOfLine.TryGetValue(block.Line, out SiemensUnit? unit) ? unit : null;
    }

    /// <summary>
    /// The subprogram of the file with this name; null when the file holds none.
    /// </summary>
    /// <param name="name">The name in capitals.</param>
    public SiemensUnit? FindSub(string name)
    {
        foreach (SiemensUnit unit in Units)
        {
            if (unit.Kind == SiemensUnitKind.Sub && unit.Name == name)
            {
                return unit;
            }
        }

        return null;
    }

    /// <summary>
    /// The statement of a block: its first word after the N number and the label; null for a block of those alone.
    /// </summary>
    public static SourceWord? StatementOf(SourceBlock block)
    {
        foreach (SourceWord word in block.Words)
        {
            if (word.Address is not ("N" or ":"))
            {
                return word;
            }
        }

        return null;
    }

    /// <summary>
    /// The label a block carries: NAME of NAME:, in capitals; null for a block without one.
    /// </summary>
    public static string? LabelOf(SourceBlock block)
    {
        return block.Find(":")?.Text;
    }

    /// <summary>
    /// The block number of a block as a label, N300 of N0300, which a jump may name (controllers siemens.md 8);
    /// null for
    /// a block without one.
    /// </summary>
    public static string? NumberLabelOf(SourceBlock block)
    {
        return block.Find("N") is SourceWord number ? NumberLabel(number.Text) : null;
    }

    /// <summary>
    /// A label NCX can write: an identifier other than END, which JUMP=END reserves (language 4.9).
    /// </summary>
    public static bool IsWritable(string label)
    {
        return label != "END" && WritableLabel().IsMatch(label);
    }

    /// <summary>
    /// The labels the jumps of a block name, in capitals: GOTOF, GOTOB, GOTO, GOTOC alone or after IF, the cases of
    /// CASE, the labels of REPEAT and REPEATB (controllers siemens.md 8); a computed target names none.
    /// </summary>
    public static List<string> TargetsOf(SourceBlock block)
    {
        var targets = new List<string>();
        if (StatementOf(block) is not SourceWord statement)
        {
            return targets;
        }

        List<string> tokens = Tokens(statement.Text);
        switch (statement.Address)
        {
            case "GOTOF" or "GOTOB" or "GOTO" or "GOTOC":
                AddTarget(targets, tokens, 0);
                break;
            case "IF" or "CASE":
                for (int index = 0; index + 1 < tokens.Count; index++)
                {
                    if (tokens[index] is "GOTOF" or "GOTOB" or "GOTO" or "GOTOC")
                    {
                        AddTarget(targets, tokens, index + 1);
                    }
                }

                break;
            case "REPEAT" or "REPEATB":
                for (int index = 0; index < tokens.Count && index < 2 && tokens[index] != "P"; index++)
                {
                    AddTarget(targets, tokens, index);
                }

                break;
        }

        return targets;
    }

    /// <summary>
    /// True for GOTOS, alone or after IF, which jumps to the start of the program (controllers siemens.md 8).
    /// </summary>
    public static bool IsJumpToStart(SourceBlock block)
    {
        SourceWord? statement = StatementOf(block);
        return statement?.Address == "GOTOS"
            || (statement?.Address == "IF" && Tokens(statement.Text).Contains("GOTOS"));
    }

    /// <summary>
    /// The blocks the unit of a block holds after it, in file order.
    /// </summary>
    public SourceBlock? NextInUnit(SourceBlock block)
    {
        if (UnitOf(block) is not SiemensUnit unit)
        {
            return null;
        }

        int index = unit.Blocks.IndexOf(block);
        return index >= 0 && index + 1 < unit.Blocks.Count ? unit.Blocks[index + 1] : null;
    }

    /// <summary>
    /// The block number as a label, "N" and the number without leading zeros.
    /// </summary>
    public static string NumberLabel(string digits)
    {
        string trimmed = digits.TrimStart('0');
        return "N" + (trimmed.Length == 0 ? "0" : trimmed);
    }

    // PROC NAME(REAL LENGTH=10, INT N, VAR REAL RESULT) SBLOF DISPLOF (controllers siemens.md 8).
    private static SiemensProc ParseProc(string text)
    {
        int nameEnd = SiemensScanner.ReadIdentifier(text, 0);
        if (nameEnd < 0)
        {
            return new SiemensProc { Name = "", Unreadable = true };
        }

        string name = text.Substring(0, nameEnd).ToUpperInvariant();
        int position = SiemensScanner.SkipBlanks(text, nameEnd);
        var parameters = new List<SiemensParameter>();
        bool unreadable = false;
        if (position < text.Length && text[position] == '(')
        {
            int end = SiemensScanner.ReadGroup(text, position);
            if (end < 0)
            {
                return new SiemensProc { Name = name, Unreadable = true };
            }

            foreach (string declaration in SiemensArguments.Split(text.Substring(position, end - position)))
            {
                SiemensParameter? parameter = ParseParameter(declaration);
                unreadable |= parameter is null;
                if (parameter is not null)
                {
                    parameters.Add(parameter);
                }
            }

            position = end;
        }

        var flags = new List<string>();
        foreach (string flag in text.Substring(position).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            flags.Add(flag.ToUpperInvariant());
        }

        return new SiemensProc { Name = name, Parameters = parameters, Flags = flags, Unreadable = unreadable };
    }

    // [VAR] TYPE NAME [= default]: REAL LENGTH=10, INT N, VAR REAL RESULT, STRING[32] TEXT.
    private static SiemensParameter? ParseParameter(string declaration)
    {
        Match match = Parameter().Match(declaration.Trim());
        if (!match.Success)
        {
            return null;
        }

        string? defaultValue = match.Groups[4].Success ? match.Groups[4].Value.Trim() : null;
        return new SiemensParameter(match.Groups[3].Value.ToUpperInvariant(), match.Groups[2].Value.ToUpperInvariant(),
            match.Groups[1].Success, defaultValue);
    }

    private static SiemensUnit Header(SourceBlock block, string header)
    {
        Match match = UnitHeader().Match(header);
        if (!match.Success)
        {
            return new SiemensUnit { Kind = SiemensUnitKind.Program, Begin = block, Name = null };
        }

        SiemensUnitKind kind = match.Groups[2].Value.ToUpperInvariant() switch
        {
            "SPF" => SiemensUnitKind.Sub,
            "INI" => SiemensUnitKind.Ini,
            _ => SiemensUnitKind.Program,
        };
        return new SiemensUnit { Kind = kind, Begin = block, Name = match.Groups[1].Value.ToUpperInvariant() };
    }

    private void Add(SiemensUnit unit, SourceBlock block)
    {
        _unitOfLine[block.Line] = unit;
    }

    // The labels of a unit, the labels its jumps enter, and the labels whose jumps end the program; the calls of the
    // subprograms of the file (controllers siemens.md 8; controller-mapping 1, JUMP=END).
    private void FindLabels(SiemensUnit unit)
    {
        foreach (SourceBlock block in unit.Blocks)
        {
            if (LabelOf(block) is string label)
            {
                unit.Labels.Add(label);
            }

            if (NumberLabelOf(block) is string number)
            {
                unit.Labels.Add(number);
            }

            if (IsJumpToStart(block))
            {
                unit.JumpsToStart = true;
            }

            foreach (string name in SiemensCalls.NamesCalledBy(block))
            {
                CallCounts[name] = CallCounts.GetValueOrDefault(name) + 1;
            }
        }

        // A repeat whose range becomes a SUB section jumps to none of its labels; the structure pass finds the range by
        // the labels of its first and its last block (controller-mapping 6, REPEAT + TIMES).
        for (int index = 0; index < unit.Blocks.Count; index++)
        {
            if (SiemensRepeat.Of(this, unit, index) is not SiemensRepeat repeat)
            {
                continue;
            }

            unit.Repeats[unit.Blocks[index]] = repeat;
            if (repeat.IsRange)
            {
                unit.RangeLabels.TryAdd(repeat.Blocks[0], repeat.FirstLabel);
                unit.RangeLabels.TryAdd(repeat.Blocks[^1], repeat.LastLabel);
            }
        }

        foreach (SourceBlock block in unit.Blocks)
        {
            if (unit.Repeats.TryGetValue(block, out SiemensRepeat? range) && range.IsRange)
            {
                continue;
            }

            foreach (string target in TargetsOf(block))
            {
                if (!unit.Labels.Contains(target) || !IsWritable(target))
                {
                    continue;
                }

                if (IsBeforeTheEnd(unit, target))
                {
                    unit.EndTargets.Add(target);
                }
                else
                {
                    unit.Targets.Add(target);
                }
            }
        }

        unit.EndTargets.ExceptWith(unit.Targets);
    }

    // The block of the label holds nothing but its number and the label, and the next block of the unit only its
    // number and M30 or M2, or the block of the label holds them itself: a jump to it ends the program.
    private static bool IsBeforeTheEnd(SiemensUnit unit, string target)
    {
        for (int index = 0; index < unit.Blocks.Count; index++)
        {
            SourceBlock block = unit.Blocks[index];
            if (LabelOf(block) != target && NumberLabelOf(block) != target)
            {
                continue;
            }

            if (HoldsOnly(block, endAllowed: true) is bool ends)
            {
                return ends || (index + 1 < unit.Blocks.Count
                    && HoldsOnly(unit.Blocks[index + 1], endAllowed: true) == true);
            }

            return false;
        }

        return false;
    }

    // True when the block holds its number, its label and M30 or M2; false when it holds only its number and label;
    // null when it holds anything else or a block skip.
    private static bool? HoldsOnly(SourceBlock block, bool endAllowed)
    {
        if (block.BlockSkip)
        {
            return null;
        }

        bool end = false;
        foreach (SourceWord word in block.Words)
        {
            if (word.Address is "N" or ":")
            {
                continue;
            }

            if (endAllowed && !end && word.Address == "M" && NativeCode.Of(word) is "M30" or "M2")
            {
                end = true;
                continue;
            }

            return null;
        }

        return end;
    }

    private static void AddTarget(List<string> targets, List<string> tokens, int index)
    {
        if (index >= tokens.Count)
        {
            return;
        }

        string token = tokens[index];
        if (token.Length > 1 && token[0] == 'N' && IsDigits(token.Substring(1)))
        {
            targets.Add(NumberLabel(token.Substring(1)));
        }
        else if (SiemensScanner.ReadIdentifier(token, 0) == token.Length)
        {
            targets.Add(token);
        }
    }

    // The words of a statement in capitals, strings and groups kept whole.
    private static List<string> Tokens(string text)
    {
        var tokens = new List<string>();
        int position = 0;
        while (position < text.Length)
        {
            position = SiemensScanner.SkipBlanks(text, position);
            if (position >= text.Length)
            {
                break;
            }

            int end = text[position] switch
            {
                '"' => SiemensScanner.ReadString(text, position),
                '(' or '[' => SiemensScanner.ReadGroup(text, position),
                _ => SiemensScanner.StartsIdentifier(text[position])
                    ? SiemensScanner.ReadIdentifier(text, position)
                    : position + 1,
            };
            end = end < 0 ? text.Length : end;
            tokens.Add(text.Substring(position, end - position).ToUpperInvariant());
            position = end;
        }

        return tokens;
    }

    private static bool IsDigits(string text)
    {
        foreach (char character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return text.Length > 0 && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    // _N_SHAFT_MPF, _N_TH1_HS_01_SPF, _N_INDEX_INI (controllers siemens.md 1).
    [GeneratedRegex(@"^_N_(.+)_(MPF|SPF|INI)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex UnitHeader();

    [GeneratedRegex(@"^(VAR\s+)?([A-Za-z]+(?:\[[0-9]+\])?)\s+([A-Za-z_][A-Za-z0-9_]*)\s*(?:=\s*(.+))?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Parameter();

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex WritableLabel();
}
