using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The M codes of a Fanuc block and the codes the reader keeps as RAW (controllers fanuc.md 1, 8, 9 rule 7;
/// controller-mapping 1, 4, 7, 9; machine-config 5): M0 and M1 as STOP, the M codes of the machine's function tables
/// compared by number (D105), the workpiece codes of [workpiece], the wait marks of [sync] as SYNC with WITH, any other
/// M code as MFUNC with a WARNING; G10, G31, G38, the builder macros of [raw] and every G code the reader does not map
/// stay RAW with a WARNING, never dropped (D5). The NT NURSE branch commands of the builder nakamura are not RAW:
/// FanucMacro reads them as JUMP with IF (controller-mapping 6, 9).
/// </summary>
internal static class FanucBuilder
{
    // The M codes of the controller itself, which the structure pass, the calls and the tool change read.
    private static readonly string[] s_ownCodes = ["M0", "M1", "M2", "M30", "M6", "M98", "M99", "M198"];

    // The G codes the reader maps in every G-code system (controllers fanuc.md 3, 4; controller-mapping 1, 2).
    private static readonly string[] s_commonCodes =
    [
        "G0", "G1", "G2", "G3", "G4", "G15", "G16", "G17", "G18", "G19", "G28", "G30", "G40", "G41", "G42", "G43",
        "G43.4", "G43.5", "G44", "G49", "G52", "G53", "G53.1", "G54", "G54.1", "G55", "G56", "G57", "G58", "G59",
        "G65", "G66", "G67", "G68", "G68.1", "G68.2", "G69", "G5.1", "G7.1", "G12.1", "G13.1", "G50.1", "G51.1", "G80",
        "G96", "G97",
    ];

    // The G codes of a mill and of every G-code system of a lathe (controllers fanuc.md 3, 6).
    private static readonly string[] s_millCodes =
    [
        "G20", "G21", "G73", "G74", "G76", "G81", "G82", "G83", "G84", "G85", "G86", "G87", "G88", "G89", "G90", "G91",
        "G92", "G94", "G95", "G98", "G99",
    ];

    private static readonly string[] s_systemACodes =
    [
        "G20", "G21", "G50", "G70", "G71", "G72", "G73", "G74", "G75", "G76", "G83", "G84", "G85", "G87", "G88", "G89",
        "G90", "G92", "G94", "G98", "G99",
    ];

    private static readonly string[] s_systemBCodes =
    [
        "G20", "G21", "G70", "G71", "G72", "G73", "G74", "G75", "G76", "G77", "G78", "G79", "G83", "G84", "G85", "G87",
        "G88", "G89", "G90", "G91", "G92", "G94", "G95", "G98", "G99",
    ];

    private static readonly string[] s_systemCCodes =
    [
        "G70", "G71", "G77", "G78", "G79", "G83", "G84", "G85", "G87", "G88", "G89", "G90", "G91", "G92", "G94", "G95",
        "G98", "G99",
    ];

    /// <summary>
    /// Why a block is kept as RAW before it is read, for a code NCX cannot express: the data setting of G10, the skip
    /// of G31, the push check of G38, a builder macro of [raw], a G code the reader does not map (controllers fanuc.md
    /// 9 rule 7; controller-mapping 9); null for a block the reader reads.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static string? ReasonToKeepAsRaw(FanucBlock block)
    {
        foreach (SourceWord word in block.Source.Words)
        {
            if (RawEmitter.IsBuilderCode(word, block.Machine.Raw))
            {
                return $"{word.Address}{word.Text} is a builder macro of the [raw] table (controller-mapping 9)";
            }

            string? code = word.Address == "G" ? NativeCode.Of(word) : null;
            if (word.Address == "G" && (code is null || !IsMapped(block, code)))
            {
                return ReasonFor(code ?? "G" + word.Text);
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the M codes of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(FanucBlock block)
    {
        foreach (SourceWord word in block.Unread())
        {
            if (block.Draft.IsRaw)
            {
                return;
            }

            if (word.Address == "M" && !block.IsRead(word))
            {
                ReadCode(block, word);
            }
        }
    }

    /// <summary>
    /// Tells whether the controller or a table of the machine names an M code, so that no reader rule is asked for it:
    /// the tables are tried first (D40, D66, machine-config 5).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="word">The M word.</param>
    public static bool Names(FanucBlock block, SourceWord word)
    {
        // M200 P of the builder nakamura calls a program as M98 does, which FanucMacro reads (controller-mapping 6, 8).
        string? code = NativeCode.Of(word);
        return code is null
            || s_ownCodes.Contains(code)
            || (code == "M200" && FanucMacro.IsBuilderNakamura(block.Machine))
            || FanucFunctions.Of(block, word, out _) is not null
            || WorkpieceOf(block, code) is not null
            || MarkOf(block, word) is not null;
    }

    // An M code: STOP, the word of a function table, WORKPIECE, SYNC, or MFUNC with a WARNING (controller-mapping 1, 4,
    // 7; machine-config 5).
    private static void ReadCode(FanucBlock block, SourceWord word)
    {
        string? code = NativeCode.Of(word);
        if (code is null || word.Number is not decimal number || number != decimal.Truncate(number))
        {
            block.Draft.KeepAsRaw($"M{word.Text} names no M code");
            return;
        }

        block.MarkRead(word);
        if (code is "M0" or "M1")
        {
            // M0 stops, M1 stops when the optional stop switch is on (controllers fanuc.md 1, language 4.1).
            block.Draft.AddState("STOP", null, new IdentValue(code == "M0" ? "PROGRAM" : "OPTIONAL"));
            return;
        }

        if (FanucFunctions.Of(block, word, out SourceWord? parameter) is FanucFunction function)
        {
            if (parameter is not null)
            {
                block.MarkRead(parameter);
            }

            block.Draft.AddState(function.Key, function.Addr, OrientationOf(block, function) ?? function.Value);
            if (function.SpindleRole is not null)
            {
                block.State.LastSpindle = function.SpindleRole;
                block.Fanuc.Unknowns.Spindle = false;
            }

            return;
        }

        if (WorkpieceOf(block, code) is string role)
        {
            block.Draft.AddState("WORKPIECE", null, new IdentValue(role));
            return;
        }

        if (MarkOf(block, word) is int mark)
        {
            ReadSync(block, mark);
            return;
        }

        // The PHASE code of [spindle_sync] is named by a table, so no MFUNC (machine-config 5), and PHASE takes the
        // angle of the phase-synchronous run (language 4.5), which the code does not carry: the block stays RAW (D5).
        // TODO(question): Nakamura M92 runs the spindles phase-synchronous with the phase position that M93 set in the
        // control (controller-mapping 4, SPINDLE_SYNC and PHASE); the documents do not say which PHASE an M92 is.
        if (FanucFunctions.IsPhaseCode(block, word))
        {
            block.Draft.KeepAsRaw($"M{word.Text} is the PHASE of [spindle_sync], which NCX writes with its angle only");
            return;
        }

        // An M code that matches no entry becomes MFUNC=n with a WARNING (machine-config 5, language 4.6), reported
        // when the block is written and not kept as RAW for another word (FanucDraft.Warnings).
        long mfunc = decimal.ToInt64(number);
        string text = mfunc.ToString(CultureInfo.InvariantCulture);
        block.Draft.Warnings.Add((DiagnosticCodes.FanucMCodeNotNamed,
            $"M{text} is no code of the controller or of a table of the machine; it is written as MFUNC={text} "
            + "(machine-config 5)."));
        block.Draft.AddState("MFUNC", null, new IntegerValue(mfunc, text));
    }

    // M19 S or M19 R orients the spindle to the angle of S or R (controller-mapping 4, ORIENT).
    private static Value? OrientationOf(FanucBlock block, FanucFunction function)
    {
        if (function.Key != "ORIENT")
        {
            return null;
        }

        SourceWord? angle = block.Take("S") ?? block.Take("R");
        return angle is null ? null : FanucMacro.ValueOf(block, angle);
    }

    // The M code of a [workpiece] template selects the holder the program machines, WORKPIECE=role (controller-mapping
    // 4, WORKPIECE; language 4.10; D40, D57).
    // TODO(question): [workpiece] writes the spindle's datum with the code, "G54 M428" (machine-config 5), and the
    // source writes them in separate blocks, N100 M428 and G54 G18 of the Nakamura program; the reader takes the M code
    // of the template as WORKPIECE and reads the datum as ORIGIN where it stands.
    private static string? WorkpieceOf(FanucBlock block, string code)
    {
        if (block.Machine.Workpiece is not WorkpieceConfig workpiece)
        {
            return null;
        }

        foreach (KeyValuePair<string, string> template in workpiece.Templates)
        {
            foreach (string token in template.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.StartsWith('M') && NativeCode.SameCode(token, code))
                {
                    return template.Key;
                }
            }
        }

        return null;
    }

    // The wait codes of the mark range of [sync] are the marks of SYNC (controllers fanuc.md 8; controller-mapping 7;
    // machine-config 5).
    private static int? MarkOf(FanucBlock block, SourceWord word)
    {
        if (block.Machine.Sync?.MarkRange is not MarkRange range || word.Number is not decimal number)
        {
            return null;
        }

        return number >= range.First && number <= range.Last ? decimal.ToInt32(number) : null;
    }

    // SYNC=m with the participating paths of P as WITH, a path list P12 or a bitmask P3 as [sync] paths says; without P
    // all paths wait (controllers fanuc.md 8; controller-mapping 7; language 4.8).
    private static void ReadSync(FanucBlock block, int mark)
    {
        block.Draft.AddState("SYNC", null, new IntegerValue(mark, mark.ToString(CultureInfo.InvariantCulture)));
        SyncConfig sync = block.Machine.Sync!;
        if (sync.Wait?.Contains("{paths}", StringComparison.Ordinal) != true || block.Take("P") is not SourceWord paths)
        {
            return;
        }

        var channels = new List<string>();
        string digits = paths.Text.TrimEnd('.');
        if (sync.Paths == SyncPaths.Bitmask && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture,
            out int mask))
        {
            for (int channel = 1; channel <= 8; channel++)
            {
                if ((mask & (1 << (channel - 1))) != 0)
                {
                    channels.Add(channel.ToString(CultureInfo.InvariantCulture));
                }
            }
        }
        else
        {
            foreach (char channel in digits)
            {
                channels.Add(channel.ToString());
            }
        }

        block.Draft.AddState("WITH", null, new ListValue(channels));
    }

    // Whether the reader maps a G code: the codes of every system and of the machine's system, and the builder codes of
    // the machine's [tool_change] and [transform] templates.
    private static bool IsMapped(FanucBlock block, string code)
    {
        string[] system = block.System switch
        {
            null => s_millCodes,
            GcodeSystem.A => s_systemACodes,
            GcodeSystem.B => s_systemBCodes,
            _ => s_systemCCodes,
        };
        if (s_commonCodes.Contains(code) || system.Contains(code) || FanucMacro.IsBranchCommand(block.Machine, code))
        {
            return true;
        }

        TransformTable? transform = block.Machine.Transform;
        ToolChangeConfig? toolChange = block.Machine.ToolChange;
        string?[] templates =
        [
            toolChange?.Change, toolChange?.Preload, transform?.PolarOn, transform?.PolarOff, transform?.CylinderOn,
            transform?.CylinderOff, transform?.TcpmOn, transform?.TcpmOff,
        ];
        foreach (string? template in templates)
        {
            string? first = template?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (first is not null && first.StartsWith('G') && NativeCode.SameCode(first, code))
            {
                return true;
            }
        }

        return false;
    }

    // The reason a G code the reader does not map is kept as RAW, in the words of the machine (controller-mapping 1,
    // 9).
    private static string ReasonFor(string code)
    {
        return code switch
        {
            "G10" => "G10 writes data of the control, datums and offsets (controller-mapping 9)",
            "G31" => "G31 is a skip move for probing (controller-mapping 9)",
            "G38" => "G38 is a push check (controller-mapping 9)",
            "G5" or "G8" or "G61.1" => $"{code} switches the path smoothing with a tolerance in a parameter "
                + "(controller-mapping 1, TOLERANCE)",
            "G61" or "G62" or "G63" or "G64" => $"{code}, the cutting mode of group 13, has no NCX word",
            "G33" => "G33, thread cutting, has no NCX word",
            _ => $"{code} has no NCX word the Fanuc reader maps (controller-mapping 9)",
        };
    }
}
