using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine;

/// <summary>
/// Diameter programming (language 4.2, virtual machine 3.1, D28, D60): under DIAMETER=ON the X words that are
/// coordinates of the workpiece are diameters and the virtual machine stores radii, so they are halved on the way into
/// the position store; every other radial distance is a radius value already.
/// </summary>
internal static class DiameterRules
{
    // The cycle words that are absolute coordinates along the drilling axis (language 4.7).
    private static readonly string[] s_drillingAxisCoordinates = ["SURFACE", "CLEARANCE", "DEPTH", "SAFE"];

    /// <summary>
    /// Tells whether DIAMETER=ON makes a word a diameter: X and IX, the absolute CENTER:X, and the X words of a cycle
    /// with AXIS=X; CENTER:IX, R, PECK and every other radial distance stay radius values (D60).
    /// </summary>
    /// <param name="word">The word as written.</param>
    /// <param name="cycleAxis">The drilling axis of the cycle the word belongs to; null for a word of no cycle.</param>
    public static bool IsDiameterWord(Word word, string? cycleAxis)
    {
        // The absolute and incremental X words of the workpiece (language 4.2, D60).
        if (word.Addr is null && word.Key is "X" or "IX")
        {
            return true;
        }

        // The absolute CENTER:X; CENTER:IX is relative to the start point and a radius value (D60).
        if (word.Key == "CENTER" && word.Addr == "X")
        {
            return true;
        }

        // Under AXIS=X the drilling axis is X and the coordinates along it are X values, diameters like every other X
        // (language 4.7, virtual machine 3.3, D59, D60). SAFE is one of them: the X values of an AXIS=X cycle are
        // halved like every other X (virtual machine 3.3, D60), and SAFE is an absolute coordinate along the drilling
        // axis as SURFACE, CLEARANCE and DEPTH are (language 4.2, DIAMETER row; 4.7; wave-1 question #98).
        return cycleAxis == "X" && s_drillingAxisCoordinates.Contains(word.Key);
    }

    /// <summary>
    /// The value a word brings into the position store: half of it for a diameter word under DIAMETER=ON, the value as
    /// written otherwise (D60).
    /// </summary>
    /// <param name="word">The word as written.</param>
    /// <param name="value">Its value.</param>
    /// <param name="diameterOn">True under DIAMETER=ON.</param>
    /// <param name="cycleAxis">The drilling axis of the cycle the word belongs to; null for a word of no cycle.</param>
    public static decimal ToRadius(Word word, decimal value, bool diameterOn, string? cycleAxis)
    {
        return diameterOn && IsDiameterWord(word, cycleAxis) ? value / 2m : value;
    }
}
