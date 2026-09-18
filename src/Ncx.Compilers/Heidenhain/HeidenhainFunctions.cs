using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The M functions of Klartext (controllers heidenhain.md 1, 2, 4; controller-mapping 1, 2, 4; machine-config 5): the
/// feed mode as M136 and M137, the spindle, its orientation and the coolant through the tables of the machine, a named
/// function through [func], MFUNC as its M function with a WARNING, and STOP as M0 and M1. A function stands in a block
/// of its own, before the motion of its NCX block, and STOP after it.
/// </summary>
internal static class HeidenhainFunctions
{
    // What the control has active (TargetState).
    private const string FeedModeKey = "FEED_MODE";
    private const string SpindleKey = "SPINDLE:";
    private const string CoolantKey = "COOLANT:";

    // The channel a bare COOLANT addresses (language 4.6, virtual machine 2.5).
    private const string DefaultCoolant = "STANDARD";

    // TODO(question): D252: when the M functions of a Klartext block act, at its start or at its end, is open; every
    // function of an NCX block is written in a Klartext block of its own before the motion, which acts before the
    // motion whichever the answer (language 5 rule 3), and which honours the recommendation of D252 for M5, M9 and M0.

    /// <summary>
    /// Writes the functions of the block that act before its motion: FEED_MODE, SPINDLE, ORIENT, COOLANT, FUNC and
    /// MFUNC (language 5 rule 3).
    /// </summary>
    public static void Write(HeidenhainBlock writing)
    {
        // In the canonical order of the words, whatever order the block writes them in (language 5 rule 6, buckets 8,
        // 10 and 11).
        WriteFeedMode(writing);
        foreach (Word word in writing.TakeAll("SPINDLE"))
        {
            WriteSpindle(writing, word);
        }

        foreach (Word word in writing.TakeAll("ORIENT"))
        {
            WriteOrient(writing, word);
        }

        foreach (Word word in writing.TakeAll("COOLANT"))
        {
            WriteCoolant(writing, word);
        }

        foreach (Word word in writing.TakeAll("FUNC"))
        {
            WriteFunction(writing, word);
        }

        foreach (Word word in writing.TakeAll("MFUNC"))
        {
            WriteMFunction(writing, word);
        }
    }

    /// <summary>
    /// Writes STOP after the motion of its block: STOP=PROGRAM as M0, STOP=OPTIONAL as M1 (controller-mapping 1, STOP;
    /// controllers heidenhain.md 1).
    /// </summary>
    public static void WriteStop(HeidenhainBlock writing)
    {
        if (writing.Take("STOP") is Word stop)
        {
            writing.Line(stop.Value is IdentValue { Name: "OPTIONAL" } ? "M1" : "M0");
        }
    }

    /// <summary>
    /// The role that addresses a resource in [roles], the key of its function tables (machine-config 4 and 5); null
    /// when no role names it.
    /// </summary>
    public static string? RoleOf(MachineConfig machine, string? resource)
    {
        foreach (KeyValuePair<string, string> role in machine.Roles)
        {
            if (role.Value == resource)
            {
                return role.Key;
            }
        }

        return null;
    }

    // FEED_MODE=PER_REV as M136, PER_MIN as M137, on change (controller-mapping 2, F and FEED_MODE; controllers
    // heidenhain.md 2).
    // TODO(question): the target state of a program starts unknown (P3-03), so the FEED_MODE=PER_MIN of the complete
    // header of D34 is written as M137, which the Klartext sources 2.5D_FRAESEN.h and BOHREN.h do not write; no
    // document says whether the control has M137 active at the start of a program (virtual machine 4 resets the feed
    // mode at PROGRAM=END), so that the header's feed mode would write nothing.
    private static void WriteFeedMode(HeidenhainBlock writing)
    {
        if (writing.Take("FEED_MODE") is not Word mode)
        {
            return;
        }

        string code = mode.Value is IdentValue { Name: "PER_REV" } ? "M136" : "M137";
        if (writing.Target.Changes(FeedModeKey, code))
        {
            writing.Line(code);
        }
    }

