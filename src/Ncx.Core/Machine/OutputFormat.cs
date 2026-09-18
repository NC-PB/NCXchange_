using Ncx.Core.Model;

namespace Ncx.Core.Machine;

/// <summary>
/// [format]: how the compiler writes numbers, block numbers, line endings and the ends of programs and subprograms
/// (machine-config 2).
/// </summary>
public sealed record OutputFormat
{
    /// <summary>
    /// decimal_separator: "." or "," for Heidenhain; null when the file leaves it out.
    /// </summary>
    public string? DecimalSeparator { get; init; }

    /// <summary>
    /// decimals: the decimals per address, X = 3, F = 1, S = 0; empty when the file leaves it out.
    /// </summary>
    public IReadOnlyDictionary<string, int> Decimals { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// trailing_zeros: whether trailing zeros are written; false when the file leaves it out.
    /// </summary>
    public bool TrailingZeros { get; init; }

    /// <summary>
    /// block_numbers: whether and how blocks are numbered; null when the file leaves it out.
    /// </summary>
    public BlockNumbering? BlockNumbers { get; init; }

    /// <summary>
    /// line_ending: CRLF or LF; null when the file leaves it out.
    /// </summary>
    public LineEnding? LineEnding { get; init; }

    /// <summary>
    /// program_end: the template written for PROGRAM=END, M30 (D48, D49); null when the file leaves it out.
    /// </summary>
    public string? ProgramEnd { get; init; }

    /// <summary>
    /// sub_end: the template written for SUB=END and RETURN, M99, "LBL 0", RET; null when the file leaves it out.
    /// </summary>
    public string? SubEnd { get; init; }

    /// <summary>
    /// program_layout: one output file for all programs, or one file per program (D48); null when the file leaves it
    /// out.
    /// </summary>
    public ProgramLayout? ProgramLayout { get; init; }

    /// <summary>
    /// max_line_length: the longest line the control takes, 0 for unlimited; null when the file leaves it out.
    /// </summary>
    public int? MaxLineLength { get; init; }

    /// <summary>
    /// comment_charset: the characters comments are written in, "ASCII"; null when the file leaves it out.
    /// </summary>
    public string? CommentCharset { get; init; }

    // TODO(question): implementation 16 (P6-02) names the output files of a job "from [machine] channels and a
    // [format] key", and machine-config 2 names no such key; channel_files and its three values stand for it until
    // that is answered.

    /// <summary>
    /// channel_files: how the job compiler names the file of each channel, O1000 and O1000.P-2, _C1 and _C2, or after
    /// the program NAME; null when the file leaves it out, and then each channel keeps the name the compiler gives the
    /// file of its program.
    /// </summary>
    public ChannelFiles? ChannelFiles { get; init; }

    // TODO(question): the four keys below are the [format] options that phase 3 asks for the habits of the Fanuc
    // sources ("add the option, not a special case", implementation 13, Risks); machine-config 2 does not name them,
    // and their names and values wait for an answer, with them where the offset of length_offset_with_tool_axis
    // stands around a canned cycle, HOME, G53, SETPOS, a jump, a call and the end.

    /// <summary>
    /// header: the start block of the machine, the lines a compiler writes after the beginning of every program, "G0
    /// G40\nG80 G90 G94 G98" on the Fanuc mill of the sources; null when the file leaves it out.
    /// </summary>
    public string? Header { get; init; }

    /// <summary>
    /// plane_with_first_motion: the code of the working plane stands in the first motion block after it changes, G0
    /// G17 X50.4 of the sources, not in a block of its own; false when the file leaves it out.
    /// </summary>
    public bool PlaneWithFirstMotion { get; init; }

    /// <summary>
    /// motion_code_after_tool_change: the first motion after a tool change writes its motion code although the control
    /// has it active, G0 G90 X10. after T2 M6 of the sources; false when the file leaves it out.
    /// </summary>
    public bool MotionCodeAfterToolChange { get; init; }

    /// <summary>
    /// length_offset_with_tool_axis: the tool length offset stands in the first block after it that moves the tool
    /// axis in the workpiece frame, a RAPID, LINE or ARC with a word of the tool axis or a CYCLE_CALL, G43 Z2. H1 of
    /// the sources, and not where the OFFSET:LEN word stands. A HOME and a G53 move, which end at machine positions,
    /// leave it waiting; it stands in a line of its own before a LABEL, JUMP, CALL or RAW block, before a skipped block
    /// that moves the tool axis and before the end of the program or subprogram, so that no path runs without it, and
    /// before the line of a SETPOS with a word of the tool axis, which declares that position with the offset active.
    /// False when the file leaves it out.
    /// </summary>
    public bool LengthOffsetWithToolAxis { get; init; }
}
