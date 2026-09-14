namespace Ncx.Core.VirtualMachine;

/// <summary>
/// How a walk of a program or a subprogram ended. The walks hand out one step per executed block, so that the job
/// scheduler can advance a channel by one block per round, also inside a called subprogram (virtual machine 3.7), and
/// a walk that hands out its steps cannot return a value of its own: it sets this before its last step instead.
/// </summary>
internal sealed class SectionEnd
{
    /// <summary>
    /// How the walk left its section; Returned until the walk says otherwise.
    /// </summary>
    public SectionExit Exit { get; set; } = SectionExit.Returned;
}
