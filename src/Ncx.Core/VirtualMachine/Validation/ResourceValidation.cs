using System.Globalization;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// Roles, functions and axes of the machine (language 4.6, 4.10; virtual machine 3.8, 5; D93, D103): the resolution of
/// roles, functions, coolant channels and machine axes (raised by ResourceResolver), MFUNC with a code the machine
/// names, and the change of the workpiece holder while its spindle runs.
/// </summary>
internal static class ResourceValidation
{
    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Resource",
        Summary = "Roles, functions, coolant channels and machine axes, with a machine file and without one "
            + "(language 4.6, 4.10; VM 3.8, 5; D93, D103).",
        Rules =
        [
            ValidationRule.Error(DiagnosticCodes.UnknownRole, "Unknown role, when a machine file is given.",
                "language 4.10; VM 3.8 rule 1, 5"),
            ValidationRule.Error(DiagnosticCodes.WrongResourceType,
                "A role that names a resource of the wrong type for its word.", "VM 3.8 rule 1"),
            ValidationRule.Warning(DiagnosticCodes.NotCheckedNoMachineFile,
                "A role, function, machine axis or coolant channel the built-in default machine lacks when no machine "
                + "file is given: \"not checked: no machine file\", once per name.", "VM 3.8, 5; D103"),
            ValidationRule.Error(DiagnosticCodes.NoDefaultResource,
                "A word without a role address on a machine without a default resource of its kind.",
                "VM 3.8 rule 2"),
            ValidationRule.Error(DiagnosticCodes.UnknownMachineAxis,
                "A machine axis word not declared in [[axis]], when a machine file is given.",
                "VM 3.8 rule 3, 5; D93"),
            ValidationRule.Error(DiagnosticCodes.UnknownFunction, "Unknown function, when a machine file is given.",
                "language 4.6; VM 3.8, 5"),
            ValidationRule.Error(DiagnosticCodes.UnknownFunctionState,
                "Unknown value: FUNC with a state its function does not list.", "machine-config 5; VM 5"),
            ValidationRule.Error(DiagnosticCodes.UnknownCoolantChannel,
                "COOLANT with a channel [coolant] does not name, when a machine file is given.",
                "language 4.6; machine-config 5"),
            ValidationRule.Warning(DiagnosticCodes.MfuncNamedByMachine,
                "MFUNC with a number that the configuration names.", "language 4.6; VM 5; D105"),
            ValidationRule.Warning(DiagnosticCodes.WorkpieceChangeWhileSpindleRuns,
                "WORKPIECE change while the old holder's spindle runs.", "language 4.10; VM 5"),
        ],
    };

    /// <summary>
    /// MFUNC is a raw M function by number, only for functions the machine configuration does not name (language
    /// 4.6); a number whose M code a template of the configuration writes is a WARNING (virtual machine 5). The loader
    /// normalizes every M code of the file to its spelling without leading zeros (machine-config 5, D105), so a
    /// template holds M8, never M08.
    /// </summary>
    public static void CheckMfunc(Block block, MachineConfig machine, Diagnostics diagnostics)
    {
        if (block.Find("MFUNC") is not Word mfunc || mfunc.Value is not IntegerValue number)
        {
            return;
        }

        string code = "M" + number.Number.ToString(CultureInfo.InvariantCulture);
        foreach (KeyValuePair<string, string> template in Templates(machine))
        {
            if (!WritesCode(template.Value, code))
            {
                continue;
            }

            diagnostics.Warning(block, DiagnosticCodes.MfuncNamedByMachine,
                $"{mfunc.ToCanonical()} is {code}, which the machine configuration writes for {template.Key}; the "
                + "NCX word for it says what it does (language 4.6, virtual machine 5).");
            return;
        }
    }

    /// <summary>
    /// A WORKPIECE change while the old holder's spindle runs is a WARNING (virtual machine 5): the part changes hands
    /// while it turns. The state words of a block do not depend on each other (virtual machine 3 step 3), so a SPINDLE
    /// of the old holder that goes OFF in the same block stops it first; selecting the holder that already holds the
    /// part is no change (virtual machine 3.4).
    /// </summary>
    /// <param name="context">The block after its state words.</param>
    /// <param name="holderBefore">The workpiece holder before the block.</param>
    public static void CheckWorkpieceChange(BlockContext context, string? holderBefore)
    {
        if (context.Block.Find("WORKPIECE") is not Word workpiece
            || holderBefore is null
            || context.State.Frame.WorkpieceHolder == holderBefore
            || !context.State.Spindles.TryGetValue(holderBefore, out SpindleState? spindle)
            || spindle.Direction == SpindleDirection.Off)
        {
            return;
        }

        context.Diagnostics.Warning(context.Block, DiagnosticCodes.WorkpieceChangeWhileSpindleRuns,
            $"{workpiece.ToCanonical()} changes the workpiece holder while the spindle "
            + $"{SpindleValidation.ResourceName(context.Machine, holderBefore)} of the old holder runs (language 4.10, "
            + "virtual machine 5).");
    }

    // An M code as a word of a template: "M3" in "M3 P11" and in "H7={value} M7", not in "M30" or "M{mark}".
    private static bool WritesCode(string template, string code)
    {
        foreach (string part in template.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == code)
            {
                return true;
            }
        }

        return false;
    }

    // The templates the compiler writes for the words of a program, by the table and state that hold them: the program
    // end of [format] (machine-config 2), the tool change, HOME and SETPOS (3), and every table of functions and
    // coolant (5).
    private static Dictionary<string, string> Templates(MachineConfig machine)
    {
        var templates = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(templates, "[format] program_end", machine.Format?.ProgramEnd);
        Add(templates, "[format] sub_end", machine.Format?.SubEnd);
        Add(templates, "[tool_change] change", machine.ToolChange?.Change);
        Add(templates, "[tool_change] change_preloaded", machine.ToolChange?.ChangePreloaded);
        Add(templates, "[tool_change] preload", machine.ToolChange?.Preload);
        Add(templates, "[tool_change] unload", machine.ToolChange?.Unload);
        Add(templates, "[home]", machine.Home?.Template);
        Add(templates, "[setpos]", machine.Setpos?.Template);
        foreach (KeyValuePair<string, FunctionTable> table in machine.SpindleTables)
        {
            AddStates(templates, "[spindle." + table.Key + "]", table.Value);
        }

        foreach (KeyValuePair<string, FunctionTable> table in machine.SpindleModeTables)
        {
            AddStates(templates, "[spindle_mode." + table.Key + "]", table.Value);
        }

        if (machine.SpindleSync is FunctionTable sync)
        {
            AddStates(templates, "[spindle_sync]", sync);
        }

        AddWorkpiece(templates, machine.Workpiece);
        foreach (KeyValuePair<string, FunctionTable> channel in machine.Coolant)
        {
            AddStates(templates, "[coolant] " + channel.Key, channel.Value);
        }

        foreach (KeyValuePair<string, FunctionTable> function in machine.Functions)
        {
            AddStates(templates, "[func] " + function.Key, function.Value);
        }

        AddTransform(templates, machine.Transform);
        Add(templates, "[retract] MAX", machine.Retract?.Max);
        Add(templates, "[retract] BY", machine.Retract?.By);
        Add(templates, "[tolerance] ON", machine.Tolerance?.On);
        Add(templates, "[tolerance] OFF", machine.Tolerance?.Off);
        Add(templates, "[diameter] ON", machine.Diameter?.On);
        Add(templates, "[diameter] OFF", machine.Diameter?.Off);
        return templates;
    }

    private static void AddWorkpiece(Dictionary<string, string> templates, WorkpieceConfig? workpiece)
    {
        if (workpiece is null)
        {
            return;
        }

        foreach (KeyValuePair<string, string> holder in workpiece.Templates)
        {
            Add(templates, "[workpiece] " + holder.Key, holder.Value);
        }

        foreach (KeyValuePair<string, FunctionTable> mirror in workpiece.Mirrors)
        {
            AddStates(templates, "[workpiece] " + mirror.Key + "_mirror", mirror.Value);
        }
    }

    private static void AddTransform(Dictionary<string, string> templates, TransformTable? transform)
    {
        if (transform is null)
        {
            return;
        }

        Add(templates, "[transform] CYLINDER_ON", transform.CylinderOn);
        Add(templates, "[transform] CYLINDER_OFF", transform.CylinderOff);
        Add(templates, "[transform] POLAR_ON", transform.PolarOn);
        Add(templates, "[transform] POLAR_OFF", transform.PolarOff);
        Add(templates, "[transform] TCPM_ON", transform.TcpmOn);
        Add(templates, "[transform] TCPM_OFF", transform.TcpmOff);
        Add(templates, "[transform] TILT_ON", transform.TiltOn);
        Add(templates, "[transform] TILT_OFF", transform.TiltOff);
        Add(templates, "[transform] TILT_AXIS_ON", transform.TiltAxisOn);
        Add(templates, "[transform] TILT_TURN", transform.TiltTurn);
        Add(templates, "[transform] ROTARY_PATH_SHORTEST", transform.RotaryPathShortest);
        Add(templates, "[transform] ROTARY_PATH_FULL", transform.RotaryPathFull);
        Add(templates, "[transform] ROTARY_FEED_MM_MIN", transform.RotaryFeedMmMin);
        Add(templates, "[transform] ROTARY_FEED_DEG_MIN", transform.RotaryFeedDegMin);
    }

    private static void AddStates(Dictionary<string, string> templates, string table, FunctionTable function)
    {
        foreach (KeyValuePair<string, string> state in function.States)
        {
            Add(templates, table + " " + state.Key, state.Value);
        }
    }

    private static void Add(Dictionary<string, string> templates, string name, string? template)
    {
        if (!string.IsNullOrEmpty(template))
        {
            templates.TryAdd(name, template);
        }
    }
}
