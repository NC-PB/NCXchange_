namespace Ncx.Cli;

// The diagnostic codes of Ncx.Cli (D98, code-guidelines 6, implementation 00-method 4).
//
// A code is the area prefix CLI and three digits; the codes of Ncx.Core (PAR, VM) and Ncx.Config (CFG) live in the
// DiagnosticCodes class of their own project. A diagnostic renders as "file(line): ERROR CLI002: message" with the
// severities ERROR, WARNING and INFO. A constant is named after its rule, and a code is never renumbered or reused, so
// that tests and users can rely on it.
//
// A command that needs codes of its own adds a part of this class, a file DiagnosticCodes.<Command>.cs next to this
// one, and takes its codes from a range of its own:
//
//   CLI001-CLI099  the command line, reading the input, writing the output, ncx format (P0-06)
//   CLI100-CLI199  ncx check, trace and annotate: their pipeline and the machine file of --machine (P1-07)
//   CLI200-CLI249  the machine of a run: --machine by name, ncx.toml, the cycle catalog of the machine (P2-04)
//   CLI250-CLI299  ncx convert: the machine file it requires, the reader of the machine's controller (P3-02)
//   CLI350-CLI399  ncx convert --batch: its folder, the crash of a file; the code page of a program (P3-07, D229)
//   CLI500-CLI549  ncx check --job and ncx analyze --job: the job manifest and the files of its channels (P6-01)

/// <summary>
/// The diagnostic codes of Ncx.Cli: one constant per rule, named after the rule, the area prefix CLI and three digits
/// (D98).
/// </summary>
public static partial class DiagnosticCodes
{
    /// <summary>
    /// CLI001: the command line is no ncx command: an unknown command or option (--machine on format, D91), a missing
    /// file, --check with --output; decided before the run starts, exit code 2 (D97, architecture 10).
    /// </summary>
    public const string UsageError = "CLI001";

    /// <summary>
    /// CLI002: the input file cannot be read, or its bytes are no UTF-8 text; decided before the run starts, exit code
    /// 2 (D97; language 3, Encoding).
    /// </summary>
    public const string InputUnreadable = "CLI002";

    /// <summary>
    /// CLI003, an INFO: under format --check, the first line on which the file differs from its canonical form; the
    /// difference sets exit code 1 (D97, architecture 10).
    /// </summary>
    public const string NotCanonical = "CLI003";

    /// <summary>
    /// CLI004: the output file cannot be written; the run has started, so the exit code is 1 (D97, architecture 10).
    /// </summary>
    public const string OutputUnwritable = "CLI004";
}
