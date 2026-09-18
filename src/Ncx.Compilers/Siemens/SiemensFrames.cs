using System.Globalization;
using System.Text.RegularExpressions;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// The frames of a SINUMERIK block (controllers siemens.md 3, 4, 6, 12 rule 4; controller-mapping 1; machine-config
/// 3, 4, 5; language 4.2; D31): G54 to G57 and G505 to G599 for ORIGIN; the chain in program order, TRANS, ROT RPL=
/// and MIRROR for its first entry, ATRANS, AROT RPL= and AMIRROR for the rest, CYCLE800 from the [transform] template
/// with {dir} from MOVE; DIAMON and DIAMOF from [diameter]; TRAORI, TRACYL and TRANSMIT from [transform]; CYCLE832
/// from [tolerance]; G74 and G75 from [home]; PRESETON from [setpos]; RETRACT from [retract].
/// </summary>
internal static partial class SiemensFrames
{
    // The target keys of the settable frame and of the diameter programming (controllers siemens.md 2, groups 8, 29).
    private const string DatumGroup = "G54";
    private const string DiameterGroup = "DIAMON";

    // The target keys of the programmable frame (group 3) and of the swivel of CYCLE800, which a RAW line may change
    // outside the chain of the virtual machine (controllers siemens.md 2, 4): each stands in the target state only
    // while it is unknown, from such a RAW line to the block that ends it again (AfterRaw, WriteChain).
    private const string ProgrammableFrame = "TRANS";
    private const string Swivel = "CYCLE800";

    // The words of the transform chain (language 4.2).
    private static readonly string[] s_chainWords = ["SHIFT", "ROTATE", "MIRROR", "TILT", "TILT_AXIS", "MOVE", "ROT"];

    // The values of {dir} per MOVE where [transform] move names none: TURN = -1 positions the rotary axes, STAY = 0
    // only computes the frame (machine-config 5, move; controller-mapping 1, TILT).
    private static readonly Dictionary<TiltMove, string> s_directions = new()
    {
        [TiltMove.Turn] = "-1",
        [TiltMove.Stay] = "0",
    };

    /// <summary>
    /// What the block did to the chain, in the order of D31 (controllers siemens.md 4, 12 rule 4): where it removed a
    /// shift, rotation or mirror, the programmable frame is written again from its first entry, TRANS alone where none
    /// stays; a removed tilt is CYCLE800() and the tilts that stay are written again; then every appended entry in
    /// program order, the first of the chain replacing, the others additive, each in a block of its own.
    /// </summary>
    public static void WriteChain(SiemensBlock write)
    {
        Block block = write.Block;
        foreach (string key in s_chainWords)
        {
            write.Written(key);
        }

        if (block.Verb?.Key is "SHIFT" or "TILT" or "TILT_AXIS")
        {
            foreach (SiemensAxisWord axis in SiemensAxes.Of(block))
            {
                write.Written(axis.Word);
            }
        }

        // ROT is not available on Siemens and is ignored with a WARNING (controller-mapping 1, MOVE and ROT; language
        // 4.2, ROT).
        if (block.Has("ROT"))
        {
            write.Warning(DiagnosticCodes.SiemensOptionNotWritten,
                "ROT has no SINUMERIK form and is not written (controller-mapping 1, MOVE and ROT; language 4.2).");
        }

        // After a RAW line that may have changed the programmable frame or the swivel (AfterRaw), a block that ends
        // them in NCX ends them on the control too, although the chain of the virtual machine holds nothing of what the
        // RAW text did: ORIGIN starts an empty chain and a RESET removes the entries of its kind (language 4.2); TRANS
        // alone ends the programmable frame and CYCLE800() the swivel (controllers siemens.md 4). What stays of the
        // chain is written again after it, so that the control has the chain of the virtual machine from here on.
        bool endsProgrammable = write.IsMadeUnknown(ProgrammableFrame) && EndsProgrammableFrame(block);
        bool endsSwivel = write.IsMadeUnknown(Swivel) && EndsSwivel(write);
        ChainChange change = ChainWriter.Between(write.Before.Frame, write.After.Frame);
        if (change.IsEmpty && !endsProgrammable && !endsSwivel)
        {
            return;
        }

        int kept = write.After.Frame.Chain.Count - change.Appended.Count;
        WriteRemoved(write, change, kept, endsProgrammable, endsSwivel);
        if (endsProgrammable)
        {
            write.Target.Forget(ProgrammableFrame);
        }

        if (endsSwivel)
        {
            write.Target.Forget(Swivel);
        }

        IReadOnlyList<TransformEntry> chain = write.After.Frame.Chain;
        for (int index = kept; index < chain.Count; index++)
        {
            WriteEntry(write, chain[index], index == 0, appended: true);
        }
    }

