using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Analytics;

/// <summary>
/// An analytic: a listener of the virtual machine that reads the events it needs and writes a text table at the end;
/// it never touches the state of the virtual machine (architecture 9, virtual machine 8, D61). ncx analyze subscribes
/// the analytics the command line names and writes their reports one after the other.
/// </summary>
public interface IAnalytic : IVmListener
{
    /// <summary>
    /// The block range the analytic looks at: the NCX line numbers of the file from --from to --to, so that it can
    /// look at one operation instead of the whole program (virtual machine 8, D67).
    /// </summary>
    BlockRange Range { get; }

    /// <summary>
    /// The report over the events received so far, plain text in aligned columns or CSV (virtual machine 8); called
    /// once, after the run.
    /// </summary>
    string Report();
}
