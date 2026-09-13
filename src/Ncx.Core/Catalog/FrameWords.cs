namespace Ncx.Core.Catalog;

/// <summary>
/// The frame words of language 4.2: working plane, datum, the transforms of the frame chain with their reset forms,
/// the tilted plane and its options, SETPOS, and the kinematic transformations CYLINDER, POLAR and TCPM.
/// </summary>
internal static class FrameWords
{
    /// <summary>
    /// The rows of the table of language 4.2. DIAMETER is listed among the lathe words of 4.11 as well and has its one
    /// entry here (D90).
    /// </summary>
    public static IReadOnlyList<WordDefinition> Definitions { get; } =
    [
        new WordDefinition
        {
            Key = "WORKPLANE",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["XY", "ZX", "YZ"],
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Workplane,
            Section = "4.2",
            Description = "Working plane with the tool axis perpendicular to it (G17, G18, G19).",
        },
        new WordDefinition
        {
            Key = "ORIGIN",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.Integer,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Origin,
            Section = "4.2",
            Description = "Workpiece datum number, G54 to G59 as 1 to 6; starts an empty frame chain (D31).",
        },
        new WordDefinition
        {
            Key = "FRAME",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["MACHINE"],
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Frame,
            Section = "4.2",
            Description = "The coordinates of this block refer to the machine datum (G53, M91).",
        },
        new WordDefinition
        {
            Key = "DIAMETER",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["ON", "OFF"],
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Diameter,
            Section = "4.2",
            Description = "Lathe diameter programming, the X words are diameters; default OFF. Listed among the lathe "
                + "words of 4.11 as well (D28, D60, D90).",
        },

        // SHIFT with axis words is the verb; SHIFT=RESET is a frame word (language 5 rules 1 and 6, D90).
        new WordDefinition
        {
            Key = "SHIFT",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.Bare | ValueKinds.Ident,
            AllowedIdents = [WordCatalog.Reset],
            IsVerb = true,
            TakesAxisWords = true,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Verb,
            ResetRank = CanonicalRanks.ShiftReset,
            Section = "4.2",
            Description = "Datum shift in the active frame, appended to the frame chain; SHIFT=RESET removes the "
                + "shifts and what follows them.",
        },
        new WordDefinition
        {
            Key = "ROTATE",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.NumberOrExpr | ValueKinds.Ident,
            AllowedIdents = [WordCatalog.Reset],
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Rotate,
            Section = "4.2",
            Description = "Rotation of the working plane about the tool axis in degrees, appended to the chain; "
                + "ROTATE=RESET removes it and what follows.",
        },
        new WordDefinition
        {
            Key = "MIRROR",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.List | ValueKinds.Ident,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Mirror,
            Section = "4.2",
            Description = "Mirrors the named axes, an axis list or a single axis, appended to the chain; MIRROR=OFF.",
        },
        new WordDefinition
        {
            Key = "TILT",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.Bare | ValueKinds.Ident,
            AllowedIdents = [WordCatalog.Reset],
            IsVerb = true,
            TakesAxisWords = true,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Verb,
            ResetRank = CanonicalRanks.TiltReset,
            Section = "4.2",
            Description = "Tilted working plane by the spatial angles A, B, C, appended to the chain; TILT=RESET "
                + "removes it and what follows.",
        },
        new WordDefinition
        {
            Key = "TILT_AXIS",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.Bare | ValueKinds.Ident,
            AllowedIdents = [WordCatalog.Reset],
            IsVerb = true,
            TakesAxisWords = true,
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Verb,
            ResetRank = CanonicalRanks.TiltAxisReset,
            Section = "4.2",
            Description = "Tilted working plane by the rotary axis positions of this machine, appended to the chain; "
                + "TILT_AXIS=RESET removes it and what follows (D82).",
        },
        new WordDefinition
        {
            Key = "MOVE",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["TURN", "MOVE", "STAY"],
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Move,
            Section = "4.2",
            Description = "How the machine reaches the tilted plane, with TILT or TILT_AXIS; default STAY (D82).",
        },
        new WordDefinition
        {
            Key = "ROT",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["TABLE", "COORD"],
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Rot,
            Section = "4.2",
            Description = "On table kinematics, whether the table turns or only the coordinate system, with TILT or "
                + "TILT_AXIS; default TABLE (D82).",
        },
        new WordDefinition
        {
            Key = "SETPOS",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.Bare,
            IsVerb = true,
            TakesAxisWords = true,
            Scope = Scope.Block,
            CanonicalRank = CanonicalRanks.Verb,
            Section = "4.2",
            Description = "Declares that the current position has these coordinates in the active workpiece frame; "
                + "nothing moves (D55, D101).",
        },

        // The value of CYLINDER is the reference radius; there is no ON form (D96).
        new WordDefinition
        {
            Key = "CYLINDER",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.NumberOrExpr | ValueKinds.Ident,
            AllowedIdents = ["OFF"],
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Cylinder,
            Section = "4.2",
            Description = "Cylinder surface transformation, on with the reference radius as the value, off with OFF; "
                + "there is no ON form (D54, D96, D102).",
        },
        new WordDefinition
        {
            Key = "POLAR",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["ON", "OFF"],
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Polar,
            Section = "4.2",
            Description = "Face transformation, Cartesian programming of the face with X and C (G12.1, TRANSMIT; "
                + "D54, D102).",
        },
        new WordDefinition
        {
            Key = "TCPM",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["ON", "OFF"],
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.Tcpm,
            Section = "4.2",
            Description = "Tool center point control for 5-axis simultaneous motion (G43.4, TRAORI, M128; D54).",
        },
        new WordDefinition
        {
            Key = "ROTARY_PATH",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["SHORTEST", "FULL"],
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.RotaryPath,
            Section = "4.2",
            Description = "Rotary axes take the shortest way (M126) or turn exactly as programmed (M127); default "
                + "FULL (D86).",
        },
        new WordDefinition
        {
            Key = "ROTARY_FEED",
            Group = WordKind.Frame,
            ValueKinds = ValueKinds.Ident,
            AllowedIdents = ["MM_MIN", "DEG_MIN"],
            Scope = Scope.Modal,
            CanonicalRank = CanonicalRanks.RotaryFeed,
            Section = "4.2",
            Description = "Feed of rotary axes in length per minute at the tool tip (M116) or in degrees per minute "
                + "(M117); default DEG_MIN (D86).",
        },
    ];
}
