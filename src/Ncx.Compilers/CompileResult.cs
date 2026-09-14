using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// What one compile gives: the text of every output file and the diagnostics of the run (architecture 8).
/// </summary>
public sealed record CompileResult
{
    /// <summary>
    /// The output files in the order program_layout gives them: one for "one_file", one per program for
    /// "file_per_program" (machine-config 2, D48); empty when an ERROR stopped the compile, since a run stops on ERROR
    /// (virtual machine 2.9, code-guidelines 6).
    /// </summary>
    public required IReadOnlyList<CompiledFile> Files { get; init; }

    /// <summary>
    /// Every diagnostic in the order it was reported: the parser, the expander, the virtual machine, the compiler
    /// (D98).
    /// </summary>
    public required Diagnostics Diagnostics { get; init; }
}
