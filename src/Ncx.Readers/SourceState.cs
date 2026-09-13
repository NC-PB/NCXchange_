using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Readers;

/// <summary>
/// The small source-side virtual machine of a reader: the modal facts the controller keeps implicit and the mapping
/// rules consult, the active code of every modal group, absolute or incremental, the plane, the feed mode, the active
/// cycle, the preloaded tool, the spindle that owns the last S, the G-code system and the positions the reader needs
/// for chamfers and polar conversions (architecture 7). The full meaning is established afterwards by the NCX virtual
/// machine. A reader rule of a plugin reads it and never changes it (D106).
/// </summary>
public sealed class SourceState
{
    private readonly IReadOnlyDictionary<string, int> _modalGroupOfCode;
    private readonly Dictionary<int, string> _modalGroups = [];
    private readonly Dictionary<string, decimal> _positions = [];

    /// <summary>
    /// Starts the state of one file: no modal code active, absolute, nothing preloaded, no position known.
    /// </summary>
    /// <param name="gcodeSystem">The G-code system of the machine, for Fanuc lathes (machine-config 1).</param>
    /// <param name="modalGroupOfCode">The modal group of every modal code of the controller family, by the code
    /// without leading zeros: "G1" is group 1 on Fanuc (controllers fanuc.md 3); empty for a family without modal
    /// groups.</param>
    public SourceState(GcodeSystem? gcodeSystem, IReadOnlyDictionary<string, int> modalGroupOfCode)
    {
        GcodeSystem = gcodeSystem;
        _modalGroupOfCode = modalGroupOfCode;
    }

    /// <summary>
    /// The active code of every modal group that a block has set, "G1" in group 1 (controllers fanuc.md 3).
    /// </summary>
    public IReadOnlyDictionary<int, string> ModalGroups => _modalGroups;

    /// <summary>
    /// True while coordinates are incremental, G91 (controllers fanuc.md 3, group 03).
    /// </summary>
    public bool Incremental { get; internal set; }

    /// <summary>
    /// The working plane, G17 to G19 or the tool axis of TOOL CALL; null until the source sets it.
    /// </summary>
    public Workplane? Workplane { get; internal set; }

    /// <summary>
    /// The feed mode, per minute or per revolution; null until the source sets it.
    /// </summary>
    public FeedMode? FeedMode { get; internal set; }

    /// <summary>
    /// The active cycle as the source names it, "G81", "200", "CYCLE81"; null while no cycle is active.
    /// </summary>
    public string? ActiveCycle { get; internal set; }

    /// <summary>
    /// The tool preloaded by T alone or TOOL DEF; null when none is (controller-mapping 3).
    /// </summary>
    public ToolRef? Preloaded { get; internal set; }

    /// <summary>
    /// The role of the spindle that owns a bare S, the spindle of the last spindle M code (controller-mapping 4, the
    /// Fanuc S binding rule); null until one is set.
    /// </summary>
    public string? LastSpindle { get; internal set; }

    /// <summary>
    /// The G-code system of the machine, A, B or C, for Fanuc lathes; null otherwise (machine-config 1).
    /// </summary>
    public GcodeSystem? GcodeSystem { get; }

    /// <summary>
    /// The last absolute position of every axis the source has placed, by axis name, for chamfers and polar
    /// conversions.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> Positions => _positions;

    /// <summary>
    /// The active code of a modal group.
    /// </summary>
    /// <param name="group">The group number, 1 for motion on Fanuc.</param>
    /// <returns>The code, "G1"; null while no block has set the group.</returns>
    public string? ActiveCode(int group)
    {
        return _modalGroups.TryGetValue(group, out string? code) ? code : null;
    }

    /// <summary>
    /// Applies the modal codes of a block before the block is read: every code of a modal group replaces the active
    /// code of its group, compared by number (controllers fanuc.md 3, D105). The codes of a block act for the block
    /// itself, so the block is read with them.
    /// </summary>
    /// <param name="block">The source block.</param>
    internal void Apply(SourceBlock block)
    {
        // One value per group is active until another code of the same group appears (controllers fanuc.md 3).
        foreach (SourceWord word in block.Words)
        {
            string? code = NativeCode.Of(word);
            if (code is not null && _modalGroupOfCode.TryGetValue(code, out int group))
            {
                _modalGroups[group] = code;
            }
        }
    }

    /// <summary>
    /// Records the absolute position of an axis.
    /// </summary>
    /// <param name="axis">The axis name, "X".</param>
    /// <param name="position">The absolute position in the source's coordinates.</param>
    internal void SetPosition(string axis, decimal position)
    {
        _positions[axis] = position;
    }

    /// <summary>
    /// Forgets the position of an axis, after a move the reader cannot follow.
    /// </summary>
    /// <param name="axis">The axis name, "X".</param>
    internal void ForgetPosition(string axis)
    {
        _positions.Remove(axis);
    }
}
