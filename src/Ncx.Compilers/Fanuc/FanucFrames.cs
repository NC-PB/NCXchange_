using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Fanuc;

/// <summary>
/// The frames of a Fanuc block (controllers fanuc.md 4; controller-mapping 1; language 4.2; D31): G54 to G59 and G54.1
/// P for ORIGIN, the chain in program order with G52, G68, G51.1 and G68.2 with G53.1, G28 and G30 for HOME, G92 or G50
/// for SETPOS, G43.4, G12.1 and G7.1 for the transformations, G5.1 for TOLERANCE, the rotary options and RETRACT from
/// the templates of the machine.
/// </summary>
internal static class FanucFrames
{
    // The words of the transform chain (language 4.2).
    private static readonly string[] s_chainWords = ["SHIFT", "ROTATE", "MIRROR", "TILT", "TILT_AXIS", "MOVE", "ROT"];

    /// <summary>
    /// What the block did to the chain, in the order of D31: every removed entry cancelled, the last first, then every
    /// appended entry in program order, each in a line of its own before the main line; a chain the one G52, G68,
    /// G51.1 and G68.2 of a Fanuc control cannot hold in that order is an ERROR (controllers fanuc.md 4, 9 rule 6).
    /// </summary>
    public static void WriteChain(FanucBlock write)
    {
        Block block = write.Block;
        foreach (string key in s_chainWords)
        {
            write.Written(key);
        }

        if (block.Verb?.Key is "SHIFT" or "TILT" or "TILT_AXIS")
        {
            foreach (FanucAxisWord axis in FanucAxes.Of(block))
            {
                write.Written(axis.Word);
            }
        }

        // ROT is not available on Fanuc, a WARNING on compile (controller-mapping 1, MOVE and ROT).
        if (block.Has("ROT"))
        {
            write.Warning(DiagnosticCodes.FanucOptionNotWritten,
                "ROT has no Fanuc form and is not written (controller-mapping 1, MOVE, ROT).");
        }

        ChainChange change = ChainWriter.Between(write.Before.Frame, write.After.Frame);
        if (change.IsEmpty)
        {
            return;
        }

        if (!IsWritable(write.After.Frame.Chain))
        {
            write.Error(DiagnosticCodes.FanucChainNotWritable,
                "The transform chain after the block holds more than one shift, rotation, mirror or tilt, or a shift "
                + "after another entry, which a Fanuc control with one G52, G68, G51.1 and G68.2 cannot hold in "
                + "program order (controllers fanuc.md 4; D31).");
            return;
        }

        WriteRemoved(write, change);
        foreach (TransformEntry entry in change.Appended)
        {
            WriteAppended(write, entry);
        }
    }

    /// <summary>
    /// ORIGIN in the main line, the transformations, the tolerance, the rotary options and the diameter switch in lines
    /// of their own before it (controller-mapping 1; machine-config 4, 5).
    /// </summary>
    public static void WriteTransformations(FanucBlock write)
    {
        Block block = write.Block;
        if (block.Find("ORIGIN")?.Value is IntegerValue origin)
        {
            // G54 to G59 are the datums 1 to 6, G54.1 Pn the datum 6 + n (language 4.2, ORIGIN; controller-mapping 1).
            write.Written("ORIGIN");
            long datum = origin.Number;
            string code = datum <= 6
                ? "G" + (53 + datum).ToString(CultureInfo.InvariantCulture)
                : "G54.1 P" + (datum - 6).ToString(CultureInfo.InvariantCulture);
            write.Main.Code(FanucCodes.RankOf(FanucCodes.Origin), code);
            write.Target.Set(FanucCodes.Origin, code);
        }

        // G43.4 is tool center point control, G43.5 the same with the tool vector as I J K on the lines (controllers
        // fanuc.md 4; controller-mapping 1, TCPM, and 2, tool vectors), where [transform] names no code of its own; the
        // H of both is the length register, which they apply, so a length offset waiting for the tool axis is taken.
        // G49 ends TCPM and the length offset together, which the holder keeps, so the offset stands again after it.
        TransformTable? transform = write.Machine.Transform;
        string tcpmOn = transform?.TcpmOn ?? (FanucMotion.VectorsFollow(write) ? "G43.5 H{offset}" : "G43.4 H{offset}");
        if (write.Block.Has("TCPM"))
        {
            FanucToolWords.TakeWaitingLength(write);
        }

        OnOff(write, "TCPM", tcpmOn, transform?.TcpmOff ?? "G49", "TCPM");
        if (write.Block.Has("TCPM", null, "OFF"))
        {
            FanucToolWords.RestoreLength(write);
        }
        OnOff(write, "POLAR", transform?.PolarOn ?? "G12.1", transform?.PolarOff ?? "G13.1", "POLAR");
        WriteCylinder(write, transform);
        WriteTolerance(write);
        Option(write, "ROTARY_PATH", "SHORTEST", transform?.RotaryPathShortest, transform?.RotaryPathFull);
        Option(write, "ROTARY_FEED", "MM_MIN", transform?.RotaryFeedMmMin, transform?.RotaryFeedDegMin);
        WriteDiameter(write);
    }

