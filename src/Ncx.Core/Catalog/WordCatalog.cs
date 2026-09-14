using System.Diagnostics.CodeAnalysis;
using Ncx.Core.Model;

namespace Ncx.Core.Catalog;

/// <summary>
/// The word catalog, the schema of the language: one entry per word of the tables of language 4 and the pseudo-words
/// of 4.15, looked up by key, and the two rules for the keys it does not list, the machine axis words of D93 and the
/// native cycle parameters of D94 (architecture 4). The entries are tables, one class per table of language 4, so
/// SPINDLE is found in SpindleWords (code-guidelines 5, table-driven dispatch; D76, the catalog is code).
/// </summary>
public static class WordCatalog
{
    // The value of the reset form of SHIFT, TILT and TILT_AXIS (language 4.2).
    internal const string Reset = "RESET";

    // The cycle word, whose controller address makes a block the native form of a cycle (language 4.7.1, D94).
    private const string CycleKey = "CYCLE";

    // The letters a machine axis name starts with (language 3, KEY; D93).
    private const string MachineAxisLetters = "XYZABCUVW";

    // The standard axes X Y Z A B C and their incremental forms (language 4.3).
    private static readonly string[] s_standardAxes =
        ["X", "Y", "Z", "A", "B", "C", "IX", "IY", "IZ", "IA", "IB", "IC"];

    // Every entry: the tables of language 4 in their order, then the pseudo-words.
    private static readonly List<WordDefinition> s_all = Collect();

    // The entries by key. A second entry of a key already taken is not indexed; the catalog tests report it.
    private static readonly Dictionary<string, WordDefinition> s_byKey = IndexByKey(s_all);

    /// <summary>
    /// Finds the entry of a key.
    /// </summary>
    /// <param name="key">The key, uppercase as language 3 writes it: "SPINDLE".</param>
    /// <returns>The entry, or null for a key the catalog does not list: a machine axis word (D93), a native
    /// parameter (D94), an unknown key.</returns>
    public static WordDefinition? Lookup(string key)
    {
        return s_byKey.TryGetValue(key, out WordDefinition? definition) ? definition : null;
    }

    /// <summary>
    /// Every entry of the catalog, the tables of language 4 in their order, then the pseudo-words of 4.15.
    /// </summary>
    public static IReadOnlyList<WordDefinition> All()
    {
        return s_all;
    }

    /// <summary>
    /// Tells whether a key is a standard axis word: X Y Z A B C or their incremental forms IX to IC (language 4.3).
    /// </summary>
    /// <param name="key">The key, uppercase: "IX".</param>
    public static bool IsStandardAxis(string key)
    {
        return s_standardAxes.Contains(key);
    }

    /// <summary>
    /// Tells whether a key is a machine axis word by its form (D93): [XYZABCUVW] and up to two digits, or I followed
    /// by that, and no word of the catalog. Whether the block lets it stand is the parser's question: a verb that
    /// takes axis words, and no CYCLE:controller=n, in whose block every unknown key is a native parameter
    /// (language 3, KEY; 5 rule 2; D94).
    /// </summary>
    /// <param name="key">The key, uppercase: "Z2".</param>
    /// <param name="machineAxis">The machine axis word, null when the key is none.</param>
    public static bool TryMachineAxis(string key, [NotNullWhen(true)] out MachineAxisWord? machineAxis)
    {
        machineAxis = null;

        // A key the catalog lists is a word of its own and never a machine axis: X, IX, C (D93).
        if (s_byKey.ContainsKey(key))
        {
            return false;
        }

        // An axis letter and up to two digits, or I followed by that. I is no axis letter, so a key that starts with
        // it is the incremental form (language 3, KEY).
        bool isIncremental = key.StartsWith('I');
        string axisName = isIncremental ? key.Substring(1) : key;
        if (axisName.Length is < 1 or > 3 || !MachineAxisLetters.Contains(axisName[0]))
        {
            return false;
        }

        int? number = null;
        for (int index = 1; index < axisName.Length; index++)
        {
            if (!char.IsAsciiDigit(axisName[index]))
            {
                return false;
            }

            number = ((number ?? 0) * 10) + (axisName[index] - '0');
        }

        machineAxis = new MachineAxisWord(key, axisName, axisName[0], number, isIncremental);
        return true;
    }

    /// <summary>
    /// Tells whether a block carries CYCLE:controller=n, the native form of a cycle, in whose block every key the
    /// catalog does not know is a native parameter with a number or expression value, one of the machine-axis form
    /// included (language 4.7.1, D94).
    /// </summary>
    /// <param name="block">The block with all its words.</param>
    public static bool IsNativeParameterAllowed(Block block)
    {
        // TODO(question): language 4.7.1 lets a program name any cycle of the machine's cycle catalog with the
        // parameter names of that catalog (CYCLE=RECT_POCKET LENGTH=60 WIDTH=40), which are no words of this catalog
        // and, under D94, no native parameters, while ncx format reads a program without a machine file (D91). Only
        // the native form opens a block to keys the catalog does not know until D114 is answered.
        //
        // The native form is the cycle word with a controller address: CYCLE:HEIDENHAIN=251 (language 4.7).
        foreach (Word word in block.Words)
        {
            if (word.Key == CycleKey && word.Addr is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tells whether a word is the verb of its block: its entry is a verb (language 5 rule 1) and it is not the
    /// =RESET form of SHIFT, TILT or TILT_AXIS, which stands with the frame words (language 4.2, 5 rule 6, D90).
    /// </summary>
    /// <param name="word">The word, with or without its definition filled in.</param>
    public static bool IsVerb(Word word)
    {
        WordDefinition? definition = word.Definition ?? Lookup(word.Key);
        return definition is { IsVerb: true } && !IsResetForm(word, definition);
    }

    // SHIFT=RESET, TILT=RESET and TILT_AXIS=RESET remove their entry and what follows it from the frame chain; the
    // rank table puts them with the frame words of bucket 12 (language 4.2, 5 rule 6, D90).
    internal static bool IsResetForm(Word word, WordDefinition definition)
    {
        return definition.ResetRank is not null && word.Value is IdentValue { Name: Reset };
    }

    // The tables of language 4 in the order of the sections, then the pseudo-words of 4.15. DIAMETER of 4.11 is the
    // entry of 4.2 (D90).
    private static List<WordDefinition> Collect()
    {
        var all = new List<WordDefinition>();
        all.AddRange(FileWords.Definitions);
        all.AddRange(FrameWords.Definitions);
        all.AddRange(MotionWords.Definitions);
        all.AddRange(ToolWords.Definitions);
        all.AddRange(SpindleWords.Definitions);
        all.AddRange(FunctionWords.Definitions);
        all.AddRange(CycleWords.Definitions);
        all.AddRange(ChannelWords.Definitions);
        all.AddRange(FlowWords.Definitions);
        all.AddRange(ResourceWords.Definitions);
        all.AddRange(LatheWords.Definitions);
        all.AddRange(PseudoWords.Definitions);
        return all;
    }

    private static Dictionary<string, WordDefinition> IndexByKey(List<WordDefinition> definitions)
    {
        var byKey = new Dictionary<string, WordDefinition>(StringComparer.Ordinal);
        foreach (WordDefinition definition in definitions)
        {
            byKey.TryAdd(definition.Key, definition);
        }

        return byKey;
    }
}
