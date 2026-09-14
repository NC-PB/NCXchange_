using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The transformations, the diameter programming and the path tolerance of a SINUMERIK block (controllers siemens.md
/// 3, 6; controller-mapping 1; language 4.1, 4.2; D54, D60, D85, D96): TRAORI as TCPM, TRACYL(d) as CYLINDER with the
/// radius d/2, TRANSMIT as POLAR, TRAFOOF switching off the one that is on; DIAMON and DIAMOF as DIAMETER,
/// DIAM90 folded
/// into DIAMETER=ON with the incremental X doubled (SiemensMotion); CYCLE832(tol, mode, otol) as TOLERANCE with its
/// mode and rotary tolerance, CTOL and OTOL as TOLERANCE and TOLERANCE:ROTARY.
/// </summary>
internal static class SiemensTransformations
{
    /// <summary>
    /// Reads the transformations, the diameter programming and the tolerance of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        ReadTransformation(block);
        ReadDiameter(block);
        ReadCycle832(block);
        ReadTolerances(block);
    }

    // TRAORI and TRAORI(1) switch the 5-axis transformation on, TCPM=ON; TRAORI with a tool direction and rotary
    // offsets is RAW (controller-mapping 1, TCPM); TRACYL(d) is CYLINDER with the radius, TRACYL(d, n) RAW; TRANSMIT is
    // POLAR=ON; TRAFOOF switches off the one that is on (controller-mapping 1, CYLINDER and POLAR; D96, D102).
    private static void ReadTransformation(SiemensBlock block)
    {
        SiemensFacts facts = block.Facts;
        foreach (SourceWord word in block.Unread())
        {
            List<string> arguments = SiemensArguments.Split(word.Text);
            switch (word.Address)
            {
                case "TRAORI" when arguments.Count == 0 || (arguments.Count == 1 && arguments[0] == "1"):
                    block.MarkRead(word);
                    Switch(block, "TCPM", new IdentValue("ON"));
                    break;
                case "TRANSMIT" when arguments.Count == 0:
                    block.MarkRead(word);
                    Switch(block, "POLAR", new IdentValue("ON"));
                    break;
                case "TRACYL" when arguments.Count == 1:
                    block.MarkRead(word);
                    if (Radius(block, arguments[0]) is Value radius)
                    {
                        Switch(block, "CYLINDER", radius);
                    }

                    break;
                case "TRAFOOF" when word.Text.Length == 0:
                    block.MarkRead(word);
                    string off = facts.TransformOn.Length > 0 ? facts.TransformOn : "TCPM";
                    block.Draft.AddState(off, null, new IdentValue("OFF"));
                    facts.TransformOn = "";
                    facts.Unknown.Remove(SiemensFacts.Transform);
                    block.Siemens.ForgetPositions();
                    break;
                case "TRAORI" or "TRACYL" or "TRANSMIT" or "TRACON" or "TRAANG" or "TRAFOON":
                    block.MarkRead(word);
                    block.Draft.KeepAsRaw($"{word.Address}{word.Text} selects a transformation with parameters NCX has "
                        + "no word for (controller-mapping 1, CYLINDER, POLAR, TCPM)");
                    return;
            }
        }
    }

    private static void Switch(SiemensBlock block, string key, Value value)
    {
        block.Draft.AddState(key, null, value);
        block.Facts.TransformOn = key;
        block.Facts.Unknown.Remove(SiemensFacts.Transform);
        block.Siemens.ForgetPositions();
    }

    // TRACYL takes the working diameter, CYLINDER the reference radius (D96; millturn1.toml, CYLINDER_ON).
    private static Value? Radius(SiemensBlock block, string diameter)
    {
        Value? value = SiemensExpression.ValueOf(block, diameter, out string? problem);
        if (SiemensNumbers.NumberOf(value) is decimal number && number > 0)
        {
            return SiemensNumbers.Of(number / 2);
        }

        Value? half = value is ExprValue expression ? SiemensExpression.Parse(block, "(" + expression.Text + ") / 2",
            out problem) : null;
        if (half is null)
        {
            block.Draft.KeepAsRaw($"TRACYL({diameter}) gives no working diameter: {problem}");
        }

        return half;
    }

    // DIAMON and DIAMOF switch the diameter programming, DIAMETER; DIAM90 is diameter for absolute and radius for
    // incremental X, which the reader folds into DIAMETER=ON with the incremental X doubled; DIAMCYCOF acts inside the
    // cycles only and is ignored (controller-mapping 1, DIAMETER; D60).
    private static void ReadDiameter(SiemensBlock block)
    {
        foreach (SourceWord word in block.Unread())
        {
            if (word.Text.Length > 0 || word.Address is not ("DIAMON" or "DIAMOF" or "DIAM90" or "DIAMCYCOF"))
            {
                continue;
            }

            block.MarkRead(word);
            if (word.Address == "DIAMCYCOF")
            {
                continue;
            }

            block.Facts.DiameterMode = word.Address;
            block.Facts.Unknown.Remove(SiemensFacts.Diameter);
            SiemensGroups.AddModal(block, "DIAMETER", word.Address == "DIAMOF" ? "OFF" : "ON");
        }
    }

    // CYCLE832(tol, mode, otol) sets the path tolerance for HSC programs: the mode's ones digit 1 finish, 3 rough by
    // the machine's [tolerance] mode, 2 semi-finish and 4 precision without an NCX mode; the tens digit 1 takes the
    // orientation tolerance of the third parameter; CYCLE832(0, 0, 1) switches it off (controller-mapping 1,
    // TOLERANCE; D85).
    private static void ReadCycle832(SiemensBlock block)
    {
        if (block.Take("CYCLE832") is not SourceWord cycle)
        {
            return;
        }

        List<string> arguments = SiemensArguments.Split(cycle.Text);
        decimal? tolerance = arguments.Count > 0 ? SiemensNumbers.NumberOf(SiemensNumbers.Parse(arguments[0])) : null;
        int? mode = arguments.Count > 1 ? SiemensNumbers.WholeNumber(arguments[1]) : 0;
        if (tolerance is not decimal tol || mode is not int modeValue)
        {
            block.Draft.KeepAsRaw("CYCLE832 gives its tolerance and mode as numbers (controller-mapping 1, TOLERANCE)");
            return;
        }

        if (tol == 0)
        {
            block.Draft.AddState("TOLERANCE", null, new IdentValue("OFF"));
            block.Facts.ToleranceOn = false;
            return;
        }

        string? ncxMode = ModeOf(block, modeValue % 10);
        decimal? rotary = modeValue / 10 % 10 == 1 && arguments.Count > 2
            ? SiemensNumbers.NumberOf(SiemensNumbers.Parse(arguments[2]))
            : null;
        if (ncxMode is null || modeValue >= 20 || (modeValue / 10 % 10 == 1 && rotary is null))
        {
            block.Draft.KeepAsRaw($"the mode {modeValue} of CYCLE832 has no NCX tolerance mode (controller-mapping 1, "
                + "TOLERANCE)");
            return;
        }

        block.Draft.AddState("TOLERANCE", null, SiemensNumbers.Of(tol));
        block.Draft.AddState("TOLERANCE_MODE", null, new IdentValue(ncxMode));
        if (rotary is decimal orientation)
        {
            block.Draft.AddState("TOLERANCE", "ROTARY", SiemensNumbers.Of(orientation));
        }

        block.Facts.ToleranceOn = true;
    }

    // The NCX mode of the ones digit through the machine's [tolerance] mode, else 1 finish and 3 rough (siemens 3).
    private static string? ModeOf(SiemensBlock block, int digit)
    {
        string text = digit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (block.Machine.Tolerance?.Mode is IReadOnlyDictionary<string, string> modes && modes.Count > 0)
        {
            foreach (KeyValuePair<string, string> mode in modes)
            {
                if (mode.Value == text)
                {
                    return mode.Key;
                }
            }

            return null;
        }

        return digit switch
        {
            1 => "FINISH",
            3 => "ROUGH",
            _ => null,
        };
    }

    // CTOL= is the contour tolerance without the technology switch, TOLERANCE; OTOL= the orientation tolerance,
    // TOLERANCE:ROTARY, which needs an active TOLERANCE (controller-mapping 1, TOLERANCE; language 4.1).
    private static void ReadTolerances(SiemensBlock block)
    {
        if (block.Find("CTOL") is SourceWord contour && contour.Number is decimal value && value > 0)
        {
            block.MarkRead(contour);
            block.Draft.AddState("TOLERANCE", null, SiemensNumbers.Of(value));
            block.Facts.ToleranceOn = true;
        }

        if (block.Find("OTOL") is SourceWord orientation && orientation.Number is decimal angle && angle > 0
            && block.Facts.ToleranceOn)
        {
            block.MarkRead(orientation);
            block.Draft.AddState("TOLERANCE", "ROTARY", SiemensNumbers.Of(angle));
        }

        foreach (string address in new[] { "CTOL", "OTOL" })
        {
            if (block.Find(address) is SourceWord other)
            {
                block.KeepAsRawWord(other);
            }
        }
    }
}
