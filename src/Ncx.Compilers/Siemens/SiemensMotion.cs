using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Siemens;

/// <summary>
/// The motion of a SINUMERIK block (controllers siemens.md 2, 3, 12 rule 2; controller-mapping 2; language 4.3): G0 to
/// G3 on every motion block, the axis words with the equals sign where the control needs it, the arcs of SiemensArcs, F
/// where the control's feed differs, G94 and G95, G40 to G42, G17 to G19, G70 and G71, the tool vector as A3= B3= C3=
/// and the normal as A5= B5= C5= under TRAORI, G53 for FRAME=MACHINE, G4 F for DWELL.
/// </summary>
internal static class SiemensMotion
{
    // The target keys of the G groups this compiler writes (controllers siemens.md 2).
    private const string MotionGroup = "G0";
    private const string PlaneGroup = "G17";
    private const string CompensationGroup = "G40";
    private const string DistanceGroup = "G90";
    private const string FeedTypeGroup = "G94";
    private const string UnitsGroup = "G70";
    private const string Feed = "F";

    // The verbs whose axis words the motion writes (language 4.3).
    private static readonly string[] s_motions = ["RAPID", "LINE", "ARC"];

    // The tool vector and the surface normal at the end of the block and their SINUMERIK addresses under TRAORI
    // (controllers siemens.md 6; controller-mapping 2, tool vectors; D81).
    private static readonly string[][] s_vectors =
    [
        ["TX", "A3"], ["TY", "B3"], ["TZ", "C3"], ["NX", "A5"], ["NY", "B5"], ["NZ", "C5"],
    ];

    /// <summary>
    /// The modal words of the block as their codes where the control has another active: UNITS as G71 or G70,
    /// FEED_MODE as G94 or G95, WORKPLANE as G17 to G19, COMP as G40 to G42; G90 where the control's group 14 is not
    /// known (controllers siemens.md 2, 12; controller-mapping 1, 2).
    /// </summary>
    // TODO(question): siemens 2 names G70 and G71 (geometry only) and G700 and G710 (also feeds and offsets), and no
    // document says which pair the compiler writes for UNITS (phase 5, decisions needed first; the P5-01 question of
    // the pair); G71 and G70 are written, until that is answered.
    public static void WriteModalWords(SiemensBlock write)
    {
        Block block = write.Block;
        ChannelSnapshot after = write.After;
        if (block.Has("UNITS"))
        {
            write.Written("UNITS");
            string? units = after.Frame.Units switch
            {
                Units.Mm => "G71",
                Units.Inch => "G70",
                _ => null,
            };
            if (units is not null)
            {
                Change(write, UnitsGroup, SiemensLine.UnitsRank, units);
            }

            WriteDistance(write);
        }

        if (block.Has("FEED_MODE"))
        {
            write.Written("FEED_MODE");
            WriteFeedType(write);
        }

        if (block.Has("WORKPLANE"))
        {
            write.Written("WORKPLANE");
            WritePlane(write);
        }

        // G41 and G42 apply from the motion of the same block, G40 ends the compensation (controllers siemens.md 3).
        if (block.Has("COMP"))
        {
            write.Written("COMP");
            Change(write, CompensationGroup, SiemensLine.CompensationRank, CompensationCode(write));
        }
    }

    /// <summary>
    /// A modal code of a group where the control has another active, a modal word written only on change (phase 3,
    /// P3-03).
    /// </summary>
    public static void Change(SiemensBlock write, string group, int rank, string code)
    {
        if (write.Target.Changes(group, code))
        {
            write.Main.Code(rank, code);
        }
    }

    /// <summary>
    /// G17, G18 or G19 where the control has another plane active (language 4.2, WORKPLANE; controllers siemens.md 2).
    /// </summary>
    public static void WritePlane(SiemensBlock write)
    {
        string plane = write.After.Frame.Workplane switch
        {
            Workplane.ZX => "G18",
            Workplane.YZ => "G19",
            _ => "G17",
        };
        Change(write, PlaneGroup, SiemensLine.PlaneRank, plane);
    }

    /// <summary>
    /// G94 or G95 where the control has another feed type active (language 4.3, FEED_MODE; controllers siemens.md 2);
    /// under the constant surface speed G96 or G961, since G94 and G95 would switch it off.
    /// </summary>
    public static void WriteFeedType(SiemensBlock write)
    {
        // G93 to G97 and G961 are one group, feed type and constant surface speed, one of them active (controllers
        // siemens.md 2, group 15): under the constant surface speed of the virtual machine the feed type is G96 per
        // revolution or G961 per minute (siemens 5; controller-mapping 4, CSS; the TODO(question) of
        // SiemensSpindles.AddToMain), where the control has it in that group or its group is not known, in a
        // subprogram or after RAW (D99).
        string? active = write.ActiveOf(FeedTypeGroup);
        bool surfaceSpeed = SurfaceSpeedOn(write.After) && (active is null || IsSurfaceSpeed(active));
        string code = surfaceSpeed ? SurfaceSpeedCode(write)
            : write.After.Motion.FeedMode == FeedMode.PerRev ? "G95" : "G94";
        Change(write, FeedTypeGroup, SiemensLine.FeedTypeRank, code);
    }

