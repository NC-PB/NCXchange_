using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Core.Expander;

/// <summary>
/// An expansion rule as generated NCX blocks around the block that triggered it (machine-config 5a, architecture 5.5).
/// </summary>
internal static class RuleBlocks
{
    /// <summary>
    /// Puts the blocks of a rule around the block: before it the @SAVE of every variable of restore, the words that
    /// establish requires and the pre blocks; after it the post blocks and the @RESTORE of every variable of restore
    /// (architecture 5.5).
    /// </summary>
    public static void Apply(TriggeredRule triggered, BlockExpansion expansion, GeneratedText generated,
        MachineConfig machine)
    {
        ExpansionRule rule = triggered.Rule;
        Block origin = expansion.Origin;
        var before = new List<Block>();
        var after = new List<Block>();

        // requires: the expander inserts the words that establish the conditions, preceded by @SAVE of the variables
        // named in restore (machine-config 5a). restore puts back what @SAVE kept, so the @SAVE blocks stand first
        // whenever the rule has a restore list, with or without requires; the flowchart of architecture 5.5 draws them
        // in the requires branch, where every example has them.
        foreach (string variable in rule.Restore)
        {
            Add(before, generated.FromRule("@SAVE=" + StateKeys.WithRole(variable, machine), "restore", triggered,
                origin, GeneratedPlacement.Before));
        }

        // One block per condition, state variable = value, in the order the rule writes them (machine-config 5a).
        foreach (KeyValuePair<string, string> condition in rule.Requires)
        {
            Add(before, generated.FromRule(StateKeys.WithRole(condition.Key, machine) + "=" + condition.Value,
                "requires", triggered, origin, GeneratedPlacement.Before));
        }

        // pre and post: lists of NCX blocks inserted before and after the block (machine-config 5a).
        foreach (string text in rule.Pre)
        {
            Add(before, generated.FromRule(text, "pre", triggered, origin, GeneratedPlacement.Before));
        }

        foreach (string text in rule.Post)
        {
            Add(after, generated.FromRule(text, "post", triggered, origin, GeneratedPlacement.After));
        }

        // restore: the state variables put back after the block through @RESTORE; the virtual machine re-applies the
        // saved value from its own snapshot, so the rule never knows the speed the spindle had (machine-config 5a,
        // virtual machine 3.10).
        foreach (string variable in rule.Restore)
        {
            Add(after, generated.FromRule("@RESTORE=" + StateKeys.WithRole(variable, machine), "restore", triggered,
                origin, GeneratedPlacement.After));
        }

        if (!expansion.Surround(before, after))
        {
            generated.Error(origin, DiagnosticCodes.GeneratedBlockOutsideSection,
                $"The blocks of {triggered.Source} would stand before a BEGIN or after an END block, outside every "
                + "program and subprogram (language 4.13).");
        }
    }

    private static void Add(List<Block> blocks, Block? block)
    {
        if (block is not null)
        {
            blocks.Add(block);
        }
    }
}
