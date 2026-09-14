using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The G groups of the SINUMERIK 840D sl (controllers siemens.md 2): one command per group is active, and the reader
/// keeps the active one of the groups it consults in its source-side state. The groups with an NCX word write it where
/// the source changes them: the plane (6) as WORKPLANE, the compensation (7) as COMP, the inch and metric pairs (13) as
/// UNITS, G94 and G95 (15) as FEED_MODE; G90 and G91 (14) and the motion (1) are read into the words of the motion
/// block; the words NCX has no meaning for stay RAW modal words of the block, written back in place by the Siemens
/// compiler (controllers siemens.md 2, 11 rule 8; controller-mapping 2 and 9).
/// </summary>
internal static class SiemensGroups
{
    /// <summary>The group of the modal motion.</summary>
    public const int Motion = 1;

    /// <summary>The group of the non-modal motion, dwell, reference point, fixed point.</summary>
    public const int NonModalMotion = 2;

    /// <summary>The group of the programmable frames, the working area limits and the pole.</summary>
    public const int Frames = 3;

    /// <summary>The group of the plane.</summary>
    public const int Plane = 6;

    /// <summary>The group of the settable datum.</summary>
    public const int Datum = 8;

    /// <summary>The group of the feed type and the constant surface speed.</summary>
    public const int FeedType = 15;

    /// <summary>The group of the diameter programming.</summary>
    public const int Diameter = 29;

    // The groups whose commands NCX has no word for, kept as RAW modal words (controllers siemens.md 2;
    // controller-mapping 9): the block buffer, the exact stop and the continuous path, the corner and approach
    // behaviour, the acceleration,
    // the offset types, the feed forward, the orientation, the compressors, the path reference, the point-to-point
    // motion, the frame rotations of the tool and the workpiece, the dynamic mode.
    private static readonly int[] s_rawGroups =
    [
        4, 10, 11, 12, 16, 17, 18, 21, 22, 24, 25, 30, 45, 49, 50, 51, 52, 53, 59,
    ];

    // Words outside the table of siemens 2 that NCX has no word for either (controllers siemens.md 3, 6).
    private static readonly HashSet<string> s_rawWords = new(StringComparer.Ordinal)
    {
        "RTLION", "RTLIOF", "ORISON", "ORISOF", "ORIC", "ORID", "CPRECON", "CPRECOF", "SOFTA", "BRISKA", "DRIVEA",
        "CUTCONON", "CUTCONOF", "WALIMON", "WALIMOF", "CDON", "CDOF", "CDOF2", "SBLON", "SBLOF", "DISPLON", "DISPLOF",
    };

    private static readonly Dictionary<string, int> s_groups = Table();

    /// <summary>
    /// The group of every modal G code with its number, "G1" in group 1, for the source-side state (architecture 7).
    /// </summary>
    public static IReadOnlyDictionary<string, int> CodeGroups { get; } = GCodes();

    /// <summary>
    /// The group of a command, "G0" or "CIP"; null for a word of no group.
    /// </summary>
    /// <param name="command">The command in capitals.</param>
    public static int? GroupOf(string command)
    {
        return s_groups.TryGetValue(command, out int group) ? group : null;
    }

    /// <summary>
    /// The command of a word: the code of a letter with its number, G1 of G01, or a word alone, CIP; null for another
    /// word.
    /// </summary>
    /// <param name="word">A word of the block.</param>
    public static string? CommandOf(SourceWord word)
    {
        if (word.Address.Length == 1)
        {
            return SiemensBlock.CodeOf(word);
        }

        return word.Text.Length == 0 ? word.Address : null;
    }

