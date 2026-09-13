using Ncx.Core.Catalog;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Core.Expander;

/// <summary>
/// Which expansion rules of the machine configuration a block triggers (machine-config 5, 5a): the rule of the tool
/// change for a TOOL word, of every function table one of its words sets, of the catalog entry of its cycle.
/// </summary>
internal static class ExpansionRules
{
    // The coolant channel that a bare COOLANT addresses (virtual machine 2.5, F29).
    private const string DefaultCoolantChannel = "STANDARD";

    /// <summary>
    /// The rules a block triggers, each table's rule once, in the canonical order of the words that trigger them
    /// (language 5 rule 6).
    /// </summary>
    public static IReadOnlyList<TriggeredRule> Of(Block block, MachineConfig machine)
    {
        // TODO(question): architecture 5.5 and machine-config 5a do not say in which order the blocks of several rules
        // on one block stand; they nest, the rule of the first word in canonical order outermost, and the blocks of the
        // rewriters stand inside those of the rules, next to the block, until that is answered.
        var triggered = new List<TriggeredRule>();
        foreach (Word word in CanonicalOrder.Sort(block))
        {
            if (RuleOf(word, machine) is TriggeredRule rule && !Contains(triggered, rule.Source))
            {
                triggered.Add(rule);
            }
        }

        return triggered;
    }

    // Every function state, the tool change and every catalog cycle may carry pre, post, requires and restore
    // (machine-config 5a): TOOL triggers the rule of [tool_change]; SPINDLE, RPM, ORIENT, CSS, VC and RPM_MAX the rule
    // of the [spindle.ROLE] table of their spindle, whose states they set (machine-config 5); SPINDLE_MODE the rule of
    // [spindle_mode.ROLE], SPINDLE_SYNC the rule of [spindle_sync], COOLANT the rule of its [coolant] channel, FUNC the
    // rule of its [func] entry, CYCLE the rule of the catalog entry of its cycle.
    // TODO(question): machine-config 5a gives the four keys to "every function state", while its example writes them
    // once per table, THROUGH = { ON = "M51", OFF = "M9", requires = ..., restore = ... }, and the machine model keeps
    // one rule per table (FunctionTable.Rule); the rule applies to every word that sets a state of its table,
    // COOLANT:THROUGH=OFF as well as COOLANT:THROUGH=ON, until that is answered.
    private static TriggeredRule? RuleOf(Word word, MachineConfig machine)
    {
        switch (word.Key)
        {
            case "TOOL":
                return Triggered(machine.ToolChange?.Rule, "[tool_change]");
            case "SPINDLE" or "RPM" or "ORIENT" or "CSS" or "VC" or "RPM_MAX":
                string? spindleRole = word.Addr ?? StateKeys.RoleOfDefaultSpindle(machine);
                return spindleRole is null
                    ? null
                    : Triggered(TableOf(machine.SpindleTables, spindleRole)?.Rule, "[spindle." + spindleRole + "]");
            case "SPINDLE_MODE":
                string? modeRole = word.Addr ?? StateKeys.RoleOfDefaultSpindle(machine);
                return modeRole is null
                    ? null
                    : Triggered(TableOf(machine.SpindleModeTables, modeRole)?.Rule, "[spindle_mode." + modeRole + "]");
            case "SPINDLE_SYNC":
                return Triggered(machine.SpindleSync?.Rule, "[spindle_sync]");
            case "COOLANT":
                string channel = word.Addr ?? DefaultCoolantChannel;
                return Triggered(TableOf(machine.Coolant, channel)?.Rule, "[coolant] " + channel);
            case "FUNC" when word.Addr is string function:
                return Triggered(TableOf(machine.Functions, function)?.Rule, "[func] " + function);
            // TODO(question): machine-config 5a turns the keys of a catalog cycle into blocks "around the triggering
            // block", and language 4.7 defines a cycle once (modal) and executes it by CYCLE_CALL at each position;
            // neither names the triggering block of a catalog cycle: the definition, each CYCLE_CALL, or both. A pre
            // on the definition gives the mode code before the cycle of language 4.7.1 (the Doosan M291 before G83),
            // but a post, or a requires with its restore, then undoes the mode or the required state before the
            // CYCLE_CALL blocks drill. The rule fires on the block that names the cycle, never on a CYCLE_CALL block,
            // until that is answered.
            case "CYCLE":
                return CycleEntryOf(word, machine) is CycleEntry entry
                    ? Triggered(entry.Rule, "cycle " + entry.Name)
                    : null;
            default:
                return null;
        }
    }

    // A cycle is named by CYCLE=name, a name of the cycle catalog (language 4.7.1), or written natively as
    // CYCLE:<controller>=n, which the catalog of that controller family passes through and finds by its native cycle,
    // n compared as the catalog writes it (CycleCatalog.FindNative, wave-1 question #69). CYCLE=OFF names no cycle.
    private static CycleEntry? CycleEntryOf(Word word, MachineConfig machine)
    {
        CycleCatalog catalog = machine.CycleCatalog;
        if (word.Addr is null)
        {
            return word.Value is IdentValue name && name.Name != "OFF" ? catalog.Find(name.Name) : null;
        }

        return catalog.PassesThrough(word.Addr) ? catalog.FindNative(word.Value.ToCanonical()) : null;
    }

    private static FunctionTable? TableOf(IReadOnlyDictionary<string, FunctionTable> tables, string name)
    {
        return tables.TryGetValue(name, out FunctionTable? table) ? table : null;
    }

    private static TriggeredRule? Triggered(ExpansionRule? rule, string source)
    {
        return rule is null ? null : new TriggeredRule(rule, source);
    }

    private static bool Contains(List<TriggeredRule> triggered, string source)
    {
        foreach (TriggeredRule rule in triggered)
        {
            if (rule.Source == source)
            {
                return true;
            }
        }

        return false;
    }
}
