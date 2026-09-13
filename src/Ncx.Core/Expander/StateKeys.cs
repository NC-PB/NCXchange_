using Ncx.Core.Machine;

namespace Ncx.Core.Expander;

/// <summary>
/// The state keys the expander writes for requires and restore (machine-config 5a, virtual machine 3.10). A key the rule
/// writes without an address names the variable of the resource that its word without a role address targets, and the
/// expander writes the role of that resource: requires = { SPINDLE = "OFF" } becomes SPINDLE:MAIN=OFF and restore =
/// ["SPINDLE"] becomes @SAVE=SPINDLE:MAIN (architecture 5.5; virtual machine 3.8 rule 2).
/// </summary>
internal static class StateKeys
{
    /// <summary>
    /// The state key with the role of its default resource when the rule writes none, SPINDLE to SPINDLE:MAIN; the key
    /// as written when it has an address, when its word addresses no role (COOLANT, F), or when no role of [roles]
    /// names the default resource.
    /// </summary>
    /// <param name="stateKey">The key as the rule writes it: "SPINDLE", "SPINDLE:SUB", "COOLANT".</param>
    /// <param name="machine">The machine file with its [roles] and default resources.</param>
    public static string WithRole(string stateKey, MachineConfig machine)
    {
        string key = stateKey.Trim();
        if (key.Contains(':', StringComparison.Ordinal))
        {
            return key;
        }

        // SPINDLE, RPM, SPINDLE_MODE, ORIENT, CSS, VC and RPM_MAX without a role address target default_spindle, TOOL
        // and PRELOAD default_holder (virtual machine 3.8 rule 2).
        string? role = key.ToUpperInvariant() switch
        {
            "SPINDLE" or "RPM" or "SPINDLE_MODE" or "ORIENT" or "CSS" or "VC" or "RPM_MAX" =>
                RoleOfDefaultSpindle(machine),
            "TOOL" or "PRELOAD" => RoleOf(machine.ResolveDefaultHolder()?.Id, machine),
            _ => null,
        };
        return role is null ? key : key + ":" + role;
    }

    /// <summary>
    /// The role of the spindle that the spindle words without a role address target, MAIN; null when the machine has
    /// no default spindle or no role names it (virtual machine 3.8 rule 2).
    /// </summary>
    public static string? RoleOfDefaultSpindle(MachineConfig machine)
    {
        return RoleOf(machine.ResolveDefaultSpindle()?.Id, machine);
    }

    // The role [roles] gives a resource, the first in the order of the file; null when no role names it
    // (language 4.10: a program addresses resources by role, never by the machine's own id).
    private static string? RoleOf(string? resourceId, MachineConfig machine)
    {
        if (resourceId is null)
        {
            return null;
        }

        foreach (KeyValuePair<string, string> role in machine.Roles)
        {
            if (role.Value == resourceId)
            {
                return role.Key;
            }
        }

        return null;
    }
}
