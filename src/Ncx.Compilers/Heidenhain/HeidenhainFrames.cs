using System.Globalization;
using Ncx.Config.Templates;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The machine frame and the modes of the control (controllers heidenhain.md 2; 8 rule 5; controller-mapping 1;
/// machine-config 4 and 5; D83, D85, D86, D100): HOME as the M91 move to the reference coordinates of [[axis]], RETRACT
/// as M140 MB through [retract], TCPM as M128 and M129 or the templates of [transform] (FUNCTION TCPM), ROTARY_PATH as
/// M126 and M127, ROTARY_FEED as M116 and M117, and TOLERANCE as cycle 32 through [tolerance].
/// </summary>
internal static class HeidenhainFrames
{
    /// <summary>
    /// Writes the modes of the block, each on change: TCPM, ROTARY_PATH, ROTARY_FEED and TOLERANCE.
    /// </summary>
    public static void WriteModes(HeidenhainBlock writing)
    {
        TransformTable? transform = writing.Machine.Transform;

        // M128 and M129 switch the tool center point control, M126 and M127 the shortest path of the rotary axes, M116
        // and M117 their feed in mm/min, where the machine names no template of its own, FUNCTION TCPM on the TNC 640
        // (controllers heidenhain.md 2; controller-mapping 1, TCPM, ROTARY_PATH, ROTARY_FEED; machine-config 5; D86).
        WriteMode(writing, "TCPM", "ON", transform?.TcpmOn, "M128", transform?.TcpmOff, "M129");
        WriteMode(writing, "ROTARY_PATH", "SHORTEST", transform?.RotaryPathShortest, "M126", transform?.RotaryPathFull,
            "M127");
        WriteMode(writing, "ROTARY_FEED", "MM_MIN", transform?.RotaryFeedMmMin, "M116", transform?.RotaryFeedDegMin,
            "M117");
        WriteTolerance(writing);
    }

    /// <summary>
    /// Writes HOME as L X+0 Y+0 FMAX M91, the move in the machine frame to the reference coordinates of the named axes,
    /// home or home2 under POINT=2; Klartext has no reference point command, and an axis without them is an ERROR of
    /// this compiler (heidenhain 8 rule 5; machine-config 3 and 4; D100). As an L block it carries the radius
    /// compensation where it changed, L X+0 Y+0 R0 FMAX M91 (language 4.4; controllers heidenhain.md 2).
    /// </summary>
    public static void WriteHome(HeidenhainBlock writing)
    {
        writing.Take("HOME");
        int point = writing.Take("POINT")?.Value is IntegerValue number ? (int)number.Number : 1;
        var words = new List<string> { "L" };
        foreach (HeidenhainAxisWord axis in HeidenhainAxes.Of(writing.Block))
        {
            writing.MarkWritten(axis.Word);
            AxisDef? definition = writing.Machine.ResolveAxis(axis.Axis);
            decimal? reference = point switch
            {
                1 => definition?.Home,
                2 => definition?.Home2,
                _ => null,
            };
            if (reference is not decimal coordinate)
            {
                string key = point == 2 ? "home2" : "home";
                writing.Error(DiagnosticCodes.HeidenhainHomeWithoutReferencePoint,
                    $"HOME {axis.Axis}: the [[axis]] table of the machine \"{writing.Machine.Machine.Name}\" gives no "
                    + $"{(point is 1 or 2 ? key : "reference point " + point.ToString(CultureInfo.InvariantCulture))} "
                    + $"for {axis.Axis}, and Klartext has no reference point command; the compiler writes HOME as the "
                    + "M91 move to those coordinates (controllers heidenhain.md 8 rule 5, D100).");
                return;
            }

            string letter = HeidenhainAxes.LetterOf(writing, axis.Axis);
            words.Add(letter + HeidenhainNumbers.Signed(writing, letter, coordinate));
        }

        // COMP applies from the motion of its block on (language 4.4), and the L block of HOME is where Klartext
        // switches it (controllers heidenhain.md 2).
        if (HeidenhainMotion.CompensationWord(writing) is string compensation)
        {
            words.Add(compensation);
        }

        words.Add("FMAX");
        words.Add("M91");
        writing.Line(string.Join(" ", words));
    }

    /// <summary>
    /// Writes RETRACT through [retract]: bare as MAX, M140 MB MAX, with a value as BY, M140 MB50 (controllers
    /// heidenhain.md 2; controller-mapping 1, RETRACT; machine-config 5; D83). An empty template means a computed
    /// machine-frame move with the kinematics module, which is an ERROR without it.
    /// </summary>
    public static void WriteRetract(HeidenhainBlock writing)
    {
        Word retract = writing.Take("RETRACT") ?? throw new InvalidOperationException("The block has no RETRACT.");

        // M140 runs with the compensation of the program, which only an L block switches (language 4.4; controllers
        // heidenhain.md 2).
        HeidenhainMotion.CheckCompensation(writing, "The RETRACT, written as M140,");
        RetractTable? table = writing.Machine.Retract;
        var values = new TemplateValues();
        string? template = table?.Max;
        string what = "[retract] MAX (machine-config 5, D83)";
        if (retract.Value is not NoValue)
        {
            if (HeidenhainNumbers.NumberOf(retract.Value) is not decimal distance)
            {
                HeidenhainNumbers.ReportValue(writing, retract);
                return;
            }

            values.Set("distance", distance);
            template = table?.By;
            what = "[retract] BY (machine-config 5, D83)";
        }

        if (template?.Length == 0)
        {
            writing.Error(DiagnosticCodes.HeidenhainWordNeedsKinematics,
                $"{retract.ToCanonical()}: the {what} of the machine is empty, a machine-frame move that only the "
                + "kinematics module computes; without it RETRACT is an ERROR at compile time (language 4.3, D83).");
            return;
        }

        writing.Template(template, what, values);
    }

    // A mode written on change with the template of the machine, or the controller's own M function where the machine
    // names none.
    private static void WriteMode(HeidenhainBlock writing, string key, string on, string? onTemplate, string onCode,
        string? offTemplate, string offCode)
    {
        if (writing.Take(key) is not Word word)
        {
            return;
        }

        string state = word.Value.ToCanonical();
        if (!writing.Target.Changes(key, state))
        {
            return;
        }

        bool switchedOn = state == on;
        string? template = switchedOn ? onTemplate : offTemplate;
        if (template is null)
        {
            writing.Line(switchedOn ? onCode : offCode);
            return;
        }

        writing.Template(template, $"[transform] {key}={state} (machine-config 5)", new TemplateValues());
    }

    // TOLERANCE, TOLERANCE:ROTARY and TOLERANCE_MODE together, cycle 32 through [tolerance] ON with {tol}, {mode} and
    // {rotary}, and OFF for TOLERANCE=OFF, on change (controllers heidenhain.md 2; controller-mapping 1, TOLERANCE;
    // machine-config 5; D85).
    // TODO(question): D151: the ON template of machines/heidenhain-itnc530.toml needs {rotary}, which TOLERANCE:ROTARY
    // leaves out when the program sets none; the placeholder then has no value, and the template reports it.
    private static void WriteTolerance(HeidenhainBlock writing)
    {
        bool tolerance = writing.Take("TOLERANCE") is not null;
        tolerance |= writing.Take("TOLERANCE", "ROTARY") is not null;
        tolerance |= writing.Take("TOLERANCE_MODE") is not null;
        if (!tolerance)
        {
            return;
        }

        ToleranceState state = writing.After.Frame.Tolerance;
        ToleranceTable? table = writing.Machine.Tolerance;
        string mode = state.Mode == ToleranceMode.Rough ? "ROUGH" : "FINISH";
        string key = state.Value is decimal active
            ? string.Join(" ", active.ToString(CultureInfo.InvariantCulture),
                state.Rotary?.ToString(CultureInfo.InvariantCulture) ?? "", mode)
            : "OFF";
        if (!writing.Target.Changes("TOLERANCE", key))
        {
            return;
        }

        if (state.Value is not decimal value)
        {
            writing.Template(table?.Off, "[tolerance] OFF (machine-config 5, D85)", new TemplateValues());
            return;
        }

        var values = new TemplateValues();
        values.Set("tol", value);
        if (table is not null && table.Mode.TryGetValue(mode, out string? modeValue))
        {
            values.Set("mode", modeValue);
        }

        if (state.Rotary is decimal rotary)
        {
            values.Set("rotary", rotary);
        }

        writing.Template(table?.On, "[tolerance] ON (machine-config 5, D85)", values);
    }
}
