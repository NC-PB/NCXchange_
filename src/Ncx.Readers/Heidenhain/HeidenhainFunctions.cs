using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// The M functions of a Klartext block (controllers heidenhain.md 1, 2, 4; controller-mapping 1, 2, 4; machine-config
/// 5): M0 and M1 as STOP, the functions of the machine's tables compared by number (D105), the controller's own M126
/// and M127 as ROTARY_PATH, M116 and M117 as ROTARY_FEED, M128 and M129 as TCPM, M136 and M137 as FEED_MODE, M140 MB as
/// RETRACT, and any other M function as MFUNC with a WARNING. M91 is the motion's, M99 the cycle's, M30 and M2 the
/// structure pass's.
/// </summary>
internal static class HeidenhainFunctions
{
    // The M functions of the controller itself, which no table of the machine needs to name (controllers heidenhain.md
    // 1, 2; differences.md, M functions).
    private static readonly string[] s_ownCodes =
    [
        "M0", "M1", "M2", "M30", "M91", "M92", "M99", "M116", "M117", "M126", "M127", "M128", "M129", "M136", "M137",
        "M140",
    ];

    // The states of [spindle.ROLE] that SPINDLE takes as its value (machine-config 5, language 4.5).
    private static readonly string[] s_spindleStates = ["CW", "CCW", "OFF"];

    /// <summary>
    /// Tells whether the controller or a table of the machine names an M function, so that no reader rule is asked for
    /// it: the tables are tried first (D40, D66, machine-config 5).
    /// </summary>
    /// <param name="word">The M word.</param>
    /// <param name="templates">The templates of the machine.</param>
    public static bool Names(SourceWord word, TemplateSet templates)
    {
        string? code = NativeCode.Of(word);
        return code is null || s_ownCodes.Contains(code) || templates.FindFunctionByCode(code) is not null;
    }

    /// <summary>
    /// Reads the M functions of a block that the other concerns left.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(HeidenhainBlock block)
    {
        if (!block.Draft.IsRaw)
        {
            ReadRetract(block);
        }

        foreach (SourceWord word in block.Unread())
        {
            if (block.Draft.IsRaw)
            {
                return;
            }

            if (word.Address == "M")
            {
                ReadCode(block, word);
            }
        }
    }

    // An M function: STOP, a word of the controller, the word of a function table, or MFUNC with a WARNING
    // (controller-mapping 1, 2, 4; machine-config 5).
    private static void ReadCode(HeidenhainBlock block, SourceWord word)
    {
        string? code = NativeCode.Of(word);
        if (code is null || word.Number is not decimal number || number != decimal.Truncate(number))
        {
            block.Draft.KeepAsRaw($"M{word.Text} names no M function");
            return;
        }

        block.MarkRead(word);
        if (ReadOwnCode(block, word, code))
        {
            return;
        }

        string? state = block.Templates.FindFunctionByCode(code);
        if (state is not null)
        {
            ReadTableState(block, code, state);
            return;
        }

        // An M function that matches no entry becomes MFUNC=n with a WARNING (machine-config 5, language 4.6), reported
        // when the block is written (HeidenhainDraft.Warnings).
        long mfunc = decimal.ToInt64(number);
        string text = mfunc.ToString(CultureInfo.InvariantCulture);
        block.Draft.Warnings.Add(new HeidenhainWarning(DiagnosticCodes.HeidenhainMCodeNotNamed,
            $"M{text} is no M function of the controller or of a table of the machine; it is written as MFUNC={text} "
            + "(machine-config 5)."));
        block.Draft.AddState("MFUNC", null, new IntegerValue(mfunc, text));
    }

    // The M functions of the controller that NCX has words for (controllers heidenhain.md 1, 2; controller-mapping 1
    // and 2, STOP, ROTARY_PATH, ROTARY_FEED, TCPM, FEED_MODE; D86); true when the code is one of them.
    private static bool ReadOwnCode(HeidenhainBlock block, SourceWord word, string code)
    {
        string? key = code switch
        {
            "M0" or "M1" => "STOP",
            "M126" or "M127" => "ROTARY_PATH",
            "M116" or "M117" => "ROTARY_FEED",
            "M128" or "M129" => "TCPM",
            "M136" or "M137" => "FEED_MODE",
            _ => null,
        };
        string value = code switch
        {
            "M0" => "PROGRAM",
            "M1" => "OPTIONAL",
            "M126" => "SHORTEST",
            "M127" => "FULL",
            "M116" => "MM_MIN",
            "M117" => "DEG_MIN",
            "M128" => "ON",
            "M129" => "OFF",
            "M136" => "PER_REV",
            _ => "PER_MIN",
        };
        if (key is null)
        {
            ReadCodeWithoutWord(block, code);
            return code is "M91" or "M92" or "M99";
        }

        // M128 F1000 switches TCPM on with the feed of the compensating motion (controllers heidenhain.md 2), an option
        // of TCPM that has no NCX word (D86): the block stays RAW.
        if (code == "M128" && FeedAfter(block, word) is not null)
        {
            block.Draft.KeepAsRaw("the feed of the compensating motion of M128 F has no NCX word (D86)");
            return true;
        }

        if (key == "TCPM")
        {
            block.Heidenhain.Tcpm = code == "M128";
        }

        block.Draft.AddState(key, null, new IdentValue(value));
        return true;
    }

