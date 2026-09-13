using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.State;

/// <summary>
/// The program rows of virtual machine 2.1: the section that runs, program.active, name and number, and ended.
/// Mutable; only the virtual machine changes it (code-guidelines 7).
/// </summary>
internal sealed class ProgramState
{
    // program.active, name and number start false, empty and 0, ended starts false, and no section runs before the
    // first PROGRAM=BEGIN (virtual machine 2.1).
    public ProgramState()
    {
        Section = null;
        Active = false;
        Name = "";
        Number = 0;
        Ended = false;
    }

    public Section? Section { get; set; }

    public bool Active { get; set; }

    public string Name { get; set; }

    public int Number { get; set; }

    public bool Ended { get; set; }

    /// <summary>
    /// An immutable copy of the program rows as they are now.
    /// </summary>
    public ProgramSnapshot Snapshot()
    {
        return new ProgramSnapshot
        {
            Section = Section,
            Active = Active,
            Name = Name,
            Number = Number,
            Ended = Ended,
        };
    }
}