    /// <summary>
    /// The code of the constant surface speed for the feed type the block leaves: G96, which switches G95 on, or G961
    /// with the feed per minute (controllers siemens.md 5; controller-mapping 4, CSS).
    /// </summary>
    public static string SurfaceSpeedCode(SiemensBlock write)
    {
        return write.After.Motion.FeedMode == FeedMode.PerRev ? "G96" : "G961";
    }

    /// <summary>
    /// True for a code of group 15, feed type and constant surface speed (controllers siemens.md 2).
    /// </summary>
    public static bool IsFeedTypeCode(string code)
    {
        return code is "G93" or "G94" or "G95" or "G96" or "G961" or "G962" or "G97" or "G971" or "G972" or "G973";
    }

    /// <summary>
    /// Records the code of group 15 that a template wrote (controllers siemens.md 2, 5): G93 to G96, G961 and G962 as
    /// they are, G962 with either feed type; G97, G972 and G973 switch the surface speed off and leave the feed type of
    /// the code before, G971 the feed per minute.
    /// </summary>
    // TODO(question): the documents give the feed type that G96 and G961 set, not the one G97, G971 and G972 leave (the
    // question of the Siemens reader, P5-01); the compiler reads them as the reader does, G971 per minute and G97 and
    // G972 the feed type before, and G973 leaves it unknown, until that is answered.
    public static void FeedTypeSwitched(SiemensBlock write, string code)
    {
        string? before = write.ActiveOf(FeedTypeGroup);
        string? after = code switch
        {
            "G97" or "G972" => before switch
            {
                "G96" or "G95" => "G95",
                "G961" or "G94" => "G94",
                _ => null,
            },
            "G971" => "G94",
            "G973" => null,
            _ => code,
        };
        if (after is null)
        {
            write.MakeUnknown(FeedTypeGroup);
            return;
        }

        write.Target.Set(FeedTypeGroup, after);
    }

    // A code of group 15 under which the constant surface speed is on (controllers siemens.md 5).
    private static bool IsSurfaceSpeed(string code)
    {
        return code is "G96" or "G961" or "G962";
    }