    /// <summary>
    /// Reads the commands of the groups of a block: the active one of each group into the facts, the NCX words of the
    /// plane, the compensation, the units and the feed mode, and the RAW modal words.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        foreach (SourceWord word in block.Unread())
        {
            string? command = CommandOf(word);
            if (command is null)
            {
                continue;
            }

            if (s_rawWords.Contains(command))
            {
                block.KeepAsRawWord(word);
                continue;
            }

            if (GroupOf(command) is not int group)
            {
                continue;
            }

            if (Array.IndexOf(s_rawGroups, group) >= 0)
            {
                // ORIMKS makes a tool vector mean the machine axes (controller-mapping 2, tool vectors).
                block.Facts.MachineOrientation = command == "ORIMKS" || (command != "ORIWKS"
                    && block.Facts.MachineOrientation);
                block.Facts.Unknown.Remove(SiemensFacts.Orientation);
                block.KeepAsRawWord(word);
                continue;
            }

            ReadGroup(block, word, command, group);
        }
    }

    private static void ReadGroup(SiemensBlock block, SourceWord word, string command, int group)
    {
        switch (group)
        {
            case Motion:
                ReadMotionCode(block, word, command);
                break;
            case NonModalMotion when command is not ("G4" or "G74" or "G75"):
                block.MarkRead(word);
                block.Draft.KeepAsRaw($"{command} is an approach, retract or repositioning motion that NCX has no "
                    + "word for (controllers siemens.md 2, 3)");
                break;
            case Plane:
                ReadPlane(block, word, command);
                break;
            case 7:
                block.MarkRead(word);
                string comp = command switch
                {
                    "G41" => "LEFT",
                    "G42" => "RIGHT",
                    _ => "OFF",
                };
                AddModal(block, "COMP", comp);
                break;
            case 9:
                // G53, G153 and SUPA suppress the frames for the block: FRAME=MACHINE on its motion (controllers
                // siemens.md 3; controller-mapping 1, FRAME=MACHINE).
                block.MachineFrame = word;
                break;
            case 13:
                ReadUnits(block, word, command);
                break;
            case 14:
                block.MarkRead(word);
                block.Facts.Incremental = command == "G91";
                block.State.Incremental = block.Facts.Incremental;
                block.Facts.Unknown.Remove(SiemensFacts.Distance);
                break;
            case FeedType when command is "G93" or "G94" or "G95":
                ReadFeedType(block, word, command);
                break;
        }
    }

    // G0 to G3 are the verbs of the motion blocks; CIP and CT are arcs the reader converts; the splines, polynomials,
    // threads and involutes have no NCX motion (controllers siemens.md 3; controller-mapping 2).
    private static void ReadMotionCode(SiemensBlock block, SourceWord word, string command)
    {
        block.MarkRead(word);
        if (command is "G0" or "G1" or "G2" or "G3" or "CIP" or "CT")
        {
            block.Facts.MotionCode = command;
            block.Facts.Unknown.Remove(SiemensFacts.Motion);
            return;
        }

        block.Facts.MotionCode = command;
        block.Facts.Unknown.Remove(SiemensFacts.Motion);
        block.Draft.KeepAsRaw($"{command} is a motion NCX has no word for "
            + "(controllers siemens.md 3; controller-mapping "
            + "2, ANGLE and RAW)");
    }

    // G17 to G19 are WORKPLANE XY, ZX, YZ (controller-mapping 1, WORKPLANE).
    private static void ReadPlane(SiemensBlock block, SourceWord word, string command)
    {
        block.MarkRead(word);
        Workplane plane = command switch
        {
            "G18" => Workplane.ZX,
            "G19" => Workplane.YZ,
            _ => Workplane.XY,
        };
        block.Facts.WorkingPlane = plane;
        block.State.Workplane = plane;
        block.Facts.Unknown.Remove(SiemensFacts.Plane);
        AddModal(block, "WORKPLANE", plane.ToString());
    }

    // G70 and G700 are inch, G71 and G710 metric (controller-mapping 1, UNITS).
    // TODO(question): controller-mapping 1 has the reader report which pair the source used, G70/G71 for the geometry
    // only or G700/G710 for everything, so that the compiler keeps it, and no word or key carries it yet (the draft
    // decision of phase 5); both pairs are read as UNITS, and under G70 on a metric machine the feed stays metric on
    // the control while NCX reads it in inches.
    private static void ReadUnits(SiemensBlock block, SourceWord word, string command)
    {
        block.MarkRead(word);
        AddModal(block, "UNITS", command is "G70" or "G700" ? "INCH" : "MM");
    }

    // G94 is feed per minute, G95 per revolution of the master spindle (controller-mapping 2, FEED_MODE); G93, inverse
    // time, has no NCX feed mode and stays a RAW modal word, and so do the F words under it (SiemensMotion).
    private static void ReadFeedType(SiemensBlock block, SourceWord word, string command)
    {
        block.Facts.FeedType = command;
        block.Facts.Unknown.Remove(SiemensFacts.Feed);
        if (command == "G93")
        {
            block.KeepAsRawWord(word);
            return;
        }

        block.MarkRead(word);
        AddModal(block, "FEED_MODE", command == "G95" ? "PER_REV" : "PER_MIN");
    }

    /// <summary>
    /// Writes a modal state word where the value changes against the value the reader wrote last (language 2 rule 2,
    /// modal state).
    /// </summary>
    /// <param name="block">The block being read.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The identifier value.</param>
    public static void AddModal(SiemensBlock block, string key, string value)
    {
        if (block.Facts.TakeChange(key, value))
        {
            block.Draft.AddState(key, null, new IdentValue(value));
        }
    }

    private static Dictionary<string, int> Table()
    {
        var table = new Dictionary<string, int>(StringComparer.Ordinal);
        Add(table, 1, "G0 G1 G2 G3 CIP CT ASPLINE BSPLINE CSPLINE POLY G33 G331 G332 G34 G35 INVCW INVCCW");
        Add(table, 2, "G4 G63 G74 G75 REPOSL REPOSQ REPOSH REPOSA REPOSQA REPOSHA G147 G148 G247 G248 G347 G348 G340 "
            + "G341 G5 G7");
        Add(table, 3, "TRANS ROT SCALE MIRROR ATRANS AROT ASCALE AMIRROR ROTS AROTS CROTS G25 G26 G110 G111 G112 G58 "
            + "G59");
        Add(table, 4, "STARTFIFO STOPFIFO FIFOCTRL");
        Add(table, 6, "G17 G18 G19");
        Add(table, 7, "G40 G41 G42");
        Add(table, 8, "G500 G54 G55 G56 G57");
        for (int datum = 505; datum <= 599; datum++)
        {
            table["G" + datum.ToString(System.Globalization.CultureInfo.InvariantCulture)] = 8;
        }

        Add(table, 9, "G53 G153 SUPA");
        Add(table, 10, "G60 G64 G641 G642 G643 G644 G645");
        Add(table, 11, "G9");
        Add(table, 12, "G601 G602 G603");
        Add(table, 13, "G70 G71 G700 G710");
        Add(table, 14, "G90 G91");
        Add(table, 15, "G93 G94 G95 G96 G961 G962 G97 G971 G972 G973");
        Add(table, 16, "CFC CFTCP CFIN");
        Add(table, 17, "NORM KONT KONTC KONTT");
        Add(table, 18, "G450 G451");
        Add(table, 21, "BRISK SOFT DRIVE");
        Add(table, 22, "CUT2D CUT2DF CUT3DC CUT3DF CUT3DFF CUT3DCC CUT3DCCD CUT2DD CUT2DFD");
        Add(table, 24, "FFWOF FFWON");
        Add(table, 25, "ORIWKS ORIMKS");
        Add(table, 29, "DIAMOF DIAMON DIAM90 DIAMCYCOF");
        Add(table, 30, "COMPOF COMPON COMPCURV COMPCAD COMPSURF");
        Add(table, 45, "SPATH UPATH");
        Add(table, 49, "CP PTP PTPG0");
        Add(table, 50, "ORIEULER ORIRPY ORIVIRT1 ORIVIRT2 ORIAXPOS ORIRPY2");
        Add(table, 51, "ORIVECT ORIAXES ORIPATH ORIPLANE ORICONCW ORICONCCW ORICONIO ORICONTO ORICURVE");
        Add(table, 52, "PAROT PAROTOF");
        Add(table, 53, "TOROTOF TOROT TOROTZ TOROTY TOROTX TOFRAME TOFRAMEZ TOFRAMEY TOFRAMEX");
        Add(table, 59, "DYNNORM DYNPOS DYNROUGH DYNSEMIFIN DYNFINISH");
        return table;
    }

    private static void Add(Dictionary<string, int> table, int group, string commands)
    {
        foreach (string command in commands.Split(' '))
        {
            table[command] = group;
        }
    }

    private static Dictionary<string, int> GCodes()
    {
        var codes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, int> entry in s_groups)
        {
            if (entry.Key.Length > 1 && entry.Key[0] == 'G' && char.IsAsciiDigit(entry.Key[1])
                && entry.Value is not (NonModalMotion or 9 or 11))
            {
                codes[entry.Key] = entry.Value;
            }
        }

        return codes;
    }
}
