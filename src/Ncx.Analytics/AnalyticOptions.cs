using Ncx.Core.Machine;
using Ncx.Core.VirtualMachine;

namespace Ncx.Analytics;

/// <summary>
/// What an analytic is created with, passed explicitly (code-guidelines 5, Options): the machine of the run, the names
/// its report gives the file and the machine, the block range of D67, the table format and the mode of the run.
/// </summary>
public sealed record AnalyticOptions
{
    /// <summary>
    /// The machine the run executes against: the machine file, or the built-in default machine of D103 without one;
    /// the runtime estimate reads its [[axis]] dynamics, [dynamics] and accel_time (machine-config 4, 5; D64).
    /// </summary>
    public required MachineConfig Machine { get; init; }

    /// <summary>
    /// The machine as the report names it, the file name of the machine file, so that the maintainer can correct the
    /// file instead of the code (implementation 14, risks): "fanuc-mill-30i.toml", "the built-in default machine".
    /// </summary>
    public required string MachineName { get; init; }

    /// <summary>
    /// The NCX file as the report names it, its file name: "3D_FRAESEN.ncx".
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// --from and --to (virtual machine 8, D67); the whole file by default.
    /// </summary>
    public BlockRange Range { get; init; } = BlockRange.Whole;

    /// <summary>
    /// Aligned columns or CSV (virtual machine 8); aligned columns by default.
    /// </summary>
    public TableFormat Format { get; init; } = TableFormat.Text;

    /// <summary>
    /// The mode of the run the report is about: INTERPRETED, the mode of analyze, or STATIC under --static (virtual
    /// machine 1).
    /// </summary>
    public ExecutionMode Mode { get; init; } = ExecutionMode.Interpreted;
}
