using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The subprograms and their calls (controllers siemens.md 8, 11 rule 6; controller-mapping 1 and 6; language 4.9):
/// PROC NAME(...) declares a subprogram whose parameters are the ARG names of its calls, EXTERN announces one outside
/// the file, L100, NAME, NAME(1, , 3), NAME P3 and CALL "NAME" call it as CALL=NAME with TIMES and ARG, EXTCALL calls a
/// program outside the file as CALL="NAME". A subprogram runs with the state of its callers, and a caller continues
/// with what its subprogram may change unknown (virtual machine 3.9).
/// </summary>
internal static class SiemensCalls
{
    // The marker of the feed among the facts a subprogram may change.
    private const string FeedChange = "F";

    // The commands of the groups whose state a subprogram changes, by the fact (controllers siemens.md 2).
    private static readonly Dictionary<string, string> s_keywordFacts = KeywordFacts();

    /// <summary>
    /// The names a block calls, for counting the calls of each subprogram of the file: L100 as L100, a name alone or
    /// with its arguments, CALL "NAME"; a name after MCALL is no call.
    /// </summary>
    /// <param name="block">A source block.</param>
    public static List<string> NamesCalledBy(SourceBlock block)
    {
        var names = new List<string>();
        SourceWord? statement = SiemensUnits.StatementOf(block);
        if (statement?.Address == "CALL")
        {
            if (SiemensArguments.StringOf(statement.Text) is string called)
            {
                names.Add(called.ToUpperInvariant());
            }

            return names;
        }

        foreach (SourceWord word in block.Words)
        {
            if (word.Address == "MCALL")
            {
                break;
            }

            if (word.Address == "L" && word.Number is not null)
            {
                names.Add("L" + word.Text);
            }
            else if (IsName(word))
            {
                names.Add(word.Address);
            }
        }

        return names;
    }

    /// <summary>
    /// Reads the declaration of a subprogram, EXTERN, CALL, and the calls of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        SourceWord? statement = SiemensUnits.StatementOf(block.Source);
        switch (statement?.Address)
        {
            case "PROC":
                ReadProc(block);
                return;
            case "EXTERN":
                ReadExtern(block, statement);
                return;
            case "CALL":
                ReadIndirect(block, statement);
                return;
        }

        if (block.Find("MCALL") is not null)
        {
            return;
        }

