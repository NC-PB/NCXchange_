using System.Globalization;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Readers.Siemens;

/// <summary>
/// What the reader knows of the modal state of the control where it reads, beyond the positions of the source-side
/// state (architecture 7): the active code of the groups it consults, the master spindle and the spindles, the modal
/// cycle of MCALL, the transformation, the chain of transforms, the feed of the control and the feed written last, the
/// pole, the modal rounding, the preloaded tool, the words written last. A fact is known or unknown; in a subprogram
/// the facts its callers agree on are known (virtual machine 3.9), and a block that depends on an unknown fact stays
/// RAW (D5).
/// </summary>
internal sealed class SiemensFacts
{
    /// <summary>The code of group 1, G0 to G3, CIP, CT (controllers siemens.md 2).</summary>
    public const string Motion = "motion";

    /// <summary>G90 or G91 (group 14).</summary>
    public const string Distance = "distance";

    /// <summary>G17 to G19 (group 6).</summary>
    public const string Plane = "plane";

    /// <summary>The feed type of group 15, G93 to G97.</summary>
    public const string Feed = "feed";

    /// <summary>DIAMON, DIAMOF, DIAM90 (group 29).</summary>
    public const string Diameter = "diameter";

    /// <summary>The master spindle of SETMS and the state of the spindles.</summary>
    public const string Spindles = "spindles";

    /// <summary>The modal cycle of MCALL.</summary>
    public const string Cycle = "cycle";

    /// <summary>TRAORI, TRACYL, TRANSMIT.</summary>
    public const string Transform = "transform";

    /// <summary>The chain of transforms.</summary>
    public const string Frames = "frames";

    /// <summary>The pole of G110 to G112.</summary>
    public const string Pole = "pole";

    /// <summary>The modal rounding of RNDM.</summary>
    public const string Rounding = "rounding";

    /// <summary>The preloaded tool.</summary>
    public const string Preload = "preload";

    /// <summary>ORIWKS or ORIMKS.</summary>
    public const string Orientation = "orientation";

    private static readonly string[] s_facts =
    [
        Motion, Distance, Plane, Feed, Diameter, Spindles, Cycle, Transform, Frames, Pole, Rounding, Preload,
        Orientation,
    ];

    // The facts the reader writes as modal NCX words of their own, WORKPLANE, FEED_MODE, DIAMETER, CYCLE, TCPM and the
    // chain; the others it writes into every block that needs them.
    private static readonly string[] s_writtenFacts = [Plane, Feed, Diameter, Cycle, Transform, Frames];

