namespace Ncx.Readers;

/// <summary>
/// What a source block is for the structure of the file: the file frame, the begin of a program or a subprogram,
/// the end of a program, the return of a subprogram (language 4.13, controller-mapping 1 and 6).
/// </summary>
public enum StructureRole
{
    /// <summary>
    /// An ordinary block.
    /// </summary>
    None,

    /// <summary>
    /// A block that frames the file, the % of a Fanuc file: FILE=BEGIN NCX=1 when it is the first block of the file,
    /// FILE=END when it is the last; anywhere else it writes nothing. A file without such blocks is framed at its start
    /// and its end (controller-mapping 1).
    /// </summary>
    FileFrame,

    /// <summary>
    /// The begin of a program the source names as one: BEGIN PGM, %_N_name_MPF (controller-mapping 1).
    /// </summary>
    ProgramBegin,

    /// <summary>
    /// The begin of a subprogram the source names as one: LBL n of a subprogram, PROC, %_N_name_SPF
    /// (controller-mapping 1, 6).
    /// </summary>
    SubBegin,

    /// <summary>
    /// The begin of a section the source does not name as a program or a subprogram, the Fanuc O line. It is a
    /// subprogram when a block of the file calls it (SourceStructure.Calls), since a CALL of a program is an ERROR
    /// (language 4.13). The documents do not yet say what a section is that no block of the file calls (D210); until
    /// they do, it is a program when it holds M30 or M2 and a subprogram otherwise, and the first of them is the
    /// program of a file that has none.
    /// </summary>
    SectionBegin,

    /// <summary>
    /// The executed end of a program: M30, M2 (controller-mapping 1, PROGRAM=END).
    /// </summary>
    ProgramEnd,

    /// <summary>
    /// The return of a subprogram to its caller, M99, LBL 0, RET; in a program, where no subprogram is active, the
    /// jump back to its first block (controller-mapping 6, the M99 loop rule).
    /// </summary>
    Return,
}
