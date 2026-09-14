using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// Writes an NCX program for one machine (architecture 8): runs the virtual machine STATIC over the program and writes
/// one or more lines per block from the state before and after it and the templates of the machine. A compiler never
/// decides what a word means; it decides how the machine writes it. One compiler per controller family, chosen by the
/// controller of the machine file through the <see cref="CompilerRegistry"/> (code-guidelines 5, Strategy).
/// </summary>
public interface ICompiler
{
    /// <summary>
    /// The controller family whose programs this compiler writes, the controller of the machine file (machine-config
    /// 1).
    /// </summary>
    Controller Controller { get; }

    /// <summary>
    /// Compiles one NCX file for one machine. Never throws on bad input: what the program or the machine lacks is a
    /// diagnostic of the result (code-guidelines 6).
    /// </summary>
    /// <param name="program">The parsed NCX file, with the diagnostics of the parser.</param>
    /// <param name="machine">The machine the program is compiled for, with its cycle catalog.</param>
    /// <param name="options">The plugins and the tool table of the run.</param>
    /// <returns>The text of every output file and the diagnostics of the run.</returns>
    CompileResult Compile(NcxProgram program, MachineConfig machine, CompileOptions options);
}
