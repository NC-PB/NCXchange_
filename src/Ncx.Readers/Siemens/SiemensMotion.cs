using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The motion of a SINUMERIK block (controllers siemens.md 3, 6, 11 rules 2 and 9; controller-mapping 2; language 4.3):
/// G0 to G3, CIP and CT with the verb on every block; X10 absolute under G90 and incremental under G91, X=AC() and
/// X=IC() per word; C=DC(), ACP(), ACN() as C= with the direction kept as RAW; the extended axes Z2=, C4= by the
/// letters of the machine; FRAME=MACHINE under G53, G153 and SUPA; the tool vector A3 B3 C3 and the normal A5 B5 C5
/// under TRAORI; F, FB restored after its block, G4 as DWELL. The arcs are SiemensArcs, the polar coordinates
/// SiemensPolar, the chamfers and roundings SiemensCorners; under MCALL a block with a position calls the cycle
/// (SiemensCycles).
/// </summary>
internal static class SiemensMotion
{
    // The address words of a motion block that NCX has no word for, kept as RAW words (controllers siemens.md 3, 6;
    // controller-mapping 2 and 9): the feed per tooth, the velocity limits, the override, the accelerations, the
    // smoothing distances, the tolerances of an axis, the contour allowance, the transition circle, the cutting speed
    // of a tool, the reference axis of G96, the lead and tilt angles.
    private static readonly HashSet<string> s_rawAddresses = new(StringComparer.Ordinal)
    {
        "FZ", "FL", "FGROUP", "FA", "OVR", "ACC", "ADIS", "ADISPOS", "ATOL", "OFFN", "DISC", "SVC", "SCC", "LEAD",
        "TILT", "FGREF",
    };

    /// <summary>
    /// Reads the dwell, the words kept as RAW, the feed and the motion of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        ReadDwell(block);
        ReadRawWords(block);
        if (!block.Draft.IsRaw)
        {
            ReadMotion(block);
        }

