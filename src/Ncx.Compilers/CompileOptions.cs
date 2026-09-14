using Ncx.Config;
using Ncx.Core.Expander;
using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Compilers;

/// <summary>
/// The options of one compile, passed explicitly; there are no global settings (code-guidelines 5, Options). A test
/// constructs the options it needs; the command line fills them from ncx.toml and the machine file.
/// </summary>
public sealed record CompileOptions
{
    /// <summary>
    /// The program rewriters of the plugins, in their order, which the expander runs over every block before the
    /// virtual machine (architecture 5.5, D106); empty without plugins.
    /// </summary>
    public IReadOnlyList<IProgramRewriter> Rewriters { get; init; } = [];

    /// <summary>
    /// The listeners of the plugins, in their order, which the compiler subscribes to its STATIC run after itself, so
    /// that they read every event of the run (virtual machine 7, architecture 9, D106); empty without plugins.
    /// </summary>
    public IReadOnlyList<IVmListener> Listeners { get; init; } = [];

    /// <summary>
    /// The block writers of the plugins, in their order, which receive BLOCK_WRITE with the lines of every block
    /// before they reach the output (virtual machine 7, D106); empty without plugins.
    /// </summary>
    public IReadOnlyList<IBlockWriter> BlockWriters { get; init; } = [];

    /// <summary>
    /// The tool table the machine names, loaded (machine-config 1, D10); null when the machine names none or the file
    /// is missing, and then {kind} takes the default and the program gets the warning block of D10.
    /// </summary>
    public ToolTable? ToolTable { get; init; }

    /// <summary>
    /// The tool table file the machine names, as the user finds it, which the warning block of D10 names as the
    /// expected file; null when the machine names none.
    /// </summary>
    public string? ToolTableFile { get; init; }
}
