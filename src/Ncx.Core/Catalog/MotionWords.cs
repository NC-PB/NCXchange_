namespace Ncx.Core.Catalog;

/// <summary>
/// The motion words of language 4.3: the motion verbs, the axis words, the arc words, the tool vector and the surface
/// normal, feed and feed mode.
/// </summary>
internal static class MotionWords
{
    // The table of language 4.3.
    private const string TableSection = "4.3";

    /// <summary>
    /// The rows of the table of language 4.3, with POINT of the row of HOME and the axis words Y to C and IY to IC
    /// that the rows of X and IX name.
    /// </summary>
    public static IReadOnlyList<WordDefinition> Definitions { get; } =
    [
        MotionVerb("RAPID", ValueKinds.Bare, [],
            "Rapid positioning (G0, FMAX) to the target given by the axis words."),
        MotionVerb("LINE", ValueKinds.Bare, [],
            "Linear interpolation at the active feed (G1, L with F)."),
        MotionVerb("ARC", ValueKinds.Ident, ["CW", "CCW"],
            "Circular interpolation in the working plane to the target, with CENTER or R, or over a sweep ANGLE "
            + "around CENTER."),
        new WordDefinition
        {
            Key = "ANGLE",
            Group = WordKind.Motion,
            ValueKinds = ValueKinds.NumberOrExpr,
            IsAxisWord = true,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Angle,
            Section = TableSection,
            Description = "Sweep angle of an ARC in degrees, greater than 0, in the direction of its verb, instead of "
                + "the plane end point (D84).",
        },

        // RETRACT moves the tool axis only and carries no axis words (language 5 rule 2, D83).
        new WordDefinition
        {
            Key = "RETRACT",
            Group = WordKind.Motion,
            ValueKinds = ValueKinds.Bare | ValueKinds.NumberOrExpr,
            IsVerb = true,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Verb,
            Section = TableSection,
            Description = "Retract along the tool axis, bare to the axis limit, with a value by that distance (D83).",
        },

        // HOME carries bare axis names, a machine axis of the D93 form among them (language 5 rule 2, D93).
        MotionVerb("HOME", ValueKinds.Bare, [],
            "Reference point return for the axes named bare in the block, machine axes included (D93, D100)."),
        new WordDefinition
        {
            Key = "POINT",
            Group = WordKind.Motion,
            ValueKinds = ValueKinds.Integer,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Point,
            Section = TableSection,
            Description = "The reference point HOME moves to, POINT=2 for the second (G30 P2); only with HOME.",
        },

        AbsoluteAxis("X", CanonicalRanks.X),
        AbsoluteAxis("Y", CanonicalRanks.Y),
        AbsoluteAxis("Z", CanonicalRanks.Z),
        AbsoluteAxis("A", CanonicalRanks.A),
        AbsoluteAxis("B", CanonicalRanks.B),
        AbsoluteAxis("C", CanonicalRanks.C),
        IncrementalAxis("IX", CanonicalRanks.IncrementalX),
        IncrementalAxis("IY", CanonicalRanks.IncrementalY),
        IncrementalAxis("IZ", CanonicalRanks.IncrementalZ),
        IncrementalAxis("IA", CanonicalRanks.IncrementalA),
        IncrementalAxis("IB", CanonicalRanks.IncrementalB),
        IncrementalAxis("IC", CanonicalRanks.IncrementalC),

        // The address of CENTER is a plane axis, absolute or incremental, in the order of bucket 5 (language 4.3, D90).
        new WordDefinition
        {
            Key = "CENTER",
            Group = WordKind.Motion,
            ValueKinds = ValueKinds.NumberOrExpr,
            IsAxisWord = true,
            AddrKind = AddrKind.Axis,
            IsAddrRequired = true,
            AddrRanks = new Dictionary<string, int>
            {
                ["X"] = CanonicalRanks.CenterX,
                ["Y"] = CanonicalRanks.CenterY,
                ["Z"] = CanonicalRanks.CenterZ,
                ["C"] = CanonicalRanks.CenterC,
                ["IX"] = CanonicalRanks.CenterIncrementalX,
                ["IY"] = CanonicalRanks.CenterIncrementalY,
                ["IZ"] = CanonicalRanks.CenterIncrementalZ,
                ["IC"] = CanonicalRanks.CenterIncrementalC,
            },
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.CenterX,
            Section = TableSection,
            Description = "Arc center on a plane axis, CENTER:X absolute, CENTER:IX incremental from the start point "
                + "of the arc (the I J K of Fanuc and Siemens).",
        },

        VectorWord("TX", CanonicalRanks.Tx,
            "X part of the tool axis direction at the end of a LINE under TCPM=ON, a unit vector (D81)."),
        VectorWord("TY", CanonicalRanks.Ty,
            "Y part of the tool axis direction at the end of a LINE under TCPM=ON, a unit vector (D81)."),
        VectorWord("TZ", CanonicalRanks.Tz,
            "Z part of the tool axis direction at the end of a LINE under TCPM=ON, a unit vector (D81)."),
        VectorWord("NX", CanonicalRanks.Nx,
            "X part of the surface normal at the end of the LINE, only together with TX TY TZ (D81)."),
        VectorWord("NY", CanonicalRanks.Ny,
            "Y part of the surface normal at the end of the LINE, only together with TX TY TZ (D81)."),
        VectorWord("NZ", CanonicalRanks.Nz,
            "Z part of the surface normal at the end of the LINE, only together with TX TY TZ (D81)."),

        new WordDefinition
        {
            Key = "R",
            Group = WordKind.Motion,
            ValueKinds = ValueKinds.NumberOrExpr,
            IsAxisWord = true,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.R,
            Section = TableSection,
            Description = "Arc radius, not 0, positive for an arc of 180 degrees or less, negative for more; the key "
                + "belongs to arcs only (D96).",
        },
        new WordDefinition
        {
            Key = "F",
            Group = WordKind.Motion,
            ValueKinds = ValueKinds.NumberOrExpr,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.F,
            Section = TableSection,
            Description = "Feed in the active feed mode.",
        },
        new WordDefinition
        {
            Key = "FEED_MODE",
            Group = WordKind.Motion,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["PER_MIN", "PER_REV"],
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.FeedMode,
            Section = TableSection,
            Description = "Feed per minute (G94) or per spindle revolution (G95); default PER_MIN.",
        },
    ];