    // The constant surface speed of a spindle is on in the virtual machine (language 4.11, CSS).
    private static bool SurfaceSpeedOn(ChannelSnapshot state)
    {
        foreach (SpindleSnapshot spindle in state.Spindles.Values)
        {
            if (spindle.Css)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Writes RAPID, LINE and ARC: the motion code, G53 for FRAME=MACHINE, the axis words, the arc words, the tool
    /// vector, the feed (language 4.3; controllers siemens.md 3, 12 rule 2).
    /// </summary>
    public static void Write(SiemensBlock write)
    {
        Block block = write.Block;
        string? verb = block.Verb?.Key;
        if (verb is null || !s_motions.Contains(verb))
        {
            if (block.Has("F"))
            {
                WriteFeed(write);
            }

            return;
        }

        // Every motion block writes its motion code, G0 Z2=-58 as the comments of MILLTURN_TRANSFER write it (phase 5,
        // P5-02 acceptance; language 2 rule 2: every block that moves says how it moves).
        // TODO(question): P3-03 writes a modal word only on change, and the acceptance of P5-02 wants G0 Z2=-58 after a
        // G0; no document says whether a SINUMERIK program repeats G0 to G3 on every motion block. The motion code is
        // written on every motion block, until that is answered.
        write.Written(verb);
        string code = MotionCode(write, verb);
        write.Main.Code(SiemensLine.MotionRank, code);
        write.Target.Set(MotionGroup, code);
        if (block.Has("FRAME", null, "MACHINE"))
        {
            // G53 suppresses the programmable and settable frames in its block (controllers siemens.md 3;
            // controller-mapping 1, FRAME=MACHINE).
            write.Written("FRAME");
            write.Main.Code(SiemensLine.SuppressionRank, "G53");
        }

        List<SiemensAxisWord> axes = SiemensAxes.Of(block);
        if (HasAbsoluteWord(axes))
        {
            WriteDistance(write);
        }

        if (verb == "ARC")
        {
            WritePlane(write);
            SiemensArcs.Write(write, axes);
        }
        else
        {
            WriteAxes(write, axes);
        }

        WriteVector(write);
        if (verb != "RAPID" || block.Has("F"))
        {
            if (verb != "RAPID")
            {
                WriteFeedType(write);
            }

            WriteFeed(write);
        }
    }

    /// <summary>
    /// The axis words of a block in the main line.
    /// </summary>
    public static void WriteAxes(SiemensBlock write, List<SiemensAxisWord> axes)
    {
        foreach (SiemensAxisWord axis in axes)
        {
            write.Written(axis.Word);
            if (SiemensAxes.Word(write, axis) is string word)
            {
                write.Main.Word(SiemensLine.AxisRank, word);
            }
        }
    }

    /// <summary>
    /// G90 where the control's group 14 is not known: the compiler writes every incremental word with IC() and never
    /// G91, so the control stays in G90 once it has it (controllers siemens.md 3; controller-mapping 2, IX).
    /// </summary>
    public static void WriteDistance(SiemensBlock write)
    {
        if (write.ActiveOf(DistanceGroup) is null)
        {
            Change(write, DistanceGroup, SiemensLine.DistanceRank, "G90");
        }
    }

    /// <summary>
    /// Makes the control's modal state unknown after a block the compiler does not write itself, RAW, which may change
    /// any of it (language 4.1, RAW).
    /// </summary>
    public static void ForgetAfterRaw(SiemensBlock write)
    {
        string[] groups = [MotionGroup, PlaneGroup, CompensationGroup, DistanceGroup, FeedTypeGroup, UnitsGroup, Feed];
        foreach (string group in groups)
        {
            write.MakeUnknown(group);
        }
    }

    /// <summary>
    /// F where the control has another feed active: the F word of the block, or on a feed motion the feed of the
    /// virtual machine where a cycle or a subprogram left the control another (language 4.3, F; controller-mapping 5,
    /// CYCLE_F).
    /// </summary>
    public static void WriteFeed(SiemensBlock write)
    {
        string? feed = null;
        bool expression = false;
        if (write.Block.Find("F") is Word word)
        {
            write.Written(word);
            feed = SiemensExpressions.ValueText(write, word.Value, "F");
            expression = word.Value is ExprValue;
        }
        else if (write.After.Motion.Feed is decimal known)
        {
            feed = write.Format("F", known);
        }

        if (feed is not null && write.Target.Changes(Feed, feed))
        {
            write.Main.Word(SiemensLine.FeedRank, SiemensAxes.Address("F", feed, "", expression));
        }
    }

    /// <summary>
    /// Records the feed the control has active after a word the compiler wrote elsewhere, the modal F of a cycle
    /// (controller-mapping 5, CYCLE_F).
    /// </summary>
    public static void FeedWritten(SiemensBlock write, string feed)
    {
        write.Target.Set(Feed, feed);
    }

    /// <summary>
    /// DWELL=1.5 is G4 F1.5 in a block of its own; the F of a dwell does not touch the modal feed (controllers
    /// siemens.md 3; controller-mapping 1, DWELL).
    /// </summary>
    public static void WriteDwell(SiemensBlock write)
    {
        if (write.Block.Find("DWELL") is not Word dwell)
        {
            return;
        }

        write.Written(dwell);
        if (SiemensExpressions.ValueText(write, dwell.Value, "DWELL") is string seconds)
        {
            write.Write("G4 " + SiemensAxes.Address("F", seconds, "", dwell.Value is ExprValue));
        }
    }

    // G0 rapid, G1 at the feed, G2 and G3 arcs (controllers siemens.md 3); on a side whose datum runs Z the other way
    // an arc in a plane that holds Z turns the other way.
    private static string MotionCode(SiemensBlock write, string verb)
    {
        if (verb == "RAPID")
        {
            return "G0";
        }

        if (verb == "LINE")
        {
            return "G1";
        }

        bool clockwise = write.Block.Has("ARC", null, "CW");
        return clockwise != SiemensArcs.Mirrored(write) ? "G2" : "G3";
    }

    // G41 left of the contour, G42 right, G40 off (controllers siemens.md 3); mirrored with the arcs.
    private static string CompensationCode(SiemensBlock write)
    {
        return write.After.Motion.Comp switch
        {
            Compensation.Left => SiemensArcs.Mirrored(write) ? "G42" : "G41",
            Compensation.Right => SiemensArcs.Mirrored(write) ? "G41" : "G42",
            _ => "G40",
        };
    }

    // TX TY TZ as A3= B3= C3=, NX NY NZ as A5= B5= C5= (controllers siemens.md 6; D81).
    private static void WriteVector(SiemensBlock write)
    {
        foreach (string[] vector in s_vectors)
        {
            if (write.Block.Find(vector[0]) is not Word word)
            {
                continue;
            }

            write.Written(word);
            string axis = vector[0].Substring(1);
            decimal factor = axis == "Z" && SiemensAxes.IsZNegated(write) ? -1m : 1m;
            string? value = word.Value switch
            {
                IntegerValue integer => write.FormatComputed(vector[1], integer.Number * factor),
                DecimalValue number => factor == 1m
                    ? write.Format(vector[1], number.Number)
                    : write.FormatComputed(vector[1], number.Number * factor),
                ExprValue expression when SiemensExpressions.Text(write, expression) is string text =>
                    factor == 1m ? text : "-(" + text + ")",
                _ => null,
            };
            if (value is not null)
            {
                write.Main.Word(SiemensLine.VectorRank, vector[1] + "=" + value);
            }
        }
    }

    private static bool HasAbsoluteWord(List<SiemensAxisWord> axes)
    {
        foreach (SiemensAxisWord axis in axes)
        {
            if (!axis.Incremental)
            {
                return true;
            }
        }

        return false;
    }
}
