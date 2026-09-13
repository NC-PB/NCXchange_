using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Events;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Cli.History;

/// <summary>
/// One state variable that one executed block changed: a row of ncx trace and a value of ncx annotate (virtual machine
/// 6). The variable is named by the key that sets it (3.10), the values as NCX writes them, empty when unknown.
/// </summary>
internal sealed record TraceRow
{
    // A variable is set by VAR (language 4.9), so the key that sets it is VAR with the variable as its address, the
    // state key of virtual machine 3.10.
    private const string VariableKey = "VAR:";

    /// <summary>
    /// channel.id of the channel that executed the block (virtual machine 2.8).
    /// </summary>
    public required int Channel { get; init; }

    /// <summary>
    /// The block as the virtual machine executed it: its line, and the origin of a generated block (3.10).
    /// </summary>
    public required Block Block { get; init; }

    /// <summary>
    /// The pc of the block, its index in the program the virtual machine runs (virtual machine 2.7).
    /// </summary>
    public required int BlockIndex { get; init; }

    /// <summary>
    /// The state variable: X, F, SPINDLE:S1, TOOL:H1, VAR:Q1.
    /// </summary>
    public required string Variable { get; init; }

    /// <summary>
    /// The value before the block; empty when it was unknown or none (virtual machine 6).
    /// </summary>
    public required string OldValue { get; init; }

    /// <summary>
    /// The value after the block; empty when it is unknown or none.
    /// </summary>
    public required string NewValue { get; init; }

    /// <summary>
    /// The row of an event: a STATE_CHANGE, or a VAR_CHANGE that changed its variable; null for every other event.
    /// </summary>
    /// <param name="vmEvent">An event of the virtual machine.</param>
    public static TraceRow? Of(VmEvent vmEvent)
    {
        switch (vmEvent)
        {
            // STATE_CHANGE is raised on any modal change with the variable, the old and the new value, empty when
            // unknown (virtual machine 6, 7).
            case StateChangeEvent change:
                return new TraceRow
                {
                    Channel = change.Channel,
                    Block = change.Block,
                    BlockIndex = change.After.Flow.Pc,
                    Variable = change.Variable,
                    OldValue = change.OldValue,
                    NewValue = change.NewValue,
                };

            // The variables are state variables of the channel as well (virtual machine 2.7), and trace shows them
            // (virtual machine 6; language 6, PATTERN_LOOP note 1). VAR_CHANGE is raised at every VAR and ARG; a row
            // stands for a value that changed. An unassigned or UNKNOWN value is empty (virtual machine 1, 6).
            case VarChangeEvent variable:
                string oldValue = ValueText(variable.OldValue);
                string newValue = ValueText(variable.NewValue);
                if (oldValue == newValue)
                {
                    return null;
                }

                return new TraceRow
                {
                    Channel = variable.Channel,
                    Block = variable.Block,
                    BlockIndex = variable.After.Flow.Pc,
                    Variable = VariableKey + variable.Name,
                    OldValue = oldValue,
                    NewValue = newValue,
                };

            default:
                return null;
        }
    }

    private static string ValueText(VariableValue? value)
    {
        return value is null || value.IsUnknown ? "" : value.ToString();
    }
}
