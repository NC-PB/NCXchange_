using System.Globalization;

namespace Ncx.Readers.Siemens;

/// <summary>
/// What the reader holds of the file being read: its blocks and units, the facts of the modal state where it reads
/// (SiemensFacts), the states the calls of each subprogram enter it with (virtual machine 3.9), the structures the
/// reader is inside and the labels their lowering writes, and the programs INIT selects for the channels.
/// </summary>
internal sealed class SiemensState
{
    private readonly Dictionary<int, int> _indexOfLine = [];
    private readonly Dictionary<string, List<SiemensFacts>> _callers = new(StringComparer.Ordinal);
    private int _structureCount;

    /// <summary>
    /// Starts the state of one file.
    /// </summary>
    /// <param name="source">The source-side state of the reader.</param>
    /// <param name="blocks">Every block of the file.</param>
    public SiemensState(SourceState source, IReadOnlyList<SourceBlock> blocks)
    {
        Source = source;
        Blocks = blocks;
        Units = SiemensUnits.Of(blocks);
        for (int index = 0; index < blocks.Count; index++)
        {
            _indexOfLine[blocks[index].Line] = index;
        }
    }

    /// <summary>
    /// The source-side state these facts belong to; a new read begins with a new one.
    /// </summary>
    public SourceState Source { get; }

    /// <summary>
    /// Every block of the file, in file order.
    /// </summary>
    public IReadOnlyList<SourceBlock> Blocks { get; }

    /// <summary>
    /// The units of the file.
    /// </summary>
    public SiemensUnits Units { get; }

    /// <summary>
    /// The facts of the modal state where the reader reads.
    /// </summary>
    public SiemensFacts Facts { get; } = new();

    /// <summary>
    /// The structures the reader is inside, the innermost last.
    /// </summary>
    public List<SiemensStructure> Structures { get; } = [];

    /// <summary>
    /// The unit the reader reads.
    /// </summary>
    public SiemensUnit? Unit { get; set; }

    /// <summary>
    /// The program INIT selected for each channel, by channel, which the START of the channel names (controllers
    /// siemens.md 9).
    /// </summary>
    public Dictionary<int, string> ChannelPrograms { get; } = [];

    /// <summary>
    /// The label after the header of the program being read that GOTOS jumps to; null for a program without GOTOS
    /// (controller-mapping 1, JUMP=END).
    /// </summary>
    public string? StartLabel { get; set; }

    /// <summary>
    /// The line of the second block of a contour by two angles, ANG= and ANG=, whose angle the block before it used
    /// (controllers siemens.md 3); null while none is pending.
    /// </summary>
    public int? AngleSecondLine { get; set; }

    /// <summary>
    /// The subprograms outside the file that an EXTERN announces, by name (controllers siemens.md 8).
    /// </summary>
    public HashSet<string> Externs { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The facts each subprogram of the file may change, once the reader has worked them out (SiemensCalls).
    /// </summary>
    public Dictionary<string, HashSet<string>> Changes { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The blocks of each SUB section the structure pass made of the range of a REPEAT, by its name, once the reader
    /// has read a REPEAT that calls it (controller-mapping 6, REPEAT + TIMES).
    /// </summary>
    public Dictionary<string, IReadOnlyList<SourceBlock>> RepeatSections { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The next block with words of the file after a block; null after the last.
    /// </summary>
    public SourceBlock? NextBlock(SourceBlock block)
    {
        if (!_indexOfLine.TryGetValue(block.Line, out int index))
        {
            return null;
        }

        for (int next = index + 1; next < Blocks.Count; next++)
        {
            if (!Blocks[next].IsTrivia)
            {
                return Blocks[next];
            }
        }

        return null;
    }

    /// <summary>
    /// Forgets the positions of the axes and the direction of the last motion, after a move the reader cannot follow.
    /// </summary>
    public void ForgetPositions()
    {
        foreach (string axis in new List<string>(Source.Positions.Keys))
        {
            Source.ForgetPosition(axis);
        }

        Facts.Tangent = null;
    }

    /// <summary>
    /// The state of the caller at a call of a subprogram of the file (virtual machine 3.9).
    /// </summary>
    /// <param name="name">The name of the subprogram.</param>
    /// <param name="caller">The state the call enters it with.</param>
    public void RecordCall(string name, SiemensFacts caller)
    {
        if (!_callers.TryGetValue(name, out List<SiemensFacts>? callers))
        {
            callers = [];
            _callers[name] = callers;
        }

        callers.Add(caller);
    }

    /// <summary>
    /// The state a subprogram runs with (virtual machine 3.9): what every call agrees on where the reader has read
    /// every call of the file before the subprogram, else the state of a caller the reader does not know.
    /// </summary>
    /// <param name="name">The name of the subprogram; null for one the reader cannot name.</param>
    public SiemensFacts EntryOf(string? name)
    {
        return name is not null && Units.CallCounts.TryGetValue(name, out int count)
            && _callers.TryGetValue(name, out List<SiemensFacts>? callers) && callers.Count == count
            ? SiemensFacts.Agreed(callers)
            : SiemensFacts.AllUnknown();
    }

    /// <summary>
    /// The state the SUB section of a repeated range runs with: what every REPEAT that calls a section of a range with
    /// this first block agrees on; the REPEATs of a section stand before its end, behind which the structure pass puts
    /// the SUB sections of its ranges, so the reader has read all of them (virtual machine 3.9).
    /// </summary>
    /// <param name="first">The first block of the range.</param>
    public SiemensFacts EntryOfRepeat(SourceBlock first)
    {
        var callers = new List<SiemensFacts>();
        foreach (KeyValuePair<string, IReadOnlyList<SourceBlock>> section in RepeatSections)
        {
            if (ReferenceEquals(section.Value[0], first)
                && _callers.TryGetValue(section.Key, out List<SiemensFacts>? calls))
            {
                callers.AddRange(calls);
            }
        }

        return SiemensFacts.Agreed(callers);
    }

    /// <summary>
    /// Begins the reading of a unit: no structure is open, and the labels of its structures count from 1.
    /// </summary>
    /// <param name="unit">The unit.</param>
    public void BeginUnit(SiemensUnit? unit)
    {
        Unit = unit;
        Structures.Clear();
        _structureCount = 0;
    }

    /// <summary>
    /// A new structure number of the unit, for the labels of its lowering; a label that the unit holds already gets
    /// another number.
    /// </summary>
    /// <param name="kind">IF, WHILE, FOR, LOOP or REPEAT.</param>
    /// <param name="suffixes">The endings of the labels of the structure, "", "_END", "_ELSE".</param>
    public string NewStructureName(string kind, params string[] suffixes)
    {
        while (true)
        {
            _structureCount++;
            string name = kind + _structureCount.ToString(CultureInfo.InvariantCulture);
            bool free = true;
            foreach (string suffix in suffixes)
            {
                free &= Unit is null || !Unit.Labels.Contains(name + suffix);
            }

            if (free)
            {
                return name;
            }
        }
    }
}