    /// <summary>
    /// The facts the reader does not know.
    /// </summary>
    public HashSet<string> Unknown { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The code of group 1 as the source wrote it, G0, G1, G2, G3, CIP, CT; null before the first.
    /// </summary>
    public string? MotionCode { get; set; }

    /// <summary>
    /// True under G91.
    /// </summary>
    public bool Incremental { get; set; }

    /// <summary>
    /// The working plane of G17 to G19.
    /// </summary>
    public Workplane WorkingPlane { get; set; } = Workplane.XY;

    /// <summary>
    /// The code of group 15, G94 feed per minute by default.
    /// </summary>
    public string FeedType { get; set; } = "G94";

    /// <summary>
    /// The code of group 29, DIAMOF by default.
    /// </summary>
    public string DiameterMode { get; set; } = "DIAMOF";

    /// <summary>
    /// The master spindle SETMS(n) selected; null for the configured one.
    /// </summary>
    public int? MasterSpindle { get; set; }

    /// <summary>
    /// The spindles in axis mode, M70, by role.
    /// </summary>
    public HashSet<string> AxisSpindles { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The speed of each spindle, by role, where the source gave it.
    /// </summary>
    public Dictionary<string, decimal> Rpm { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The modal cycle MCALL made; null while none is.
    /// </summary>
    public SiemensCycle? ModalCycle { get; set; }

    /// <summary>
    /// The transformation that is on: "TCPM", "POLAR", "CYLINDER", or "" for none.
    /// </summary>
    public string TransformOn { get; set; } = "";

    /// <summary>
    /// The chain of transforms the reader wrote.
    /// </summary>
    public SiemensChain Chain { get; } = new();

    /// <summary>
    /// The modal F of the control; null before the first and where it is not known.
    /// </summary>
    public Value? ControlFeed { get; set; }

    /// <summary>
    /// The F the reader wrote last; null before the first and where it is not known.
    /// </summary>
    public Value? WrittenFeed { get; set; }

    /// <summary>
    /// The pole of the polar coordinates in the plane; null while none is defined.
    /// </summary>
    public SiemensPoint? PolePoint { get; set; }

    /// <summary>
    /// The radius of the modal rounding RNDM; null while none is on.
    /// </summary>
    public decimal? RoundingRadius { get; set; }

    /// <summary>
    /// The modal feed of the chamfers and roundings, FRCM; null while none is (controllers siemens.md 3).
    /// </summary>
    public Value? ModalCornerFeed { get; set; }

    /// <summary>
    /// The tool T selected before its M6; null while none is (controllers siemens.md 5).
    /// </summary>
    public ToolRef? PreloadedTool { get; set; }

    /// <summary>
    /// True while ORIMKS is active: a tool vector means the machine axes (controller-mapping 2).
    /// </summary>
    public bool MachineOrientation { get; set; }

    /// <summary>
    /// True while a path tolerance is on, CYCLE832 or CTOL (language 4.1, TOLERANCE).
    /// </summary>
    public bool ToleranceOn { get; set; }

    /// <summary>
    /// The direction the last motion ended in, which CT continues; null where the reader does not know it.
    /// </summary>
    public SiemensPoint? Tangent { get; set; }

    /// <summary>
    /// The modal words the reader wrote last, by key and address: a code the source repeats writes no word.
    /// </summary>
    public Dictionary<string, string> Written { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// True while the reader does not know the fact.
    /// </summary>
    /// <param name="fact">The fact, Motion.</param>
    public bool IsUnknown(string fact)
    {
        return Unknown.Contains(fact);
    }

    /// <summary>
    /// Records a modal word the reader writes; false when the reader wrote the same value last, so that the word need
    /// not stand again.
    /// </summary>
    /// <param name="key">The key with its address, "FEED_MODE", "SPINDLE:SUB".</param>
    /// <param name="value">The value as written.</param>
    public bool TakeChange(string key, string value)
    {
        if (Written.TryGetValue(key, out string? written) && written == value)
        {
            return false;
        }

        Written[key] = value;
        return true;
    }

    /// <summary>
    /// A copy, the state a call enters its subprogram with (virtual machine 3.9).
    /// </summary>
    public SiemensFacts Copy()
    {
        var copy = new SiemensFacts
        {
            MotionCode = MotionCode,
            Incremental = Incremental,
            WorkingPlane = WorkingPlane,
            FeedType = FeedType,
            DiameterMode = DiameterMode,
            MasterSpindle = MasterSpindle,
            ModalCycle = ModalCycle,
            TransformOn = TransformOn,
            ControlFeed = ControlFeed,
            WrittenFeed = WrittenFeed,
            PolePoint = PolePoint,
            RoundingRadius = RoundingRadius,
            ModalCornerFeed = ModalCornerFeed,
            PreloadedTool = PreloadedTool,
            MachineOrientation = MachineOrientation,
            ToleranceOn = ToleranceOn,
        };
        copy.Unknown.UnionWith(Unknown);
        copy.AxisSpindles.UnionWith(AxisSpindles);
        foreach (KeyValuePair<string, decimal> speed in Rpm)
        {
            copy.Rpm[speed.Key] = speed.Value;
        }

        copy.Chain.CopyFrom(Chain);
        return copy;
    }

    /// <summary>
    /// Takes every fact of another state, the one a subprogram runs with; the words written last are none.
    /// </summary>
    /// <param name="other">The state to take.</param>
    public void TakeFrom(SiemensFacts other)
    {
        MotionCode = other.MotionCode;
        Incremental = other.Incremental;
        WorkingPlane = other.WorkingPlane;
        FeedType = other.FeedType;
        DiameterMode = other.DiameterMode;
        MasterSpindle = other.MasterSpindle;
        ModalCycle = other.ModalCycle;
        TransformOn = other.TransformOn;
        ControlFeed = other.ControlFeed;
        WrittenFeed = other.WrittenFeed;
        PolePoint = other.PolePoint;
        RoundingRadius = other.RoundingRadius;
        ModalCornerFeed = other.ModalCornerFeed;
        PreloadedTool = other.PreloadedTool;
        MachineOrientation = other.MachineOrientation;
        ToleranceOn = other.ToleranceOn;
        Unknown.Clear();
        Unknown.UnionWith(other.Unknown);
        AxisSpindles.Clear();
        AxisSpindles.UnionWith(other.AxisSpindles);
        Rpm.Clear();
        foreach (KeyValuePair<string, decimal> speed in other.Rpm)
        {
            Rpm[speed.Key] = speed.Value;
        }

        Chain.CopyFrom(other.Chain);
        Tangent = null;
        Written.Clear();
    }

    /// <summary>
    /// What every call agrees on, fact by fact: a fact the calls hold differently is unknown, and a feed they hold
    /// differently is none, since the virtual machine walks the subprogram with the feed of each caller (virtual
    /// machine 3.9); no call gives every fact unknown.
    /// </summary>
    /// <param name="calls">The states of the calls.</param>
    public static SiemensFacts Agreed(IReadOnlyList<SiemensFacts> calls)
    {
        if (calls.Count == 0)
        {
            return AllUnknown();
        }

        SiemensFacts agreed = calls[0].Copy();
        foreach (SiemensFacts call in calls)
        {
            foreach (string fact in s_facts)
            {
                if (call.IsUnknown(fact) || call.KeyOf(fact) != agreed.KeyOf(fact))
                {
                    agreed.MakeUnknown(fact);
                }
            }

            if (call.ControlFeed?.ToCanonical() != agreed.ControlFeed?.ToCanonical())
            {
                agreed.ControlFeed = null;
            }

            if (call.WrittenFeed?.ToCanonical() != agreed.WrittenFeed?.ToCanonical())
            {
                agreed.WrittenFeed = null;
            }
        }

        return agreed;
    }

    /// <summary>
    /// The state of a caller the reader does not know: every fact unknown.
    /// </summary>
    public static SiemensFacts AllUnknown()
    {
        var facts = new SiemensFacts();
        foreach (string fact in s_facts)
        {
            facts.MakeUnknown(fact);
        }

        return facts;
    }

    /// <summary>
    /// Forgets a fact: the reader no longer knows it.
    /// </summary>
    /// <param name="fact">The fact, Motion.</param>
    public void MakeUnknown(string fact)
    {
        Unknown.Add(fact);
        switch (fact)
        {
            case Cycle:
                ModalCycle = null;
                break;
            case Pole:
                PolePoint = null;
                break;
            case Transform:
                TransformOn = "";
                break;
            case Frames:
                Chain.Forget();
                break;
            case Preload:
                PreloadedTool = null;
                break;
            case Spindles:
                Rpm.Clear();
                AxisSpindles.Clear();
                break;
        }
    }

    /// <summary>
    /// Forgets the facts a block kept as RAW changed where the reader writes them as modal words of their own: the
    /// control changes them when it runs the block, the output holds none of the words, and a block that depends on
    /// them stays RAW (D5). The facts the reader writes into every block, the motion code or the master spindle, stay
    /// as the block set them.
    /// </summary>
    /// <param name="before">The facts before the block.</param>
    public void ForgetChangesSince(SiemensFacts before)
    {
        foreach (string fact in s_writtenFacts)
        {
            if (IsUnknown(fact) != before.IsUnknown(fact) || KeyOf(fact) != before.KeyOf(fact))
            {
                MakeUnknown(fact);
            }
        }
    }

    // A fact as text, to compare two callers.
    private string KeyOf(string fact)
    {
        return fact switch
        {
            Motion => MotionCode ?? "",
            Distance => Incremental ? "G91" : "G90",
            Plane => WorkingPlane.ToString(),
            Feed => FeedType,
            Diameter => DiameterMode,
            Spindles => string.Create(CultureInfo.InvariantCulture, $"{MasterSpindle}|{RpmText()}"),
            Cycle => ModalCycle?.ToText() ?? "",
            Transform => TransformOn,
            Pole => PolePoint?.ToString() ?? "",
            Rounding => RoundingRadius?.ToString(CultureInfo.InvariantCulture) ?? "",
            Preload => PreloadedTool?.ToString() ?? "",
            Orientation => MachineOrientation ? "ORIMKS" : "ORIWKS",
            _ => Chain.ToText() ?? "?",
        };
    }

    private string RpmText()
    {
        var speeds = new List<string>();
        foreach (KeyValuePair<string, decimal> speed in Rpm)
        {
            speeds.Add(speed.Key + "=" + speed.Value.ToString(CultureInfo.InvariantCulture));
        }

        speeds.Sort(StringComparer.Ordinal);
        var axes = new List<string>(AxisSpindles);
        axes.Sort(StringComparer.Ordinal);
        return string.Join(",", speeds) + "|" + string.Join(",", axes);
    }
}