        foreach (SourceWord word in block.Unread())
        {
            if (word.Address == "L" && word.Number is not null && word.Expression is null)
            {
                Call(block, word, "L" + word.Text, null);
            }
            else if (word.Address == "EXTCALL" && word.Text.StartsWith('('))
            {
                ReadExtcall(block, word);
            }
            else if (IsName(word) && (block.Siemens.Units.FindSub(word.Address) is not null
                || block.Siemens.Externs.Contains(word.Address)))
            {
                Call(block, word, word.Address, word.Text.StartsWith('(') ? word.Text : null);
            }
        }
    }

    /// <summary>
    /// After the blocks of a call are written: the caller's state enters the subprogram, and the caller continues with
    /// what the subprogram may change unknown; where the tool stands is unknown, also after a program outside the file,
    /// which is not followed (virtual machine 3.9). A call with TIMES runs each further pass from the state the pass
    /// before left.
    /// </summary>
    /// <param name="block">The block that was read.</param>
    public static void Enter(SiemensBlock block)
    {
        SiemensState state = block.Siemens;
        if (block.CallsOutside)
        {
            state.ForgetPositions();
            state.Facts.Written.Clear();
        }

        foreach (KeyValuePair<string, int> call in block.Calls)
        {
            HashSet<string> changes = ChangesOf(state, call.Key, new HashSet<string>(StringComparer.Ordinal));
            SiemensFacts caller = state.Facts.Copy();
            if (call.Value > 1)
            {
                Apply(changes, caller);
            }

            state.RecordCall(call.Key, caller);
            Apply(changes, state.Facts);
            state.ForgetPositions();
            state.Facts.Written.Clear();
        }
    }

    /// <summary>
    /// Makes unknown what a block may change that the reader keeps as RAW without reading it, a block of a structure
    /// kept as RAW: the control may or may not run it, and the reader does not know which (D5; virtual machine 3.9).
    /// </summary>
    /// <param name="block">The block kept as RAW.</param>
    public static void ForgetChangesOf(SiemensBlock block)
    {
        SiemensState state = block.Siemens;
        var changes = new HashSet<string>(StringComparer.Ordinal);
        AddChanges(block.Source, changes);
        foreach (string called in NamesCalledBy(block.Source))
        {
            changes.UnionWith(ChangesOf(state, called, new HashSet<string>(StringComparer.Ordinal)));
        }

        Apply(changes, block.Facts);
    }

    // The PROC of a subprogram carries its name and its parameters, which the SUB section and the calls take; the
    // flags SBLOF, DISPLOF and SAVE are kept as RAW flags of the section (controller-mapping 6, SUB=BEGIN).
    private static void ReadProc(SiemensBlock block)
    {
        block.MarkAllRead();
        SiemensUnit? unit = block.Unit;
        bool declares = unit is not null && (unit.DeclarationLine == block.Line || ReferenceEquals(unit.Begin,
            block.Source));
        if (!declares || unit!.Proc is not SiemensProc proc)
        {
            block.Draft.KeepAsRaw("PROC stands inside a unit, where it declares no subprogram "
                + "(controllers siemens.md 8)");
            return;
        }

        if (proc.Unreadable)
        {
            block.Draft.KeepAsRaw("the parameters of the PROC are none the reader reads (controllers siemens.md 8)");
        }
        else if (proc.Flags.Count > 0)
        {
            block.Draft.KeepAsRaw($"{string.Join(" ", proc.Flags)} of the PROC have no NCX word and are kept as RAW "
                + "flags of the section (controller-mapping 6, SUB=BEGIN)");
            block.Draft.RawKeepsState = true;
        }
    }

    // EXTERN NAME(REAL, INT) announces a subprogram with parameters in the caller (controllers siemens.md 8): one of
    // the file needs no announcement in NCX, its SUB section carries the parameters; one outside the file is kept as
    // RAW for the control, and its calls name their arguments by position.
    private static void ReadExtern(SiemensBlock block, SourceWord statement)
    {
        block.MarkAllRead();
        int end = SiemensScanner.ReadIdentifier(statement.Text, 0);
        string name = end < 0 ? "" : statement.Text.Substring(0, end).ToUpperInvariant();
        if (name.Length > 0 && block.Siemens.Units.FindSub(name) is not null)
        {
            return;
        }

        if (name.Length > 0)
        {
            block.Siemens.Externs.Add(name);
        }

        block.Draft.KeepAsRaw("EXTERN announces a subprogram outside the file to the control "
            + "(controllers siemens.md 8)");
        block.Draft.RawKeepsState = true;
    }

    // CALL "NAME" calls the subprogram by its name; CALL with a variable and CALL BLOCK are RAW (controller-mapping 6,
    // CALL).
    private static void ReadIndirect(SiemensBlock block, SourceWord statement)
    {
        if (SiemensArguments.StringOf(statement.Text) is not string name)
        {
            block.MarkAllRead();
            block.Draft.KeepAsRaw("CALL with a variable or CALL BLOCK is an indirect call NCX has no word for "
                + "(controller-mapping 6, CALL)");
            return;
        }

        Call(block, statement, name.ToUpperInvariant(), null);
    }

    // EXTCALL("path/NAME") calls a program from an external drive: CALL="NAME" (controller-mapping 6, CALL).
    private static void ReadExtcall(SiemensBlock block, SourceWord word)
    {
        block.MarkRead(word);
        List<string> arguments = SiemensArguments.Split(word.Text);
        string? path = arguments.Count == 1 ? SiemensArguments.StringOf(arguments[0]) : null;
        if (path is null)
        {
            block.Draft.KeepAsRaw("EXTCALL names no program as a string");
            return;
        }

        // The file name of a path on the control is _N_NAME_SPF or _N_NAME_MPF; the program is NAME (controllers
        // siemens.md 1).
        int slash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        string name = path.Substring(slash + 1);
        if (name.StartsWith("_N_", StringComparison.OrdinalIgnoreCase) && name.Length > 7
            && (name.EndsWith("_SPF", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("_MPF", StringComparison.OrdinalIgnoreCase)))
        {
            name = name.Substring(3, name.Length - 7);
        }

        block.Draft.AddState("CALL", null, new StringValue(name));
        block.CallsOutside = true;
    }

    // CALL=NAME for a subprogram of the file, CALL="NAME" for a program outside it; P repeats the call, TIMES; the
    // arguments by the names of the PROC, an empty position with the default of the PROC (controller-mapping 6, CALL,
    // TIMES and ARG).
    private static void Call(SiemensBlock block, SourceWord word, string name, string? arguments)
    {
        block.MarkRead(word);
        SiemensUnit? sub = block.Siemens.Units.FindSub(name);
        Value? target = sub is null ? new StringValue(name) : SectionName(name);
        if (target is null)
        {
            block.Draft.KeepAsRaw($"the subprogram {name} has a name NCX cannot call as a section of the file "
                + "(language "
                + "4.9, CALL)");
            return;
        }

        // A call without arguments leaves every position empty, which takes the default of the PROC (controllers
        // siemens.md 8).
        var words = new List<Word> { new() { Key = "CALL", Value = target } };
        string? given = arguments ?? (sub?.Proc?.Parameters.Count > 0 ? "()" : null);
        if (given is not null && !AddArguments(block, sub, name, given, words))
        {
            return;
        }

        int passes = 1;
        if (block.Take("P") is SourceWord repeat)
        {
            int? count = repeat.Number is not null ? SiemensNumbers.WholeNumber(repeat.Text) : null;
            if (count is not int times || times < 1)
            {
                block.Draft.KeepAsRaw("P of a call counts its passes as a whole number (controller-mapping 6, TIMES)");
                return;
            }

            passes = times;
            words.Add(new Word { Key = "TIMES", Value = new IntegerValue(times, repeat.Text) });
        }

        foreach (Word callWord in words)
        {
            block.Draft.Main.Add(callWord.Key, callWord.Addr, callWord.Value);
        }

        if (sub is null)
        {
            block.CallsOutside = true;
        }
        else
        {
            block.Calls.Add(new KeyValuePair<string, int>(name, passes));
        }
    }

    // The arguments of a call: by the names of the parameters of the PROC of the file, else by position ARG:P1 for a
    // subprogram EXTERN announces; a VAR parameter writes back to the caller and is no ARG (D149); a string is no value
    // of ARG (language 4.9).
    private static bool AddArguments(SiemensBlock block, SiemensUnit? sub, string name, string arguments,
        List<Word> words)
    {
        List<string> values = SiemensArguments.Split(arguments);
        IReadOnlyList<SiemensParameter>? parameters = sub?.Proc?.Parameters;
        if (sub is not null && (parameters is null || values.Count > parameters.Count || sub.Proc!.Unreadable))
        {
            block.Draft.KeepAsRaw($"the call gives {name} more arguments than its PROC declares "
                + "(controllers siemens.md "
                + "8)");
            return false;
        }

        int count = parameters?.Count ?? values.Count;
        for (int index = 0; index < count; index++)
        {
            string value = index < values.Count ? values[index] : "";
            SiemensParameter? parameter = parameters?[index];
            if (parameter is { ByReference: true } && value.Length > 0)
            {
                // TODO(question): D149, a VAR parameter writes back to the caller and is no ARG; the call stays RAW.
                block.Draft.KeepAsRaw($"{parameter.Name} of {name} is a VAR parameter, which writes back to the caller "
                    + "and is no ARG (D149)");
                return false;
            }

            string? written = value.Length > 0 ? value : parameter?.Default;
            if (written is null)
            {
                continue;
            }

            Value? argument = SiemensArguments.StringOf(written) is null
                ? SiemensExpression.ValueOf(block, written, out string? problem)
                : null;
            if (argument is null)
            {
                block.Draft.KeepAsRaw($"the argument {written} of {name} has no NCX value (language 4.9, ARG)");
                return false;
            }

            string argumentName = parameter?.Name ?? "P" + (index + 1).ToString(CultureInfo.InvariantCulture);
            words.Add(new Word { Key = "ARG", Addr = argumentName, Value = argument });
        }

        return true;
    }

    // The NAME of the SUB section of a subprogram as the structure pass writes it: an identifier, or a number where the
    // name is digits (language 4.1, D90); a name of another form cannot be called as a section.
    private static Value? SectionName(string name)
    {
        if (SiemensUnits.IsWritable(name))
        {
            return new IdentValue(name);
        }

        return long.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out long number)
            ? new IntegerValue(number, name)
            : null;
    }

    // What a subprogram of the file, or the SUB section of a repeated range, may change, worked out once: the facts of
    // the words of its blocks and of the subprograms it calls; SAVE restores the modal G codes of the caller at the
    // return (controllers siemens.md 8; controller-mapping 6, REPEAT + TIMES).
    private static HashSet<string> ChangesOf(SiemensState state, string name, HashSet<string> visited)
    {
        if (state.Changes.TryGetValue(name, out HashSet<string>? known))
        {
            return known;
        }

        var changes = new HashSet<string>(StringComparer.Ordinal);
        SiemensUnit? sub = state.Units.FindSub(name);
        IReadOnlyList<SourceBlock>? blocks = sub?.Blocks
            ?? (state.RepeatSections.TryGetValue(name, out IReadOnlyList<SourceBlock>? range) ? range : null);
        if (blocks is null || !visited.Add(name))
        {
            return changes;
        }

        foreach (SourceBlock block in blocks)
        {
            AddChanges(block, changes);
            foreach (string called in NamesCalledBy(block))
            {
                if (state.Units.FindSub(called) is not null)
                {
                    changes.UnionWith(ChangesOf(state, called, visited));
                }
            }
        }

        if (sub?.Proc?.Saves == true)
        {
            changes.ExceptWith([SiemensFacts.Motion, SiemensFacts.Distance, SiemensFacts.Plane, SiemensFacts.Feed,
                SiemensFacts.Diameter]);
        }

        state.Changes[name] = changes;
        return changes;
    }

    // The facts a block may change.
    private static void AddChanges(SourceBlock block, HashSet<string> changes)
    {
        foreach (SourceWord word in block.Words)
        {
            string? command = SiemensGroups.CommandOf(word);
            int? group = command is null ? null : SiemensGroups.GroupOf(command);
            string? fact = group switch
            {
                SiemensGroups.Motion => SiemensFacts.Motion,
                14 => SiemensFacts.Distance,
                SiemensGroups.Plane => SiemensFacts.Plane,
                SiemensGroups.FeedType => SiemensFacts.Feed,
                SiemensGroups.Diameter => SiemensFacts.Diameter,
                SiemensGroups.Frames or SiemensGroups.Datum => SiemensFacts.Frames,
                25 => SiemensFacts.Orientation,
                _ => null,
            };
            if (fact is not null)
            {
                changes.Add(fact);
            }

            if (command is "G96" or "G961" or "G962" or "G97" or "G971" or "G972" or "G973")
            {
                changes.Add(SiemensFacts.Spindles);
            }

            if (command is "G110" or "G111" or "G112")
            {
                changes.Add(SiemensFacts.Pole);
            }

            AddWordChanges(word, changes);
        }
    }

    private static void AddWordChanges(SourceWord word, HashSet<string> changes)
    {
        string address = word.Address;
        if (s_keywordFacts.TryGetValue(address, out string? fact))
        {
            changes.Add(fact);
        }

        string? code = SiemensBlock.CodeOf(word);
        if (address == "S" || code is "M3" or "M4" or "M5" or "M19" or "M70"
            || (address.Length > 1 && address[0] is 'S' or 'M' && char.IsAsciiDigit(address[1])))
        {
            changes.Add(SiemensFacts.Spindles);
        }

        if (address == "T" || code == "M6"
            || (address.Length > 1 && address[0] == 'T' && char.IsAsciiDigit(address[1])))
        {
            changes.Add(SiemensFacts.Preload);
        }

        if (address is "F" or "FB")
        {
            changes.Add(FeedChange);
        }
    }

    private static void Apply(HashSet<string> changes, SiemensFacts facts)
    {
        foreach (string change in changes)
        {
            if (change == FeedChange)
            {
                facts.ControlFeed = null;
                facts.WrittenFeed = null;
            }
            else
            {
                facts.MakeUnknown(change);
            }
        }
    }

    // A name alone or with its argument list, the form of a subprogram call (controllers siemens.md 8).
    private static bool IsName(SourceWord word)
    {
        return word.Address.Length > 1 && char.IsAsciiLetter(word.Address[0]) && !word.Address.Contains('[')
            && (word.Text.Length == 0 || word.Text.StartsWith('('));
    }

    private static Dictionary<string, string> KeywordFacts()
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string keyword in new[] { "DIAMON", "DIAMOF", "DIAM90" })
        {
            facts[keyword] = SiemensFacts.Diameter;
        }

        foreach (string keyword in new[] { "TRANS", "ATRANS", "ROT", "AROT", "MIRROR", "AMIRROR", "SCALE", "ASCALE",
            "ROTS", "AROTS", "CYCLE800" })
        {
            facts[keyword] = SiemensFacts.Frames;
        }

        foreach (string keyword in new[] { "TRAORI", "TRAFOOF", "TRACYL", "TRANSMIT" })
        {
            facts[keyword] = SiemensFacts.Transform;
        }

        foreach (string keyword in new[] { "SETMS", "SPOS", "SPOSA", "LIMS", "COUPON", "COUPOF" })
        {
            facts[keyword] = SiemensFacts.Spindles;
        }

        facts["CIP"] = SiemensFacts.Motion;
        facts["CT"] = SiemensFacts.Motion;
        facts["MCALL"] = SiemensFacts.Cycle;
        facts["RNDM"] = SiemensFacts.Rounding;
        return facts;
    }
}
