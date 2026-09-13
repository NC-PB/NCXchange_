using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// The spindles (language 4.5, 4.11; virtual machine 2.4, 3.8 rules 4 and 5, 5; D64): the mode and the synchronization
/// (raised by ResourceResolver), a spindle that is OFF while a LINE cuts, and the speed limits of the machine.
/// </summary>
internal static class SpindleValidation
{
    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Spindle",
        Summary = "The spindles, their mode and synchronization, and their speed limits (language 4.5, 4.11; VM 2.4, "
            + "3.8 rules 4 and 5, 5; machine-config 5; D64).",
        Rules =
        [
            ValidationRule.Error(DiagnosticCodes.SpindleInAxisMode, "SPINDLE on a spindle in AXIS mode.",
                "VM 3.8 rule 4, 5"),
            ValidationRule.Error(DiagnosticCodes.AxisOfSpindleInSpindleMode, "C= on a spindle in SPINDLE mode.",
                "VM 3.8 rule 4, 5"),
            ValidationRule.Error(DiagnosticCodes.SyncSpindleNotInSpindleMode,
                "SPINDLE_SYNC with a spindle that is not in SPINDLE mode.", "VM 3.8 rule 5"),
            ValidationRule.Warning(DiagnosticCodes.SynchronizedSpindleCommanded,
                "RPM or SPINDLE of the following spindle while it runs synchronized.", "VM 3.8 rule 5"),
            ValidationRule.Error(DiagnosticCodes.SyncNeedsTwoSpindles,
                "SPINDLE_SYNC with more or fewer than two spindle roles.", "language 4.5; VM 3.8 rule 5"),
            ValidationRule.Warning(DiagnosticCodes.SpindleOffBeforeLine,
                "Spindle OFF before a LINE: the spindle of the current tool holder, the default spindle when the "
                + "holder has none.", "VM 2.3, 5") with { SuppressedInUncalledSub = true },
            ValidationRule.Warning(DiagnosticCodes.RpmAboveMax, "RPM above the spindle's rpm_max.",
                "VM 5; machine-config 5; D64"),
            ValidationRule.Warning(DiagnosticCodes.RpmBelowMin, "RPM below the spindle's rpm_min.",
                "VM 5; machine-config 5; D64"),
        ],
    };

    /// <summary>
    /// Spindle OFF before a LINE is a WARNING: the spindle of the current tool holder ([[resource]] spindle), the
    /// default spindle when the holder has none (virtual machine 5). The current tool holder is the holder of the last
    /// TOOL (virtual machine 2.3), and the state words of the block take effect before its motion (language 5 rule 3).
    /// The rule depends on the caller's state and is suppressed inside a subprogram that no program of the file calls
    /// (3.9, D99). Which spindle is the default one on the built-in default machine is wave-1 question #53.
    /// </summary>
    // TODO(question): virtual machine 5 names the spindle OFF "before a LINE", while ARC and CYCLE_CALL cut with the
    // spindle as well (language 4.3, 4.7); only LINE is checked until that is answered, as for "LINE without feed".
    public static void CheckSpindleBeforeLine(BlockContext context)
    {
        if (context.Block.Verb?.Key != "LINE")
        {
            return;
        }

        ChannelState state = context.State;
        MachineConfig machine = context.Machine;
        string? holder = state.LastHolder;
        string? holderSpindle = holder is null ? null : machine.FindResource(holder)?.Spindle;
        string? spindleId = holderSpindle ?? machine.ResolveDefaultSpindle()?.Id;
        if (spindleId is null
            || !state.Spindles.TryGetValue(spindleId, out SpindleState? spindle)
            || spindle.Direction != SpindleDirection.Off)
        {
            return;
        }

        string holderName = holder is null || HolderName(machine, holder) is not string name ? "" : " " + name;
        string message = holderSpindle is not null
            ? $"LINE cuts while spindle {ResourceName(machine, spindleId)}, the spindle of the current tool "
                + $"holder{holderName}, is OFF (virtual machine 5)."
            : $"LINE cuts while the default spindle {ResourceName(machine, spindleId)} is OFF; the current tool "
                + $"holder{holderName} has no spindle of its own (virtual machine 5).";
        context.CallerRuleDiagnostics.Warning(context.Block, DiagnosticCodes.SpindleOffBeforeLine, message);
    }

    /// <summary>
    /// RPM above the spindle's rpm_max or below its rpm_min is a WARNING (virtual machine 5, D64); the limits stand in
    /// the [spindle.ROLE] table of the spindle's role (machine-config 5). RPM keeps its meaning and is ignored while
    /// CSS is on (language 4.11), so it is not compared then. With limits = "clamp" the expander rewrites the value
    /// before the block runs and says so itself (D64).
    /// </summary>
    public static void CheckRpmLimits(BlockContext context)
    {
        foreach (Word word in context.Block.Words)
        {
            if (word.Key != "RPM"
                || !context.ResourceOf.TryGetValue(word, out string? spindleId)
                || MotionRules.NumberOf(word) is not decimal rpm
                || context.State.Spindles[spindleId].Css
                || SpindleTableOf(context.Machine, spindleId) is not FunctionTable table)
            {
                continue;
            }

            string spindle = ResourceName(context.Machine, spindleId);
            if (table.RpmMax is decimal max && rpm > max)
            {
                context.Diagnostics.Warning(context.Block, DiagnosticCodes.RpmAboveMax,
                    $"{word.ToCanonical()} is above the rpm_max {Text(max)} of the spindle {spindle} (virtual machine "
                    + "5, D64).");
            }
            else if (table.RpmMin is decimal min && rpm < min)
            {
                context.Diagnostics.Warning(context.Block, DiagnosticCodes.RpmBelowMin,
                    $"{word.ToCanonical()} is below the rpm_min {Text(min)} of the spindle {spindle} (virtual machine "
                    + "5, D64).");
            }
        }
    }

    /// <summary>
    /// The name a program uses for a resource: the role that names it in [roles] (language 4.10); a resource created on
    /// the spot is named by its role already (D103); otherwise its id.
    /// </summary>
    public static string ResourceName(MachineConfig machine, string resourceId)
    {
        foreach (KeyValuePair<string, string> role in machine.Roles)
        {
            if (role.Value == resourceId)
            {
                return role.Key;
            }
        }

        return resourceId;
    }

    // The name of a holder in a message: its role, or the role it was created on the spot for (D103); null for a holder
    // of the machine without a role, the default holder that TOOL without an address reaches (virtual machine 3.8
    // rule 2).
    private static string? HolderName(MachineConfig machine, string holderId)
    {
        string name = ResourceName(machine, holderId);
        return name != holderId || machine.FindResource(holderId) is null ? name : null;
    }

    // The [spindle.ROLE] table of a spindle: the table of a role that names it (machine-config 5).
    private static FunctionTable? SpindleTableOf(MachineConfig machine, string spindleId)
    {
        foreach (KeyValuePair<string, string> role in machine.Roles)
        {
            if (role.Value == spindleId && machine.SpindleTables.TryGetValue(role.Key, out FunctionTable? table))
            {
                return table;
            }
        }

        return null;
    }

    private static string Text(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