    /// <summary>
    /// HOME, SETPOS and RETRACT, the verbs of the frame that move or declare (language 4.2, 4.3).
    /// </summary>
    public static void WriteVerbs(FanucBlock write)
    {
        switch (write.Block.Verb?.Key)
        {
            case "HOME":
                WriteHome(write);
                break;
            case "SETPOS":
                WriteSetpos(write);
                break;
            case "RETRACT":
                WriteRetract(write);
                break;
        }
    }

    // A Fanuc control holds one local shift inside the datum, one rotation, one mirror and one tilted plane, the shift
    // first (controllers fanuc.md 4).
    private static bool IsWritable(IReadOnlyList<TransformEntry> chain)
    {
        var kinds = new List<TransformKind>();
        for (int index = 0; index < chain.Count; index++)
        {
            TransformKind kind = chain[index].Kind == TransformKind.TiltAxis ? TransformKind.Tilt : chain[index].Kind;
            if (kinds.Contains(kind) || (kind == TransformKind.Shift && index > 0))
            {
                return false;
            }

            kinds.Add(kind);
        }

        return true;
    }

    // A removed shift is G52 with its axes at 0, unless a new G52 replaces it (controllers fanuc.md 4: a new G52
    // replaces the old one, G52 X0 Y0 Z0 cancels); a rotation or a tilt G69, which cancels both, so that one of them
    // cannot stay; a mirror G50.1 with its axes.
    private static void WriteRemoved(FanucBlock write, ChainChange change)
    {
        bool replaced = change.Appended.Any(entry => entry.Kind == TransformKind.Shift);
        bool cancelled = false;
        foreach (TransformEntry entry in change.Removed)
        {
            switch (entry.Kind)
            {
                case TransformKind.Shift when !replaced:
                    write.Write(Absolute(write) + "G52 " + string.Join(" ", ZeroWords(write, entry.Shift.Keys)));
                    break;
                case TransformKind.Mirror:
                    write.Write("G50.1 " + string.Join(" ", ZeroWords(write, entry.Mirrored)));
                    break;
                case TransformKind.Rotate or TransformKind.Tilt or TransformKind.TiltAxis when !cancelled:
                    write.Write(entry.Kind == TransformKind.Rotate ? "G69" : write.Machine.Transform?.TiltOff ?? "G69");
                    cancelled = true;
                    break;
            }
        }

        bool stays = write.After.Frame.Chain.Any(entry => entry.Kind is TransformKind.Rotate or TransformKind.Tilt
            or TransformKind.TiltAxis);
        if (cancelled && stays)
        {
            write.Error(DiagnosticCodes.FanucChainNotWritable,
                "G69 cancels the rotation and the tilted plane together, and the chain keeps one of them (controllers "
                + "fanuc.md 4; D31).");
        }
    }

