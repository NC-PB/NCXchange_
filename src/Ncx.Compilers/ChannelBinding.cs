using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// The channel binding of the function tables (virtual machine 3.8 rule 2a; machine-config 5; D56): a table with
/// channel = n is accepted by the machine only from channel n, a table with channels = "all" must stand in every
/// channel program behind a wait. The virtual machine ignores the binding; the job compiler moves or duplicates the
/// words of the expanded channel programs, and a single-channel compile reports the ERROR.
/// </summary>
internal static class ChannelBinding
{
    // The spindle words written from the table of their spindle role, [spindle.ROLE] (machine-config 5).
    private static readonly string[] s_spindleKeys = ["SPINDLE", "RPM", "ORIENT", "CSS", "VC", "RPM_MAX"];

    /// <summary>
    /// The table a word is written from, when that table is bound to one channel or to every channel; null for a word
    /// of an unbound table, and for a word of no table.
    /// </summary>
    public static FunctionTable? BoundTableOf(Word word, MachineConfig machine)
    {
        FunctionTable? table = TableOf(word, machine);
        return table is not null && (table.Channel is not null || table.AllChannels) ? table : null;
    }

    /// <summary>
    /// The section of machine-config 5 that binds a word, as a message names it: "[spindle_sync]", "[func] DOOR".
    /// </summary>
    public static string TableName(Word word, MachineConfig machine)
    {
        return word.Key switch
        {
            "SPINDLE_SYNC" or "PHASE" => "[spindle_sync]",
            "SPINDLE_MODE" => $"[spindle_mode.{word.Addr}]",
            "COOLANT" => $"[coolant] {word.Addr ?? "STANDARD"}",
            "FUNC" => $"[func] {word.Addr}",
            _ => $"[spindle.{word.Addr ?? DefaultSpindleRole(machine)}]",
        };
    }

    /// <summary>
    /// A single-channel compile writes no word the machine accepts only from another channel (virtual machine 3.8 rule
    /// 2a, D56), whether the file or an expansion rule writes it: a program runs on its CHANNEL, 1 without one; a
    /// subprogram on the channels of the programs of the file.
    /// </summary>
    // TODO(question): virtual machine 3.8 rule 2a makes only a word bound to another channel an ERROR of a
    // single-channel compile, D56 says "a single-file compile reports an ERROR" for both forms, and the task of
    // implementation 16 lists the ERROR for the channels = "all" word as well; a word bound to every channel is an
    // ERROR too when the machine has more than one channel, since the compile cannot put it into the other programs,
    // until that is answered.
    public static void Check(NcxProgram program, MachineConfig machine, Diagnostics diagnostics)
    {
        var fileChannels = new List<int>();
        foreach (Section section in program.Programs)
        {
            fileChannels.Add(section.Channel);
        }

        foreach (Section section in program.Sections)
        {
            List<int> channels = section.Kind == SectionKind.Program ? [section.Channel] : fileChannels;
            for (int index = section.FirstBlock; index <= section.LastBlock; index++)
            {
                CheckBlock(program.Blocks[index], channels, machine, diagnostics);
            }
        }
    }

    // Every bound word of a block against the channels its section runs on.
    private static void CheckBlock(Block block, List<int> channels, MachineConfig machine, Diagnostics diagnostics)
    {
        foreach (Word word in block.Words)
        {
            if (BoundTableOf(word, machine) is not FunctionTable table)
            {
                continue;
            }

            string name = TableName(word, machine);
            if (table.Channel is int owner && !channels.Contains(owner))
            {
                diagnostics.Error(block, DiagnosticCodes.WordBoundToAnotherChannel, string.Create(
                    CultureInfo.InvariantCulture,
                    $"{word.ToCanonical()}: the machine accepts {name} only from channel {owner}, and the program "
                    + $"runs on channel {string.Join(", ", channels)}; compile the job, which moves the word to the "
                    + $"program of that channel (virtual machine 3.8 rule 2a, D56)."));
            }
            else if (table.AllChannels && machine.Machine.Channels.Count > 1)
            {
                diagnostics.Error(block, DiagnosticCodes.WordBoundToEveryChannel,
                    $"{word.ToCanonical()}: {name} must stand in every channel program behind a wait (channels = "
                    + "\"all\"), which a single-channel compile cannot write; compile the job, which duplicates the "
                    + "word (virtual machine 3.8 rule 2a, D56).");
            }
        }
    }

    // The table of machine-config 5 a word is written from: [spindle.ROLE] for the spindle words, the role of the
    // default spindle without an address (virtual machine 3.8 rule 2); [spindle_mode.ROLE]; [spindle_sync] for
    // SPINDLE_SYNC and its PHASE; [coolant] of the channel, STANDARD without one (virtual machine 2.5); [func] NAME.
    private static FunctionTable? TableOf(Word word, MachineConfig machine)
    {
        if (s_spindleKeys.Contains(word.Key))
        {
            return Find(machine.SpindleTables, word.Addr ?? DefaultSpindleRole(machine));
        }

        return word.Key switch
        {
            "SPINDLE_MODE" => Find(machine.SpindleModeTables, word.Addr),
            "SPINDLE_SYNC" or "PHASE" => machine.SpindleSync,
            "COOLANT" => Find(machine.Coolant, word.Addr ?? "STANDARD"),
            "FUNC" => Find(machine.Functions, word.Addr),
            _ => null,
        };
    }

    // The role of the default spindle whose table the machine gives (virtual machine 3.8 rule 2; machine-config 5).
    private static string? DefaultSpindleRole(MachineConfig machine)
    {
        string? id = machine.ResolveDefaultSpindle()?.Id;
        foreach (KeyValuePair<string, string> role in machine.Roles)
        {
            if (role.Value == id && machine.SpindleTables.ContainsKey(role.Key))
            {
                return role.Key;
            }
        }

        return null;
    }

    private static FunctionTable? Find(IReadOnlyDictionary<string, FunctionTable> tables, string? name)
    {
        return name is not null && tables.TryGetValue(name, out FunctionTable? table) ? table : null;
    }
}
