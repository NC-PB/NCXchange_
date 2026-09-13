namespace Ncx.Core.Machine;

// TODO(question): machine-config 6 has no key for the kind of a parameter ("Params with their kinds", phase 2 P2-03).
// Every parameter is taken as a number or an expression, as a native parameter of D94 is, and AXIS as the axis name of
// language 4.7; the entry records no kind until the documents give one.

/// <summary>
/// One entry of a cycle catalog: the NCX cycle name, the native cycle it maps to, the NCX words of its parameters with
/// their native names, and how the native cycle is written (machine-config 6, language 4.7.1). An entry comes from the
/// built-in drilling family, from the catalog file of the controller family or from a [[cycle]] of a machine file, and
/// each overrides the entry of the same name before it (machine-config 6).
/// </summary>
public sealed record CycleEntry
{
    // The words of language 4.7 that an entry answers for by name.
    private const string CycleFWord = "CYCLE_F";
    private const string CycleDwellWord = "CYCLE_DWELL";
    private const string AxisWord = "AXIS";
    private const string ContourWord = "CONTOUR";

    /// <summary>
    /// name: the NCX cycle name, the value of CYCLE in a program, "RECT_POCKET" (language 4.7, 4.7.1).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// native: the native cycle, "G81", "200" for CYCL DEF 200, "CYCLE81"; a cycle number is kept as its text.
    /// </summary>
    public required string Native { get; init; }