    // An appended shift is G52, a rotation G68 about the origin of its plane with R, a mirror G51.1 with the axes at 0,
    // a tilt the TILT_ON template with G53.1 for MOVE=TURN (controllers fanuc.md 4; controller-mapping 1, SHIFT,
    // ROTATE, MIRROR, TILT, MOVE; machine-config 5).
    private static void WriteAppended(FanucBlock write, TransformEntry entry)
    {
        switch (entry.Kind)
        {
            case TransformKind.Shift:
                WriteShift(write, entry);
                break;
            case TransformKind.Rotate:
                WriteRotation(write, entry);
                break;
            case TransformKind.Mirror:
                write.Write(Absolute(write) + "G51.1 " + string.Join(" ", ZeroWords(write, entry.Mirrored)));
                break;
            case TransformKind.Tilt:
                WriteTilt(write, entry);
                break;
            case TransformKind.TiltAxis:
                // TILT_AXIS needs the kinematics module where the machine has no TILT_AXIS_ON (D82); G68.1 is no source
                // form of it (D246).
                string? template = write.Machine.Transform?.TiltAxisOn;
                if (string.IsNullOrEmpty(template))
                {
                    write.Error(DiagnosticCodes.FanucTiltAxisNotWritable,
                        "TILT_AXIS has no TILT_AXIS_ON template in [transform] of the machine, and without the "
                        + "kinematics module the target cannot write it (language 4.2; D82, D246).");
                    return;
                }

                WriteTilt(write, entry, template);
                break;
        }
    }

    private static void WriteShift(FanucBlock write, TransformEntry entry)
    {
        var words = new List<string>();
        foreach (KeyValuePair<string, decimal?> axis in entry.Shift)
        {
            string letter = FanucAxes.LetterOf(write, axis.Key, incremental: false) ?? axis.Key;
            Word? word = write.Block.Verb?.Key == "SHIFT" ? write.Block.Find(axis.Key) : null;
            string? value = word is not null
                ? FanucExpressions.WordValue(write, FanucMotion.DecimalsAddress(write, axis.Key), word.Value,
                    FanucAxes.WordFactor(write, axis.Key))
                : axis.Value is decimal shift
                    ? write.FormatComputed(FanucMotion.DecimalsAddress(write, axis.Key),
                        shift * FanucAxes.StateFactor(write, axis.Key))
                    : null;
            if (value is null)
            {
                write.Error(DiagnosticCodes.FanucChainNotWritable,
                    $"The shift of {axis.Key} is not known, so G52 cannot be written (virtual machine 1).");
                return;
            }

            words.Add(letter + value);
        }

        write.Write(Absolute(write) + "G52 " + string.Join(" ", words));
    }

    // ROTATE turns about the current origin (language 4.2).
    // TODO(question): D245: what G68 without X Y turns about is open; G68 is written with the origin of the plane,
    // which means ROTATE whatever the answer, until D245 is answered.
    private static void WriteRotation(FanucBlock write, TransformEntry entry)
    {
        Word? word = write.Block.Find("ROTATE");
        string? angle = word is not null ? FanucExpressions.WordValue(write, "R", word.Value, 1m)
            : entry.Angle is decimal known ? write.Format("R", known) : null;
        if (angle is null)
        {
            return;
        }

        string centre = entry.Workplane switch
        {
            Workplane.ZX => "Z0 X0",
            Workplane.YZ => "Y0 Z0",
            _ => "X0 Y0",
        };
        write.Write(Absolute(write) + "G68 " + centre + " R" + angle);
    }

