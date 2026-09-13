namespace Ncx.Core.Machine;

/// <summary>
/// [format] program_layout: how the programs and subprograms of one NCX file become output files (machine-config 2,
/// D48).
/// </summary>
public enum ProgramLayout
{
    /// <summary>
    /// program_layout = "one_file": every program and subprogram into one output file (Fanuc O programs, Siemens
    /// %_N_ units).
    /// </summary>
    OneFile,

    /// <summary>
    /// program_layout = "file_per_program": one output file per program, the subprograms copied after the M30 of
    /// every caller (Heidenhain).
    /// </summary>
    FilePerProgram,
}
