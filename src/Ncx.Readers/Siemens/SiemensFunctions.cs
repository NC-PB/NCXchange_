using System.Globalization;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The M functions of a SINUMERIK block that no other concern reads (controllers siemens.md 1, 5; controller-mapping
/// 1 and 4; machine-config 5; language 4.1, 4.6): M0 and M1 stop the program, the codes of the machine's tables are
/// their words, coolant, named functions and the spindle states, codes compared by number (D105), any other M is
/// MFUNC=n with a WARNING; the H functions of the PLC have no NCX word and stay RAW words of the block.
/// </summary>
internal static class SiemensFunctions
{
    // The M codes the controller defines, which the reader maps itself: the stops, the ends, the spindles, the change
    // (controllers siemens.md 1, 5).
    private static readonly string[] s_controllerCodes =
        ["M0", "M1", "M2", "M3", "M4", "M5", "M6", "M17", "M19", "M30", "M70"];

    /// <summary>
    /// Tells whether the controller or a table of the machine names an M word, so that no reader rule is asked for it
    /// (D40, D66, machine-config 5).
    /// </summary>
    /// <param name="block">The block, with the templates of the machine.</param>
    /// <param name="word">An M word.</param>
    public static bool Names(SiemensBlock block, SourceWord word)
    {
        if (SiemensBlock.CodeOf(word) is not string code)
        {
            return true;
        }

        return Array.IndexOf(s_controllerCodes, code) >= 0 || block.Templates.FindFunctionByCode(code) is not null;
    }

    /// <summary>
    /// Reads the M and H words no other concern read.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        foreach (SourceWord word in block.Unread())
        {
            if (word.Address == "H" || SiemensSpindles.NumberOfAddress(word.Address, 'H') is not null)
            {
                // H functions go to the PLC; NCX has no word for them (controllers siemens.md 1).
                block.KeepAsRawWord(word);
            }
            else if (word.Address == "M" && SiemensBlock.CodeOf(word) is string code)
            {
                ReadCode(block, word, code);
            }
            else if (SiemensSpindles.NumberOfAddress(word.Address, 'M') is not null && word.Text.Length > 0)
            {
                ReadExtended(block, word);
            }
        }
    }

    // M0 is STOP=PROGRAM, M1 STOP=OPTIONAL (controller-mapping 1, STOP); a code of a table is its word; any other M is
    // MFUNC=n (controller-mapping 4, MFUNC).
    private static void ReadCode(SiemensBlock block, SourceWord word, string code)
    {
        block.MarkRead(word);
        if (code is "M0" or "M1")
        {
            block.Draft.AddState("STOP", null, new IdentValue(code == "M0" ? "PROGRAM" : "OPTIONAL"));
            return;
        }

        if (block.Templates.FindFunctionByCode(code) is string state && WordOf(block, state) is Word named)
        {
            block.Draft.AddState(named.Key, named.Addr, named.Value);
            return;
        }

        long number = (long)word.Number!.Value;
        block.Draft.AddState("MFUNC", null, new IntegerValue(number, number.ToString(CultureInfo.InvariantCulture)));
        block.Draft.Warnings.Add(new SiemensWarning(DiagnosticCodes.SiemensMCodeNotNamed,
            $"{code} is named by no table of the machine and is written as MFUNC={number} (machine-config 5, language "
            + "4.6)."));
    }

    // M1=28 and the like: a table may name the code with its extension; MFUNC takes a number without one, so any other
    // stays a RAW word (controller-mapping 4).
    private static void ReadExtended(SiemensBlock block, SourceWord word)
    {
        string text = word.Address + "=" + word.Text;
        if (block.Templates.FindFunctionByCode(text) is string state && WordOf(block, state) is Word named)
        {
            block.MarkRead(word);
            block.Draft.AddState(named.Key, named.Addr, named.Value);
            return;
        }

        block.KeepAsRawWord(word);
    }

    // The word of a table state (machine-config 5): SPINDLE:role=CW with the role address of D154, ORIENT at 0,
    // SPINDLE_MODE:role, COOLANT with the channel STANDARD as the bare word (language 4.6), FUNC:name=state.
    // TODO(question): D154, the words of the states of the spindle tables and of [spindle_sync]; the reader follows its
    // recommendation, and a PHASE code without an angle stays RAW.
    private static Word? WordOf(SiemensBlock block, string state)
    {
        string[] keyAndValue = state.Split('=');
        string[] keyAndAddr = keyAndValue[0].Split(':');
        string key = keyAndAddr[0];
        string? addr = keyAndAddr.Length > 1 ? keyAndAddr[1] : null;
        string value = keyAndValue[1];
        return key switch
        {
            "SPINDLE" when value is "CW" or "CCW" or "OFF" && addr is not null =>
                new Word { Key = key, Addr = SiemensSpindles.Address(block, addr), Value = new IdentValue(value) },
            "SPINDLE" when value == "ORIENT" && addr is not null =>
                new Word
                {
                    Key = "ORIENT",
                    Addr = SiemensSpindles.Address(block, addr),
                    Value = new IntegerValue(0, "0"),
                },
            "SPINDLE_MODE" => new Word { Key = key, Addr = addr, Value = new IdentValue(value) },
            "SPINDLE_SYNC" when value == "OFF" => new Word { Key = key, Value = new IdentValue(value) },
            "COOLANT" => new Word { Key = key, Addr = addr == "STANDARD" ? null : addr, Value = new IdentValue(value) },
            "FUNC" => new Word { Key = key, Addr = addr, Value = new IdentValue(value) },
            _ => null,
        };
    }
}
