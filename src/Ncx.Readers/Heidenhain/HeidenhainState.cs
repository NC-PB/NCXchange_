using Ncx.Core.VirtualMachine.State;

namespace Ncx.Readers.Heidenhain;

/// <summary>
/// What the Heidenhain reader knows of the file being read beyond the source-side state (architecture 7; controllers
/// heidenhain.md 7 rule 1): the blocks of the file and their labels, the working plane of the TOOL CALL, the active CC
/// pole, the direction the last contour element ended in, the active cycle definition and whether NCX has it on, the
/// chain of transforms with the values of cycle 7, TCPM, the points of the last PATTERN DEF, and the state each call of
/// a subprogram enters with. A fact is null where the reader does not know it.
/// </summary>
internal sealed class HeidenhainState
{
    private readonly List<SourceBlock> _blocks = [];
    private readonly Dictionary<int, int> _indexOfLine = [];

    /// <summary>
    /// Starts the facts of one file.
    /// </summary>
    /// <param name="source">The source-side state of the read, new for every file.</param>
    /// <param name="blocks">Every block of the file as the tokenizer cut it, trivia included.</param>
    public HeidenhainState(SourceState source, IReadOnlyList<SourceBlock> blocks)
    {
        Source = source;
        foreach (SourceBlock block in blocks)
        {
            if (!block.IsTrivia)
            {
                _indexOfLine[block.Line] = _blocks.Count;
                _blocks.Add(block);
            }
        }

        Labels = HeidenhainLabels.Classify(_blocks);
    }

    /// <summary>
    /// The source-side state of the read these facts belong to.
    /// </summary>
    public SourceState Source { get; }

    /// <summary>
    /// The blocks with words of the file, in file order.
    /// </summary>
    public IReadOnlyList<SourceBlock> Blocks => _blocks;

    /// <summary>
    /// The subprograms and the labels of the file (controllers heidenhain.md 7 rule 3).
    /// </summary>
    public HeidenhainLabels Labels { get; }

    /// <summary>
    /// The working plane, perpendicular to the tool axis of the TOOL CALL (controller-mapping 1, WORKPLANE); null where
    /// the reader does not know it.
    /// </summary>
    public Workplane? Plane { get; set; } = Workplane.XY;

    /// <summary>
    /// The pole of CC, the centre of C and CP and the pole of LP (controllers heidenhain.md 2); null while none is
    /// known.
    /// </summary>
    public HeidenhainPoint? Pole { get; set; }

    /// <summary>
    /// The decimals of the source words the pole was taken from.
    /// </summary>
    public int PoleDecimals { get; set; }

    /// <summary>
    /// The direction the last contour element ended in, which CT continues (controllers heidenhain.md 2); null while it
    /// is not known.
    /// </summary>
    public HeidenhainPoint? Tangent { get; set; }

    /// <summary>
    /// The chamfer or the rounding the contour element before a CHF or RND block prepared, which that block writes and
    /// the blocks up to the element after it are read from (D58); null while none is.
    /// </summary>
    public HeidenhainCorner? Corner { get; set; }

    /// <summary>
    /// The active machining cycle definition (controllers heidenhain.md 5); null while none is.
    /// </summary>
    public HeidenhainDefinition? Definition { get; set; }

    /// <summary>
    /// True where the reader does not know the active definition: in a subprogram whose callers define different
    /// cycles, after a call of a subprogram that defines one.
    /// </summary>
    public bool DefinitionUnknown { get; set; }

    /// <summary>
    /// True while the NCX cycle of the definition is on, false while it is off, null where the reader does not know
    /// (language 4.7, virtual machine 2.6).
    /// </summary>
    public bool? CycleOn { get; set; } = false;

    /// <summary>
    /// True once the NCX cycle has been called since it was written; the next non-cycle motion switches it off
    /// (controllers heidenhain.md 5).
    /// </summary>
    public bool CycleCalled { get; set; }

    /// <summary>
    /// The chain of transforms the reader wrote (language 4.2, D31).
    /// </summary>
    public HeidenhainChain Chain { get; } = new();

    /// <summary>
    /// The values of the active cycle 7 per axis; an axis a new cycle 7 omits keeps its value (controllers
    /// heidenhain.md 3).
    /// </summary>
    public Dictionary<string, decimal> DatumShift { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// True while M128 is on, false while it is off, null where the reader does not know (controller-mapping 1, TCPM).
    /// </summary>
    public bool? Tcpm { get; set; } = false;

    /// <summary>
    /// The points of the last PATTERN DEF in the working plane, which CYCL CALL PAT calls at; null while no pattern is
    /// known (controllers heidenhain.md 5).
    /// </summary>
    public List<HeidenhainPoint>? Pattern { get; set; }

    /// <summary>
    /// The blocks CYCL DEF n.1, n.2 ... of a cycle that CYCL DEF n.0 reads as a whole, by line: the reason the group is
    /// kept as RAW, or null when the first block of the group wrote its words.
    /// </summary>
    public Dictionary<int, string?> GroupMembers { get; } = [];

    /// <summary>
    /// The state each call of a subprogram enters with (virtual machine 3.9).
    /// </summary>
    public HeidenhainCalls Calls { get; } = new();

    /// <summary>
    /// The block with words after a block; null after the last.
    /// </summary>
    /// <param name="block">A block of the file.</param>
    public SourceBlock? NextBlock(SourceBlock block)
    {
        return _indexOfLine.TryGetValue(block.Line, out int index) && index + 1 < _blocks.Count
            ? _blocks[index + 1]
            : null;
    }

    /// <summary>
    /// The axes of the working plane and the tool axis: X Y and Z for XY, Z X and Y for ZX, Y Z and X for YZ
    /// (language 4.2, WORKPLANE); false while the plane is not known.
    /// </summary>
    /// <param name="first">The first axis of the plane.</param>
    /// <param name="second">The second axis of the plane.</param>
    /// <param name="tool">The tool axis.</param>
    public bool PlaneAxes(out string first, out string second, out string tool)
    {
        string axes = Plane switch
        {
            Workplane.XY => "XYZ",
            Workplane.ZX => "ZXY",
            Workplane.YZ => "YZX",
            _ => "",
        };
        first = axes.Length > 0 ? axes.Substring(0, 1) : "";
        second = axes.Length > 0 ? axes.Substring(1, 1) : "";
        tool = axes.Length > 0 ? axes.Substring(2, 1) : "";
        return axes.Length > 0;
    }

    /// <summary>
    /// The current position in the working plane; null when an axis of the plane is not known.
    /// </summary>
    public HeidenhainPoint? Position()
    {
        return PlaneAxes(out string first, out string second, out _)
            && Source.Positions.TryGetValue(first, out decimal x) && Source.Positions.TryGetValue(second, out decimal y)
            ? new HeidenhainPoint(x, y)
            : null;
    }

    /// <summary>
    /// Forgets every position and the direction of the last contour element, where the blocks before do not tell them:
    /// a label a jump enters, a block kept as RAW, a call.
    /// </summary>
    public void ForgetPositions()
    {
        var axes = new List<string>(Source.Positions.Keys);
        foreach (string axis in axes)
        {
            Source.ForgetPosition(axis);
        }

        Tangent = null;
    }

    /// <summary>
    /// Forgets the positions and the pole where the frame changes: the coordinates the blocks before gave are not those
    /// of the new frame (virtual machine 3.4).
    /// </summary>
    public void ForgetFrame()
    {
        ForgetPositions();
        Pole = null;
    }
}
