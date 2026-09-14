using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The PLANE functions (controllers heidenhain.md 3, 7 rule 6; controller-mapping 1, TILT, TILT_AXIS, MOVE and ROT;
/// language 4.2; D82, D83): PLANE SPATIAL SPA SPB SPC is TILT A B C, PLANE AXIAL A B C is TILT_AXIS, both with MOVE from
/// TURN, MOVE or STAY and ROT from TABLE ROT or COORD ROT; PLANE RESET STAY is the RESET of the active tilt; the other
/// forms, EULER, PROJECTED, VECTOR, POINTS, RELATIV, stay RAW. A new PLANE replaces the earlier one.
/// </summary>
internal static class HeidenhainPlane
{
    /// <summary>
    /// Reads a PLANE block.
    /// </summary>
    /// <param name="block">The block being read, whose first word is PLANE.</param>
    public static void Read(HeidenhainBlock block)
    {
        block.MarkLeading(2);
        string form = block.Keyword(1);
        HeidenhainDraftBlock? entry = form switch
        {
            "SPATIAL" => Angles(block, ["SPA", "SPB", "SPC"], "TILT"),
            "AXIAL" => Angles(block, ["A", "B", "C"], "TILT_AXIS"),
            _ => null,
        };
        if (form != "RESET" && entry is null)
        {
            block.Draft.KeepAsRaw($"PLANE {form} is kept as RAW: of the PLANE forms NCX reads SPATIAL as TILT and "
                + "AXIAL as TILT_AXIS (controllers heidenhain.md 7 rule 6; controller-mapping 9)");
            return;
        }

        if (block.Draft.IsRaw || !Options(block, entry, out HeidenhainDraftBlock? retract))
        {
            return;
        }

        HeidenhainState state = block.Heidenhain;
        string kind = entry?.Verb ?? state.Chain.TiltKind() ?? "TILT";
        var blocks = new List<HeidenhainDraftBlock>();
        if (retract is not null)
        {
            blocks.Add(retract);
        }

        int before = blocks.Count;
        HeidenhainChainEntry? next = entry is null ? null : new HeidenhainChainEntry(kind, entry);
        if (!state.Chain.TryReplace(kind, next, blocks))
        {
            state.Chain.Forget();
            block.Draft.KeepAsRaw("the new PLANE would replace a tilt that is not the last entry of the chain of "
                + "transforms, or one the reader does not know (language 4.2, D31)");
            return;
        }

        // PLANE RESET where no tilt is active writes the RESET all the same, so that the block keeps its place.
        if (blocks.Count == before)
        {
            blocks.Add(HeidenhainChain.ResetOf(kind));
        }

        block.Draft.Before.AddRange(blocks);
        state.ForgetFrame();
    }

    // The angles of the plane: SPA, SPB, SPC the spatial angles about X, Y and Z (TILT), A, B, C the rotary axis angles
    // (TILT_AXIS), as the source writes them (language 4.2; D82).
    private static HeidenhainDraftBlock? Angles(HeidenhainBlock block, string[] angles, string verb)
    {
        string[] axes = ["A", "B", "C"];
        var entry = new HeidenhainDraftBlock().WithVerb(verb);
        for (int index = 0; index < angles.Length; index++)
        {
            if (block.Take(angles[index]) is SourceWord word && HeidenhainMotion.ValueOf(block, word) is Value value)
            {
                entry.Add(axes[index], value);
            }
        }

        bool complete = verb == "TILT" ? entry.Words.Count == 3 : entry.Words.Count > 0;
        if (!complete && !block.Draft.IsRaw)
        {
            block.Draft.KeepAsRaw(verb == "TILT"
                ? "PLANE SPATIAL needs its three spatial angles SPA, SPB and SPC"
                : "PLANE AXIAL needs the angle of a rotary axis");
        }

        return complete ? entry : null;
    }

    // TURN positions the rotary axes with the retract of MB first, MOVE positions them with the tool tip on the
    // workpiece, STAY only rotates the coordinate system (MOVE=TURN, MOVE, STAY); TABLE ROT and COORD ROT are ROT=TABLE
    // and ROT=COORD; FMAX, the rapid of the positioning, is the form language 6 reads as MOVE=TURN (controllers
    // heidenhain.md 3; language 4.2, 6; D82). MB MAX and MB n retract along the tool axis before the rotary axes turn,
    // written as RETRACT in front of the tilt, as the reader of Siemens writes the _FR retract of CYCLE800
    // (controller-mapping 1, TILT; D83). SEQ, ABST, F, SYM and the other options have no NCX word and keep the block RAW
    // (controller-mapping 1, MOVE and ROT; D5).
    // TODO(question): MOVE=TURN positions the rotary axes "tool retracted first" (language 4.2), and MB says by how
    // much; whether the MB of a PLANE is the RETRACT in front of the TILT, or a part of MOVE=TURN that the compiler's
    // move template writes, is not said; the reader writes the RETRACT, and a PLANE RESET that positions the axes back
    // (TURN, MOVE) stays RAW.
    private static bool Options(HeidenhainBlock block, HeidenhainDraftBlock? entry, out HeidenhainDraftBlock? retract)
    {
        retract = null;
        string? move = null;
        string? rot = null;
        List<SourceWord> words = block.Unread();
        for (int index = 0; index < words.Count; index++)
        {
            SourceWord word = words[index];
            string next = index + 1 < words.Count ? words[index + 1].Address : "";
            block.MarkRead(word);
            if (word.Address is "TURN" or "MOVE" or "STAY" && word.Text.Length == 0 && move is null)
            {
                move = word.Address;
            }
            else if (word.Address == "FMAX")
            {
                continue;
            }
            else if (word.Address is "TABLE" or "COORD" && next == "ROT" && rot is null)
            {
                rot = word.Address;
                block.MarkRead(words[++index]);
            }
            else if (word.Address == "MB" && word.Text.Length == 0 && next == "MAX" && retract is null)
            {
                retract = new HeidenhainDraftBlock().WithVerb("RETRACT");
                block.MarkRead(words[++index]);
            }
            else if (word.Address == "MB" && word.Number is decimal distance && retract is null)
            {
                retract = distance == 0 ? new HeidenhainDraftBlock() : new HeidenhainDraftBlock().WithVerb("RETRACT",
                    word.ToNcxNumber()!);
            }
            else
            {
                block.Draft.KeepAsRaw($"{word.Address}{word.Text} of a PLANE has no NCX word (controller-mapping 1, MOVE "
                    + "and ROT)");
                return false;
            }
        }

        retract = retract?.Verb is null ? null : retract;
        if (entry is null && (move is "TURN" or "MOVE" || retract is not null || rot is not null))
        {
            block.Draft.KeepAsRaw("PLANE RESET that positions the rotary axes back, TURN or MOVE, has no NCX word; "
                + "TILT=RESET only removes the tilt (language 4.2)");
            return false;
        }

        if (retract is not null && move is not ("TURN" or "MOVE"))
        {
            block.Draft.KeepAsRaw("MB retracts before the rotary axes turn, which STAY does not do");
            return false;
        }

        if (entry is not null && move is not null)
        {
            entry.Add("MOVE", new IdentValue(move));
        }

        if (entry is not null && rot is not null)
        {
            entry.Add("ROT", new IdentValue(rot));
        }

        return true;
    }
}
