using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The Q parameters of a CYCL DEF from the words of its CYCLE block (controllers heidenhain.md 5; 8 rule 7;
/// controller-mapping 5; machine-config 6): the words of the entry's params, the planes relative to the surface Q203,
/// Q204 from CYCLE_RETRACT, and the rules for the Q parameters of the signature that no word carries (D163, D164,
/// D226).
/// </summary>
internal static class HeidenhainCycleValues
{
    private const string Surface = "SURFACE";
    private const string Clearance = "CLEARANCE";
    private const string Depth = "DEPTH";
    private const string Safe = "SAFE";
    private const string Peck = "PECK";
    private const string CycleFeed = "CYCLE_F";
    private const string CycleDwell = "CYCLE_DWELL";

    // The literal of Q208 that retracts at the rapid (examples/sources/BOHREN.h; D164).
    private const string RapidRetract = "MAX";

    // The words whose numbers the Q parameters are computed from (language 4.7).
    private static readonly string[] s_numberWords =
        [Surface, Clearance, Depth, Safe, Peck, CycleFeed, CycleDwell, "PITCH"];

    /// <summary>
    /// The Q parameters of the cycle in the order of the signature, each as Qnnn=value with its sign; null when one has
    /// no value, which is reported on the block.
    /// </summary>
    public static List<string>? Of(HeidenhainBlock writing, CycleEntry entry)
    {
        Dictionary<string, decimal>? words = NumbersOf(writing);
        if (words is null)
        {
            return null;
        }

        // Q203 is the surface every plane is relative to (heidenhain 5; machine-config 6, absolute_from_surface).
        if (!words.TryGetValue(Surface, out decimal surface))
        {
            Report(writing, entry, "Q203", "the cycle block names no SURFACE, to which Q203 and the planes relative to "
                + "it belong");
            return null;
        }

        var parameters = new List<string>();
        foreach (string parameter in entry.Signature)
        {
            string? value = WordOf(entry, parameter) is string word
                ? Mapped(writing, entry, word, words, surface)
                : Unmapped(writing, entry, parameter, words, surface);
            if (value is null)
            {
                Report(writing, entry, parameter, "neither a word of the cycle block nor a rule of the compiler gives "
                    + "its value");
                return null;
            }

            parameters.Add(parameter + "=" + value);
        }

        return parameters;
    }

    // A Q parameter the entry maps to a word: its number, relative to the surface for the planes (machine-config 6).
    private static string? Mapped(HeidenhainBlock writing, CycleEntry entry, string word,
        Dictionary<string, decimal> words, decimal surface)
    {
        string parameter = entry.NativeOf(word)!;
        switch (word)
        {
            // CYCLE_RETRACT=SAFE becomes Q204 (heidenhain 8 rule 7): SAFE = Q203 + Q204.
            // TODO(question): D226: which Q204 the compiler writes for CYCLE_RETRACT=CLEARANCE is open; as its
            // recommendation says, Q204 equals Q200, the clearance above the surface.
            case Safe:
                bool toSafe = writing.Block.Find("CYCLE_RETRACT")?.Value is IdentValue { Name: Safe };
                string plane = toSafe ? Safe : Clearance;
                return words.TryGetValue(plane, out decimal level)
                    ? Signed(writing, parameter, entry.ToNative(Safe, level, surface))
                    : null;

            // TODO(question): D163: both DRILL and DRILL_DWELL are cycle 200, told apart by Q211; as its recommendation
            // says, Q211 is CYCLE_DWELL, or 0 when the block has none.
            case CycleDwell:
                return Signed(writing, parameter, words.TryGetValue(CycleDwell, out decimal dwell) ? dwell : 0m);

            // Without PECK the cycle drills to the depth in one infeed (virtual machine 3.3: PECK splits the feed), the
            // Q202 of cycle 200 in D164.
            case Peck:
                return Infeed(words, surface) is decimal infeed ? Signed(writing, parameter, infeed) : null;

            default:
                return words.TryGetValue(word, out decimal value)
                    ? Signed(writing, parameter, entry.ToNative(word, value, surface))
                    : null;
        }
    }

    // TODO(question): D164: the values of the Q parameters of the signature that no NCX word carries are open; as its
    // recommendation says, the compiler writes the values of examples/sources/BOHREN.h: Q202 of cycle 200 the full
    // depth, Q205 of 203 equal to Q202, Q208 of 201 equal to Q206 and of 203 MAX, Q210 and Q212 zero, Q256 0.6.
    // TODO(question): D163: Q213 tells PECK from CHIP_BREAK on cycle 203; as its recommendation says, the compiler
    // writes 0 for PECK and the number of infeeds, the depth over PECK rounded up, for CHIP_BREAK.
    private static string? Unmapped(HeidenhainBlock writing, CycleEntry entry, string parameter,
        Dictionary<string, decimal> words, decimal surface)
    {
        decimal? infeed = Infeed(words, surface);
        switch (parameter)
        {
            case "Q202" or "Q205":
                return infeed is decimal step ? Signed(writing, parameter, step) : null;
            case "Q210" or "Q212":
                return Signed(writing, parameter, 0m);
            case "Q256":
                return Signed(writing, parameter, 0.6m);
            case "Q208" when entry.Native == "203":
                return RapidRetract;
            case "Q208" when entry.Native == "201":
                return words.TryGetValue(CycleFeed, out decimal feed) ? Signed(writing, parameter, feed) : null;
            case "Q213" when entry.Name == "PECK":
                return Signed(writing, parameter, 0m);
            case "Q213" when entry.Name == "CHIP_BREAK":
                return infeed is decimal peck && peck > 0 && DepthBelow(words, surface) is decimal depth
                    ? Signed(writing, parameter, Math.Ceiling(depth / peck))
                    : null;
            default:
                return null;
        }
    }

    // The depth of one infeed: PECK, or the whole depth below the surface without it.
    private static decimal? Infeed(Dictionary<string, decimal> words, decimal surface)
    {
        return words.TryGetValue(Peck, out decimal peck) ? peck : DepthBelow(words, surface);
    }

    private static decimal? DepthBelow(Dictionary<string, decimal> words, decimal surface)
    {
        return words.TryGetValue(Depth, out decimal depth) ? Math.Abs(depth - surface) : null;
    }

    // The word the entry maps to a Q parameter; null for a Q parameter no word carries.
    private static string? WordOf(CycleEntry entry, string parameter)
    {
        foreach (KeyValuePair<string, string> mapped in entry.Params)
        {
            if (mapped.Value == parameter)
            {
                return mapped.Key;
            }
        }

        return null;
    }

    // The numbers of the cycle words of the block; null when one is an expression, which the planes relative to the
    // surface cannot take (reported).
    private static Dictionary<string, decimal>? NumbersOf(HeidenhainBlock writing)
    {
        var numbers = new Dictionary<string, decimal>();
        foreach (string key in s_numberWords)
        {
            if (writing.Block.Find(key) is not Word word)
            {
                continue;
            }

            if (HeidenhainNumbers.NumberOf(word.Value) is not decimal number)
            {
                HeidenhainNumbers.ReportValue(writing, word);
                return null;
            }

            numbers[key] = number;
        }

        return numbers;
    }

    // Q parameters carry their sign, Q200=+2 (controllers heidenhain.md 5).
    private static string Signed(HeidenhainBlock writing, string parameter, decimal value)
    {
        return HeidenhainNumbers.Signed(writing, parameter, value);
    }

    private static void Report(HeidenhainBlock writing, CycleEntry entry, string parameter, string reason)
    {
        writing.Error(DiagnosticCodes.HeidenhainCycleParameterWithoutValue,
            $"{parameter} of cycle {entry.Native} ({entry.Name}) has no value: {reason} (controllers heidenhain.md 8 "
            + "rule 7; D164).");
    }
}