    /// <summary>
    /// params: the NCX word of each parameter and the native name that carries it, DEPTH = "Q201" (machine-config 6).
    /// </summary>
    public IReadOnlyDictionary<string, string> Params { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// absolute_from_surface: the words whose native value is relative to the surface while the NCX value is
    /// absolute, NCX = SURFACE + native, the DEPTH = Q203 + Q201 of Heidenhain (machine-config 6, heidenhain 5).
    /// </summary>
    public IReadOnlyList<string> AbsoluteFromSurface { get; init; } = [];

    /// <summary>
    /// modal: the native cycle stays active after its block and every following block with a position calls it
    /// again, as Fanuc G81..G89 and G90, G92, G94 do; false for a block that runs once where it stands (machine-config
    /// 6, language 4.7.1).
    /// </summary>
    public bool Modal { get; init; }

    /// <summary>
    /// contour: the native words that carry the contour of CONTOUR=name. Two are the first and the last block of the
    /// contour range (Fanuc P and Q), one names the contour subprogram (Siemens CYCLE95 NPP); empty for a cycle
    /// without a contour (machine-config 6, D65).
    /// </summary>
    public IReadOnlyList<string> Contour { get; init; } = [];

    /// <summary>
    /// signature: every native parameter in the order the control writes it, also those without an NCX word; empty
    /// for Fanuc entries, whose parameters are address words of the cycle block (machine-config 6).
    /// </summary>
    public IReadOnlyList<string> Signature { get; init; } = [];

    /// <summary>
    /// fixed: native parameters the entry always writes with this value, which tell two entries of one native cycle
    /// apart, VARI = 1 for PECK on CYCLE83 (machine-config 6, controller-mapping 5).
    /// </summary>
    public IReadOnlyDictionary<string, decimal> Fixed { get; init; } = new Dictionary<string, decimal>();

    /// <summary>
    /// pre, post, requires and restore of the entry, from which the expander makes generated blocks around the cycle
    /// block (machine-config 5a); null when the entry has none of the four keys.
    /// </summary>
    public ExpansionRule? Rule { get; init; }

    // TODO(question): machine-config 6 has no key for the words a rule carries, so only the built-in drilling family
    // names them; a catalog file cannot name them for a cycle of its own.

    /// <summary>
    /// The NCX words that a rule of the reader and the compiler of the controller family carries instead of a native
    /// parameter: SURFACE and SAFE of a Fanuc drilling cycle, CYCLE_RETRACT on every family (controller-mapping 5).
    /// Set by the built-in drilling family; empty for the other entries.
    /// </summary>
    public IReadOnlyList<string> RuleWords { get; init; } = [];

    /// <summary>
    /// The keys of machine-config 6 and 5a that the file of the entry wrote, in file order; a later entry of the same
    /// name overrides exactly these (OverriddenBy). Empty for an entry built in code.
    /// </summary>
    public IReadOnlyList<string> WrittenKeys { get; init; } = [];

    /// <summary>
    /// The native name of CYCLE_F, the plunge feed of the cycle (language 4.7, D29); null when the entry does not map
    /// it. IsAddressWord says where it stands.
    /// </summary>
    public string? CycleF => NativeOf(CycleFWord);

    /// <summary>
    /// The native name of CYCLE_DWELL, the dwell at the bottom (language 4.7, D29); null when the entry does not map
    /// it.
    /// </summary>
    public string? CycleDwell => NativeOf(CycleDwellWord);

    /// <summary>
    /// The native name of AXIS, the drilling axis, Siemens _AXN (D59); null when the rule of the family takes the
    /// drilling axis from the plane or the tool axis (controller-mapping 5).
    /// </summary>
    public string? Axis => NativeOf(AxisWord);

    /// <summary>
    /// The NCX words of the entry: the words its params map, the words a rule of its family carries, and CONTOUR when
    /// it carries a contour (machine-config 6, language 4.7, D65).
    /// </summary>
    public IReadOnlyList<string> Words
    {
        get
        {
            // The words of the cycle block come from the native parameters or from a rule of the family, SURFACE of a
            // Fanuc drilling cycle is R minus the clearance (controller-mapping 5); a contour is named by CONTOUR=name
            // on the cycle block (language 4.7, D65).
            var words = new List<string>(Params.Keys);
            foreach (string word in RuleWords)
            {
                if (!words.Contains(word))
                {
                    words.Add(word);
                }
            }

            if (Contour.Count > 0 && !words.Contains(ContourWord))
            {
                words.Add(ContourWord);
            }

            return words;
        }
    }

    /// <summary>
    /// The native name that carries an NCX word, DEPTH to "Q201"; null for a word the entry does not map.
    /// </summary>
    /// <param name="word">The NCX word, DEPTH.</param>
    public string? NativeOf(string word)
    {
        return Params.TryGetValue(word, out string? native) ? native : null;
    }

    /// <summary>
    /// Whether a mapped word is written as an address word of its own rather than as a position of the signature:
    /// every parameter of a Fanuc entry, the modal F that Siemens CYCLE81..CYCLE83 use (machine-config 6); false for a
    /// word the entry does not map.
    /// </summary>
    /// <param name="word">The NCX word, CYCLE_F.</param>
    public bool IsAddressWord(string word)
    {
        // A native name in params that the signature does not list is an address word of its own; Fanuc entries leave
        // the signature out, because their parameters are address words of the cycle block (machine-config 6).
        string? native = NativeOf(word);
        return native is not null && !Signature.Contains(native);
    }

    /// <summary>
    /// The NCX value of a word from its native value: absolute for a word of absolute_from_surface, as it stands for
    /// every other word (machine-config 6).
    /// </summary>
    /// <param name="word">The NCX word, DEPTH.</param>
    /// <param name="native">The native value, Q201 = -20.</param>
    /// <param name="surface">The surface the native value is relative to, the NCX SURFACE (Q203, RFP).</param>
    public decimal ToNcx(string word, decimal native, decimal surface)
    {
        // Native Q201 is relative to Q203, NCX DEPTH is absolute: NCX = surface + native (machine-config 6; heidenhain
        // 5, CLEARANCE = Q203 + Q200; siemens 7, CLEARANCE = RFP + SDIS).
        return AbsoluteFromSurface.Contains(word) ? surface + native : native;
    }

    /// <summary>
    /// The native value of a word from its NCX value: relative to the surface for a word of absolute_from_surface, as
    /// it stands for every other word (machine-config 6).
    /// </summary>
    /// <param name="word">The NCX word, DEPTH.</param>
    /// <param name="ncx">The NCX value, DEPTH=-15.</param>
    /// <param name="surface">The NCX SURFACE the native value is written relative to.</param>
    public decimal ToNative(string word, decimal ncx, decimal surface)
    {
        // The compiler writes the relative value back, native = NCX - surface (machine-config 6, heidenhain 8.7).
        return AbsoluteFromSurface.Contains(word) ? ncx - surface : ncx;
    }

    /// <summary>
    /// This entry overridden by a later entry of the same name: the keys the later entry wrote replace those of this
    /// entry, the keys it left out stay (machine-config 6).
    /// </summary>
    /// <param name="entry">The later entry, from the catalog file or the machine file.</param>
    public CycleEntry OverriddenBy(CycleEntry entry)
    {
        // TODO(question): machine-config 6 says an entry "can be overridden per machine" without saying whether an
        // override replaces the whole entry or the keys it writes. Key by key is taken, because the entries of
        // machine-config 5a and doosan-puma-2600sy.toml write name, native and pre alone and would otherwise lose the
        // address words of G83.
        var writtenKeys = new List<string>(WrittenKeys);
        foreach (string key in entry.WrittenKeys)
        {
            if (!writtenKeys.Contains(key))
            {
                writtenKeys.Add(key);
            }
        }

        return new CycleEntry
        {
            Name = Name,
            Native = entry.Writes("native") ? entry.Native : Native,
            Params = entry.Writes("params") ? entry.Params : Params,
            AbsoluteFromSurface = entry.Writes("absolute_from_surface")
                ? entry.AbsoluteFromSurface
                : AbsoluteFromSurface,
            Modal = entry.Writes("modal") ? entry.Modal : Modal,
            Contour = entry.Writes("contour") ? entry.Contour : Contour,
            Signature = entry.Writes("signature") ? entry.Signature : Signature,
            Fixed = entry.Writes("fixed") ? entry.Fixed : Fixed,
            Rule = OverriddenRule(entry),
            RuleWords = entry.RuleWords.Count > 0 ? entry.RuleWords : RuleWords,
            WrittenKeys = writtenKeys,
        };
    }

    // Whether the file of the entry wrote a key of machine-config 6 or 5a.
    private bool Writes(string key)
    {
        return WrittenKeys.Contains(key);
    }

    // The four keys of the expansion rule override one by one like the other keys: a later post leaves the pre of this
    // entry in place (machine-config 5a, 6).
    private ExpansionRule? OverriddenRule(CycleEntry entry)
    {
        bool writesRule = entry.Writes("pre") || entry.Writes("post") || entry.Writes("requires")
            || entry.Writes("restore");
        if (!writesRule)
        {
            return Rule;
        }

        ExpansionRule earlier = Rule ?? new ExpansionRule();
        ExpansionRule later = entry.Rule ?? new ExpansionRule();
        return new ExpansionRule
        {
            Pre = entry.Writes("pre") ? later.Pre : earlier.Pre,
            Post = entry.Writes("post") ? later.Post : earlier.Post,
            Requires = entry.Writes("requires") ? later.Requires : earlier.Requires,
            Restore = entry.Writes("restore") ? later.Restore : earlier.Restore,
        };
    }
}
