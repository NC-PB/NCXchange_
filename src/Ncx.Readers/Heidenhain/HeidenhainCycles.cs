using System.Globalization;
using System.Text.RegularExpressions;
using Ncx.Core.Catalog;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The machining cycles of a Klartext program (controllers heidenhain.md 5, 7 rules 7 and 9; controller-mapping 5;
/// language 4.7, 4.7.1; machine-config 6): a CYCL DEF becomes a CYCLE block with the coordinates along the tool axis
/// made absolute from Q203, CYCL CALL, M99 and CYCL CALL PAT become CYCLE_CALL blocks, a PATTERN DEF expands into one
/// call per point. The definition stays active until the next CYCL DEF; there is no cancel word, so the reader writes
/// CYCLE=OFF before the next non-cycle motion (HeidenhainReader) and the definition again before a later call.
/// </summary>
internal static partial class HeidenhainCycles
{
    /// <summary>
    /// Tells whether a cycle number is one of the cycles that act where they stand and are never called: 7, 8, 9, 10,
    /// 19, 32 and 247 (controllers heidenhain.md 2, 3, 5).
    /// </summary>
    /// <param name="number">The cycle number.</param>
    public static bool IsFrameCycle(int number)
    {
        return number is 7 or 8 or 9 or 10 or 19 or 32 or 247;
    }

    /// <summary>
    /// The number of the cycle acting where it stands that a CYCL DEF block defines, 7 of CYCL DEF 7.1; null for every
    /// other CYCL DEF, a cycle whose number the reader cannot read included, which replaces the active machining
    /// definition (controllers heidenhain.md 5).
    /// </summary>
    /// <param name="block">A CYCL DEF block.</param>
    public static int? FrameCycleOf(SourceBlock block)
    {
        return block.Words.Count > 2 && HeidenhainFrameCycles.NumberOf(block.Words[2], out int number, out _)
            && IsFrameCycle(number)
            ? number
            : null;
    }

    /// <summary>
    /// Keeps a CYCL DEF of a machining cycle as RAW (heidenhain 7 rule 9) and makes it the active definition all the
    /// same: the definition stays active until the next CYCL DEF (controllers heidenhain.md 5), so the CYCL CALL, M99
    /// and CYCL CALL PAT after it stay RAW with it and call no definition before it.
    /// </summary>
    /// <param name="block">The block being read, CYCL DEF with its words, all of them read.</param>
    /// <param name="native">The cycle number, "12" of CYCL DEF 12.0; the word after CYCL DEF as written where it is no
    /// cycle number, empty where there is none.</param>
    /// <param name="reason">Why the definition is kept as RAW.</param>
    public static void DefineAsRaw(HeidenhainBlock block, string native, string reason)
    {
        Activate(block, new HeidenhainDefinition { Native = native, RawReason = reason });
    }

    /// <summary>
    /// Reads CYCL DEF n of a machining cycle into its CYCLE block (heidenhain 7 rule 7).
    /// </summary>
    /// <param name="block">The block being read, CYCL DEF with its Q parameters.</param>
    /// <param name="number">The cycle number.</param>
    public static void ReadDefinition(HeidenhainBlock block, int number)
    {
        block.MarkAllRead();
        string native = number.ToString(CultureInfo.InvariantCulture);
        var parameters = new List<SourceWord>();
        foreach (SourceWord word in block.Source.Words)
        {
            if (word.Address.Length > 1 && word.Address[0] == 'Q' && char.IsAsciiDigit(word.Address[1]))
            {
                parameters.Add(word);
            }
        }

        // The reader turns the cycle into the entry of the catalog that maps it; of two entries of one cycle, DRILL and
        // DRILL_DWELL on cycle 200, PECK and CHIP_BREAK on cycle 203, the first
        // (controller-mapping 5; machine-config 6).
        // TODO(question): which Q211 makes a cycle 200 a DRILL and which Q256 or Q213 makes a cycle 203 a CHIP_BREAK
        // is open (wave-1 questions #20 and #72); the first entry is taken.
        CycleEntry? entry = block.Machine.CycleCatalog.FindNative(native);
        HeidenhainDefinition definition = entry is null
            ? new HeidenhainDefinition
            {
                Native = native,
                RawReason = $"cycle {native} has no entry in the cycle catalog of the machine (machine-config 6)",
            }
            : Define(block, entry, native, parameters);
        if (!Activate(block, definition))
        {
            return;
        }

        foreach (Word word in definition.Words)
        {
            block.Draft.AddState(word.Key, word.Addr, word.Value);
        }

        block.Heidenhain.CycleOn = true;
        block.Heidenhain.CycleCalled = false;
    }

    /// <summary>
    /// Reads CYCL CALL, the call at the current position, and CYCL CALL PAT, one call per point of the PATTERN DEF
    /// (heidenhain 7 rule 7).
    /// </summary>
    /// <param name="block">The block being read, whose first words are CYCL CALL.</param>
    public static void ReadCall(HeidenhainBlock block)
    {
        block.MarkLeading(2);
        string form = block.Keyword(2);
        if (form == "POS")
        {
            block.Draft.KeepAsRaw("heidenhain 7 rule 7 reads CYCL CALL, M99 and CYCL CALL PAT as calls; CYCL CALL POS "
                + "is kept as RAW");
            return;
        }

        if (form == "PAT")
        {
            block.MarkLeading(3);
            ReadPatternCall(block);
            return;
        }

        Call(block, block.Draft.Main);
    }

    /// <summary>
    /// Reads M99 in a positioning block, the call at the position of the block (controllers heidenhain.md 5): the rapid
    /// positioning in the plane is the CYCLE_CALL with its axis words (virtual machine 3.3); a positioning at the feed,
    /// in the machine frame or along the tool axis stays a motion of its own, and the bare CYCLE_CALL follows it.
    /// </summary>
    /// <param name="block">The block being read, after its motion.</param>
    public static void ReadM99(HeidenhainBlock block)
    {
        if (block.FindCode("M99") is not SourceWord code)
        {
            return;
        }

        block.MarkRead(code);
        if (block.Draft.IsRaw)
        {
            return;
        }

        HeidenhainDraftBlock main = block.Draft.Main;
        bool plane = block.Heidenhain.PlaneAxes(out _, out _, out string tool);
        bool inPlane = main.Verb == "RAPID" && plane && !main.Has("FRAME", null) && !main.Has(tool, null)
            && !main.Has("I" + tool, null);
        if (main.Verb is null || inPlane)
        {
            Call(block, main);
            return;
        }

        main.PositionsCall = true;
        var call = new HeidenhainDraftBlock();
        block.Draft.After.Insert(0, call);
        Call(block, call);
    }

    /// <summary>
    /// Reads PATTERN DEF with its points POS1 (X+25 Y+33.5 Z+0) ... for the CYCL CALL PAT after it (controllers
    /// heidenhain.md 5): the block writes nothing, its points are the calls. The other forms, and a pattern no CYCL
    /// CALL PAT calls, stay RAW (heidenhain 7 rule 9).
    /// </summary>
    /// <param name="block">The block being read, whose first words are PATTERN DEF.</param>
    public static void ReadPattern(HeidenhainBlock block)
    {
        block.MarkAllRead();
        HeidenhainState state = block.Heidenhain;
        state.Pattern = null;
        if (!CalledLater(block) || !state.PlaneAxes(out string first, out string second, out string tool))
        {
            block.Draft.KeepAsRaw("no CYCL CALL PAT calls this PATTERN DEF in a known working plane; PATTERN DEF "
                + "beyond the expanded calls is RAW (controllers heidenhain.md 7 rule 9)");
            return;
        }

        string body = PatternHead().Replace(block.Content, "");
        var points = new List<HeidenhainPoint>();
        foreach (Match point in PatternPoint().Matches(body))
        {
            if (PointOf(point.Groups[1].Value, first, second, tool, out string? problem) is not HeidenhainPoint read)
            {
                block.Draft.KeepAsRaw(problem!);
                return;
            }

            points.Add(read);
        }

        if (points.Count == 0 || PatternPoint().Replace(body, "").Trim().Length > 0)
        {
            block.Draft.KeepAsRaw(
                "a PATTERN DEF of other forms than POS points is RAW (controllers heidenhain.md 7 rule 9)");
            return;
        }

        state.Pattern = points;
    }

    /// <summary>
    /// Tells whether a CYCL CALL or M99 reads into a CYCLE_CALL of the active definition (Call): the definition is
    /// known and written as a CYCLE; one kept RAW keeps its calls RAW, and so does a call of no definition or of one a
    /// caller or a called subprogram defines (controllers heidenhain.md 5; heidenhain 7 rules 7 and 9).
    /// </summary>
    /// <param name="state">The source-side state of the reader.</param>
    public static bool WritesCall(HeidenhainState state)
    {
        return !state.DefinitionUnknown && state.Definition is { RawReason: null };
    }

    // A CYCL DEF of a machining cycle replaces the active definition, which stays active until the next CYCL DEF
    // (controllers heidenhain.md 5). One kept as RAW keeps its block RAW and writes no CYCLE block, so the NCX cycle
    // stays as it was, on or off; false for such a definition.
    private static bool Activate(HeidenhainBlock block, HeidenhainDefinition definition)
    {
        HeidenhainState state = block.Heidenhain;
        state.Definition = definition;
        state.DefinitionUnknown = false;
        block.State.ActiveCycle = definition.Native;
        if (definition.RawReason is string reason)
        {
            block.Draft.KeepAsRaw(reason);
            return false;
        }

        return true;
    }

    // A call of the active definition: where NCX has the cycle off, the definition is written again first, since the
    // control keeps it until the next CYCL DEF (controllers heidenhain.md 5; language 4.7). Afterwards the plane axes
    // stand at the call point and the drilling axis at the plane the cycle returns to (virtual machine 3.3).
    private static bool Call(HeidenhainBlock block, HeidenhainDraftBlock target)
    {
        HeidenhainState state = block.Heidenhain;
        if (state.DefinitionUnknown)
        {
            block.Draft.KeepAsRaw("the cycle it calls is defined by a caller of the subprogram or in a called "
                + "subprogram, which the reader does not know (virtual machine 3.9)");
            return false;
        }

        if (state.Definition is not HeidenhainDefinition definition)
        {
            block.Diagnostics.Error(block.Line, DiagnosticCodes.HeidenhainCycleCallWithoutDefinition,
                "The block calls a cycle and no CYCL DEF of a machining cycle stands before it; the block is kept as "
                + "RAW (controllers heidenhain.md 5).");
            block.Draft.KeepAsRaw("no cycle is defined");
            return false;
        }

        // A definition kept as RAW stays the one its calls call, and they stay RAW with it (heidenhain 7 rule 9).
        if (definition.RawReason is not null)
        {
            block.Draft.KeepAsRaw(definition.Native.Length > 0
                ? $"the definition of cycle {definition.Native} it calls is kept as RAW"
                : "the CYCL DEF it calls is kept as RAW");
            return false;
        }

        if (state.CycleOn != true)
        {
            var again = new HeidenhainDraftBlock();
            foreach (Word word in definition.Words)
            {
                again.Add(word.Key, word.Addr, word.Value);
            }

            block.Draft.Before.Add(again);
            state.CycleOn = true;
        }

        target.WithVerb("CYCLE_CALL");
        state.CycleCalled = true;
        state.Tangent = null;
        if (state.PlaneAxes(out _, out _, out string tool))
        {
            string retract = FindWord(definition, "CYCLE_RETRACT")?.Value.ToCanonical() ?? "CLEARANCE";
            decimal? level = HeidenhainNumbers.NumberOf(FindWord(definition, retract)?.Value);
            if (level is decimal known)
            {
                block.State.SetPosition(tool, known);
            }
            else
            {
                block.State.ForgetPosition(tool);
            }
        }

        return true;
    }

    // CYCL CALL PAT calls the cycle at every point of the PATTERN DEF, one CYCLE_CALL per point (heidenhain 7 rule 7);
    // the feed between the points, CYCL CALL PAT F, has no NCX word, since a CYCLE_CALL positions at rapid (virtual
    // machine 3.3).
    private static void ReadPatternCall(HeidenhainBlock block)
    {
        block.Take("FMAX");
        HeidenhainState state = block.Heidenhain;
        if (block.Find("F") is not null)
        {
            block.Draft.KeepAsRaw("the feed between the points of CYCL CALL PAT has no NCX word; CYCLE_CALL positions "
                + "at rapid (virtual machine 3.3)");
            return;
        }

        if (state.Pattern is not List<HeidenhainPoint> points
            || !state.PlaneAxes(out string first, out string second, out _))
        {
            block.Draft.KeepAsRaw("CYCL CALL PAT needs the points of a PATTERN DEF that the reader expands "
                + "(controllers heidenhain.md 7 rules 7 and 9)");
            return;
        }

        for (int index = 0; index < points.Count; index++)
        {
            HeidenhainDraftBlock target = index == 0 ? block.Draft.Main : new HeidenhainDraftBlock();
            if (index > 0)
            {
                block.Draft.After.Add(target);
            }

            if (!Call(block, target))
            {
                return;
            }

            target.Add(first, HeidenhainNumbers.Of(points[index].First))
                .Add(second, HeidenhainNumbers.Of(points[index].Second));
            block.State.SetPosition(first, points[index].First);
            block.State.SetPosition(second, points[index].Second);
        }
    }

    // The CYCLE block of a definition through its catalog entry. A cycle of the built-in drilling family, and a catalog
    // cycle whose every Q parameter the entry maps to a word of the language, is the catalog name with its words, the
    // coordinates along the tool axis made absolute: SURFACE = Q203, CLEARANCE = Q203 + Q200, DEPTH = Q203 + Q201, SAFE
    // = Q203 + Q204 (heidenhain 5; machine-config 6, absolute_from_surface). Any other cycle is written natively,
    // CYCLE:HEIDENHAIN=n with its Q parameters in source order (language 4.7.1, D94; cycles/heidenhain.toml).
    // TODO(question): the catalog maps cycles 204 to 209, 240, 241 and 251 to 257 only partly (wave-1 question #22),
    // and a catalog cycle's own parameter names, LENGTH and WIDTH of RECT_POCKET, are not words the parser knows
    // (wave-1 questions #36 and #59); such a definition is written natively.
    private static HeidenhainDefinition Define(HeidenhainBlock block, CycleEntry entry, string native,
        List<SourceWord> parameters)
    {
        bool family = entry.RuleWords.Contains("CYCLE_RETRACT");
        var unmapped = new List<string>();
        foreach (SourceWord parameter in parameters)
        {
            if (!entry.Params.Values.Contains(parameter.Address))
            {
                unmapped.Add(parameter.Address);
            }
        }

        bool languageWords = true;
        foreach (string word in entry.Params.Keys)
        {
            languageWords &= WordCatalog.Lookup(word) is not null;
        }

        if (!family && (unmapped.Count > 0 || !languageWords))
        {
            return Native(block, native, parameters);
        }

        var words = new List<Word> { new() { Key = "CYCLE", Value = new IdentValue(entry.Name) } };
        SourceWord? surfaceWord = FindParameter(parameters, entry.NativeOf("SURFACE"));
        Value? surface = surfaceWord is null ? null : ValueOf(block, surfaceWord, out _);
        foreach (KeyValuePair<string, string> parameter in entry.Params)
        {
            if (FindParameter(parameters, parameter.Value) is not SourceWord word)
            {
                continue;
            }

            Value? value = ValueOf(block, word, out string? problem);
            if (value is not null && entry.AbsoluteFromSurface.Contains(parameter.Key))
            {
                value = surface is null ? null : Absolute(block, surface, value, out problem);
                problem ??= $"{parameter.Value} is relative to the surface Q203, which the definition does not give";
            }

            if (value is null)
            {
                return new HeidenhainDefinition { Native = native, RawReason = problem };
            }

            words.Add(new Word { Key = parameter.Key, Value = value });
        }

        AddRetract(entry, parameters, words, family);
        WarnUnmapped(block, entry, native, unmapped);
        return new HeidenhainDefinition { Native = native, Words = words };
    }

    // CYCLE_RETRACT is the cycle with or without Q204 (controller-mapping 5): with Q204 the tool returns to SAFE = Q203
    // + Q204, the second set-up clearance, the retract height after the cycle (heidenhain 5), without it to CLEARANCE.
    // TODO(question): controller-mapping 5 does not say whether a definition whose Q204 puts SAFE on CLEARANCE,
    // BOHREN.h with Q200=5 and Q204=5 against G99 and R5. of the Fanuc source, or whose Q204 is 0, reads as
    // CYCLE_RETRACT=CLEARANCE; the reader writes SAFE and CYCLE_RETRACT=SAFE for every definition with Q204, which
    // keeps Q204, and Expected/BOHREN.ncx keeps the Fanuc reading until D226 is answered (phase 3, P3-05).
    private static void AddRetract(CycleEntry entry, List<SourceWord> parameters, List<Word> words, bool family)
    {
        string? safe = entry.NativeOf("SAFE");
        if (!family && safe is null)
        {
            return;
        }

        bool toSafe = safe is not null && FindParameter(parameters, safe) is not null;
        words.Add(new Word { Key = "CYCLE_RETRACT", Value = new IdentValue(toSafe ? "SAFE" : "CLEARANCE") });
    }

    // TODO(question): a Q parameter of a drilling definition that no word of its catalog entry carries (Q202 and Q210
    // of cycle 200, Q208 of cycle 201, Q212, Q213, Q205, Q208 and Q256 of cycle 203) has no NCX word, and the documents
    // say neither which value the compiler writes for it nor what the reader does with it (D164, which repeats wave-1
    // question #21 and whose recommendation is this reader side: the named entry only where the values equal what the
    // compiler writes, CYCLE:HEIDENHAIN=n otherwise). The reader does not write it and reports it with a WARNING, so
    // that the definition reads into the words of controller-mapping 5, until D164 is answered.
    private static void WarnUnmapped(HeidenhainBlock block, CycleEntry entry, string native, List<string> unmapped)
    {
        if (unmapped.Count == 0)
        {
            return;
        }

        block.Draft.Warnings.Add(new HeidenhainWarning(DiagnosticCodes.HeidenhainCycleParameterNotCarried,
            $"{string.Join(", ", unmapped)} of cycle {native} {(unmapped.Count == 1 ? "has" : "have")} no NCX word in "
            + $"the catalog entry {entry.Name} and {(unmapped.Count == 1 ? "is" : "are")} not written (controllers "
            + "heidenhain.md 5, machine-config 6)."));
    }

    // CYCLE:HEIDENHAIN=n with every Q parameter as written, in source order (language 4.7.1, D94).
    private static HeidenhainDefinition Native(HeidenhainBlock block, string native, List<SourceWord> parameters)
    {
        var words = new List<Word>
        {
            new() { Key = "CYCLE", Addr = "HEIDENHAIN", Value = new IntegerValue(long.Parse(native,
                CultureInfo.InvariantCulture), native) },
        };
        foreach (SourceWord parameter in parameters)
        {
            if (ValueOf(block, parameter, out string? problem) is not Value value)
            {
                return new HeidenhainDefinition { Native = native, RawReason = problem };
            }

            words.Add(new Word { Key = parameter.Address, Value = value });
        }

        return new HeidenhainDefinition { Native = native, Words = words };
    }

    // The absolute coordinate from the surface and the relative value, NCX = SURFACE + native (machine-config 6).
    private static Value? Absolute(HeidenhainBlock block, Value surface, Value relative, out string? problem)
    {
        problem = null;
        if (HeidenhainNumbers.NumberOf(surface) is decimal from && HeidenhainNumbers.NumberOf(relative) is decimal by)
        {
            return HeidenhainNumbers.Of(from + by);
        }

        return HeidenhainExpression.Parse("(" + TextOf(surface) + ") + (" + TextOf(relative) + ")", block.Line,
            block.Diagnostics.File, out problem);
    }

    private static string TextOf(Value value)
    {
        return value is ExprValue expression ? expression.Text : value.ToCanonical();
    }

    private static Value? ValueOf(HeidenhainBlock block, SourceWord word, out string? problem)
    {
        Value? value = HeidenhainExpression.ValueOf(word.Text, block.Line, block.Diagnostics.File, out problem);
        problem = value is null ? $"{word.Address}={word.Text}: {problem}" : null;
        return value;
    }

    private static SourceWord? FindParameter(List<SourceWord> parameters, string? name)
    {
        return name is null ? null : parameters.Find(parameter => parameter.Address == name);
    }

    private static Word? FindWord(HeidenhainDefinition definition, string key)
    {
        foreach (Word word in definition.Words)
        {
            if (word.Key == key && word.Addr is null)
            {
                return word;
            }
        }

        return null;
    }

    // A point of a PATTERN DEF: the two axes of the plane, and on the tool axis 0 or nothing.
    // TODO(question): heidenhain 5 does not say what the tool-axis coordinate of a PATTERN DEF point does to the cycle
    // (a surface for the point, or added to Q203); a point with one other than 0 keeps the pattern RAW until D254 is
    // answered.
    private static HeidenhainPoint? PointOf(string coordinates, string first, string second, string tool,
        out string? problem)
    {
        problem = null;
        decimal? x = null;
        decimal? y = null;
        foreach (string coordinate in coordinates.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string axis = coordinate.Substring(0, 1).ToUpperInvariant();
            decimal? value = HeidenhainNumbers.Parse(coordinate.Substring(1));
            x = axis == first ? value : x;
            y = axis == second ? value : y;
            if (value is null || (axis == tool && value != 0m) || (axis != first && axis != second && axis != tool))
            {
                problem = $"{coordinate} of a PATTERN DEF point has no NCX form; the pattern stays RAW";
                return null;
            }
        }

        problem = x is null || y is null ? "a PATTERN DEF point needs both axes of the plane" : null;
        return x is decimal pointX && y is decimal pointY ? new HeidenhainPoint(pointX, pointY) : null;
    }

    // A CYCL CALL PAT stands after the PATTERN DEF before the next one.
    private static bool CalledLater(HeidenhainBlock block)
    {
        for (SourceBlock? next = block.Heidenhain.NextBlock(block.Source); next is not null;
            next = block.Heidenhain.NextBlock(next))
        {
            string first = next.Words[0].Address;
            if (first == "PATTERN")
            {
                return false;
            }

            if (first == "CYCL" && next.Words.Count > 2 && next.Words[1].Address == "CALL"
                && next.Words[2].Address == "PAT")
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"^PATTERN\s+DEF\s*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PatternHead();

    [GeneratedRegex(@"POS[0-9]+\s*\(([^)]*)\)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PatternPoint();
}
