using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The program rows of virtual machine 2.1 as they stood when the snapshot was taken; immutable, part of the Before
/// and After of every event (architecture 5.3).
/// </summary>
public sealed record ProgramSnapshot
{
    /// <summary>
    /// The program or subprogram section that runs; null before the first PROGRAM=BEGIN.
    /// </summary>
    public required Section? Section { get; init; }

    /// <summary>
    /// program.active: true from PROGRAM=BEGIN on.
    /// </summary>
    public required bool Active { get; init; }

    /// <summary>
    /// program.name: the NAME of the program; empty without one.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// program.number: the NUMBER of the program; 0 without one.
    /// </summary>
    public required int Number { get; init; }

    /// <summary>
    /// ended: true from PROGRAM=END on, also when it is reached by JUMP=END.
    /// </summary>
    public required bool Ended { get; init; }
}
