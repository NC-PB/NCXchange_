using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The motion of a Fanuc block (controllers fanuc.md 3, 4, 9 rule 1; controller-mapping 2; language 4.3): G0 to G3 with
/// the verb on every block, G90/G91 and the incremental addresses per word, I J K as the centre and R, the helix, F,
/// the tool vector of G43.5, and the modal words of the plane, the units, the feed mode and the compensation where the
/// source changes them; G4 as DWELL. Chamfers and roundings are FanucCorners, G16 polar coordinates
/// FanucPolarCoordinates.
/// </summary>
internal static class FanucMotion
{
    /// <summary>
    /// Reads the modal words, the dwell, the feed and the motion of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(FanucBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        ReadModalWords(block);
        ReadDwell(block);
        ReadFeed(block);
        if (!block.Draft.IsRaw)
        {
            ReadMotion(block);
        }
    }

    /// <summary>
    /// Follows a word on the source-side positions: an absolute number sets the axis, an incremental one moves a known
    /// axis, an expression makes it unknown (architecture 7).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="axis">The axis word.</param>
    /// <param name="value">Its NCX value.</param>
    public static void Move(FanucBlock block, FanucAxisWord axis, Value value)
    {
        decimal? number = FanucNumbers.NumberOf(value);
        if (number is not decimal distance)
        {
            block.State.ForgetPosition(axis.Axis);
        }
        else if (!axis.Incremental)
        {
            block.State.SetPosition(axis.Axis, distance);
        }
        else if (block.State.Positions.TryGetValue(axis.Axis, out decimal position))
        {
            block.State.SetPosition(axis.Axis, position + distance);
        }
    }

    /// <summary>
    /// The two axes of the working plane and their centre words: X Y with I J under G17, Z X with K I under G18, Y Z
    /// with J K under G19, X C with I J under polar interpolation (language 4.2; controller-mapping 2; D102).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="first">The first axis of the plane.</param>
    /// <param name="second">The second axis of the plane.</param>
    /// <param name="firstCentre">The centre word of the first axis.</param>
    /// <param name="secondCentre">The centre word of the second axis.</param>
    /// <param name="outside">The centre word of the axis outside the plane.</param>
    public static void PlaneAxes(FanucBlock block, out string first, out string second, out string firstCentre,
        out string secondCentre, out string outside)
    {
        string? plane = block.Fanuc.Polar ? "POLAR" : block.State.ActiveCode(FanucModalGroups.Plane);
        first = "X";
        second = "Y";
        firstCentre = "I";
        secondCentre = "J";
        outside = "K";
        if (plane == "POLAR")
        {
            second = "C";
        }
        else if (plane == "G18")
        {
            first = "Z";
            second = "X";
            firstCentre = "K";
            secondCentre = "I";
            outside = "J";
        }
        else if (plane == "G19")
        {
            first = "Y";
            second = "Z";
            firstCentre = "J";
            secondCentre = "K";
            outside = "I";
        }
    }

    /// <summary>
    /// True when the plane holds an X axis programmed in diameters, whose I is a radius value (D60): the reader does
    /// not add a radius to a diameter.
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="first">The first axis of the plane.</param>
    /// <param name="second">The second axis of the plane.</param>
    public static bool DiameterInPlane(FanucBlock block, string first, string second)
    {
        return (first == "X" || second == "X") && block.Machine.ResolveAxis("X")?.Programming == Programming.Diameter;
    }

    // The modal words the source changes: WORKPLANE from G17 to G19, UNITS from G20/G21 (G70/G71 in system C),
    // FEED_MODE from G94/G95 (G98/G99 in system A), COMP from G40 to G42; G0 to G3, G90/G91 and G15/G16 are read into
    // the verb and the words of the block (controllers fanuc.md 3, 9 rule 1; controller-mapping 1, 2).
    private static void ReadModalWords(FanucBlock block)
    {
        foreach (SourceWord word in block.Unread())
        {
            string? code = word.Address == "G" ? NativeCode.Of(word) : null;
            if (code is "G0" or "G1" or "G2" or "G3" or "G15" or "G16"
                || (code is "G90" or "G91" && block.System != GcodeSystem.A))
            {
                block.MarkRead(word);
                continue;
            }

            string? key = ModalWord(code, block.System, out string value);
            if (key is null)
            {
                continue;
            }

            block.MarkRead(word);
            if (block.Fanuc.TakeChange(key, value))
            {
                block.Draft.Main.Add(key, new IdentValue(value));
            }
        }
    }

    // The modal word of a code of groups 02, 05, 06 and 07 and its value; null for another code.
    private static string? ModalWord(string? code, GcodeSystem? system, out string value)
    {
        bool systemA = system == GcodeSystem.A;
        bool systemC = system == GcodeSystem.C;
        value = code switch
        {
            "G17" => "XY",
            "G18" => "ZX",
            "G19" => "YZ",
            "G20" when !systemC => "INCH",
            "G21" when !systemC => "MM",
            "G70" when systemC => "INCH",
            "G71" when systemC => "MM",
            "G94" when !systemA => "PER_MIN",
            "G95" when !systemA => "PER_REV",
            "G98" when systemA => "PER_MIN",
            "G99" when systemA => "PER_REV",
            "G40" => "OFF",
            "G41" => "LEFT",
            "G42" => "RIGHT",
            _ => "",
        };

        return code switch
        {
            "G17" or "G18" or "G19" => "WORKPLANE",
            "G40" or "G41" or "G42" => "COMP",
            _ when value is "INCH" or "MM" => "UNITS",
            _ when value is "PER_MIN" or "PER_REV" => "FEED_MODE",
            _ => null,
        };
    }

    // G4 P dwells P milliseconds, G4 X and G4 U seconds (controllers fanuc.md 4; controller-mapping 1, DWELL).
    // TODO(question): controller-mapping 1 gives the unit of P "ms or s per TOML" and no key of the machine
    // configuration carries it (wave-1 question #14); P is taken in milliseconds, as fanuc 4 writes it.
    private static void ReadDwell(FanucBlock block)
    {
        if (!block.TakeCode("G4"))
        {
            return;
        }

        SourceWord? milliseconds = block.Take("P");
        SourceWord? seconds = milliseconds is null ? block.Take("X") ?? block.Take("U") : null;
        if (milliseconds?.Number is decimal duration && milliseconds.Expression is null)
        {
            block.Draft.Main.Add("DWELL", FanucNumbers.Of(duration / 1000m));
        }
        else if (seconds?.Expression is null && seconds?.ToNcxNumber() is Value time)
        {
            block.Draft.Main.Add("DWELL", time);
        }
        else
        {
            block.Draft.KeepAsRaw("G4 carries no dwell time P, X or U as a number");
        }
    }

    // F is the feed in the active feed mode, modal (language 4.3); the control keeps it for the blocks after it.
    private static void ReadFeed(FanucBlock block)
    {
        SourceWord? feed = block.Take("F");
        if (feed is null || FanucMacro.ValueOf(block, feed) is not Value value)
        {
            return;
        }

        block.Draft.Main.Add("F", value);
        block.Fanuc.ControlFeed = value;
        block.Fanuc.WrittenFeed = value;
    }

    // A block with axis words moves with the verb of the active code of group 01, which the reader writes on every
    // motion block, and the axis words as written, X= absolute and IX= incremental (controllers fanuc.md 9 rule 1,
    // controller-mapping 2).
    private static void ReadMotion(FanucBlock block)
    {
        string? motion = block.State.ActiveCode(FanucModalGroups.Motion);
        bool arc = motion is "G2" or "G3";
        bool vector = block.Fanuc.VectorTcpm && motion == "G1";
        List<FanucAxisWord> axes = FanucAxes.Unread(block);
        bool centreOnly = (arc || vector) && (block.Find("I") ?? block.Find("J") ?? block.Find("K")) is not null;
        if (axes.Count == 0 && !centreOnly)
        {
            return;
        }

        // A subprogram runs with the state of its caller, and a caller continues with the state its subprogram left
        // (virtual machine 3.9): where the reader does not know a fact the block moves with, the block stays RAW
        // (FanucCallerState; D5).
        if (UnknownFact(block, axes, arc) is string fact)
        {
            block.Draft.KeepAsRaw(FanucUnknowns.Reason(fact));
            return;
        }

        string? verb = VerbOf(motion, out Value verbValue);
        if (verb is null)
        {
            block.Draft.KeepAsRaw($"{motion} has no NCX motion word (controller-mapping 2)");
            return;
        }

        DraftBlock main = block.Draft.Main.WithVerb(verb, verbValue);
        if (block.MachineFrame)
        {
            main.Add("FRAME", new IdentValue("MACHINE"));
        }

        var start = new Dictionary<string, decimal>(block.State.Positions, StringComparer.Ordinal);
        if (block.State.ActiveCode(FanucModalGroups.PolarCoordinates) == "G16")
        {
            if (!FanucPolarCoordinates.Read(block, axes, main))
            {
                return;
            }

            axes = FanucAxes.Unread(block);
        }

        bool incremental = block.Incremental;
        foreach (FanucAxisWord axis in axes)
        {
            block.MarkRead(axis.Word);
            if (FanucMacro.ValueOf(block, axis.Word) is not Value read)
            {
                return;
            }

            // The line after an expanded corner starts at the end of the corner (D58).
            Value value = FanucCorners.FromCornerEnd(block, axis, read);
            main.Add(axis.Key, value);
            incremental |= axis.Incremental;
            Move(block, axis, value);
            if (block.MachineFrame)
            {
                // After a machine-frame move the axis is unknown in the workpiece frame (virtual machine 3.4, D35).
                block.State.ForgetPosition(axis.Axis);
            }
        }

        if (arc)
        {
            ReadArc(block, main, start, incremental);
        }
        else if (vector)
        {
            ReadToolVector(block, main);
        }

        if (!block.Draft.IsRaw)
        {
            AddModalFeed(block, main, verb);
            FanucCorners.Read(block, main, start);
        }
    }

    // The fact of the modal state a motion block moves with that the reader does not know: the code of group 01, G90 or
    // G91 for an absolute address, G15 or G16, tool center point control for I J K on a line, and for an arc or a
    // corner the plane and polar interpolation.
    private static string? UnknownFact(FanucBlock block, List<FanucAxisWord> axes, bool arc)
    {
        FanucUnknowns unknown = block.Fanuc.Unknowns;
        bool planar = arc || block.Find(",C") is not null || block.Find(",R") is not null;
        if (unknown.Groups.Contains(FanucModalGroups.Motion))
        {
            return "the code of group 01, G0 to G3";
        }

        if (unknown.Groups.Contains(FanucModalGroups.Distance) && axes.Exists(axis => !axis.Incremental))
        {
            return "G90 or G91";
        }

        if (unknown.Groups.Contains(FanucModalGroups.PolarCoordinates))
        {
            return "G15 or G16";
        }

        if (!arc && unknown.Tcpm && (block.Find("I") ?? block.Find("J") ?? block.Find("K")) is not null)
        {
            return "tool center point control, G43.4 or G43.5";
        }

        if (planar && unknown.Groups.Contains(FanucModalGroups.Plane))
        {
            return "the plane, G17 to G19";
        }

        return planar && unknown.Polar ? "polar interpolation, G12.1 or G13.1" : null;
    }

    // G0 is RAPID, G1 LINE, G2 ARC=CW, G3 ARC=CCW (controller-mapping 2).
    // TODO(question): the documents do not give the code of group 01 that is active before the source writes one (a
    // parameter of the control); a block with axis words before any G0 to G3, G53 X#528 of the Nakamura program, moves
    // at RAPID until D242 is answered.
    private static string? VerbOf(string? motion, out Value value)
    {
        value = motion switch
        {
            "G2" => new IdentValue("CW"),
            "G3" => new IdentValue("CCW"),
            _ => NoValue.Instance,
        };

        return motion switch
        {
            null or "G0" => "RAPID",
            "G1" => "LINE",
            "G2" or "G3" => "ARC",
            _ => null,
        };
    }

    // The centre of an arc is I J K, incremental from the start point in the plane of G17 to G19, or its radius R
    // (controllers fanuc.md 4). The reader computes the absolute centre from I J K and the start point where it knows
    // the start point, and keeps I J K as CENTER:IX where it does not (controller-mapping 2, the rows CENTER:X and
    // CENTER:IX; language 6 and examples/2.5D_FRAESEN.ncx: G3 X70. Y50. I-.534 J-19.993 is CENTER:X=50 CENTER:Y=50).
    // TODO(question): controller-mapping 2 gives I J K two readings (CENTER:X computed from I J K and the start point,
    // CENTER:IX as I J K) and the phase plan names CENTER:IX; the reader computes the absolute centre under G90 from a
    // known start point, and keeps CENTER:IX under G91, from an unknown start point and on a diameter X axis (D60),
    // until D208 is answered.
    private static void ReadArc(FanucBlock block, DraftBlock main, Dictionary<string, decimal> start,
        bool incremental)
    {
        PlaneAxes(block, out string first, out string second, out string firstCentre, out string secondCentre,
            out string outside);
        SourceWord? radius = block.Take("R");
        SourceWord? firstWord = block.Take(firstCentre);
        SourceWord? secondWord = block.Take(secondCentre);
        if (block.Find(outside) is not null)
        {
            block.Draft.KeepAsRaw($"{outside} is no centre word of the working plane");
            return;
        }

        if (radius is not null)
        {
            if (firstWord is not null || secondWord is not null)
            {
                block.Draft.KeepAsRaw("an arc carries its centre or its radius, not both");
            }
            else if (FanucMacro.ValueOf(block, radius) is Value value)
            {
                main.Add("R", value);
            }

            return;
        }

        if (firstWord is null && secondWord is null)
        {
            block.Draft.KeepAsRaw("an arc needs its centre I J K or its radius R");
            return;
        }

        // TODO(question): fanuc 4 does not say what an arc is whose I or J is left out, and the VM needs both axes of
        // the centre (virtual machine 3.2); the reader takes a centre word left out as 0 until D241 is answered.
        Value zero = new IntegerValue(0, "0");
        Value? firstValue = firstWord is null ? zero : FanucMacro.ValueOf(block, firstWord);
        Value? secondValue = secondWord is null ? zero : FanucMacro.ValueOf(block, secondWord);
        if (firstValue is null || secondValue is null)
        {
            return;
        }

        decimal? firstOffset = FanucNumbers.NumberOf(firstValue);
        decimal? secondOffset = FanucNumbers.NumberOf(secondValue);
        bool knownStart = start.TryGetValue(first, out decimal firstStart)
            & start.TryGetValue(second, out decimal secondStart);
        bool absolute = !incremental && knownStart && firstOffset is not null && secondOffset is not null
            && !block.Fanuc.Polar && !DiameterInPlane(block, first, second);
        if (absolute)
        {
            main.Add("CENTER", first, FanucNumbers.Of(firstStart + firstOffset!.Value));
            main.Add("CENTER", second, FanucNumbers.Of(secondStart + secondOffset!.Value));
            return;
        }

        main.Add("CENTER", "I" + first, firstValue);
        main.Add("CENTER", "I" + second, secondValue);
    }

    // Under G43.5 the I J K of a line are the tool vector, TX TY TZ, all three together (controller-mapping 2, D81).
    private static void ReadToolVector(FanucBlock block, DraftBlock main)
    {
        SourceWord? toolX = block.Take("I");
        SourceWord? toolY = block.Take("J");
        SourceWord? toolZ = block.Take("K");
        if (toolX is null && toolY is null && toolZ is null)
        {
            return;
        }

        if (toolX is null || toolY is null || toolZ is null)
        {
            block.Draft.KeepAsRaw("the tool vector of G43.5 needs I, J and K (D81)");
            return;
        }

        Value? x = FanucMacro.ValueOf(block, toolX);
        Value? y = FanucMacro.ValueOf(block, toolY);
        Value? z = FanucMacro.ValueOf(block, toolZ);
        if (x is not null && y is not null && z is not null)
        {
            main.Add("TX", x).Add("TY", y).Add("TZ", z);
        }
    }

    // A Fanuc F stays for the blocks after it, the F of a cycle block too, while CYCLE_F never touches the F of NCX
    // (controllers fanuc.md 2, 6; D29): a feed motion after such a block gets the F of the control, so that it moves
    // as the source does (language 2 rule 4, explicit where controllers are implicit).
    // TODO(question): the documents do not say where the F of a Fanuc cycle block, which is also the modal F of the
    // control, stands in NCX besides CYCLE_F; the reader writes it on the next LINE or ARC that moves with it until
    // D218 is answered.
    private static void AddModalFeed(FanucBlock block, DraftBlock main, string verb)
    {
        Value? feed = block.Fanuc.ControlFeed;
        if (verb == "RAPID" || main.Has("F", null) || feed is null)
        {
            return;
        }

        if (block.Fanuc.WrittenFeed is Value written && written.ToCanonical() == feed.ToCanonical())
        {
            return;
        }

        main.Add("F", feed);
        block.Fanuc.WrittenFeed = feed;
    }
}