    // TILT through TILT_ON of [transform] with its spatial angles and the origin of the plane, which the chain carries
    // as a SHIFT before it (language 4.2; machine-config 5); MOVE=TURN positions the axes with TILT_TURN, G53.1, and
    // G68.2 alone is MOVE=STAY (controller-mapping 1, MOVE). MOVE=MOVE positions the rotary axes with the tool tip on
    // the workpiece (language 4.2, MOVE; D82), which neither form does: an ERROR, never written as another motion
    // of the machine (language 2 rule 8).
    private static void WriteTilt(FanucBlock write, TransformEntry entry, string? template = null)
    {
        if (entry.Move == TiltMove.Move)
        {
            write.Error(DiagnosticCodes.FanucWordNotWritten,
                "MOVE=MOVE has no Fanuc form: G68.2 alone leaves the rotary axes where they are (MOVE=STAY) and G53.1 "
                + "after it positions them with the tool retracted (MOVE=TURN) (controller-mapping 1, MOVE; language "
                + "4.2, MOVE; D82).");
            return;
        }

        // The origin and the angles are real numbers with their point (controllers fanuc.md 10 rule 5).
        string? tiltTemplate = template ?? write.Machine.Transform?.TiltOn;
        var values = new TemplateValues();
        foreach (string axis in new[] { "x", "y", "z" })
        {
            write.SetReal(values, axis, 0m, tiltTemplate);
        }

        foreach (string angle in new[] { "A", "B", "C" })
        {
            if (entry.Angles.TryGetValue(angle, out decimal? value) && value is decimal known)
            {
                write.SetReal(values, angle.ToLowerInvariant(), known, tiltTemplate);
            }
        }

        string what = template is null ? "[transform] TILT_ON (machine-config 5)" : "[transform] TILT_AXIS_ON";
        if (write.Render(tiltTemplate, what, values) is string text)
        {
            write.Write(Absolute(write) + text);
        }

        if (entry.Move == TiltMove.Turn)
        {
            write.Write(write.Machine.Transform?.TiltTurn ?? "G53.1");
        }
    }

    // G52, G68 X Y R, G51.1 and the origin of G68.2 name coordinates and angles, and HOME (G91 G28) or an incremental
    // block may have left G91 active, under which the reader and, as D244 (b) and D245 read the manual, the control
    // take them as increments; NCX is absolute by default (language 2 rule 3), so G90 stands in front of them where
    // the control may have another distance mode, which is right whatever D244 and D245 answer. G-code system A has
    // no G90 (controllers fanuc.md 3).
    private static string Absolute(FanucBlock write)
    {
        return write.System != GcodeSystem.A && write.Target.Changes(FanucCodes.Distance, "G90") ? "G90 " : "";
    }

    // A transformation on and off by its template, or the Fanuc code of fanuc 4 where the machine gives none.
    private static void OnOff(FanucBlock write, string key, string on, string off, string target)
    {
        if (write.Block.Find(key) is not Word word)
        {
            return;
        }

        write.Written(word);
        bool switchesOn = word.Value.ToCanonical() == "ON";
        var values = new TemplateValues();
        if (write.After.LastHolder is string id && write.After.Holders.TryGetValue(id, out HolderSnapshot? holder))
        {
            values.Set("offset", holder.OffsetLen);
        }

        if (write.Render(switchesOn ? on : off, $"[transform] {key}_{(switchesOn ? "ON" : "OFF")} (machine-config 5)",
            values) is string text)
        {
            write.Write(text);
            write.Target.Set(target, switchesOn ? "ON" : "OFF");
        }
    }

    // CYLINDER=r switches on with its reference radius, {r}, and OFF switches off (language 4.2; D96).
    private static void WriteCylinder(FanucBlock write, TransformTable? transform)
    {
        if (write.Block.Find("CYLINDER") is not Word cylinder)
        {
            return;
        }

        // The radius and the diameter are real numbers with their point, G7.1 C30. (controllers fanuc.md 10 rule 5).
        write.Written(cylinder);
        var values = new TemplateValues();
        bool off = cylinder.Value.ToCanonical() == "OFF";
        string? template = off ? transform?.CylinderOff ?? "G7.1 C0" : transform?.CylinderOn ?? "G7.1 C{r}";
        if (!off && FanucFunctions.NumberOf(cylinder.Value) is decimal radius)
        {
            write.SetReal(values, "r", radius, template);
            write.SetReal(values, "d", radius * 2m, template);
        }

        if (write.Render(template, "[transform] CYLINDER (machine-config 5)", values) is string text)
        {
            write.Write(text);
        }
    }

