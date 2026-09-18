using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The straight motion of a Klartext block (controllers heidenhain.md 2, 7 rules 1 and 5; controller-mapping 2;
/// language 4.3): L with FMAX is RAPID, L with F or the modal feed LINE; X is absolute and IX incremental, never modal;
/// R0, RL and RR are COMP; M91 is FRAME=MACHINE; LN carries the tool vector and the surface normal; LP is converted to
/// Cartesian with the pole of CC. The arcs are HeidenhainArcs.
/// </summary>
internal static class HeidenhainMotion
{
    // The axis addresses of a motion block, absolute and with the I of the incremental form (controllers heidenhain.md
    // 2).
    private static readonly string[] s_axes = ["X", "Y", "Z", "A", "B", "C", "U", "V", "W"];

    /// <summary>
    /// Reads L X+10 Y-5 Z+2 R0 FMAX M3 (controllers heidenhain.md 2).
    /// </summary>
    /// <param name="block">The block being read, whose first word is L.</param>
    public static void ReadLine(HeidenhainBlock block)
    {
        block.MarkLeading(1);
        bool rapid = ReadFeed(block, canBeRapid: true);
        ReadCompensation(block);
        bool machine = block.TakeCode("M91");
        HeidenhainPoint? start = block.Heidenhain.Position();
        List<SourceWord> axes = AxisWords(block, s_axes);
        if (block.Draft.IsRaw)
        {
            return;
        }

        if (axes.Count == 0)
        {
            if (machine)
            {
                block.Draft.KeepAsRaw("M91 gives the coordinates of a motion in the machine frame, and L names none");
            }

            return;
        }

        // M91 in the block: the coordinates come from the machine datum, FRAME=MACHINE (controllers heidenhain.md 2;
        // controller-mapping 1); afterwards the moved axes are unknown in the workpiece frame (virtual machine 3.4).
        HeidenhainDraftBlock main = block.Draft.Main.WithVerb(rapid ? "RAPID" : "LINE");
        if (machine)
        {
            main.Add("FRAME", new IdentValue("MACHINE"));
        }

        if (!AddAxes(block, main, axes))
        {
            return;
        }

        if (machine)
        {
            foreach (SourceWord axis in axes)
            {
                block.State.ForgetPosition(AxisOf(axis));
            }
        }

        EndStraight(block, start);
    }

    /// <summary>
    /// Reads LN X Y Z NX NY NZ TX TY TZ F, the 5-axis line with the surface normal and the tool vector under M128
    /// (controllers heidenhain.md 2; controller-mapping 2, tool vectors; language 4.3; D81): LINE with TX TY TZ and NX
    /// NY NZ, only under TCPM=ON and with the tool vector.
    /// </summary>
    /// <param name="block">The block being read, whose first word is LN.</param>
    public static void ReadVectorLine(HeidenhainBlock block)
    {
        block.MarkLeading(1);
        if (ReadFeed(block, canBeRapid: true))
        {
            block.Draft.KeepAsRaw("LN with FMAX: the tool vector belongs to LINE (language 4.3, D81)");
            return;
        }

        ReadCompensation(block);
        HeidenhainPoint? start = block.Heidenhain.Position();
        List<SourceWord> axes = AxisWords(block, s_axes);
        List<SourceWord> tool = Vector(block, "TX", "TY", "TZ");
        List<SourceWord> normal = Vector(block, "NX", "NY", "NZ");
        if (tool.Count != 3 || normal.Count is not (0 or 3))
        {
            block.Draft.KeepAsRaw("LN without the tool vector TX TY TZ is 3D radius compensation, which NCX has no "
                + "word for; the surface normal NX NY NZ stands only with it (D81)");
            return;
        }

        if (block.Heidenhain.Tcpm != true)
        {
            block.Draft.KeepAsRaw("the tool vector of LN needs TCPM=ON, which M128 switches on (language 4.3, D81)");
            return;
        }

        HeidenhainDraftBlock main = block.Draft.Main.WithVerb("LINE");
        if (!AddAxes(block, main, axes))
        {
            return;
        }

        tool.AddRange(normal);
        foreach (SourceWord component in tool)
        {
            if (ValueOf(block, component) is Value value)
            {
                main.Add(component.Address, value);
            }
        }

        EndStraight(block, start);
    }

    /// <summary>
    /// Reads LP PR+50 PA+30, a line to a point in polar coordinates about the pole of CC, converted to Cartesian
    /// (controllers heidenhain.md 2, 7 rule 5; controller-mapping 2, ANGLE; D84).
    /// </summary>
    /// <param name="block">The block being read, whose first word is LP.</param>
    public static void ReadPolarLine(HeidenhainBlock block)
    {
        block.MarkLeading(1);
        bool rapid = ReadFeed(block, canBeRapid: true);
        ReadCompensation(block);
        HeidenhainState state = block.Heidenhain;
        HeidenhainPoint? start = state.Position();
        if (!state.PlaneAxes(out string first, out string second, out string toolAxis) || state.Pole is null)
        {
            block.Draft.KeepAsRaw("LP needs the pole of a CC in a known working plane (controllers heidenhain.md 2)");
            return;
        }

        SourceWord? radius = block.Take("PR") ?? block.Take("IPR");
        SourceWord? angle = block.Take("PA") ?? block.Take("IPA");
        if (!HeidenhainArcs.PolarPoint(block, radius, angle, out decimal x, out decimal y))
        {
            return;
        }

        List<SourceWord> tool = AxisWords(block, [toolAxis]);
        HeidenhainDraftBlock main = block.Draft.Main.WithVerb(rapid ? "RAPID" : "LINE")
            .Add(first, HeidenhainNumbers.Of(x)).Add(second, HeidenhainNumbers.Of(y));
        if (!AddAxes(block, main, tool))
        {
            return;
        }

        block.State.SetPosition(first, x);
        block.State.SetPosition(second, y);
        EndStraight(block, start);
    }

    /// <summary>
    /// Reads FMAX, rapid for the block, or F, the feed that stays (controllers heidenhain.md 2; language 4.3, F); an F
    /// after M128 is the feed of M128, not of the block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="canBeRapid">False for an arc, which moves at the feed.</param>
    /// <returns>True for FMAX.</returns>
    public static bool ReadFeed(HeidenhainBlock block, bool canBeRapid)
    {
        bool rapid = block.Take("FMAX") is not null;
        if (rapid && !canBeRapid)
        {
            block.Draft.KeepAsRaw("an arc moves at the feed, FMAX is for straight moves (controllers heidenhain.md 2)");
        }

        SourceWord? feed = block.Find("F");
        SourceWord? tcpm = block.FindCode("M128");
        if (feed is null || (tcpm is not null && block.IndexOf(tcpm) < block.IndexOf(feed)))
        {
            return rapid;
        }

        block.MarkRead(feed);
        int next = block.IndexOf(feed) + 1;
        bool auto = next < block.Source.Words.Count && block.Source.Words[next].Address == "AUTO";

        // TODO(question): heidenhain 2 gives F AUTO the feed of the tool table, which neither an NCX word nor a key of
        // the machine file holds; the block is kept RAW until D251 is answered.
        if (auto || feed.Text.Length == 0)
        {
            block.Draft.KeepAsRaw(auto
                ? "F AUTO takes the feed of the tool table, which NCX does not know (controllers heidenhain.md 2)"
                : "F names no feed");
        }
        else if (rapid)
        {
            block.Draft.KeepAsRaw("a block moves at FMAX or at F, not both");
        }
        else if (ValueOf(block, feed) is Value value)
        {
            block.Draft.Main.Add("F", value);
        }

        return rapid;
    }

    /// <summary>
    /// Reads R0, RL and RR: radius compensation off, left and right from the motion of the block on, COMP (controllers
    /// heidenhain.md 2; controller-mapping 2, COMP; language 5 rule 3). The source writes it on the block, and so does
    /// the reader, also where it does not change (examples/2.5D_FRAESEN.ncx, H9).
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void ReadCompensation(HeidenhainBlock block)
    {
        foreach (SourceWord word in block.Unread())
        {
            string? value = word.Address switch
            {
                "R" when word.Text == "0" => "OFF",
                "RL" when word.Text.Length == 0 => "LEFT",
                "RR" when word.Text.Length == 0 => "RIGHT",
                _ => null,
            };
            if (value is not null)
            {
                block.MarkRead(word);
                block.Draft.AddState("COMP", null, new IdentValue(value));
                return;
            }
        }
    }

    /// <summary>
    /// The unread axis words of the block among the given axes, absolute and incremental, each with a value.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="axes">The axis names, "X".</param>
    public static List<SourceWord> AxisWords(HeidenhainBlock block, IReadOnlyList<string> axes)
    {
        var words = new List<SourceWord>();
        foreach (SourceWord word in block.Unread())
        {
            string axis = word.Address.StartsWith('I') ? word.Address.Substring(1) : word.Address;
            if (word.Text.Length > 0 && axes.Contains(axis))
            {
                block.MarkRead(word);
                words.Add(word);
            }
        }

        return words;
    }

    /// <summary>
    /// Adds the axis words to an NCX block, X= absolute and IX= incremental as written (language 4.3), and follows them
    /// on the positions; false when a value has no NCX form, and the block is kept RAW.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="main">The NCX block.</param>
    /// <param name="axes">The axis words.</param>
    public static bool AddAxes(HeidenhainBlock block, HeidenhainDraftBlock main, List<SourceWord> axes)
    {
        foreach (SourceWord axis in axes)
        {
            if (ValueOf(block, axis) is not Value read)
            {
                return false;
            }

            // The line after an expanded chamfer or rounding starts at the end of the corner (D58).
            Value value = HeidenhainCorners.FromCornerEnd(block, axis, read);
            main.Add(axis.Address, value);
            Move(block, axis, value);
        }

        return true;
    }

    /// <summary>
    /// The NCX value of a word: the number as written, or the expression of a Q parameter, X+Q1 as {$Q1}; null, with
    /// the block kept RAW, when NCX cannot express it.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="word">The word.</param>
    public static Value? ValueOf(HeidenhainBlock block, SourceWord word)
    {
        Value? value = HeidenhainExpression.ValueOf(word.Text, block.Line, block.Diagnostics.File, out string? problem);
        if (value is null)
        {
            block.Draft.KeepAsRaw($"{word.Address}{word.Text}: {problem}");
        }

        return value;
    }

    /// <summary>
    /// The axis a word moves, X of IX.
    /// </summary>
    /// <param name="word">The axis word.</param>
    public static string AxisOf(SourceWord word)
    {
        return word.Address.StartsWith('I') ? word.Address.Substring(1) : word.Address;
    }

    // Follows a word on the source-side positions: an absolute number sets the axis, an incremental one moves a known
    // axis, an expression makes it unknown (architecture 7).
    private static void Move(HeidenhainBlock block, SourceWord word, Value value)
    {
        string axis = AxisOf(word);
        decimal? number = HeidenhainNumbers.NumberOf(value);
        if (number is not decimal distance)
        {
            block.State.ForgetPosition(axis);
        }
        else if (!word.Address.StartsWith('I'))
        {
            block.State.SetPosition(axis, distance);
        }
        else if (block.State.Positions.TryGetValue(axis, out decimal position))
        {
            block.State.SetPosition(axis, position + distance);
        }
    }

    // A straight element ends in its own direction, which a CT after it continues (controllers heidenhain.md 2); a
    // motion that leaves the plane point where it was, or from or to an unknown point, gives no direction.
    private static void EndStraight(HeidenhainBlock block, HeidenhainPoint? start)
    {
        HeidenhainPoint? end = block.Heidenhain.Position();
        block.Heidenhain.Tangent = start is not null && end is not null
            && (start.First != end.First || start.Second != end.Second)
            ? new HeidenhainPoint(end.First - start.First, end.Second - start.Second)
            : null;
    }

    // The three components of a vector, all or none of them.
    private static List<SourceWord> Vector(HeidenhainBlock block, string first, string second, string third)
    {
        var components = new List<SourceWord>();
        foreach (string address in new[] { first, second, third })
        {
            if (block.Take(address) is SourceWord word)
            {
                components.Add(word);
            }
        }

        return components;
    }
}
