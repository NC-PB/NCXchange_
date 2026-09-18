using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The arcs of Klartext (controllers heidenhain.md 2; 8 rule 4; controller-mapping 2): an ARC with CENTER as CC, the
/// pole, plus C to the end point, the canonical form; an ARC with R as CR; an ARC with the sweep ANGLE as CC plus CP
/// IPA, the incremental polar angle. DR- is CW, DR+ is CCW (language 4.3).
/// </summary>
internal static class HeidenhainArcs
{
    private const string Clockwise = "DR-";
    private const string Counterclockwise = "DR+";

    /// <summary>
    /// Writes the arc of the block: CR X+2 Y+7 R+5 DR-, CC X+50 Y+50 and C X+70 Y+50 DR+, CC X+50 Y+50 and CP
    /// IPA+737,956 IZ-5,4 DR+ (controllers heidenhain.md 2; 8 rule 4).
    /// </summary>
    public static void Write(HeidenhainBlock writing)
    {
        Word verb = writing.Take("ARC") ?? throw new InvalidOperationException("The block has no ARC.");

        // The arc runs with the compensation of the program, which only an L block switches (language 4.4;
        // controllers heidenhain.md 2).
        HeidenhainMotion.CheckCompensation(writing, "The ARC");
        bool counterclockwise = verb.Value is IdentValue { Name: "CCW" };
        string direction = counterclockwise ? Counterclockwise : Clockwise;

        // CR when the NCX block had R (heidenhain 8 rule 4).
        if (writing.Take("R") is Word radius)
        {
            WriteRadius(writing, radius, direction);
            return;
        }

        // Otherwise the pole CC and the arc about it: C to the end point, the canonical form, or CP IPA over the
        // sweep, the form of Klartext for a sweep (heidenhain 8 rule 4; virtual machine 3.2: the sweep form for
        // controllers that have it, Heidenhain CP IPA).
        if (!WritePole(writing))
        {
            return;
        }

        if (writing.Take("ANGLE") is Word angle)
        {
            WriteSweep(writing, angle, counterclockwise, direction);
        }
        else
        {
            WriteCenter(writing, direction);
        }
    }

    // CR X+2 Y+7 R+5 DR-: a positive radius for an arc of at most 180 degrees, a negative one for more (controllers
    // heidenhain.md 2; language 4.3, R).
    private static void WriteRadius(HeidenhainBlock writing, Word radius, string direction)
    {
        string? axes = HeidenhainAxes.Write(writing, HeidenhainAxes.Of(writing.Block));
        string? value = HeidenhainNumbers.SignedValue(writing, radius.Key, radius.Value);
        if (axes is null)
        {
            return;
        }

        if (value is null)
        {
            HeidenhainNumbers.ReportValue(writing, radius);
            return;
        }

        WriteArc(writing, ["CR", axes, "R" + value, direction]);
    }

    // CC X+50 Y+50, the pole absolute; CC IX IY incremental from the current position, the start of the arc
    // (controllers heidenhain.md 2; controller-mapping 2, CENTER:X and CENTER:IX).
    private static bool WritePole(HeidenhainBlock writing)
    {
        var center = new List<HeidenhainAxisWord>();
        foreach (Word word in writing.Block.Words)
        {
            if (word.Key == "CENTER" && word.Addr is string addr)
            {
                bool incremental = addr.Length > 1 && addr.StartsWith('I');
                center.Add(new HeidenhainAxisWord(word, incremental ? addr.Substring(1) : addr, incremental));
            }
        }

        center.Sort((first, second) => first.Incremental == second.Incremental
            ? string.CompareOrdinal(first.Axis, second.Axis)
            : first.Incremental ? 1 : -1);
        string? pole = HeidenhainAxes.Write(writing, center);
        if (pole is null)
        {
            return false;
        }

        writing.Line("CC " + pole);
        return true;
    }

    // C X+70 Y+50 DR+ to the end point about the pole. A block without an end point in the plane is the full circle,
    // whose end is its start (virtual machine 3.2), written with the coordinates of the start.
    // TODO(question): heidenhain.md 2 writes a helix as CP IPA with the travel IZ, and heidenhain 8 rule 4 writes the
    // arc as CC plus C and only a sweep beyond a turn as CP IPA; for an ARC with an end point and a tool-axis word, a
    // helix of at most one turn, the tool-axis word is written in the C block, as rule 4 reads.
    private static void WriteCenter(HeidenhainBlock writing, string direction)
    {
        List<HeidenhainAxisWord> axes = HeidenhainAxes.Of(writing.Block);
        string? end = axes.Count == 0 ? StartOf(writing) : HeidenhainAxes.Write(writing, axes);
        if (end is null)
        {
            return;
        }

        WriteArc(writing, ["C", end, direction]);
    }

    // CP IPA+737,956 IZ-5,4 DR+: the incremental polar angle in the direction of the arc, with the travel of a helix
    // (controllers heidenhain.md 2; language 4.3, ANGLE; D84).
    private static void WriteSweep(HeidenhainBlock writing, Word angle, bool counterclockwise, string direction)
    {
        if (HeidenhainNumbers.NumberOf(angle.Value) is not decimal sweep)
        {
            HeidenhainNumbers.ReportValue(writing, angle);
            return;
        }

        string? travel = HeidenhainAxes.Write(writing, HeidenhainAxes.Of(writing.Block));
        if (travel is null)
        {
            return;
        }

        string sign = counterclockwise ? "+" : "-";
        string polarAngle = "IPA" + sign + writing.Numbers.Format("IPA", sweep, writing.Block);
        WriteArc(writing, ["CP", polarAngle, travel, direction]);
    }

    // The arc with the feed where it changed (heidenhain 8 rule 2); the compensation is the one the L blocks before it
    // switched (Write).
    private static void WriteArc(HeidenhainBlock writing, List<string> words)
    {
        words.RemoveAll(word => word.Length == 0);
        if (HeidenhainMotion.Feed(writing) is string feed)
        {
            words.Add(feed);
        }

        writing.Line(string.Join(" ", words));
    }

    // The coordinates of the start of the arc in the plane, where the full circle ends, in the workpiece frame of the
    // program, where C takes them: the position before the block read through the setpos shift of a SETPOS (virtual
    // machine 3.4; HeidenhainSetpos.WorkpieceCoordinate); null when one is not known there, which is reported.
    private static string? StartOf(HeidenhainBlock writing)
    {
        var words = new List<string>();
        foreach (string axis in HeidenhainAxes.PlaneAxes(writing.After.Frame.Workplane))
        {
            if (HeidenhainSetpos.WorkpieceCoordinate(writing.Before, axis) is not decimal start)
            {
                writing.Error(DiagnosticCodes.HeidenhainWordWithoutKlartext,
                    $"The full circle ends where it starts, and the start of {axis} is not known in the workpiece "
                    + "frame, so C has no end point (virtual machine 3.2, 3.4; controllers heidenhain.md 2).");
                return null;
            }

            string letter = HeidenhainAxes.LetterOf(writing, axis);
            decimal value = writing.Numbers.DecimalsOf(letter) is int decimals
                ? Math.Round(start, decimals, MidpointRounding.AwayFromZero)
                : start;
            words.Add(letter + HeidenhainNumbers.Signed(writing, letter, value));
        }

        return string.Join(" ", words);
    }
}
