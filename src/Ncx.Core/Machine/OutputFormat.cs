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
}