    // TOLERANCE through [tolerance]: G5.1 Q1 switches AI contour control on, the tolerance itself sits in a parameter
    // (controllers fanuc.md 4; controller-mapping 1, TOLERANCE; D85).
    private static void WriteTolerance(FanucBlock write)
    {
        if (write.Block.Find("TOLERANCE", null) is not Word tolerance)
        {
            write.Written("TOLERANCE_MODE");
            if (write.Block.Has("TOLERANCE"))
            {
                write.Written("TOLERANCE");
            }

            return;
        }

        write.Written("TOLERANCE");
        write.Written("TOLERANCE_MODE");
        ToleranceTable? table = write.Machine.Tolerance;
        bool off = tolerance.Value.ToCanonical() == "OFF";
        string? template = off ? table?.Off : table?.On;
        var values = new TemplateValues();
        ToleranceState state = write.After.Frame.Tolerance;
        if (state.Value is decimal value)
        {
            write.SetReal(values, "tol", value, template);
        }

        if (state.Rotary is decimal rotary)
        {
            write.SetReal(values, "rotary", rotary, template);
        }

        string mode = state.Mode == ToleranceMode.Rough ? "ROUGH" : "FINISH";
        if (table is not null && table.Mode.TryGetValue(mode, out string? modeValue))
        {
            values.Set("mode", modeValue);
        }

        if (write.Render(template, "[tolerance] (machine-config 5, D85)", values) is string text)
        {
            write.Write(text);
        }
    }

    // ROTARY_PATH and ROTARY_FEED through [transform]; without a template the Fanuc roll-over setting decides, a
    // WARNING (machine-config 5: "empty = WARNING, kept as written"; controller-mapping 1; D86).
    private static void Option(FanucBlock write, string key, string first, string? firstTemplate,
        string? secondTemplate)
    {
        if (write.Block.Find(key) is not Word word)
        {
            return;
        }

        write.Written(word);
        string? template = word.Value.ToCanonical() == first ? firstTemplate : secondTemplate;
        if (string.IsNullOrEmpty(template))
        {
            write.Warning(DiagnosticCodes.FanucOptionNotWritten,
                $"{word.ToCanonical()} has no template in [transform] of the machine and is not written; the roll-over "
                + "setting of the control decides (machine-config 5; controller-mapping 1; D86).");
            return;
        }

        write.Write(template);
    }

    // DIAMETER is written through [diameter] where the X axis is switchable; elsewhere the programming of the axis
    // decides and nothing is written (language 4.2; machine-config 4; D60).
    private static void WriteDiameter(FanucBlock write)
    {
        if (write.Block.Find("DIAMETER") is not Word diameter)
        {
            return;
        }

        write.Written(diameter);
        if (write.Machine.ResolveAxis("X")?.Programming != Programming.Switchable)
        {
            return;
        }

        bool on = diameter.Value.ToCanonical() == "ON";
        string? template = on ? write.Machine.Diameter?.On : write.Machine.Diameter?.Off;
        if (write.Render(template, "[diameter] (machine-config 4, D60)", new TemplateValues()) is string text)
        {
            write.Write(text);
        }
    }

    // HOME through [home] (machine-config 3; controllers fanuc.md 4; controller-mapping 1, HOME): G28 to the reference
    // point, the point template for POINT=2 and further; {axes} are the named axes with an incremental 0, U0 W0 in
    // system A, and G91 standing in front on the other systems, as fanuc 4 writes G91 G28 Z0.
    // TODO(question): machine-config 3 expands {axes} to "G91 X0 Y0 Z0" in the template "G28 {axes}", which writes G28
    // G91 Z0, while fanuc 4 and the sources write G91 G28 Z0; G91 is written as the modal code of the block in front of
    // the template and {axes} holds the axis words, until that is answered.
    // TODO(question): D243: what an absolute word of G28 means is open; D243 recommends that only an incremental 0 is
    // no intermediate move, so HOME of an axis without an incremental letter in system A (B of G28 B0) has no form and
    // is CMP321, until D243 is answered.
    private static void WriteHome(FanucBlock write)
    {
        write.Written("HOME");
        write.Written("POINT");
        FanucCycles.CancelBeforeMotion(write);
        bool systemA = write.System == GcodeSystem.A;
        var axes = new List<string>();
        foreach (FanucAxisWord axis in FanucAxes.Of(write.Block))
        {
            write.Written(axis.Word);
            string? letter = FanucAxes.LetterOf(write, axis.Axis, incremental: systemA);
            if (letter is null)
            {
                write.Error(DiagnosticCodes.FanucIncrementalWordNotWritable,
                    $"HOME {axis.Axis} is written with the incremental letter of the axis in G-code system A, which "
                    + "[[axis]] does not give (controllers fanuc.md 3, 4).");
                return;
            }

            axes.Add(letter + "0");
        }

        if (!systemA)
        {
            FanucMotion.Change(write, FanucCodes.Distance, "G91");
        }

        int point = write.Block.Find("POINT")?.Value is IntegerValue number ? (int)number.Number : 1;
        var values = new TemplateValues();
        values.Set("axes", string.Join(" ", axes));
        values.Set("point", point);
        string? template = point > 1 ? write.Machine.Home?.Point : write.Machine.Home?.Template;
        string what = point > 1 ? "[home] point (machine-config 3, D100)" : "[home] template (machine-config 3, D100)";
        if (write.Render(template, what, values) is string text)
        {
            write.Main.Word(text);
        }
    }

