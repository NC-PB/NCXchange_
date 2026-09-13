using System.Globalization;
using Ncx.Core.Geometry;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// How an event writes its values as text: numbers as NCX writes them, the identifiers of the state as the words that
/// set them write them (CW, PER_MIN, MM), positions with the frame they are known in, ? for a value that is unknown or
/// none. The payload text of every event and the values of STATE_CHANGE come from here.
/// </summary>
internal static class EventText
{
    /// <summary>
    /// An unknown or missing value in the payload text of an event.
    /// </summary>
    public const string Unknown = "?";

    /// <summary>
    /// A number as NCX writes it: invariant culture, a point, no trailing zeros (language 3).
    /// </summary>
    public static string Number(decimal value)
    {
        // Numbers are written with the invariant culture, never the culture of the machine (code-guidelines 3.4).
        return value == 0m ? "0" : value.ToString("0.############################", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A number, or ? for none or unknown.
    /// </summary>
    public static string Number(decimal? value)
    {
        return value is decimal known ? Number(known) : Unknown;
    }

    /// <summary>
    /// A number of a state variable for STATE_CHANGE: empty when it is none or when the state names it UNKNOWN (virtual
    /// machine 1, 6).
    /// </summary>
    public static string Number(decimal? value, ChannelSnapshot snapshot, string stateKey)
    {
        return value is decimal known && !snapshot.Unknown.Contains(stateKey) ? Number(known) : "";
    }

    /// <summary>
    /// A value of STATE_CHANGE as the payload shows it: ? for the empty value.
    /// </summary>
    public static string Shown(string value)
    {
        return value.Length == 0 ? Unknown : value;
    }

    /// <summary>
    /// The position of an axis: the stored value, with the frame after it when that is not the workpiece frame, 0
    /// (MACHINE); empty when the axis is unknown in every frame (virtual machine 2.2, 3.4).
    /// </summary>
    public static string Position(AxisPosition position)
    {
        if (!position.Known)
        {
            return "";
        }

        return position.Frame == PositionFrame.Workpiece
            ? Number(position.Value)
            : Number(position.Value) + " (" + Frame(position.Frame) + ")";
    }

    /// <summary>
    /// The name of a position frame: WORKPIECE, MACHINE, POLAR, CYLINDER, UNKNOWN (virtual machine 3.4).
    /// </summary>
    public static string Frame(PositionFrame frame)
    {
        return frame switch
        {
            PositionFrame.Workpiece => "WORKPIECE",
            PositionFrame.Machine => "MACHINE",
            PositionFrame.Polar => "POLAR",
            PositionFrame.Cylinder => "CYLINDER",
            _ => "UNKNOWN",
        };
    }

    /// <summary>
    /// The axes a motion moved, each with its position before and after it: X ? -> 50.4, Y 2 -> 7; "in place" when
    /// none moved.
    /// </summary>
    public static string Moves(IReadOnlyDictionary<string, AxisPosition> from, IReadOnlyDictionary<string, AxisPosition> to)
    {
        var moves = new List<string>();
        foreach (KeyValuePair<string, AxisPosition> target in to)
        {
            AxisPosition start = from.TryGetValue(target.Key, out AxisPosition known) ? known : AxisPosition.Unknown;
            if (start != target.Value)
            {
                moves.Add(target.Key + " " + Shown(Position(start)) + " -> " + Shown(Position(target.Value)));
            }
        }

        return moves.Count == 0 ? "in place" : string.Join(", ", moves);
    }

    /// <summary>
    /// The known positions of a set of axes as axis words, X=10 Y=10 Z=5; "none" when no axis is known.
    /// </summary>
    public static string KnownPositions(IReadOnlyDictionary<string, AxisPosition> positions)
    {
        var words = new List<string>();
        foreach (KeyValuePair<string, AxisPosition> position in positions)
        {
            if (position.Value.Known)
            {
                words.Add(position.Key + "=" + Position(position.Value));
            }
        }

        return words.Count == 0 ? "none" : string.Join(" ", words);
    }

    /// <summary>
    /// Words as NCX writes them, separated by a space; "none" for no word.
    /// </summary>
    public static string Words(IReadOnlyList<Word> words)
    {
        var texts = new List<string>();
        foreach (Word word in words)
        {
            texts.Add(word.ToCanonical());
        }

        return texts.Count == 0 ? "none" : string.Join(" ", texts);
    }

    /// <summary>
    /// A text in quotes as NCX writes a string (language 3).
    /// </summary>
    public static string Quoted(string text)
    {
        return new StringValue(text).ToCanonical();
    }

    /// <summary>
    /// The name of a program in quotes, as its NAME is a string, and of a subprogram as written (language 4.1, 4.9).
    /// </summary>
    public static string SectionName(Section section)
    {
        string name = section.Name ?? "";
        return section.Kind == SectionKind.Program ? Quoted(name) : name;
    }

    /// <summary>
    /// The names of programs or subprograms, separated by a comma; "none" for none.
    /// </summary>
    public static string SectionNames(IReadOnlyList<Section> sections)
    {
        var names = new List<string>();
        foreach (Section section in sections)
        {
            names.Add(SectionName(section));
        }

        return names.Count == 0 ? "none" : string.Join(", ", names);
    }

    /// <summary>
    /// A vector of three components as the words write them, 0,0.5,0.866 (D81).
    /// </summary>
    public static string Vector(IReadOnlyList<decimal> components)
    {
        var texts = new List<string>();
        foreach (decimal component in components)
        {
            texts.Add(Number(component));
        }

        return string.Join(",", texts);
    }

    /// <summary>
    /// ON or OFF.
    /// </summary>
    public static string OnOff(bool on)
    {
        return on ? "ON" : "OFF";
    }

    /// <summary>
    /// The verb as the block writes it (language 5 rule 1).
    /// </summary>
    public static string Ident(Verb verb)
    {
        return verb switch
        {
            Verb.Rapid => "RAPID",
            Verb.Line => "LINE",
            Verb.Arc => "ARC",
            Verb.Retract => "RETRACT",
            Verb.Home => "HOME",
            Verb.CycleCall => "CYCLE_CALL",
            Verb.Shift => "SHIFT",
            Verb.Tilt => "TILT",
            Verb.TiltAxis => "TILT_AXIS",
            _ => "SETPOS",
        };
    }

    /// <summary>
    /// CW or CCW, the value of ARC (language 4.3).
    /// </summary>
    public static string Ident(ArcDirection direction)
    {
        return direction == ArcDirection.Counterclockwise ? "CCW" : "CW";
    }

    /// <summary>
    /// MM or INCH; empty while the units are UNKNOWN (language 4.1, virtual machine 2.1).
    /// </summary>
    public static string Ident(Units units)
    {
        return units switch
        {
            Units.Mm => "MM",
            Units.Inch => "INCH",
            _ => "",
        };
    }

    /// <summary>
    /// XY, ZX or YZ (language 4.2).
    /// </summary>
    public static string Ident(Workplane workplane)
    {
        return workplane switch
        {
            Workplane.ZX => "ZX",
            Workplane.YZ => "YZ",
            _ => "XY",
        };
    }

    /// <summary>
    /// PER_MIN or PER_REV (language 4.3).
    /// </summary>
    public static string Ident(FeedMode feedMode)
    {
        return feedMode == FeedMode.PerRev ? "PER_REV" : "PER_MIN";
    }

    /// <summary>
    /// OFF, LEFT or RIGHT (language 4.4).
    /// </summary>
    public static string Ident(Compensation compensation)
    {
        return compensation switch
        {
            Compensation.Left => "LEFT",
            Compensation.Right => "RIGHT",
            _ => "OFF",
        };
    }

    /// <summary>
    /// OFF, CW or CCW (language 4.5).
    /// </summary>
    public static string Ident(SpindleDirection direction)
    {
        return direction switch
        {
            SpindleDirection.Clockwise => "CW",
            SpindleDirection.Counterclockwise => "CCW",
            _ => "OFF",
        };
    }

    /// <summary>
    /// SPINDLE or AXIS (language 4.5).
    /// </summary>
    public static string Ident(SpindleMode mode)
    {
        return mode == SpindleMode.Axis ? "AXIS" : "SPINDLE";
    }

    /// <summary>
    /// SHORTEST or FULL (language 4.2, D86).
    /// </summary>
    public static string Ident(RotaryPath path)
    {
        return path == RotaryPath.Shortest ? "SHORTEST" : "FULL";
    }

    /// <summary>
    /// MM_MIN or DEG_MIN (language 4.2, D86).
    /// </summary>
    public static string Ident(RotaryFeed feed)
    {
        return feed == RotaryFeed.MmMin ? "MM_MIN" : "DEG_MIN";
    }

    /// <summary>
    /// FINISH or ROUGH (language 4.1, D85).
    /// </summary>
    public static string Ident(ToleranceMode mode)
    {
        return mode == ToleranceMode.Rough ? "ROUGH" : "FINISH";
    }

    /// <summary>
    /// TURN, MOVE or STAY (language 4.2, D82).
    /// </summary>
    public static string Ident(TiltMove move)
    {
        return move switch
        {
            TiltMove.Turn => "TURN",
            TiltMove.Move => "MOVE",
            _ => "STAY",
        };
    }

    /// <summary>
    /// TABLE or COORD (language 4.2, D82).
    /// </summary>
    public static string Ident(TiltRot rot)
    {
        return rot == TiltRot.Coord ? "COORD" : "TABLE";
    }
}
