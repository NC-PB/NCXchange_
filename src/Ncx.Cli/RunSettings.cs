using Ncx.Core.VirtualMachine;

namespace Ncx.Cli;

/// <summary>
/// What the command line asks of one run of check, trace or annotate: the file, the machine and the options the three
/// commands share (architecture 10; D37, D53, D97, D103). convert takes the file, the machine and --strict of it.
/// </summary>
internal sealed record RunSettings
{
    /// <summary>
    /// The NCX file as the command line names it; the diagnostics carry this name (D98).
    /// </summary>
    public required string File { get; init; }

    /// <summary>
    /// --machine: the machine file by name in the machine folders or by path (P2-04); null for the machine that
    /// ncx.toml names, and without that for the built-in default machine of D103 (architecture 10).
    /// </summary>
    public string? MachineFile { get; init; }

    /// <summary>
    /// The working directory, where ncx.toml is read and a relative path of --machine starts (architecture 10,
    /// machine-config 10); the working directory of the process by default.
    /// </summary>
    public string WorkingDirectory { get; init; } = Directory.GetCurrentDirectory();

    /// <summary>
    /// The tool's own folder, whose machines/ and cycles/ hold the shipped machine files and cycle catalogs
    /// (implementation 12, P2-04); the folder of ncx by default.
    /// </summary>
    public string ToolFolder { get; init; } = AppContext.BaseDirectory;

    /// <summary>
    /// --strict: a WARNING sets the exit code 1 (D97).
    /// </summary>
    public bool Strict { get; init; }

    /// <summary>
    /// --skip-blocks: the run option skip_blocks, which SKIP blocks the virtual machine skips; none by default, every
    /// SKIP block runs (D53).
    /// </summary>
    public SkipBlocks SkipBlocks { get; init; } = SkipBlocks.None;

    /// <summary>
    /// --expand-cycles: the run option ExpandCycles, a CYCLE_CALL raised as its individual MOTION events (virtual
    /// machine 3.3, D37).
    /// </summary>
    public bool ExpandCycles { get; init; }

    /// <summary>
    /// --interpreted of trace: the virtual machine runs INTERPRETED, its variables evaluated, its jumps and calls
    /// followed (virtual machine 1, 3.6); false for STATIC, the mode of check (D91).
    /// </summary>
    public bool Interpreted { get; init; }

    /// <summary>
    /// --vars: the path of the vars file with the start values of an INTERPRETED run; null for &lt;file&gt;.vars.toml
    /// next to the file when there is one (virtual machine 2.7, 3.6; machine-config 8, 10).
    /// </summary>
    public string? VarsFile { get; init; }
}
