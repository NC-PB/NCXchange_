namespace Ncx.Readers.Siemens;

/// <summary>
/// One unit of a SINUMERIK file (controllers siemens.md 1, 11 rule 1): a main program %_N_NAME_MPF, a subprogram
/// %_N_NAME_SPF or PROC NAME, an _INI unit, or the blocks of a file without a header; with its labels and the labels
/// its jumps enter, which are local to the unit.
/// </summary>
internal sealed class SiemensUnit
{
    /// <summary>
    /// A program, a subprogram or an _INI unit.
    /// </summary>
    public required SiemensUnitKind Kind { get; init; }

    /// <summary>
    /// The name, in capitals: SHAFT of %_N_SHAFT_MPF, the name of PROC; null for a file without a header.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The block that begins the unit, its % header or its PROC; null for the blocks before the first header.
    /// </summary>
    public SourceBlock? Begin { get; init; }

    /// <summary>
    /// The declaration of a subprogram, the PROC line; null for a unit without one.
    /// </summary>
    public SiemensProc? Proc { get; set; }

    /// <summary>
    /// The line of the PROC that declares a %_N_NAME_SPF unit after its header; null otherwise.
    /// </summary>
    public int? DeclarationLine { get; set; }

    /// <summary>
    /// The blocks with words after the begin, in file order.
    /// </summary>
    public List<SourceBlock> Blocks { get; } = [];

    /// <summary>
    /// The labels of the unit: NAME of NAME: and N300 of a block number, in capitals.
    /// </summary>
    public HashSet<string> Labels { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The labels a jump or a repeat of the unit enters, written as LABEL where they stand (language 4.9).
    /// </summary>
    public HashSet<string> Targets { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The labels a jump enters that stand directly before the end of the program: the jump is JUMP=END
    /// (controller-mapping 1, JUMP=END).
    /// </summary>
    public HashSet<string> EndTargets { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// True when a GOTOS of the unit jumps to the start of the program (controller-mapping 1, JUMP=END).
    /// </summary>
    public bool JumpsToStart { get; set; }

    /// <summary>
    /// The program-part repeats of the unit, REPEAT and REPEATB with their labels, by their block (controllers
    /// siemens.md 8; controller-mapping 6, REPEAT + TIMES).
    /// </summary>
    public Dictionary<SourceBlock, SiemensRepeat> Repeats { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The label by which a repeat names the first or the last block of its range, where the range becomes a SUB
    /// section: the structure pass finds the range by it (SourceStructure.Repeat).
    /// </summary>
    public Dictionary<SourceBlock, string> RangeLabels { get; } = new(ReferenceEqualityComparer.Instance);
}

/// <summary>
/// The kinds of a SINUMERIK unit (controllers siemens.md 1).
/// </summary>
internal enum SiemensUnitKind
{
    /// <summary>
    /// A main program, MPF, or the blocks of a file without a header.
    /// </summary>
    Program,

    /// <summary>
    /// A subprogram, SPF or PROC.
    /// </summary>
    Sub,

    /// <summary>
    /// An _INI unit of tool data and machine function groups, which is not NC code.
    /// </summary>
    Ini,
}
