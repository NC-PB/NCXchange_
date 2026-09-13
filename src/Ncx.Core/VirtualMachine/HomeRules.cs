using System.Globalization;
using Ncx.Core.Catalog;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// HOME, the reference point return (language 4.3; virtual machine 3 step 5, D100): the named axes move at rapid to
/// the reference point of the configuration and are known in the MACHINE frame there; an axis without the reference
/// point is a WARNING and unknown in every frame.
/// </summary>
internal static class HomeRules
{
    /// <summary>
    /// Executes a HOME block.
    /// </summary>
    /// <param name="context">The HOME block with its axis words resolved.</param>
    /// <param name="warnedAxes">The axes the WARNING of D100 named in this run, once per run and axis.</param>
    /// <param name="homedWithoutReference">The axes a HOME without a reference point left unknown and no block has
    /// named since, for SETPOS (D101).</param>
    public static void Home(BlockContext context, HashSet<string> warnedAxes, HashSet<string> homedWithoutReference)
    {
        // HOME without an axis name: ERROR (virtual machine 5).
        Block block = context.Block;
        if (!NamesAnAxis(block))
        {
            context.Diagnostics.Error(block, DiagnosticCodes.HomeWithoutAxis,
                "HOME needs the names of the axes it returns, HOME X Z (language 4.3, virtual machine 5).");
            return;
        }

        // POINT=2 selects the second reference point; without POINT it is the first (virtual machine 3 step 5).
        int point = 1;
        if (block.Find("POINT") is Word pointWord && BlockContext.IntegerOf(pointWord) is int pointNumber)
        {
            point = pointNumber;
        }

        foreach (Word word in block.Words)
        {
            if (!context.AxisOf.TryGetValue(word, out string? axis))
            {
                continue;
            }

            // Afterwards the axis is known in the MACHINE frame at the reference coordinates (virtual machine 3 step 5).
            if (context.Resources.ReferencePoint(axis, point) is decimal reference)
            {
                context.State.Motion.Position[axis] = new AxisPosition(reference, PositionFrame.Machine, Known: true);
                homedWithoutReference.Remove(axis);
                continue;
            }

            // An axis without the reference point in the configuration is a WARNING, once per run and axis, and unknown
            // in every frame afterwards; a compiler that must write the coordinates reports the ERROR (D100).
            if (warnedAxes.Add(axis))
            {
                string pointName = point == 1
                    ? "reference point"
                    : "reference point " + point.ToString(CultureInfo.InvariantCulture);
                context.Diagnostics.Warning(block, DiagnosticCodes.HomeWithoutReferencePoint,
                    $"HOME {word.Key}: the configuration has no {pointName} for {axis}; the axis is unknown in every "
                    + "frame afterwards (virtual machine 3 step 5, D100).");
            }

            context.State.Motion.Position[axis] = AxisPosition.Unknown;
            homedWithoutReference.Add(axis);
        }
    }

    // The axis names of a HOME block: X Y Z A B C and the machine axis names of the D93 form, bare (language 4.3, 5
    // rule 2).
    private static bool NamesAnAxis(Block block)
    {
        foreach (Word word in block.Words)
        {
            bool standardAxis = word.Definition is not null && WordCatalog.IsStandardAxis(word.Key);
            bool machineAxis = word.Definition is null && WordCatalog.TryMachineAxis(word.Key, out _);
            if (standardAxis || machineAxis)
            {
                return true;
            }
        }

        return false;
    }
}