    // SETPOS through [setpos], G92 X Z or G50 X Z in system A, {axes} the declared coordinates (language 4.2, SETPOS;
    // controller-mapping 1; D55). G92 takes its words as increments under G91, which HOME leaves active (D101: SETPOS
    // after HOME), so the declared coordinates stand under G90 (language 2 rule 3).
    // TODO(question): D117: whether SETPOS takes incremental words is open; a SETPOS whose every word is incremental is
    // written under G91, as the reader reads G91 G92, one that mixes the two forms is CMP323, until D117 is answered.
    private static void WriteSetpos(FanucBlock write)
    {
        write.Written("SETPOS");
        List<FanucAxisWord> declared = FanucAxes.Of(write.Block);
        bool anyIncremental = declared.Exists(axis => axis.Incremental);
        if (write.System != GcodeSystem.A && declared.Count > 0)
        {
            if (anyIncremental && declared.Exists(axis => !axis.Incremental))
            {
                write.Error(DiagnosticCodes.FanucMixedWordsUnknownTarget,
                    "SETPOS mixes absolute and incremental words, which one G92 cannot declare under G90 or G91 "
                    + "(controllers fanuc.md 3, 4; D117).");
                write.WrittenAll();
                return;
            }

            FanucMotion.Change(write, FanucCodes.Distance, anyIncremental ? "G91" : "G90");
        }

        var axes = new List<string>();
        foreach (FanucAxisWord axis in declared)
        {
            write.Written(axis.Word);
            if (FanucMotion.Word(write, axis) is not string word)
            {
                return;
            }

            axes.Add(word);
        }

        var values = new TemplateValues();
        values.Set("axes", string.Join(" ", axes));
        if (write.Render(write.Machine.Setpos?.Template, "[setpos] template (machine-config 3, D55)", values)
            is string text)
        {
            write.Main.Word(text);
        }
    }

    // RETRACT through [retract]: bare to the axis limit, with a value by that distance; an empty template is a computed
    // move of the kinematics module, without which the block is an ERROR (controller-mapping 1, RETRACT; D83).
    private static void WriteRetract(FanucBlock write)
    {
        Word retract = write.Block.Find("RETRACT")!;
        write.Written(retract);
        bool bare = retract.Value is NoValue;
        string? template = bare ? write.Machine.Retract?.Max : write.Machine.Retract?.By;
        if (string.IsNullOrEmpty(template))
        {
            write.Error(DiagnosticCodes.FanucRetractNotWritable,
                "RETRACT has no template in [retract] of the machine, and without the kinematics module the retract "
                + "cannot be computed (controller-mapping 1, RETRACT; D83).");
            return;
        }

        var values = new TemplateValues();
        if (FanucFunctions.NumberOf(retract.Value) is decimal distance)
        {
            write.SetReal(values, "distance", distance, template);
        }

        if (write.Render(template, "[retract] (machine-config 5, D83)", values) is string text)
        {
            write.Write(text);
        }
    }

    // The named axes with 0: G52 X0 Y0, G51.1 X0 (controllers fanuc.md 4).
    private static List<string> ZeroWords(FanucBlock write, IEnumerable<string> axes)
    {
        var words = new List<string>();
        foreach (string axis in axes)
        {
            words.Add((FanucAxes.LetterOf(write, axis, incremental: false) ?? axis) + "0");
        }

        return words;
    }
}
