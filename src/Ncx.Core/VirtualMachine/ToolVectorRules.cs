using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// The vector form of 5-axis motion (language 4.3, virtual machine 2.2, 3.1, D81): TX TY TZ, the tool axis direction at
/// the end point, and NX NY NZ, the surface normal, unit vectors in the active workpiece frame instead of rotary axis
/// words under TCPM=ON. The virtual machine stores them as written and never resolves them to axes; that is the
/// kinematics module's job.
/// </summary>
internal static class ToolVectorRules
{
    private static readonly string[] s_toolVectorKeys = ["TX", "TY", "TZ"];
    private static readonly string[] s_surfaceNormalKeys = ["NX", "NY", "NZ"];

    /// <summary>
    /// Applies the vector words of a motion block, and forgets the vectors after a rotary axis word.
    /// </summary>
    /// <param name="context">The motion block with its axis words resolved (step 2).</param>
    /// <param name="arcTolerance">The arc tolerance, which the unit length is checked with (D36).</param>
    public static void Apply(BlockContext context, decimal arcTolerance)
    {
        Block block = context.Block;
        MotionState motion = context.State.Motion;
        bool rotaryWord = HasRotaryWord(context);
        bool toolVectorWritten = HasAny(block, s_toolVectorKeys);
        bool surfaceNormalWritten = HasAny(block, s_surfaceNormalKeys);

        // The tool vector and the surface normal are unknown again after any rotary axis word (virtual machine 2.2,
        // D81).
        if (!toolVectorWritten && !surfaceNormalWritten)
        {
            if (rotaryWord)
            {
                motion.ToolVector = null;
                motion.SurfaceNormal = null;
            }

            return;
        }

        // TODO(question): language 4.3 and virtual machine 3.1 write the vector form for a LINE, and the parser lets
        // the vector words stand under RAPID, ARC and CYCLE_CALL as well (Heidenhain LN with FMAX is a rapid move with
        // a tool vector); what they mean there is not said. They are checked and stored as on a LINE until that is
        // answered.
        if (!CheckWords(context, rotaryWord, surfaceNormalWritten))
        {
            return;
        }

        // The VM checks the length, 1 within the arc tolerance, else ERROR; a component from an expression is not
        // evaluated in STATIC mode and leaves its vector unknown (virtual machine 1, 3.1, D36).
        IReadOnlyList<decimal>? toolVector = VectorOf(block, s_toolVectorKeys);
        IReadOnlyList<decimal>? surfaceNormal = surfaceNormalWritten ? VectorOf(block, s_surfaceNormalKeys) : null;
        bool unitLength = IsUnitLength(context, toolVector, "TX TY TZ", arcTolerance);
        unitLength = IsUnitLength(context, surfaceNormal, "NX NY NZ", arcTolerance) && unitLength;
        if (!unitLength)
        {
            return;
        }

        // The VM stores the vectors as written, marks the rotary axis positions unknown and does not resolve the
        // vectors to axes (virtual machine 3.1, D81). The surface normal belongs to the LINE that writes it and stays
        // otherwise as the state table has it (virtual machine 2.2).
        motion.ToolVector = toolVector;
        if (surfaceNormalWritten)
        {
            motion.SurfaceNormal = surfaceNormal;
        }

        foreach (string axis in new List<string>(motion.Position.Keys))
        {
            if (context.Resources.IsRotary(axis))
            {
                motion.Position[axis] = AxisPosition.Unknown;
            }
        }
    }

    // Vector words without TCPM=ON, rotary words and vector words in one block, and an incomplete vector are ERRORs:
    // TX TY TZ all three together, NX NY NZ optional and only together with TX TY TZ (language 4.3, virtual machine
    // 3.1, 5, D81).
    private static bool CheckWords(BlockContext context, bool rotaryWord, bool surfaceNormalWritten)
    {
        Block block = context.Block;
        bool valid = true;
        if (!context.State.Frame.Tcpm)
        {
            context.Diagnostics.Error(block, DiagnosticCodes.VectorWithoutTcpm,
                "TX TY TZ and NX NY NZ stand only under TCPM=ON (language 4.3, virtual machine 3.1, D81).");
            valid = false;
        }

        if (rotaryWord)
        {
            context.Diagnostics.Error(block, DiagnosticCodes.VectorWithRotaryWords,
                "A block gives the tool direction by rotary axis words or by the vector TX TY TZ, never both (language "
                + "4.3, virtual machine 3.1, D81).");
            valid = false;
        }

        if (!HasAll(block, s_toolVectorKeys) || (surfaceNormalWritten && !HasAll(block, s_surfaceNormalKeys)))
        {
            context.Diagnostics.Error(block, DiagnosticCodes.VectorIncomplete,
                "TX TY TZ come all three together, and NX NY NZ all three and only together with TX TY TZ (language "
                + "4.3, virtual machine 5, D81).");
            valid = false;
        }

        return valid;
    }

    // A unit vector: its length is 1 within the arc tolerance (virtual machine 3.1, D36, D81). An unknown vector is not
    // checked.
    private static bool IsUnitLength(BlockContext context, IReadOnlyList<decimal>? vector, string words,
        decimal arcTolerance)
    {
        if (vector is null)
        {
            return true;
        }

        double x = (double)vector[0];
        double y = (double)vector[1];
        double z = (double)vector[2];
        double length = Math.Sqrt((x * x) + (y * y) + (z * z));
        if (Math.Abs(length - 1) <= (double)arcTolerance)
        {
            return true;
        }

        string tolerance = arcTolerance.ToString(CultureInfo.InvariantCulture);
        context.Diagnostics.Error(context.Block, DiagnosticCodes.VectorNotUnitLength,
            $"{words} is a unit vector, of length 1 within the arc tolerance {tolerance}, and this one is not "
            + "(virtual machine 3.1, D81).");
        return false;
    }

    // The three components as written; null when one of them is an expression (virtual machine 1).
    private static List<decimal>? VectorOf(Block block, string[] keys)
    {
        var vector = new List<decimal>();
        foreach (string key in keys)
        {
            if (block.Find(key) is not Word word || MotionRules.NumberOf(word) is not decimal component)
            {
                return null;
            }

            vector.Add(component);
        }

        return vector;
    }

    // A rotary axis word: an axis word of the block that resolved to a rotary axis, A, B, C or C2 (virtual machine 2.2,
    // 3.8 rule 3).
    private static bool HasRotaryWord(BlockContext context)
    {
        foreach (string axis in context.AxisOf.Values)
        {
            if (context.Resources.IsRotary(axis))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAny(Block block, string[] keys)
    {
        foreach (string key in keys)
        {
            if (block.Has(key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAll(Block block, string[] keys)
    {
        foreach (string key in keys)
        {
            if (!block.Has(key))
            {
                return false;
            }
        }

        return true;
    }
}