    // M91 is read with the motion of its block, M99 with the cycle call; left here they stand in a block without a
    // motion. M92 moves in the coordinates of a second datum, which NCX has no word for (controllers heidenhain.md 2).
    private static void ReadCodeWithoutWord(HeidenhainBlock block, string code)
    {
        string? reason = code switch
        {
            "M91" => "M91 gives the coordinates of a motion in the machine frame, and the block has no motion",
            "M92" => "M92 moves in the coordinates of a second datum, which NCX has no word for (language 4.2, FRAME)",
            "M99" => "M99 calls the cycle at the position of its block, which the block does not reach",
            _ => null,
        };
        if (reason is not null)
        {
            block.Draft.KeepAsRaw(reason);
        }
    }

    // The state of a function table names its word with the state as its value, SPINDLE:TOOL=CW; the word of the
    // language is made from it, the address of the default spindle and of the default coolant channel left out
    // (language 4.6, 4.10; wave-1 questions #62 and #63). A state that NCX writes with a value the M function does not
    // carry, ORIENT with its angle, RPM with the speed, stays RAW.
    private static void ReadTableState(HeidenhainBlock block, string code, string state)
    {
        string[] keyAndValue = state.Split('=');
        string[] keyAndAddr = keyAndValue[0].Split(':');
        string key = keyAndAddr[0];
        string? addr = keyAndAddr.Length > 1 ? keyAndAddr[1] : null;
        string value = keyAndValue.Length > 1 ? keyAndValue[1] : "";
        switch (key)
        {
            case "SPINDLE" when s_spindleStates.Contains(value):
                block.Draft.AddState(key, SpindleAddress(block.Machine, addr), new IdentValue(value));
                block.State.LastSpindle = addr;
                return;
            case "SPINDLE_MODE" or "FUNC":
                block.Draft.AddState(key, addr, new IdentValue(value));
                return;
            case "COOLANT":
                // The channel STANDARD is the default channel that a bare COOLANT addresses (language 4.6).
                block.Draft.AddState(key, addr == "STANDARD" ? null : addr, new IdentValue(value));
                return;
            default:
                block.Draft.KeepAsRaw($"{code} is {state} of a table of the machine, which NCX writes with a value the "
                    + "M function does not carry (wave-1 question #62)");
                return;
        }
    }

    // M140 MB MAX retracts along the tool axis to the limit, M140 MB50 by 50, with FMAX or F (controllers heidenhain.md
    // 2; controller-mapping 1, RETRACT; D83): RETRACT and RETRACT=50. RETRACT takes no feed and no other axis words
    // (virtual machine 3.1a), and the M140 MB MAX ALF variants stay RAW (controller-mapping 1).
    private static void ReadRetract(HeidenhainBlock block)
    {
        if (block.FindCode("M140") is not SourceWord code)
        {
            return;
        }

        block.MarkRead(code);
        if (block.Draft.Main.Verb is not null || block.Draft.Main.Has("F", null))
        {
            block.Draft.KeepAsRaw("M140 in a block that also moves or feeds has no NCX form: RETRACT is a block of its "
                + "own without a feed (virtual machine 3.1a)");
            return;
        }

        SourceWord? distance = block.Take("MB");
        Value value = NoValue.Instance;
        if (distance is null)
        {
            block.Draft.KeepAsRaw("M140 names no retract MB");
            return;
        }

        if (distance.Text.Length == 0)
        {
            if (block.Take("MAX") is null)
            {
                block.Draft.KeepAsRaw("M140 MB names neither MAX nor a distance");
                return;
            }
        }
        else if (distance.Number is decimal && distance.ToNcxNumber() is Value amount)
        {
            value = amount;
        }
        else
        {
            block.Draft.KeepAsRaw($"M140 MB{distance.Text} names no distance");
            return;
        }

        block.Take("FMAX");
        if (block.Find("F") is not null)
        {
            block.Draft.KeepAsRaw("RETRACT takes no feed (virtual machine 3.1a)");
            return;
        }

        block.Draft.Main.WithVerb("RETRACT", value);
        if (block.Heidenhain.PlaneAxes(out _, out _, out string tool))
        {
            block.State.ForgetPosition(tool);
        }

        block.Heidenhain.Tangent = null;
    }

    // The F that stands after a word in the block, unread.
    private static SourceWord? FeedAfter(HeidenhainBlock block, SourceWord word)
    {
        int after = block.IndexOf(word);
        foreach (SourceWord candidate in block.Unread())
        {
            if (candidate.Address == "F" && block.IndexOf(candidate) > after)
            {
                return candidate;
            }
        }

        return null;
    }

    // The role address a spindle word carries: none for the default spindle of the machine (language 4.10, virtual
    // machine 3.8 rule 2), the role otherwise.
    private static string? SpindleAddress(MachineConfig machine, string? role)
    {
        if (role is null)
        {
            return null;
        }

        ResourceDef? defaultSpindle = machine.ResolveDefaultSpindle();
        ResourceDef? spindle = machine.ResolveRole(role);
        return defaultSpindle is not null && spindle?.Id == defaultSpindle.Id ? null : role;
    }
}