        // RNDM and FRCM in a block without a line switch the rounding of the corners after it.
        if (!block.Draft.IsRaw)
        {
            SiemensCorners.ReadModal(block);
        }
    }

    /// <summary>
    /// Follows a word on the source-side positions: an absolute number sets the axis, an incremental one moves a known
    /// axis, an expression makes it unknown (architecture 7).
    /// </summary>
    public static void Move(SiemensBlock block, string axis, Value value, bool incremental)
    {
        decimal? number = SiemensNumbers.NumberOf(value);
        if (number is not decimal distance)
        {
            block.State.ForgetPosition(axis);
        }
        else if (!incremental)
        {
            block.State.SetPosition(axis, distance);
        }
        else if (block.State.Positions.TryGetValue(axis, out decimal position))
        {
            block.State.SetPosition(axis, position + distance);
        }
    }

    /// <summary>
    /// The value of a word written as NAME(value), AC(10), IC(5), DC(90); null for a word of another form.
    /// </summary>
    /// <param name="text">The value as written.</param>
    /// <param name="name">The name before the parentheses in capitals.</param>
    public static string? Inner(string text, out string name)
    {
        name = "";
        int open = text.IndexOf('(', StringComparison.Ordinal);
        if (open <= 0 || !text.EndsWith(')') || SiemensScanner.ReadGroup(text, open) != text.Length)
        {
            return null;
        }

        name = text.Substring(0, open).Trim().ToUpperInvariant();
        return text.Substring(open + 1, text.Length - open - 2).Trim();
    }

    /// <summary>
    /// A feed motion without F gets the F of the control where NCX wrote another one last, so that it moves as the
    /// source does after a cycle took the modal F as CYCLE_F, or an FB fed one block (controller-mapping 2 and 5,
    /// FB and
    /// CYCLE_F; D29).
    /// </summary>
    public static void AddModalFeed(SiemensBlock block, SiemensDraftBlock main)
    {
        SiemensFacts facts = block.Facts;
        if (main.Has("F", null) || facts.ControlFeed is not Value feed)
        {
            return;
        }

        if (facts.WrittenFeed is Value written && written.ToCanonical() == feed.ToCanonical())
        {
            return;
        }

        main.Add("F", feed);
        facts.WrittenFeed = feed;
    }

    // G4 F1.5 dwells 1.5 seconds; G4 S10 dwells 10 revolutions of the master spindle and G4 S2=10 of spindle 2, which
    // the reader converts with the speed it knows, else the block stays RAW; the F and S of a dwell block do not touch
    // the modal feed and speed (controllers siemens.md 3; controller-mapping 1, DWELL).
    private static void ReadDwell(SiemensBlock block)
    {
        if (!block.TakeCode("G4"))
        {
            return;
        }

        if (block.Take("F") is SourceWord seconds)
        {
            if (SiemensExpression.ValueOf(block, seconds.Text, out string? problem) is Value time)
            {
                block.Draft.Main.Add("DWELL", time);
            }
            else
            {
                block.Draft.KeepAsRaw($"the dwell F{seconds.Text} has no NCX value: {problem}");
            }

            return;
        }

        // S and S0= count the revolutions of the master spindle, S2= those of spindle 2 (controllers siemens.md 5).
        SourceWord? revolutions = block.Take("S");
        string? role = revolutions is null ? null : SiemensSpindles.MasterRole(block);
        if (revolutions is null)
        {
            foreach (SourceWord word in block.Unread())
            {
                if (SiemensSpindles.IsSpeed(word) && SiemensSpindles.NumberOfAddress(word.Address, 'S') is int spindle)
                {
                    revolutions = word;
                    role = spindle == 0 ? SiemensSpindles.MasterRole(block) : SiemensSpindles.RoleOf(block, spindle);
                    block.MarkRead(word);
                    break;
                }
            }
        }

        if (revolutions?.Number is decimal count && role is not null
            && block.Facts.Rpm.TryGetValue(role, out decimal rpm) && rpm > 0)
        {
            block.Draft.Main.Add("DWELL", SiemensNumbers.Of(Math.Round(count * 60m / rpm, 3,
                MidpointRounding.AwayFromZero)));
            return;
        }

        block.Draft.KeepAsRaw("G4 dwells by revolutions of a spindle whose speed the reader does not know, or by no "
            + "time (controller-mapping 1, DWELL)");
    }

    // The words NCX has no meaning for stay RAW words of the block (controllers siemens.md 11 rule 8); FGREF is also
    // ROTARY_FEED=MM_MIN, its radius kept as RAW (controller-mapping 1, ROTARY_FEED; D86); the Euler and RPY angles A2
    // B2 C2 and the normal at the start of the block A4 B4 C4 have no NCX word (controller-mapping 2, tool vectors); an
    // F under G93, inverse time, is none of NCX either (controller-mapping 2, F).
    private static void ReadRawWords(SiemensBlock block)
    {
        foreach (SourceWord word in block.Unread())
        {
            string name = word.Address.Split('[')[0];
            bool orientation = SiemensAxes.IsOrientation(word.Address) && word.Address[1] is '2' or '4'
                && SiemensAxes.AxisOf(block, word) is null;
            bool inverseTime = word.Address == "F" && block.Facts.FeedType == "G93";
            if (!s_rawAddresses.Contains(name) && !orientation && !inverseTime)
            {
                continue;
            }

            block.KeepAsRawWord(word);
            if (name == "FGREF")
            {
                SiemensGroups.AddModal(block, "ROTARY_FEED", "MM_MIN");
            }
        }
    }

    // A block with axis words, polar coordinates or the center of an arc moves with the verb of the active code of
    // group 1, which the reader writes on every motion block (controllers siemens.md 11 rule 2; controller-mapping 2).
    private static void ReadMotion(SiemensBlock block)
    {
        SiemensFacts facts = block.Facts;
        var plane = SiemensPlane.Of(facts.WorkingPlane);
        List<SourceWord> axes = AxisWords(block);
        bool polar = block.Find("AP") is not null || block.Find("RP") is not null;
        bool arcWords = block.Find(plane.FirstCenter) is not null || block.Find(plane.SecondCenter) is not null
            || block.Find("CR") is not null || block.Find("AR") is not null;
        string code = facts.MotionCode ?? "G0";
        bool arc = code is "G2" or "G3" or "CIP" or "CT";

        // G1 ANG=30 without an end coordinate is the first line of a contour of two blocks (controllers siemens.md 3).
        bool angle = block.Find("ANG") is not null;
        if (axes.Count == 0 && !polar && !angle && !(arc && arcWords))
        {
            ReadFeedAlone(block);
            if (block.MachineFrame is SourceWord frame && !block.IsRead(frame))
            {
                block.KeepAsRawWord(frame);
            }

            return;
        }

        // The code of group 1 the caller leaves, or a subprogram changed, may be unknown (virtual machine 3.9).
        if (facts.IsUnknown(SiemensFacts.Motion))
        {
            block.Draft.KeepAsRaw(Unknown("the code of group 1, G0 to G3"));
            return;
        }

        if (facts.ModalCycle is not null && !arc)
        {
            SiemensCycles.CallAt(block, axes);
            return;
        }

        if (arc && (facts.ModalCycle is not null || facts.TransformOn is "POLAR" or "CYLINDER"))
        {
            block.Draft.KeepAsRaw("an arc under MCALL, TRANSMIT or TRACYL is not read (controllers siemens.md 6, 7)");
            return;
        }

        // TODO(question): wave-2 question #56, the code of group 1 that is active before the source writes one; a block
        // with axis words before any G0 to G3 moves at RAPID.
        SiemensDraftBlock main = block.Draft.Main;
        if (!arc)
        {
            main.WithVerb(code == "G1" ? "LINE" : "RAPID");
        }

        if (block.MachineFrame is SourceWord machineFrame)
        {
            block.MarkRead(machineFrame);
            main.Add("FRAME", new IdentValue("MACHINE"));
        }

        var start = new Dictionary<string, decimal>(block.State.Positions, StringComparer.Ordinal);
        if (polar && !SiemensPolar.Read(block, main, plane, axes))
        {
            return;
        }

        foreach (SourceWord word in axes)
        {
            if (!AddAxis(block, main, word))
            {
                return;
            }
        }

        ReadVectors(block, main);
        if (arc && !block.Draft.IsRaw)
        {
            SiemensArcs.Read(block, main, code, plane, start);
        }

        if (block.Draft.IsRaw)
        {
            return;
        }

        ReadFeed(block, main);
        SiemensCorners.Read(block, main, plane, start);
        if (!arc)
        {
            Tangent(block, plane, start);
        }

        if (block.MachineFrame is not null)
        {
            // After a machine-frame move the axes are unknown in the workpiece frame (virtual machine 3.4, D35).
            foreach (SourceWord word in axes)
            {
                if (SiemensAxes.AxisOf(block, word) is string axis)
                {
                    block.State.ForgetPosition(axis);
                }
            }

            block.Facts.Tangent = null;
        }
    }

    // The words of the block that move an axis, with a value.
    private static List<SourceWord> AxisWords(SiemensBlock block)
    {
        var axes = new List<SourceWord>();
        foreach (SourceWord word in block.Unread())
        {
            if (word.Text.Length > 0 && !word.Text.StartsWith('(') && SiemensAxes.AxisOf(block, word) is not null)
            {
                axes.Add(word);
            }
        }

        return axes;
    }

    /// <summary>
    /// Reads an axis word into the block: X10 and X=10 absolute under G90 and incremental under G91, X=AC(10) absolute
    /// and X=IC(5) incremental per word; C=DC(90), ACP(90), ACN(90) C=90 with the direction kept as RAW; under DIAM90
    /// an incremental X is a radius, which NCX writes as the doubled IX of DIAMETER=ON (controllers siemens.md 3;
    /// controller-mapping 1, DIAMETER, and 2, IX). False, with the block kept RAW, where NCX cannot express it.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="main">The block the word goes into.</param>
    /// <param name="word">The axis word.</param>
    public static bool AddAxis(SiemensBlock block, SiemensDraftBlock main, SourceWord word)
    {
        block.MarkRead(word);
        string axis = SiemensAxes.AxisOf(block, word)!;
        string text = word.Text;
        bool incremental = block.Facts.Incremental;
        bool explicitForm = false;
        if (Inner(text, out string form) is string inner && form is "AC" or "IC" or "DC" or "ACP" or "ACN")
        {
            text = inner;
            incremental = form == "IC";
            explicitForm = true;
            if (form is "DC" or "ACP" or "ACN")
            {
                block.Draft.RawWords.Add(block.SpanOf(word));
            }
        }

        if (!explicitForm && block.Facts.IsUnknown(SiemensFacts.Distance))
        {
            block.Draft.KeepAsRaw(Unknown("G90 or G91"));
            return false;
        }

        Value? value = SiemensExpression.ValueOf(block, text, out string? problem);
        if (value is null)
        {
            block.Draft.KeepAsRaw($"{word.Address}={word.Text} has no NCX value: {problem}");
            return false;
        }

        if (incremental && axis == "X" && block.Facts.DiameterMode == "DIAM90")
        {
            value = Doubled(block, value);
        }

        main.Add(incremental ? "I" + axis : axis, value);
        Move(block, axis, value, incremental);
        return true;
    }

    // The tool direction A3 B3 C3 is TX TY TZ, the surface normal at the end of the block A5 B5 C5 is NX NY NZ,
    // each all
    // three together and only under TRAORI (controller-mapping 2, tool vectors; D81); under ORIMKS the vector means the
    // machine axes, which the reader reports.
    private static void ReadVectors(SiemensBlock block, SiemensDraftBlock main)
    {
        if (!Vector(block, main, ["A3", "B3", "C3"], ["TX", "TY", "TZ"]))
        {
            return;
        }

        Vector(block, main, ["A5", "B5", "C5"], ["NX", "NY", "NZ"]);
    }

    private static bool Vector(SiemensBlock block, SiemensDraftBlock main, string[] addresses, string[] keys)
    {
        var words = new List<SourceWord>();
        foreach (string address in addresses)
        {
            if (block.Find(address) is SourceWord word && SiemensAxes.AxisOf(block, word) is null)
            {
                words.Add(word);
            }
        }

        if (words.Count == 0)
        {
            return true;
        }

        if (words.Count < 3 || block.Facts.TransformOn != "TCPM" || (keys[0] == "NX" && !main.Has("TX", null)))
        {
            block.Draft.KeepAsRaw($"{addresses[0]} {addresses[1]} {addresses[2]} stand all three, under TRAORI, the "
                + "normal with the tool direction (controller-mapping 2, tool vectors; D81)");
            return false;
        }

        for (int index = 0; index < 3; index++)
        {
            block.MarkRead(words[index]);
            if (SiemensExpression.ValueOf(block, words[index].Text, out string? problem) is not Value value)
            {
                block.Draft.KeepAsRaw(problem!);
                return false;
            }

            main.Add(keys[index], value);
        }

        if (block.Facts.MachineOrientation && keys[0] == "TX")
        {
            block.Draft.Warnings.Add(new SiemensWarning(DiagnosticCodes.SiemensVectorInMachineSystem,
                "The tool vector A3 B3 C3 stands under ORIMKS, where it means the machine axes, "
                + "while TX TY TZ are meant "
                + "in the workpiece frame (controller-mapping 2, tool vectors)."));
        }

        return true;
    }

    // F is the feed, modal; FB feeds one block and the feed of the control returns after it (controller-mapping 2,
    // F): a line or an arc writes it, a rapid keeps it for the next feed motion.
    private static void ReadFeed(SiemensBlock block, SiemensDraftBlock main)
    {
        SiemensFacts facts = block.Facts;
        Value? feed = FeedValue(block, block.Take("F"));
        Value? blockwise = FeedValue(block, block.Take("FB"));
        if (block.Draft.IsRaw)
        {
            return;
        }

        if (feed is not null)
        {
            facts.ControlFeed = feed;
        }

        if (main.Verb is not ("LINE" or "ARC"))
        {
            return;
        }

        Value? written = blockwise ?? feed;
        if (written is not null)
        {
            main.Add("F", written);
            facts.WrittenFeed = written;
            return;
        }

        AddModalFeed(block, main);
    }

    // An F without motion sets the feed of the control; the F on the line before MCALL CYCLE81 or before a cycle call
    // is the feed of the cycle, CYCLE_F, and stands in NCX on the next feed motion (controller-mapping 5, CYCLE_F;
    // D29).
    private static void ReadFeedAlone(SiemensBlock block)
    {
        block.Take("FB");
        if (block.Take("F") is not SourceWord word || FeedValue(block, word) is not Value feed)
        {
            return;
        }

        block.Facts.ControlFeed = feed;
        if (SiemensCycles.DefinesNext(block))
        {
            return;
        }

        block.Draft.Main.Add("F", feed);
        block.Facts.WrittenFeed = feed;
    }

    private static Value? FeedValue(SiemensBlock block, SourceWord? word)
    {
        if (word is null)
        {
            return null;
        }

        Value? value = SiemensExpression.ValueOf(block, word.Text, out string? problem);
        if (value is null)
        {
            block.Draft.KeepAsRaw($"{word.Address}{word.Text} has no NCX value: {problem}");
        }

        return value;
    }

    // A line ends in its own direction, which a CT after it continues (controllers siemens.md 3).
    private static void Tangent(SiemensBlock block, SiemensPlane plane, Dictionary<string, decimal> start)
    {
        SiemensPoint? from = plane.PointOf(start);
        SiemensPoint? to = plane.PointOf(block.State.Positions);
        if (from is SiemensPoint a && to is SiemensPoint b && (a.First != b.First || a.Second != b.Second))
        {
            block.Facts.Tangent = new SiemensPoint(b.First - a.First, b.Second - a.Second);
        }
        else if (from is null || to is null)
        {
            block.Facts.Tangent = null;
        }
    }

    // Twice a value, for the radius an incremental X under DIAM90 gives.
    private static Value Doubled(SiemensBlock block, Value value)
    {
        if (SiemensNumbers.NumberOf(value) is decimal number)
        {
            return SiemensNumbers.Of(number * 2);
        }

        string text = value is ExprValue expression ? expression.Text : value.ToCanonical();
        return SiemensExpression.Parse(block, "2 * (" + text + ")", out _) ?? value;
    }

    /// <summary>
    /// Why a block that depends on a fact the reader does not know stays RAW (D5).
    /// </summary>
    /// <param name="fact">The fact, "G90 or G91".</param>
    public static string Unknown(string fact)
    {
        return $"the block depends on {fact}, which the reader does not know here: a subprogram runs with the state of "
            + "its caller, and a caller continues with the state its subprogram left (virtual machine 3.9)";
    }
}
