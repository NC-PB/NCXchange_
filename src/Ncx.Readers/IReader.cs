using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers;

/// <summary>
/// Turns one controller file into an NcxProgram (architecture 7). Every reader keeps the same contract: it never
/// throws on bad input, it reports everything through the diagnostics of the program, it produces a program that
/// check can run, and it keeps what it cannot express as RAW (code-guidelines 4, Liskov substitution; D5).
/// </summary>
public interface IReader
{
    /// <summary>
    /// The controller family whose files this reader reads, the controller of the machine file (machine-config 1).
    /// </summary>
    Controller Controller { get; }

    /// <summary>
    /// Reads one file.
    /// </summary>
    /// <param name="source">The file, its name and its text.</param>
    /// <param name="machine">The machine the file was written for, whose tables, templates and cycle catalog map
    /// the native codes to NCX words (architecture 7, machine-config 5 and 6).</param>
    /// <param name="options">The reader rules of the plugins.</param>
    /// <returns>The program with its blocks, trivia, sections and diagnostics.</returns>
    NcxProgram Read(SourceFile source, MachineConfig machine, ReadOptions options);
}