    /// <summary>
    /// ORIGIN=n as the settable frame G54 to G57, G505 to G599, G500 for 0, where the control has another active
    /// (controllers siemens.md 4; controller-mapping 1, ORIGIN); the chain it clears is WriteChain's.
    /// </summary>
    public static void WriteOrigin(SiemensBlock write)
    {
        if (write.Block.Find("ORIGIN") is not Word origin)
        {
            return;
        }

        write.Written(origin);
        if (origin.Value is not IntegerValue number || DatumCode(number.Number) is not string code)
        {
            write.Error(DiagnosticCodes.SiemensOriginNotWritable,
                $"{origin.ToCanonical()} names no settable frame of the control, G54 to G57 for 1 to 4 and G505 to "
                + "G599 for 5 to 99 (controllers siemens.md 4; controller-mapping 1, ORIGIN).");
            return;
        }

        SiemensMotion.Change(write, DatumGroup, SiemensLine.DatumRank, code);
    }

    /// <summary>
    /// The code of the settable frame of ORIGIN=n: G54 to G57 for 1 to 4, G505 to G599 for 5 to 99, G500 for 0
    /// (controllers siemens.md 4; controller-mapping 1, ORIGIN); null beyond.
    /// </summary>
    public static string? DatumCode(long origin)
    {
        return origin switch
        {
            0 => "G500",
            >= 1 and <= 4 => "G" + (53 + origin).ToString(CultureInfo.InvariantCulture),
            >= 5 and <= 99 => "G5" + origin.ToString("00", CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    /// <summary>
    /// DIAMETER through [diameter], DIAMON and DIAMOF, where the X axis is switchable; elsewhere the programming of the
    /// axis decides and nothing is written (language 4.2; machine-config 4; controllers siemens.md 2, group 29; D60).
    /// </summary>
    public static void WriteDiameter(SiemensBlock write)
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
        if (write.Render(template, "[diameter] (machine-config 4, D60)", new TemplateValues()) is string text
            && text.Length > 0 && write.Target.Changes(DiameterGroup, text))
        {
            write.Main.Code(SiemensLine.KeywordRank, text);
        }
    }

    /// <summary>
    /// The transformations and the tolerance in blocks of their own: TCPM, POLAR, CYLINDER, TOLERANCE, the rotary
    /// options (machine-config 5; controller-mapping 1; controllers siemens.md 3, 6).
    /// </summary>
    public static void WriteTransformations(SiemensBlock write)
    {
        TransformTable? transform = write.Machine.Transform;
        OnOff(write, "TCPM", transform?.TcpmOn, transform?.TcpmOff);
        OnOff(write, "POLAR", transform?.PolarOn, transform?.PolarOff);
        WriteCylinder(write, transform);
        WriteTolerance(write);
        WriteRotaryPath(write, transform);
        WriteRotaryFeed(write, transform);
    }

    /// <summary>
    /// HOME, SETPOS and RETRACT, the verbs of the frame that move or declare, each in a block of its own (language 4.2,
    /// 4.3; controllers siemens.md 3).
    /// </summary>
    public static void WriteVerbs(SiemensBlock write)
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

    /// <summary>
    /// After a RAW line: a RAW text that names an instruction of the programmable frame (TRANS, ATRANS, ROT, AROT,
    /// ROTS, AROTS, CROTS, SCALE, ASCALE, MIRROR, AMIRROR, G58, G59) or of the swivel (CYCLE800, TOFRAME, TOROT, PAROT,
    /// and ROTS, AROTS, CROTS, which NCX reads as TILT), or names a frame variable ($P_PFRAME, $P_ACTFRAME, $P_BFRAME),
    /// may have changed it on the control, where the chain of the virtual machine does not hold it (controllers
    /// siemens.md 4; controller-mapping 1, TILT; language 4.1, D5): it is unknown until a block ends it (WriteChain).
    /// The settable frame is the datum, which every RAW line leaves unknown already.
    /// </summary>
    // TODO(question): the documents do not say whether a RAW line that names none of them, a builder cycle (the STAMA
    // DREH(...), NEWCOORD(...); machine-builders 2) or a subprogram it calls, changes the frame; the frame stays as the
    // chain has it after such a RAW, since TRANS and CYCLE800() before the next ORIGIN after every MSG and STOPRE would
    // stand where the source has none (controllers sample-corpus.md: MSG before every operation), until that is
    // answered, as for the modal call (SiemensCycles.AfterRaw).
    public static void AfterRaw(SiemensBlock write, string raw)
    {
        foreach (string key in KeysNamedBy(raw))
        {
            write.MakeUnknown(key);
        }
    }

    /// <summary>
    /// At the start of a subprogram and at a label, where the control arrives from a caller or a jump that the STATIC
    /// walk does not follow and every value of the target state is unknown (virtual machine 1, 3.9; D99): the
    /// programmable frame or the swivel that a RAW line of the file may change is unknown too, since the RAW line may
    /// stand in a caller or after the label, on the way of a jump back to it (controllers siemens.md 4, 8).
    /// </summary>
    public static void ForgetRawFrames(SiemensBlock write)
    {
        foreach (string key in write.File.RawFrameKeys)
        {
            write.MakeUnknown(key);
        }
    }

    /// <summary>
    /// The keys of the target state that a RAW text may change, the programmable frame and the swivel, by the
    /// instructions and the frame variables it names (AfterRaw).
    /// </summary>
    /// <param name="raw">The text of the RAW block.</param>
    public static List<string> KeysNamedBy(string raw)
    {
        // The words of the text, without its strings and its comments, whose words are no instructions.
        string code = StringOrComment().Replace(raw, " ");
        bool frameVariable = FrameVariable().IsMatch(code);
        var keys = new List<string>();
        if (frameVariable || ProgrammableInstruction().IsMatch(code))
        {
            keys.Add(ProgrammableFrame);
        }

        if (frameVariable || SwivelInstruction().IsMatch(code))
        {
            keys.Add(Swivel);
        }

        return keys;
    }

    // A removed shift, rotation or mirror: the programmable frame again from its first entry, TRANS alone where none
    // stays and no appended entry replaces it; a removed tilt: CYCLE800() and the tilts that stay again (controllers
    // siemens.md 4: every replacing instruction deletes all earlier programmable frame instructions; language 4.2).
    // The programmable frame or the swivel that a RAW line may have changed is ended the same way (WriteChain).
    private static void WriteRemoved(SiemensBlock write, ChainChange change, int kept, bool endsProgrammable,
        bool endsSwivel)
    {
        bool programmable = endsProgrammable;
        bool tilt = endsSwivel;
        foreach (TransformEntry entry in change.Removed)
        {
            programmable |= entry.Kind is TransformKind.Shift or TransformKind.Rotate or TransformKind.Mirror;
            tilt |= entry.Kind is TransformKind.Tilt or TransformKind.TiltAxis;
        }

        IReadOnlyList<TransformEntry> chain = write.After.Frame.Chain;
        if (tilt)
        {
            TransformTable? transform = write.Machine.Transform;
            if (write.Render(transform?.TiltOff, "[transform] TILT_OFF (machine-config 5)", new TemplateValues())
                is string off)
            {
                write.Write(off);
            }
        }

        bool rewritesProgrammable = false;
        for (int index = 0; index < kept; index++)
        {
            TransformEntry entry = chain[index];
            bool isTilt = entry.Kind is TransformKind.Tilt or TransformKind.TiltAxis;
            if ((isTilt && tilt) || (!isTilt && programmable))
            {
                WriteEntry(write, entry, first: !isTilt && !rewritesProgrammable, appended: false);
                rewritesProgrammable |= !isTilt;
            }
        }

        // TRANS alone clears the programmable frame where no entry of it stays and the first appended one does not
        // replace it (controllers siemens.md 4).
        bool appendedReplaces = kept == 0 && chain.Count > 0 && chain[0].Kind is not (TransformKind.Tilt
            or TransformKind.TiltAxis);
        if (programmable && !rewritesProgrammable && !appendedReplaces)
        {
            write.Write("TRANS");
        }
    }

    // One entry of the chain: TRANS, ROT RPL= or MIRROR for the first of the programmable frame, ATRANS, AROT RPL= or
    // AMIRROR for the rest, CYCLE800 for a tilt (controllers siemens.md 4, 12 rule 4; controller-mapping 1, SHIFT,
    // ROTATE, MIRROR, TILT).
    private static void WriteEntry(SiemensBlock write, TransformEntry entry, bool first, bool appended)
    {
        switch (entry.Kind)
        {
            case TransformKind.Shift:
                WriteShift(write, entry, first, appended);
                break;
            case TransformKind.Rotate:
                WriteRotation(write, entry, first, appended);
                break;
            case TransformKind.Mirror:
                WriteMirror(write, entry, first);
                break;
            case TransformKind.Tilt:
                WriteTilt(write, entry, write.Machine.Transform?.TiltOn, "[transform] TILT_ON", appended);
                break;
            case TransformKind.TiltAxis:
                // TILT_AXIS needs the kinematics module where the machine has no TILT_AXIS_ON (D82).
                string? template = write.Machine.Transform?.TiltAxisOn;
                if (string.IsNullOrEmpty(template))
                {
                    write.Error(DiagnosticCodes.SiemensTiltNotWritable,
                        "TILT_AXIS has no TILT_AXIS_ON template in [transform] of the machine, and without the "
                        + "kinematics module the target cannot write it (language 4.2; machine-config 5; D82).");
                    return;
                }

                WriteTilt(write, entry, template, "[transform] TILT_AXIS_ON", appended);
                break;
        }
    }

    // TRANS X Y Z or ATRANS X Y Z, the values of the SHIFT block as written where it appends the entry, else of the
    // entry (controllers siemens.md 4).
    // TODO(question): D125: the X of a SHIFT under DIAMETER=ON is a diameter or a radius; the virtual machine keeps it
    // as written and the compiler writes it with the factor of an X word, until D125 is answered.
    private static void WriteShift(SiemensBlock write, TransformEntry entry, bool first, bool appended)
    {
        var words = new List<string>();
        bool fromBlock = appended && write.Block.Verb?.Key == "SHIFT";
        foreach (KeyValuePair<string, decimal?> axis in Ordered(entry.Shift))
        {
            string letter = SiemensAxes.LetterOf(write, axis.Key);
            Word? word = fromBlock ? write.Block.Find(axis.Key) : null;
            Value? value = word?.Value
                ?? (axis.Value is decimal known ? new DecimalValue(known, Invariant(known)) : null);
            string? text = value is null ? null : SiemensAxes.ValueOf(write, axis.Key, value, letter);
            if (text is null)
            {
                write.Error(DiagnosticCodes.SiemensChainNotWritable,
                    $"The shift of {axis.Key} is not known, so {(first ? "TRANS" : "ATRANS")} cannot be written "
                    + "(virtual machine 1; language 4.2).");
                return;
            }

            words.Add(SiemensAxes.Address(letter, text, "", value is ExprValue));
        }

        write.Write((first ? "TRANS " : "ATRANS ") + string.Join(" ", words));
    }

    // ROT RPL= or AROT RPL=, the rotation about the axis perpendicular to the plane (controllers siemens.md 4;
    // controller-mapping 1, ROTATE); in another plane than the one of the entry, about the tool axis of its plane.
    private static void WriteRotation(SiemensBlock write, TransformEntry entry, bool first, bool appended)
    {
        Word? word = appended ? write.Block.Find("ROTATE") : null;
        string? angle = word is not null && word.Value is not IdentValue
            ? SiemensExpressions.ValueText(write, word.Value, "ROTATE")
            : entry.Angle is decimal known ? write.Format("ROTATE", known) : null;
        if (angle is null)
        {
            write.Error(DiagnosticCodes.SiemensChainNotWritable,
                "The angle of the rotation is not known, so AROT cannot be written (virtual machine 1; language 4.2).");
            return;
        }

        string about = entry.Workplane == write.After.Frame.Workplane
            ? "RPL"
            : entry.Workplane switch
            {
                Workplane.ZX => "Y",
                Workplane.YZ => "X",
                _ => "Z",
            };
        write.Write((first ? "ROT " : "AROT ") + about + "=" + angle);
    }

    // MIRROR X0 or AMIRROR X0, the value meaningless (controllers siemens.md 4).
    private static void WriteMirror(SiemensBlock write, TransformEntry entry, bool first)
    {
        var words = new List<string>();
        foreach (string axis in entry.Mirrored)
        {
            words.Add(SiemensAxes.Address(SiemensAxes.LetterOf(write, axis), "0", "", expression: false));
        }

        write.Write((first ? "MIRROR " : "AMIRROR ") + string.Join(" ", words));
    }

    // CYCLE800 from the template with the angles {a} {b} {c} of the tilt, the origin {x} {y} {z} 0 since the chain
    // carries the shifts, and {dir} from MOVE: TURN -1, STAY 0 (machine-config 5, TILT_ON and move; controller-mapping
    // 1, TILT and MOVE; controllers siemens.md 4, 12 rule 4).
    // TODO(question): machine-config 5 gives {dir} for MOVE=TURN and MOVE=STAY, and the tool tip tracking of MOVE=MOVE
    // is the tens digit of _ST, which the TILT_ON template writes as a constant; MOVE=MOVE takes the value of
    // [transform] move where the machine names one, and is an ERROR otherwise, until that is answered.
    private static void WriteTilt(SiemensBlock write, TransformEntry entry, string? template, string what,
        bool appended)
    {
        var values = new TemplateValues();
        foreach (string axis in new[] { "x", "y", "z" })
        {
            values.Set(axis, 0m);
        }

        bool fromBlock = appended && write.Block.Verb?.Key is "TILT" or "TILT_AXIS";
        foreach (string angle in new[] { "A", "B", "C" })
        {
            Word? word = fromBlock ? write.Block.Find(angle) : null;
            string? angleText = word is not null ? SiemensExpressions.ValueText(write, word.Value, angle)
                : entry.Angles.TryGetValue(angle, out decimal? known) && known is decimal value
                    ? write.Format(angle, value)
                    : entry.Angles.ContainsKey(angle) ? null : "0";
            if (angleText is not null)
            {
                values.Set(angle.ToLowerInvariant(), angleText);
            }
        }

        string option = entry.Move.ToString().ToUpperInvariant();
        TransformTable? transform = write.Machine.Transform;
        if (transform?.Move.TryGetValue(option, out string? move) == true)
        {
            values.Set("move", move);
            values.Set("dir", move);
        }
        else if (s_directions.TryGetValue(entry.Move, out string? direction))
        {
            values.Set("dir", direction);
        }
        else if (write.HasPlaceholder(template, "dir"))
        {
            write.Error(DiagnosticCodes.SiemensTiltNotWritable,
                $"MOVE={option} has no value of {{dir}}: machine-config 5 gives TURN = -1 and STAY = 0, and "
                + "[transform] move of the machine names none (controller-mapping 1, MOVE).");
            return;
        }

        if (write.Render(template, what + " (machine-config 5)", values) is string text)
        {
            write.Write(text);
        }
    }

    // A transformation on and off by its template (machine-config 5; controller-mapping 1, TCPM, POLAR).
    private static void OnOff(SiemensBlock write, string key, string? on, string? off)
    {
        if (write.Block.Find(key) is not Word word)
        {
            return;
        }

        write.Written(word);
        bool switchesOn = word.Value.ToCanonical() == "ON";
        string state = switchesOn ? "ON" : "OFF";
        if (write.Render(switchesOn ? on : off, $"[transform] {key}_{state} (machine-config 5)", new TemplateValues())
            is string text)
        {
            write.Write(text);
        }
    }

    // CYLINDER=r switches on with the reference radius {r} and the working diameter {d} that TRACYL takes, OFF switches
    // off (language 4.2; machine-config 5; D96).
    private static void WriteCylinder(SiemensBlock write, TransformTable? transform)
    {
        if (write.Block.Find("CYLINDER") is not Word cylinder)
        {
            return;
        }

        write.Written(cylinder);
        var values = new TemplateValues();
        bool off = cylinder.Value.ToCanonical() == "OFF";
        if (!off && SiemensExpressions.ValueText(write, cylinder.Value, "CYLINDER") is string radius)
        {
            values.Set("r", radius);
            decimal? number = cylinder.Value is ExprValue
                ? null
                : decimal.Parse(cylinder.Value.ToCanonical(), CultureInfo.InvariantCulture);
            values.Set("d", number is decimal known ? write.Format("CYLINDER", known * 2m) : "(" + radius + ")*2");
        }

        string what = off
            ? "[transform] CYLINDER_OFF (machine-config 5)"
            : "[transform] CYLINDER_ON (machine-config 5)";
        if (write.Render(off ? transform?.CylinderOff : transform?.CylinderOn, what, values) is string text)
        {
            write.Write(text);
        }
    }

    // TOLERANCE through [tolerance], CYCLE832(tol, mode, otol), OFF as CYCLE832(0,0,1); TOLERANCE:ROTARY and
    // TOLERANCE_MODE write it again while it is on (machine-config 5; controller-mapping 1, TOLERANCE; D85).
    // TODO(question): D151: the ON template needs {rotary}, which TOLERANCE:ROTARY may leave without a value; the
    // template is then unusable and the compiler reports it, as machine-config says of a missing placeholder value,
    // until D151 is answered.
    private static void WriteTolerance(SiemensBlock write)
    {
        Block block = write.Block;
        bool changes = block.Has("TOLERANCE") || block.Has("TOLERANCE_MODE");
        if (!changes)
        {
            return;
        }

        write.Written("TOLERANCE");
        write.Written("TOLERANCE_MODE");
        ToleranceTable? table = write.Machine.Tolerance;
        ToleranceState state = write.After.Frame.Tolerance;
        if (block.Has("TOLERANCE", null, "OFF"))
        {
            if (write.Render(table?.Off, "[tolerance] OFF (machine-config 5, D85)", new TemplateValues())
                is string off)
            {
                write.Write(off);
            }

            return;
        }

        if (state.Value is null && block.Find("TOLERANCE", null)?.Value is not ExprValue)
        {
            return;
        }

        var values = new TemplateValues();
        SetTolerance(write, values, "tol", block.Find("TOLERANCE", null), state.Value);
        SetTolerance(write, values, "rotary", block.Find("TOLERANCE", "ROTARY"), state.Rotary);
        string mode = state.Mode == ToleranceMode.Rough ? "ROUGH" : "FINISH";
        if (table is not null && table.Mode.TryGetValue(mode, out string? modeValue))
        {
            values.Set("mode", modeValue);
        }

        if (write.Render(table?.On, "[tolerance] ON (machine-config 5, D85)", values) is string on)
        {
            write.Write(on);
        }
    }

    private static void SetTolerance(SiemensBlock write, TemplateValues values, string name, Word? word,
        decimal? state)
    {
        if (word is not null && SiemensExpressions.ValueText(write, word.Value, "TOLERANCE") is string text)
        {
            values.Set(name, text);
        }
        else if (state is decimal known)
        {
            values.Set(name, write.Format("TOLERANCE", known));
        }
    }

    // ROTARY_PATH=SHORTEST writes DC() on the rotary axes (SiemensAxes), and a template where the machine gives one
    // (machine-config 5, ROTARY_PATH_SHORTEST; D86).
    private static void WriteRotaryPath(SiemensBlock write, TransformTable? transform)
    {
        if (write.Block.Find("ROTARY_PATH") is not Word path)
        {
            return;
        }

        write.Written(path);
        string? template = path.Value.ToCanonical() == "SHORTEST"
            ? transform?.RotaryPathShortest
            : transform?.RotaryPathFull;
        if (!string.IsNullOrEmpty(template))
        {
            write.Write(template);
        }
    }

    // ROTARY_FEED through [transform], FGREF[{axis}] per rotary axis of the machine; without a template it is not
    // written, with a WARNING (machine-config 5, ROTARY_FEED_MM_MIN; D86).
    private static void WriteRotaryFeed(SiemensBlock write, TransformTable? transform)
    {
        if (write.Block.Find("ROTARY_FEED") is not Word feed)
        {
            return;
        }

        write.Written(feed);
        string? template = feed.Value.ToCanonical() == "MM_MIN"
            ? transform?.RotaryFeedMmMin
            : transform?.RotaryFeedDegMin;
        if (string.IsNullOrEmpty(template))
        {
            write.Warning(DiagnosticCodes.SiemensOptionNotWritten,
                $"{feed.ToCanonical()} has no template in [transform] of the machine and is not written "
                + "(machine-config 5; controller-mapping 1, ROTARY_FEED; D86).");
            return;
        }

        foreach (AxisDef axis in write.Machine.Axes)
        {
            if (axis.Kind != AxisKind.Rotary)
            {
                continue;
            }

            var values = new TemplateValues();
            values.Set("axis", axis.Letter ?? axis.NcxName);
            if (write.Render(template, "[transform] ROTARY_FEED (machine-config 5)", values) is string text)
            {
                write.Write(text);
            }
        }
    }

    // HOME through [home] (machine-config 3; controllers siemens.md 3; controller-mapping 1, HOME): G74 with the
    // machine axis names of the axes and 0, X1=0 Z1=0, for the reference point; the point template, G75 X0 Z0 FP=2,
    // for POINT=2 and further.
    private static void WriteHome(SiemensBlock write)
    {
        write.Written("HOME");
        write.Written("POINT");
        int point = write.Block.Find("POINT")?.Value is IntegerValue number ? (int)number.Number : 1;
        var axes = new List<string>();
        foreach (SiemensAxisWord axis in SiemensAxes.Of(write.Block))
        {
            write.Written(axis.Word);
            AxisDef? definition = SiemensAxes.Resolve(write, axis.Axis);
            string name = point > 1
                ? definition?.Letter ?? axis.Axis
                : definition?.Id ?? axis.Axis;
            axes.Add(SiemensAxes.Address(name, "0", "", expression: false));
        }

        var values = new TemplateValues();
        values.Set("axes", string.Join(" ", axes));
        values.Set("point", point);
        string? template = point > 1 ? write.Machine.Home?.Point : write.Machine.Home?.Template;
        string what = point > 1 ? "[home] point (machine-config 3, D100)" : "[home] template (machine-config 3, D100)";
        if (write.Render(template, what, values) is string text)
        {
            write.Write(text);
        }
    }

    // SETPOS through [setpos], one call per axis: PRESETON(X,10) (language 4.2, SETPOS; controller-mapping 1; D55).
    private static void WriteSetpos(SiemensBlock write)
    {
        write.Written("SETPOS");
        foreach (SiemensAxisWord axis in SiemensAxes.Of(write.Block))
        {
            write.Written(axis.Word);
            string letter = SiemensAxes.LetterOf(write, axis.Axis);
            if (SiemensAxes.ValueOf(write, axis.Axis, axis.Word.Value, letter) is not string value)
            {
                continue;
            }

            var values = new TemplateValues();
            values.Set("axis", letter);
            values.Set("value", value);
            values.Set("axes", SiemensAxes.Address(letter, value, "", axis.Word.Value is ExprValue));
            if (write.Render(write.Machine.Setpos?.Template, "[setpos] template (machine-config 3, D55)", values)
                is string text)
            {
                write.Write(text);
            }
        }
    }

    // RETRACT through [retract]: bare to the axis limit, with a value by that distance; an empty template is a
    // computed move of the kinematics module, without which the block is an ERROR (controller-mapping 1, RETRACT;
    // D83).
    private static void WriteRetract(SiemensBlock write)
    {
        Word retract = write.Block.Find("RETRACT")!;
        write.Written(retract);
        bool bare = retract.Value is NoValue;
        string? template = bare ? write.Machine.Retract?.Max : write.Machine.Retract?.By;
        if (string.IsNullOrEmpty(template))
        {
            write.Error(DiagnosticCodes.SiemensRetractNotWritable,
                "RETRACT has no template in [retract] of the machine, and without the kinematics module the retract "
                + "cannot be computed (controller-mapping 1, RETRACT; D83).");
            return;
        }

        var values = new TemplateValues();
        if (!bare && SiemensExpressions.ValueText(write, retract.Value, "RETRACT") is string distance)
        {
            values.Set("distance", distance);
        }

        if (write.Render(template, "[retract] (machine-config 5, D83)", values) is string text)
        {
            write.Write(text);
        }
    }

    // The axes of a shift in the order a block writes them (language 5 rule 6).
    private static List<KeyValuePair<string, decimal?>> Ordered(IReadOnlyDictionary<string, decimal?> shift)
    {
        string[] order = ["X", "Y", "Z", "A", "B", "C"];
        var ordered = new List<KeyValuePair<string, decimal?>>();
        foreach (string axis in order)
        {
            if (shift.TryGetValue(axis, out decimal? value))
            {
                ordered.Add(new KeyValuePair<string, decimal?>(axis, value));
            }
        }

        foreach (KeyValuePair<string, decimal?> axis in shift)
        {
            if (!order.Contains(axis.Key))
            {
                ordered.Add(axis);
            }
        }

        return ordered;
    }

    private static string Invariant(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    // The words that end the programmable frame in NCX: ORIGIN starts an empty chain, SHIFT=RESET and ROTATE=RESET
    // remove their kind (language 4.2), MIRROR=OFF the mirror (D124, the reading of the virtual machine).
    private static bool EndsProgrammableFrame(Block block)
    {
        return block.Has("ORIGIN")
            || block.Has("SHIFT", null, "RESET")
            || block.Has("ROTATE", null, "RESET")
            || block.Has("MIRROR", null, "OFF");
    }

    // The words that end the swivel in NCX: TILT=RESET and TILT_AXIS=RESET (language 4.2, D82), and ORIGIN, which
    // starts an empty chain, where [transform] of the machine gives the swivel an end, TILT_OFF (machine-config 5): a
    // machine without it has no swivel the compiler could end, and the RESET words report the missing template.
    private static bool EndsSwivel(SiemensBlock write)
    {
        Block block = write.Block;
        return block.Has("TILT", null, "RESET")
            || block.Has("TILT_AXIS", null, "RESET")
            || (block.Has("ORIGIN") && write.Machine.Transform?.TiltOff is not null);
    }

    // A string in double quotes, or a comment from ; to the end of its line (controllers siemens.md 1).
    [GeneratedRegex("\"[^\"\\n]*\"|;[^\\n]*", RegexOptions.CultureInvariant)]
    private static partial Regex StringOrComment();

    // An instruction of the programmable frame, group 3 (controllers siemens.md 4), as a word of its own; names are
    // case-insensitive (siemens 1).
    [GeneratedRegex("(?<![A-Za-z0-9_$])(A?TRANS|A?ROTS?|CROTS|A?SCALE|A?MIRROR|G5[89])(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ProgrammableInstruction();

    // An instruction that swivels the plane or aligns the frame with the tool, and the spatial rotations that NCX
    // reads as TILT (controllers siemens.md 4; controller-mapping 1, TILT).
    [GeneratedRegex("(?<![A-Za-z0-9_$])(CYCLE800|TOFRAME[A-Z]*|TOROT[A-Z]*|PAROT[A-Z]*|A?ROTS|CROTS)(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SwivelInstruction();

    // A frame variable, $P_PFRAME, $P_BFRAME, $P_ACTFRAME and the others whose name ends in FRAME (controllers
    // siemens.md 4); $P_UIFR is the settable frame, the datum.
    [GeneratedRegex("(?<![A-Za-z0-9_])\\$P_[A-Z0-9_]*FRAME(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex FrameVariable();
}