    // RAPID, LINE, ARC, HOME: a motion verb that carries axis words in its block (language 5 rules 1 and 2).
    private static WordDefinition MotionVerb(
        string key, ValueKinds valueKinds, IReadOnlyList<string> allowedIdents, string description)
    {
        return new WordDefinition
        {
            Key = key,
            Group = WordKind.Motion,
            ValueKinds = valueKinds,
            AllowedIdents = allowedIdents,
            IsVerb = true,
            TakesAxisWords = true,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Verb,
            Section = TableSection,
            Description = description,
        };
    }

    // X, Y, Z, A, B, C: the absolute target coordinate, a number or an expression, bare as an axis name of HOME
    // (language 4.3, 5 rule 2). A, B, C name the rotary axis of the current workpiece holder when it has one (4.10).
    private static WordDefinition AbsoluteAxis(string key, int rank)
    {
        return new WordDefinition
        {
            Key = key,
            Group = WordKind.Motion,
            ValueKinds = ValueKinds.NumberOrExpr | ValueKinds.Bare,
            IsAxisWord = true,
            Scope = Scope.Block,
            CanonicalRank = rank,
            Section = TableSection,
            Description = $"Absolute target coordinate of the axis {key}; bare as an axis name of HOME.",
        };
    }

    // IX, IY, IZ, IA, IB, IC: the incremental target, the current position plus the value (language 4.3, D44).
    private static WordDefinition IncrementalAxis(string key, int rank)
    {
        return new WordDefinition
        {
            Key = key,
            Group = WordKind.Motion,
            ValueKinds = ValueKinds.NumberOrExpr,
            IsAxisWord = true,
            Scope = Scope.Block,
            CanonicalRank = rank,
            Section = TableSection,
            Description = $"Incremental target of the axis {key.Substring(1)}, the current position plus the value.",
        };
    }

    // TX TY TZ, the tool axis direction, and NX NY NZ, the surface normal, three numbers of a unit vector each
    // (language 4.3, D81).
    private static WordDefinition VectorWord(string key, int rank, string description)
    {
        return new WordDefinition
        {
            Key = key,
            Group = WordKind.Motion,
            ValueKinds = ValueKinds.NumberOrExpr,
            IsAxisWord = true,
            Scope = Scope.Block,
            CanonicalRank = rank,
            Section = TableSection,
            Description = description,
        };
    }
}
