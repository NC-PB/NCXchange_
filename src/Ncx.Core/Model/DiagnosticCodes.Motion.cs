namespace Ncx.Core.Model;

// The codes of motion, VM200-VM399 (the ranges in DiagnosticCodes.cs, D98): linear motion and the vector form (virtual
// machine 3.1, D81), arcs (3.2, D36, D84, D102), RETRACT (3.1a, D83) and the cycle call (3.3, D37, D94). HOME keeps its
// codes of block execution, VM060 and VM061. P1-04 builds the full table of virtual machine 5 and
// docs/spec/generated/diagnostics.md from these and its own.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// VM200: a motion before UNITS; UNITS is required before the first motion (language 4.1, virtual machine 3.1, 5,
    /// D11). Suppressed inside a subprogram that no program of the file calls (3.9, D99).
    /// </summary>
    public const string MotionBeforeUnits = "VM200";

    /// <summary>
    /// VM201: LINE while feed.value is none (virtual machine 3.1, 5). Suppressed inside a subprogram that no program of
    /// the file calls (3.9, D99).
    /// </summary>
    public const string LineWithoutFeed = "VM201";

    /// <summary>
    /// VM202: an incremental word, IX=5, from an axis whose current value is unknown (virtual machine 3.1, 5). Inside a
    /// subprogram that no program of the file calls the position becomes unknown instead (3.9, D99).
    /// </summary>
    public const string IncrementalFromUnknownPosition = "VM202";

    /// <summary>
    /// VM203: two forms of one axis in one block, X=10 IX=5 or CENTER:X and CENTER:IX; absolute and incremental words
    /// may be mixed in one block, one form per axis (language 4.3).
    /// </summary>
    public const string TwoFormsOfOneAxis = "VM203";

    /// <summary>
    /// VM210: the vector words TX TY TZ or NX NY NZ without TCPM=ON (language 4.3, virtual machine 3.1, 5, D81).
    /// </summary>
    public const string VectorWithoutTcpm = "VM210";

    /// <summary>
    /// VM211: vector words and rotary axis words A, B, C in one block (language 4.3, virtual machine 3.1, 5, D81).
    /// </summary>
    public const string VectorWithRotaryWords = "VM211";

    /// <summary>
    /// VM212: an incomplete vector: TX TY TZ come all three together, NX NY NZ all three and only together with
    /// TX TY TZ (language 4.3, virtual machine 5, D81).
    /// </summary>
    public const string VectorIncomplete = "VM212";

    /// <summary>
    /// VM213: a tool vector or surface normal whose length is not 1 within the arc tolerance (virtual machine 3.1, 5,
    /// D36, D81).
    /// </summary>
    public const string VectorNotUnitLength = "VM213";

    /// <summary>
    /// VM220: ARC without CENTER, R or ANGLE (language 4.3, virtual machine 3.2, 5).
    /// </summary>
    public const string ArcWithoutCenterRadiusOrAngle = "VM220";

    /// <summary>
    /// VM221: CENTER on one plane axis only; the CENTER form needs both plane axes (virtual machine 3.2).
    /// </summary>
    public const string ArcCenterWithoutBothPlaneAxes = "VM221";

    /// <summary>
    /// VM222: CENTER on an axis that is not an axis of the working plane; the arc runs only in the working plane
    /// (virtual machine 3.2).
    /// </summary>
    public const string ArcCenterOutsideThePlane = "VM222";

    /// <summary>
    /// VM223: ARC with both CENTER and R and no ANGLE; the arc takes CENTER or R (language 4.3).
    /// </summary>
    public const string ArcCenterWithRadius = "VM223";

    /// <summary>
    /// VM224: ANGLE with R (language 4.3, virtual machine 3.2, 5, D84).
    /// </summary>
    public const string ArcAngleWithRadius = "VM224";

    /// <summary>
    /// VM225: ANGLE with a plane end-point word (language 4.3, virtual machine 3.2, 5, D84).
    /// </summary>
    public const string ArcAngleWithPlaneEndPoint = "VM225";

    /// <summary>
    /// VM226: ANGLE without CENTER; the ANGLE form turns around CENTER (language 4.3, virtual machine 3.2, D84).
    /// </summary>
    public const string ArcAngleWithoutCenter = "VM226";

    /// <summary>
    /// VM227: ARC from a start that is not known in the working plane; start = current position, which must be known
    /// in the plane (virtual machine 3.2).
    /// </summary>
    public const string ArcStartUnknownInThePlane = "VM227";

    /// <summary>
    /// VM230: CENTER form, |start - center| and |end - center| differ by more than the arc tolerance: inconsistent
    /// center (virtual machine 3.2, 5, D36).
    /// </summary>
    public const string ArcInconsistentCenter = "VM230";

    /// <summary>
    /// VM231: R form, d > 2|R| plus the arc tolerance: radius too small (virtual machine 3.2, 5, D36).
    /// </summary>
    public const string ArcRadiusTooSmall = "VM231";

    /// <summary>
    /// VM232: R form with start = end: full circle with R; full circles need CENTER (language 4.3, virtual machine
    /// 3.2, 5).
    /// </summary>
    public const string ArcFullCircleWithRadius = "VM232";

    /// <summary>
    /// VM233: R=0; R is a number, not 0 (language 4.3).
    /// </summary>
    public const string ArcRadiusZero = "VM233";

    /// <summary>
    /// VM234: ANGLE not greater than 0 (language 4.3, virtual machine 5, D84).
    /// </summary>
    public const string ArcAngleNotGreaterThanZero = "VM234";

    /// <summary>
    /// VM240: RETRACT with a feed (virtual machine 3.1a, 5, D83); other axis words under RETRACT are the parser's ERROR
    /// (language 5 rule 2).
    /// </summary>
    public const string RetractWithFeed = "VM240";

    /// <summary>
    /// VM250: CYCLE_CALL without a cycle, cycle.name OFF (virtual machine 3.3, 5). Suppressed inside a subprogram that
    /// no program of the file calls (3.9, D99).
    /// </summary>
    public const string CycleCallWithoutCycle = "VM250";

    /// <summary>
    /// VM251: CYCLE_CALL of a built-in drilling cycle without DEPTH or CLEARANCE (virtual machine 3.3, 5). Suppressed
    /// inside a subprogram that no program of the file calls (3.9, D99).
    /// </summary>
    public const string CycleCallWithoutDepthOrClearance = "VM251";

    /// <summary>
    /// VM252: AXIS naming an axis that is not a linear axis of the machine, found at the CYCLE_CALL of a built-in
    /// drilling cycle (language 4.7, virtual machine 3.3, 5, D59).
    /// </summary>
    public const string CycleAxisNotLinear = "VM252";
}