    // SPINDLE:role=CW with the template of [spindle.ROLE], the default spindle without a role address, on change
    // (language 4.5; machine-config 5; virtual machine 3.8 rules 2 and 2a).
    private static void WriteSpindle(HeidenhainBlock writing, Word word)
    {
        writing.MarkWritten(word);
        MachineConfig machine = writing.Machine;
        string? spindle = word.Addr is string addr
            ? machine.ResolveRole(addr)?.Id
            : machine.ResolveDefaultSpindle()?.Id;
        string state = word.Value.ToCanonical();
        if (!writing.Target.Changes(SpindleKey + spindle, state))
        {
            return;
        }

        string? role = word.Addr ?? RoleOf(machine, spindle);
        writing.Template(StateOf(machine.SpindleTables, role, state), $"[spindle.{role}] {state} (machine-config 5)",
            new TemplateValues());
    }

    // ORIENT:role=90 with the ORIENT template of the spindle and its {angle} (language 4.5; machine-config 5).
    private static void WriteOrient(HeidenhainBlock writing, Word word)
    {
        writing.MarkWritten(word);
        MachineConfig machine = writing.Machine;
        string? role = word.Addr ?? RoleOf(machine, machine.ResolveDefaultSpindle()?.Id);
        var values = new TemplateValues();
        if (HeidenhainNumbers.NumberOf(word.Value) is decimal angle)
        {
            values.Set("angle", angle);
        }

        writing.Template(StateOf(machine.SpindleTables, role, "ORIENT"), $"[spindle.{role}] ORIENT (machine-config 5)",
            values);
    }

    // COOLANT:channel=ON with the template of the channel, STANDARD without an address, on change (language 4.6;
    // machine-config 5).
    private static void WriteCoolant(HeidenhainBlock writing, Word word)
    {
        writing.MarkWritten(word);
        string channel = word.Addr ?? DefaultCoolant;
        string state = word.Value.ToCanonical();
        if (writing.Target.Changes(CoolantKey + channel, state))
        {
            writing.Template(StateOf(writing.Machine.Coolant, channel, state),
                $"[coolant] {channel} {state} (machine-config 5)", new TemplateValues());
        }
    }

    // FUNC:name=STATE with the template of that state of [func] (language 4.6, machine-config 5).
    private static void WriteFunction(HeidenhainBlock writing, Word word)
    {
        writing.MarkWritten(word);
        string state = word.Value.ToCanonical();
        string? template = word.Addr is string name ? writing.Machine.FindFunction(name, state) : null;
        writing.Template(template, $"[func] {word.Addr} {state} (machine-config 5)", new TemplateValues());
    }

    // MFUNC=n is the M function n, for the functions the machine configuration does not name; the compiler warns
    // (language 4.6).
    private static void WriteMFunction(HeidenhainBlock writing, Word word)
    {
        writing.MarkWritten(word);
        if (word.Value is not IntegerValue number)
        {
            HeidenhainNumbers.ReportValue(writing, word);
            return;
        }

        string code = "M" + number.Number.ToString(CultureInfo.InvariantCulture);
        writing.Line(code);
        writing.Warning(DiagnosticCodes.HeidenhainMFunctionNotNamed,
            $"{word.ToCanonical()} is written as {code}, an M function the machine configuration does not name "
            + "(language 4.6).");
    }

    // The template of a state of a table; null when the table or the state is not there.
    private static string? StateOf(IReadOnlyDictionary<string, FunctionTable> tables, string? name, string state)
    {
        if (name is null || !tables.TryGetValue(name, out FunctionTable? table))
        {
            return null;
        }

        return table.States.TryGetValue(state, out string? template) ? template : null;
    }
}
