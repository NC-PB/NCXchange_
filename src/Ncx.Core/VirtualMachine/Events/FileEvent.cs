using Ncx.Core.Model;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// FILE_BEGIN and FILE_END (virtual machine 7): raised at FILE=BEGIN before the first program runs and at FILE=END
/// after the last walk, with the file name and the programs and subprograms the pre-pass found (2.1).
/// </summary>
public sealed record FileEvent : VmEvent
{
    /// <summary>
    /// FILE_BEGIN or FILE_END.
    /// </summary>
    public required EventPhase Phase { get; init; }

    /// <summary>
    /// The name of the file, which the diagnostics name as well (D98).
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// file.programs: the programs of the file in file order (virtual machine 2.1).
    /// </summary>
    public required IReadOnlyList<Section> Programs { get; init; }

    /// <summary>
    /// file.subs: the subprograms of the file in file order (virtual machine 2.1).
    /// </summary>
    public required IReadOnlyList<Section> Subs { get; init; }

    /// <inheritdoc/>
    public override string Kind => Phase == EventPhase.Begin ? "FILE_BEGIN" : "FILE_END";

    /// <inheritdoc/>
    protected override string PayloadText()
    {
        return $"file {FileName}, programs {EventText.SectionNames(Programs)}, "
            + $"subprograms {EventText.SectionNames(Subs)}";
    }
}
