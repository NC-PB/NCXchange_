using Ncx.Core.Catalog;
using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Validation;

/// <summary>
/// The frame of virtual machine 5 (language 4.1, 4.2; virtual machine 2.1, 3.4): SETPOS, the path tolerance and the
/// positioning options of a tilt. MOVE and ROT without TILT or TILT_AXIS are the parser's (PAR016, language 5 rule 5).
/// </summary>
internal static class FrameValidation
{
    /// <summary>
    /// The rules of the family.
    /// </summary>
    public static ValidationFamily Family { get; } = new()
    {
        Name = "Frame",
        Summary = "SETPOS, the path tolerance and the tilted plane (language 4.1, 4.2; VM 2.1, 3.4, 5). MOVE and ROT "
            + "without TILT or TILT_AXIS are PAR016 of the structure.",
        Rules =
        [
            ValidationRule.Error(DiagnosticCodes.SetposAxisUnknown,
                "SETPOS with an axis unknown in every frame, except directly after a HOME of that axis that found no "
                + "reference point.", "VM 3.4, 5; D100, D101"),
            ValidationRule.Error(DiagnosticCodes.ToleranceWordWithoutTolerance,
                "TOLERANCE:ROTARY or TOLERANCE_MODE without an active TOLERANCE.", "language 4.1; VM 5; D85"),
            ValidationRule.Error(DiagnosticCodes.SetposWithoutAxis, "SETPOS without an axis word.",
                "language 4.2; VM 5"),
            ValidationRule.Warning(DiagnosticCodes.RotWithoutTableKinematics,
                "ROT on a target without table kinematics: the machine file has no rotary axis on a table. Not checked "
                + "without a machine file.", "language 4.2; VM 5; D82"),
        ],
    };

    /// <summary>
    /// SETPOS declares the position of the axes it names (language 4.2); SETPOS without an axis word is an ERROR
    /// (virtual machine 5). An axis word of SETPOS names an axis: X Y Z A B C, their incremental forms, or a machine
    /// axis of the D93 form (language 5 rule 2).
    /// </summary>
    public static void CheckSetposNamesAnAxis(BlockContext context)
    {
        Block block = context.Block;
        if (block.Verb?.Key != "SETPOS")
        {
            return;
        }

        foreach (Word word in block.Words)
        {
            bool standardAxis = word.Definition is not null
                && word.Addr is null
                && WordCatalog.IsStandardAxis(word.Key);
            bool machineAxis = word.Definition is null && WordCatalog.TryMachineAxis(word.Key, out _);
            if (standardAxis || machineAxis)
            {
                return;
            }
        }

        context.Diagnostics.Error(block, DiagnosticCodes.SetposWithoutAxis,
            "SETPOS declares the position of the axes it names, SETPOS C=0, and this one names none (language 4.2, "
            + "virtual machine 5).");
    }

    /// <summary>
    /// TOLERANCE:ROTARY and TOLERANCE_MODE stand only with an active TOLERANCE (language 4.1): without one they are an
    /// ERROR of the modal state (virtual machine 5, D85). The state words of a block do not depend on each other
    /// (virtual machine 3 step 3), so the TOLERANCE of the same block makes it active and a TOLERANCE=OFF ends it.
    /// </summary>
    public static void CheckToleranceWords(BlockContext context)
    {
        if (context.State.Frame.Tolerance.Value is not null)
        {
            return;
        }

        foreach (Word word in context.Block.Words)
        {
            bool rotaryTolerance = word.Key == "TOLERANCE" && word.Addr is not null;
            if (!rotaryTolerance && word.Key != "TOLERANCE_MODE")
            {
                continue;
            }

            context.Diagnostics.Error(context.Block, DiagnosticCodes.ToleranceWordWithoutTolerance,
                $"{word.ToCanonical()} stands only with an active TOLERANCE, and the path tolerance is OFF (language "
                + "4.1, virtual machine 5, D85).");
        }
    }

    /// <summary>
    /// ROT chooses, on table kinematics, whether the table turns or only the coordinate system; a target that has no
    /// such option ignores it with a WARNING (language 4.2, virtual machine 5, D82). The built-in default machine of
    /// D103 is no target, because nothing is compiled for it (D77), so without a machine file ROT is not checked.
    /// </summary>
    // TODO(question): virtual machine 5 warns for "ROT on a target without table kinematics" without saying what table
    // kinematics is in a machine file, nor whether it is checked without one; here it is a rotary [[axis]] whose owner
    // is a [[resource]] of type table (machine-config 4), and a run without a machine file does not check it, until
    // D191 is answered.
    public static void CheckRot(Block block, MachineConfig machine, Diagnostics diagnostics)
    {
        if (block.Find("ROT") is not Word rot || machine.Machine.Controller is null || HasRotaryTable(machine))
        {
            return;
        }

        diagnostics.Warning(block, DiagnosticCodes.RotWithoutTableKinematics,
            $"{rot.ToCanonical()} chooses how a rotary table reaches the plane, and the machine has no rotary axis on "
            + "a table: the option is ignored (language 4.2, virtual machine 5, D82).");
    }

    // A rotary axis of the machine owned by a table (machine-config 4: [[axis]] owner, [[resource]] type).
    private static bool HasRotaryTable(MachineConfig machine)
    {
        foreach (AxisDef axis in machine.Axes)
        {
            if (axis.Kind == AxisKind.Rotary
                && axis.Owner is string owner
                && machine.FindResource(owner)?.Type == ResourceType.Table)
            {
                return true;
            }
        }

        return false;
    }
}
