using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The cycles that act where they stand (controllers heidenhain.md 2, 3, 5, 7 rule 6; controller-mapping 1; language
/// 4.1, 4.2; D31, D82, D85): cycle 247 selects the preset, ORIGIN; cycle 7 shifts the datum, replacing the earlier
/// cycle 7 with SHIFT=RESET before the new SHIFT; cycle 8 mirrors, cycle 10 rotates, cycle 19 tilts the plane by axis
/// angles, TILT_AXIS; cycle 32 sets the tolerance and cycle 9 dwells. Cycles 7, 8, 9, 10, 19 and 32 are written over
/// several blocks, CYCL DEF 7.0, 7.1, 7.2 ..., which the reader reads as one definition at its first block.
/// </summary>
internal static class HeidenhainFrameCycles
{
    private static readonly string[] s_axes = ["X", "Y", "Z", "A", "B", "C", "U", "V", "W"];

    /// <summary>
    /// Reads a block CYCL DEF n.k of the cycles 7, 8, 9, 10, 19, 32, or CYCL DEF 247.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="number">The cycle number.</param>
    /// <param name="part">The number after the point, 1 of CYCL DEF 7.1; 0 for n.0 and for cycle 247.</param>
    public static void Read(HeidenhainBlock block, int number, int part)
    {
        block.MarkAllRead();
        HeidenhainState state = block.Heidenhain;
        if (number == 247)
        {
            ReadOrigin(block);
            return;
        }

        if (state.GroupMembers.TryGetValue(block.Line, out string? groupProblem))
        {
            if (groupProblem is not null)
            {
                block.Draft.KeepAsRaw(groupProblem);
            }

            return;
        }

        if (part != 0)
        {
            block.Draft.KeepAsRaw($"CYCL DEF {number}.{part} stands without its CYCL DEF {number}.0");
            return;
        }

        List<SourceBlock> members = Members(block, number);
        string? problem = members.Exists(member => member.BlockSkip)
            ? "a block skip inside a cycle definition over several blocks has no NCX form"
            : null;
        problem ??= number switch
        {
            7 => ReadShift(block, members),
            8 => ReadMirror(block, members),
            9 => ReadDwell(block, members),
            10 => ReadRotation(block, members),
            19 => ReadTilt(block, members),
            _ => ReadTolerance(block, members),
        };
        foreach (SourceBlock member in members)
        {
            state.GroupMembers[member.Line] = problem;
        }

        if (problem is not null)
        {
            block.Draft.KeepAsRaw(problem);
        }
    }

    /// <summary>
    /// The number of a cycle and the part after its point: 7 and 1 of CYCL DEF 7.1, 247 and 0 of CYCL DEF 247.
    /// </summary>
    /// <param name="word">The word after CYCL DEF.</param>
    /// <param name="number">The cycle number.</param>
    /// <param name="part">The part after the point.</param>
    /// <returns>False for a word that is no cycle number.</returns>
    public static bool NumberOf(SourceWord word, out int number, out int part)
    {
        string[] parts = word.Text.Replace(',', '.').Split('.');
        part = 0;
        return int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out number)
            && (parts.Length == 1
                || (parts.Length == 2 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture,
                    out part)));
    }

    // CYCL DEF 247 INIT. REF.PKT Q339=1 activates preset 1 of the preset table, ORIGIN=1, which empties the chain
    // (controllers heidenhain.md 3; controller-mapping 1, ORIGIN; language 4.2).
    // TODO(question): heidenhain 3 does not say whether cycle 247 ends an active cycle 7, 8, 10 or tilted plane on the
    // control, while ORIGIN empties the chain of NCX (language 4.2, D31); the reader writes ORIGIN and does not write
    // them again, as the Fanuc reader does for G54 over G52.
    private static void ReadOrigin(HeidenhainBlock block)
    {
        SourceWord? preset = block.Source.Find("Q339");
        if (preset?.Number is not decimal number || number < 0 || number != decimal.Truncate(number))
        {
            block.Draft.KeepAsRaw("cycle 247 names no preset Q339");
            return;
        }

        int datum = decimal.ToInt32(number);
        block.Draft.AddState("ORIGIN", null, new IntegerValue(datum, datum.ToString(CultureInfo.InvariantCulture)));
        block.Heidenhain.Chain.Clear();
        block.Heidenhain.DatumShift.Clear();
        block.Heidenhain.ForgetFrame();
    }

    // CYCL DEF 7.1 X+60, 7.2 Y+40, 7.3 Z-5: the datum shift, axes in any order, an omitted axis unchanged; a new cycle
    // 7 replaces the previous one (controllers heidenhain.md 3, 7 rule 6): SHIFT=RESET before the new SHIFT, and the
    // reset alone where every axis is 0. CYCL DEF 7.1 #5 takes a line of the datum table, which NCX has no word for.
    private static string? ReadShift(HeidenhainBlock block, List<SourceBlock> members)
    {
        HeidenhainState state = block.Heidenhain;
        var shift = new Dictionary<string, decimal>(state.DatumShift, StringComparer.Ordinal);
        foreach (SourceWord word in WordsOf(members))
        {
            if (!s_axes.Contains(word.Address) || word.Number is not decimal value)
            {
                return word.Text.StartsWith('#')
                    ? "CYCL DEF 7 with a line of the datum table has no NCX word"
                    : $"{word.Address}{word.Text} has no NCX word in cycle 7";
            }

            shift[word.Address] = value;
        }

        var entry = new HeidenhainDraftBlock().WithVerb("SHIFT");
        foreach (string axis in s_axes)
        {
            if (shift.TryGetValue(axis, out decimal value) && value != 0)
            {
                entry.Add(axis, HeidenhainNumbers.Of(value));
            }
        }

        state.DatumShift.Clear();
        foreach (KeyValuePair<string, decimal> axis in shift)
        {
            state.DatumShift[axis.Key] = axis.Value;
        }

        return Change(block, "SHIFT", entry.Words.Count == 0 ? null : entry);
    }

    // CYCL DEF 8.1 X Y mirrors the named axes, MIRROR=X,Y; 8.1 without axes cancels (controllers heidenhain.md 3;
    // controller-mapping 1, MIRROR); a new cycle 8 replaces the previous one.
    private static string? ReadMirror(HeidenhainBlock block, List<SourceBlock> members)
    {
        var axes = new List<string>();
        foreach (SourceWord word in WordsOf(members))
        {
            if (!s_axes.Contains(word.Address))
            {
                return $"{word.Address}{word.Text} has no NCX word in cycle 8";
            }

            axes.Add(word.Address);
        }

        Value value = axes.Count == 1 ? new IdentValue(axes[0]) : new ListValue(axes);
        return Change(block, "MIRROR", axes.Count == 0 ? null : new HeidenhainDraftBlock().Add("MIRROR", value));
    }

    // CYCL DEF 10.1 ROT+30 rotates the plane, ROTATE=30; ROT+0 cancels (controllers heidenhain.md 3; controller-mapping
    // 1, ROTATE); a new cycle 10 replaces the previous one. IROT adds to the active rotation and stays RAW.
    private static string? ReadRotation(HeidenhainBlock block, List<SourceBlock> members)
    {
        List<SourceWord> words = WordsOf(members);
        if (words.Count != 1 || words[0].Address != "ROT"
            || HeidenhainMotion.ValueOf(block, words[0]) is not Value angle)
        {
            return "cycle 10 names no rotation ROT";
        }

        bool cancels = HeidenhainNumbers.NumberOf(angle) == 0m;
        return Change(block, "ROTATE", cancels ? null : new HeidenhainDraftBlock().Add("ROTATE", angle));
    }

    // CYCL DEF 19.1 A.. B.. C.. tilts the plane by axis angles, TILT_AXIS, the older form of PLANE AXIAL; 19.1 without
    // axes cancels (controllers heidenhain.md 3, 7 rule 6; D82).
    private static string? ReadTilt(HeidenhainBlock block, List<SourceBlock> members)
    {
        var entry = new HeidenhainDraftBlock().WithVerb("TILT_AXIS");
        foreach (SourceWord word in WordsOf(members))
        {
            if (word.Address is not ("A" or "B" or "C") || HeidenhainMotion.ValueOf(block, word) is not Value angle)
            {
                return $"{word.Address}{word.Text} has no NCX word in cycle 19 (D82)";
            }

            entry.Add(word.Address, angle);
        }

        return Change(block, "TILT_AXIS", entry.Words.Count == 0 ? null : entry);
    }

    // CYCL DEF 9.1 V.ZEIT 1.5 dwells 1.5 seconds, DWELL=1.5 (controllers heidenhain.md 5; controller-mapping 1, DWELL).
    private static string? ReadDwell(HeidenhainBlock block, List<SourceBlock> members)
    {
        foreach (SourceWord word in WordsOf(members))
        {
            if (word.Address.Length == 0 && word.Number is not null && word.ToNcxNumber() is Value seconds)
            {
                block.Draft.Before.Add(new HeidenhainDraftBlock().Add("DWELL", seconds));
                return null;
            }
        }

        return "cycle 9 names no dwell time";
    }

    // CYCL DEF 32.1 T0.02 sets the contouring tolerance, 32.2 HSC-MODE:0 TA0.05 the mode, 0 finish and 1 rough, and the
    // rotary tolerance (controllers heidenhain.md 2; controller-mapping 1, TOLERANCE; language 4.1; D85); T0 is the
    // default of the control, TOLERANCE=OFF (machines/heidenhain-itnc530.toml, [tolerance] OFF).
    private static string? ReadTolerance(HeidenhainBlock block, List<SourceBlock> members)
    {
        Value? tolerance = null;
        Value? rotary = null;
        string? mode = null;
        foreach (SourceWord word in WordsOf(members))
        {
            if (word.Address == "T" && word.Number is not null)
            {
                tolerance = word.ToNcxNumber();
            }
            else if (word.Address == "TA" && word.Number is not null)
            {
                rotary = word.ToNcxNumber();
            }
            else if (word.Address == "HSC" && word.Text is "-MODE:0" or "-MODE:1")
            {
                mode = word.Text.EndsWith('0') ? "FINISH" : "ROUGH";
            }
            else
            {
                return $"{word.Address}{word.Text} has no NCX word in cycle 32 (D85)";
            }
        }

        if (tolerance is null
            || (HeidenhainNumbers.NumberOf(tolerance) == 0m && (rotary is not null || mode is not null)))
        {
            return "cycle 32 names no tolerance T, or switches it off with further values";
        }

        var words = new HeidenhainDraftBlock();
        bool off = HeidenhainNumbers.NumberOf(tolerance) == 0m;
        words.Add("TOLERANCE", off ? new IdentValue("OFF") : tolerance);
        if (rotary is not null)
        {
            words.Add("TOLERANCE", "ROTARY", rotary);
        }

        if (mode is not null)
        {
            words.Add("TOLERANCE_MODE", new IdentValue(mode));
        }

        block.Draft.Before.Add(words);
        return null;
    }

    // The chain changes as the control's transforms do (HeidenhainChain.TryReplace); a cancel where no entry of the
    // kind is active writes the RESET alone, so that the block keeps its place. A change the chain cannot express
    // leaves the chain unknown to the reader.
    private static string? Change(HeidenhainBlock block, string kind, HeidenhainDraftBlock? next)
    {
        var blocks = new List<HeidenhainDraftBlock>();
        HeidenhainState state = block.Heidenhain;
        HeidenhainChainEntry? entry = next is null ? null : new HeidenhainChainEntry(kind, next);
        if (!state.Chain.TryReplace(kind, entry, blocks))
        {
            state.Chain.Forget();
            return $"the new {kind} of the cycle would replace an entry of the chain of transforms that is not the "
                + "last one, or one the reader does not know (language 4.2, D31)";
        }

        if (blocks.Count == 0)
        {
            blocks.Add(HeidenhainChain.ResetOf(kind));
        }

        block.Draft.Before.AddRange(blocks);
        state.ForgetFrame();
        return null;
    }

    // The blocks CYCL DEF n.1, n.2 ... that follow CYCL DEF n.0 directly.
    private static List<SourceBlock> Members(HeidenhainBlock block, int number)
    {
        var members = new List<SourceBlock>();
        for (SourceBlock? next = block.Heidenhain.NextBlock(block.Source); next is not null;
            next = block.Heidenhain.NextBlock(next))
        {
            if (next.Words.Count < 3 || next.Words[0].Address != "CYCL" || next.Words[1].Address != "DEF"
                || !NumberOf(next.Words[2], out int cycle, out int part) || cycle != number || part == 0)
            {
                break;
            }

            members.Add(next);
        }

        return members;
    }

    // The parameter words of the members, after CYCL DEF n.k.
    private static List<SourceWord> WordsOf(List<SourceBlock> members)
    {
        var words = new List<SourceWord>();
        foreach (SourceBlock member in members)
        {
            for (int index = 3; index < member.Words.Count; index++)
            {
                words.Add(member.Words[index]);
            }
        }

        return words;
    }
}
