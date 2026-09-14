using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// What the Fanuc reader knows of the file being read beyond the modal groups of the source-side state (architecture
/// 7): the blocks and the programs of the file, the modal NCX words it has written, the modal F of the control, the
/// active cycle with its modal values, the G52 shift and the transformations it opened, the WHILE loops that are open,
/// and the facts that need a look at a later block (a G53.1 after a G68.2, the contour blocks of a lathe cycle).
/// </summary>
internal sealed class FanucState
{
    private readonly List<SourceBlock> _blocks = [];
    private readonly Dictionary<int, int> _indexOfLine = [];

    // The value of every modal word the reader has written in the section being read, by key: the header of D34 and
    // the changes after it. A word whose value is written already is not written again.
    private readonly Dictionary<string, string> _written = new(StringComparer.Ordinal);

    // While the structure of the file is laid out, for the section being laid out: the labels its blocks carried so
    // far, those whose first block holds only the end of the program, and the jumps to a label no block has carried
    // yet, with the label lists of their blocks.
    private readonly HashSet<string> _sectionLabels = new(StringComparer.Ordinal);
    private readonly HashSet<string> _endLabels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<(int Line, List<string> Labels)>> _pendingJumps =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Starts the facts of one file.
    /// </summary>
    /// <param name="source">The source-side state of the read, new for every file.</param>
    public FanucState(SourceState source)
    {
        Source = source;
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
    /// True once the facts that need the whole file are gathered.
    /// </summary>
    public bool Prepared { get; set; }

    /// <summary>
    /// The numbers of the O programs of the file; a call of another number is a call of an external program.
    /// </summary>
    public HashSet<long> ProgramNumbers { get; } = [];

    /// <summary>
    /// The labels that a GOTO of the file names, "300" of GOTO 300.
    /// </summary>
    public HashSet<string> JumpTargets { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The lines of the G68.2 blocks whose next block positions the rotary axes with G53.1 (controller-mapping 1).
    /// </summary>
    public HashSet<int> TiltsTurned { get; } = [];

    /// <summary>
    /// The lines of the G53.1 blocks that turn the tilt of the block before them.
    /// </summary>
    public HashSet<int> TurnsOfTilts { get; } = [];

    /// <summary>
    /// The lines of the blocks in the contour range of a G70 to G76 cycle of a lathe that stay RAW with their cycle.
    /// </summary>
    public HashSet<int> ContourLines { get; } = [];

    /// <summary>
    /// The NAME of the SUB section of the contour each G70 to G76 block names, by the line of the block (language
    /// 4.7.1, D65).
    /// </summary>
    public Dictionary<int, string> Contours { get; } = [];

    /// <summary>
    /// The lines of the jump blocks whose label is a block that holds only the end of the program: JUMP=END
    /// (controller-mapping 1, JUMP=END; language 4.9).
    /// </summary>
    public HashSet<int> JumpsToTheEnd { get; } = [];

    /// <summary>
    /// The line of the block after an expanded corner, whose incremental plane words count from the programmed corner
    /// (D58); null while there is none.
    /// </summary>
    public int? CornerLine { get; set; }

    /// <summary>
    /// How far the end of that corner lies from the programmed corner, per plane axis.
    /// </summary>
    public Dictionary<string, decimal> CornerShift { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The calls of the sections of the file, the state each call enters with, and what each subprogram may change
    /// (virtual machine 3.9).
    /// </summary>
    public FanucCalls Calls { get; } = new();

    /// <summary>
    /// The facts the reader does not know, those a caller leaves open or a called subprogram may have changed (virtual
    /// machine 3.9).
    /// </summary>
    public FanucUnknowns Unknowns { get; } = new();

    /// <summary>
    /// The modal F of the control: every F sets it, the F of a cycle block too (controllers fanuc.md 2, 6).
    /// </summary>
    public Value? ControlFeed { get; set; }

    /// <summary>
    /// The F written last as an NCX F word; CYCLE_F does not touch it (D29).
    /// </summary>
    public Value? WrittenFeed { get; set; }

    /// <summary>
    /// The active cycle; null while none is.
    /// </summary>
    public FanucCycle? Cycle { get; set; }

    /// <summary>
    /// The chain of transforms the reader wrote, G52, G68, G51.1 and G68.2, and the G52 shift of the control (language
    /// 4.2, D31).
    /// </summary>
    public FanucChain Chain { get; } = new();

    /// <summary>
    /// True while tool center point control of G43.4 or G43.5 is on.
    /// </summary>
    public bool Tcpm { get; set; }

    /// <summary>
    /// True while G43.5 is on, whose I J K on a line are the tool vector (controller-mapping 2, D81).
    /// </summary>
    public bool VectorTcpm { get; set; }

    /// <summary>
    /// True while polar interpolation of G12.1 is on, POLAR=ON (language 4.2).
    /// </summary>
    public bool Polar { get; set; }

    /// <summary>
    /// The radius and the angle of the last point of G16 polar coordinates.
    /// </summary>
    public decimal PolarRadius { get; set; }

    /// <summary>
    /// The angle of the last point of G16 polar coordinates, degrees.
    /// </summary>
    public decimal PolarAngle { get; set; }

    /// <summary>
    /// The WHILE and DO loops that are open, innermost last (controllers fanuc.md 7).
    /// </summary>
    public List<FanucLoop> Loops { get; } = [];

    /// <summary>
    /// True while G96 constant surface speed is on, so that S is the cutting speed (controllers fanuc.md 4).
    /// </summary>
    public bool CssOn { get; set; }

    /// <summary>
    /// The last speed of every spindle role, "" for the default spindle; the pitch of a tapping cycle is F / S.
    /// </summary>
    public Dictionary<string, decimal> Rpm { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The label of N and GOTO: the number without leading zeros and without its dot, N0100 is 100 (controller-mapping
    /// 6).
    /// </summary>
    /// <param name="word">The N or GOTO word.</param>
    public static string LabelOf(SourceWord word)
    {
        string label = word.Text.TrimEnd('.').TrimStart('0');
        return label.Length == 0 ? "0" : label;
    }

    /// <summary>
    /// Adds a block with words, in file order.
    /// </summary>
    /// <param name="block">The block.</param>
    public void AddBlock(SourceBlock block)
    {
        _indexOfLine[block.Line] = _blocks.Count;
        _blocks.Add(block);
    }

    /// <summary>
    /// Starts the labels of a new program or subprogram, an O line: labels are local to their section
    /// (controller-mapping 6; language 4.9).
    /// </summary>
    public void BeginSectionLabels()
    {
        _sectionLabels.Clear();
        _endLabels.Clear();
        _pendingJumps.Clear();
    }

    /// <summary>
    /// A jump of the block at the line names a label while the structure is laid out. A jump to the block that holds
    /// only the end of the program is JUMP=END and needs no LABEL (controller-mapping 1, JUMP=END); where that block
    /// comes after the jump, its label is taken back out of the jump's list when the block is seen, which is before
    /// the structure pass reads the lists.
    /// </summary>
    /// <param name="line">The line of the jump block.</param>
    /// <param name="label">The label it names.</param>
    /// <param name="labels">The list of the labels the jump block uses.</param>
    public void AddJump(int line, string label, List<string> labels)
    {
        if (_endLabels.Contains(label))
        {
            JumpsToTheEnd.Add(line);
            return;
        }

        labels.Add(label);
        if (!_sectionLabels.Contains(label))
        {
            if (!_pendingJumps.TryGetValue(label, out List<(int Line, List<string> Labels)>? jumps))
            {
                jumps = [];
                _pendingJumps[label] = jumps;
            }

            jumps.Add((line, labels));
        }
    }

    /// <summary>
    /// A block carries a label while the structure is laid out; the first block of the section with that label is the
    /// one a jump reaches, and when it holds only the end of the program the jumps to it are JUMP=END.
    /// </summary>
    /// <param name="label">The label.</param>
    /// <param name="endsProgram">True when the block holds only its number and the M30 or M2 of the end.</param>
    public void AddLabel(string label, bool endsProgram)
    {
        if (!_sectionLabels.Add(label))
        {
            return;
        }

        if (endsProgram)
        {
            _endLabels.Add(label);
        }

        if (_pendingJumps.Remove(label, out List<(int Line, List<string> Labels)>? jumps) && endsProgram)
        {
            foreach ((int line, List<string> labels) in jumps)
            {
                labels.Remove(label);
                JumpsToTheEnd.Add(line);
            }
        }
    }

    /// <summary>
    /// The blocks of the program or subprogram a block stands in, from the O line at or before it to the block before
    /// the next O line; its labels are local to it (controller-mapping 6; language 4.9).
    /// </summary>
    /// <param name="block">A block of the file.</param>
    public IEnumerable<SourceBlock> SectionOf(SourceBlock block)
    {
        int begin = IndexOf(block);
        while (begin > 0 && _blocks[begin].Find("O") is null)
        {
            begin--;
        }

        for (int index = Math.Max(begin, 0); begin >= 0 && index < _blocks.Count; index++)
        {
            if (index > begin && _blocks[index].Find("O") is not null)
            {
                yield break;
            }

            yield return _blocks[index];
        }
    }

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
    /// The place of a block among the blocks with words; -1 for a block the file does not hold.
    /// </summary>
    /// <param name="block">A block of the file.</param>
    public int IndexOf(SourceBlock block)
    {
        return _indexOfLine.TryGetValue(block.Line, out int index) ? index : -1;
    }

    /// <summary>
    /// Tells whether a modal word changes the value the reader wrote last, and records the value: the reader writes a
    /// modal word where the source changes the state, so that the codes of the header written again (G17 of
    /// 2.5D_FRAESEN N70) give no word (examples/2.5D_FRAESEN.ncx, the header block).
    /// </summary>
    /// <param name="key">The key of the modal word, "WORKPLANE".</param>
    /// <param name="value">Its value as written, "XY".</param>
    public bool TakeChange(string key, string value)
    {
        if (_written.TryGetValue(key, out string? written) && written == value)
        {
            return false;
        }

        _written[key] = value;
        return true;
    }

    /// <summary>
    /// Records the value of a modal word the reader wrote.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value as written.</param>
    public void Record(string key, string value)
    {
        _written[key] = value;
    }

    /// <summary>
    /// Forgets the modal words written, where the state is not known from the blocks before: a label a jump enters,
    /// the start of a subprogram, a block kept as RAW.
    /// </summary>
    public void ForgetWritten()
    {
        _written.Clear();
    }

    /// <summary>
    /// Forgets the value of one modal word written, which a called subprogram may have written otherwise.
    /// </summary>
    /// <param name="key">The key of the modal word, "WORKPLANE".</param>
    public void ForgetWritten(string key)
    {
        _written.Remove(key);
    }

    /// <summary>
    /// Forgets every position of the source-side state, where the blocks before do not tell it: a label a jump enters,
    /// the start of a section, a change of the frame.
    /// </summary>
    public void ForgetPositions()
    {
        var axes = new List<string>(Source.Positions.Keys);
        foreach (string axis in axes)
        {
            Source.ForgetPosition(axis);
        }
    }
}
