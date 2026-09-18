using Ncx.Core.VirtualMachine.State;

namespace Ncx.Compilers.Heidenhain;

/// <summary>
/// The way of a JUMP or a REPEAT from its label as HeidenhainChainArrivals follows it (language 4.2, 4.9; virtual
/// machine 3.4): the chain of transforms the program has on the way, the transforms the lines written for the way the
/// text runs leave active on the control there, whether the setpos shifts are the same as on the way the text runs, and
/// the axes that stand at another place than there. The control holds one transform of each kind, as its cycles 7, 8
/// and 10 and PLANE replace the one of their kind (controllers heidenhain.md 3), in the order they were written (D31).
/// </summary>
internal sealed class HeidenhainChainWay
{
    /// <summary>
    /// The state of the program at the jump.
    /// </summary>
    public required ChannelSnapshot AtTheJump { get; init; }

    /// <summary>
    /// The chain of transforms the program has on the way, in program order (D31).
    /// </summary>
    public required List<TransformEntry> Program { get; init; }

    /// <summary>
    /// The transforms active on the control on the way, in the order they were written; at the label those of the
    /// program at the jump, which the lines before the jump wrote.
    /// </summary>
    public required List<TransformEntry> Control { get; init; }

    /// <summary>
    /// True where the setpos shifts, and the place of their cycle 7 on the control, are the same on both ways.
    /// </summary>
    public required bool SetposSame { get; init; }

    /// <summary>
    /// The axes the program has at another position on the way than on the way the text runs, until an absolute
    /// motion puts them at the same place on both.
    /// </summary>
    public HashSet<string> Displaced { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// False once a line leaves the control with transforms the documents do not give (D253).
    /// </summary>
    public bool ControlKnown { get; private set; } = true;

    /// <summary>
    /// The block after which the control has other transforms active on the way than the program, as the message
    /// names it; null while they are the same.
    /// </summary>
    public string? Differs { get; set; }

    /// <summary>
    /// A cancel line on the control, for an entry the program removes on the way the text runs: cycle 7 with the axes
    /// of that shift at 0, an omitted axis unchanged; cycle 8 without axes, cycle 10 with ROT+0, PLANE RESET, each
    /// ending the transform of its kind (controllers heidenhain.md 3; HeidenhainChain).
    /// </summary>
    public void Cancel(TransformEntry entry)
    {
        int index = IndexOfKind(entry.Kind);
        if (index < 0)
        {
            return;
        }

        TransformEntry active = Control[index];
        Dictionary<string, decimal?> kept = entry.Kind == TransformKind.Shift ? Without(active, entry) : [];
        if (kept.Count == 0)
        {
            Control.RemoveAt(index);
            return;
        }

        Control[index] = active with { Shift = kept };
    }

    /// <summary>
    /// The cycle of an entry on the control, written where the program appends it on the way the text runs: it
    /// replaces the transform of its kind, a cycle 7 the axes it names, and stands after the others (controllers
    /// heidenhain.md 3; D31). Where the transform it replaces has others after it, where the new one acts is not given
    /// (D253).
    /// </summary>
    /// <returns>False where the documents do not give what the control has after the cycle.</returns>
    public bool Write(TransformEntry entry)
    {
        int index = IndexOfKind(entry.Kind);
        if (index < 0)
        {
            Control.Add(entry);
            return true;
        }

        if (index != Control.Count - 1)
        {
            ControlKnown = false;
            return false;
        }

        // An axis the new cycle 7 omits keeps its shift (controllers heidenhain.md 3).
        TransformEntry active = Control[index];
        Dictionary<string, decimal?> kept = entry.Kind == TransformKind.Shift ? Without(active, entry) : [];
        if (kept.Count == 0)
        {
            Control[index] = entry;
            return true;
        }

        foreach (KeyValuePair<string, decimal?> shift in entry.Shift)
        {
            kept[shift.Key] = shift.Value;
        }

        Control[index] = entry with { Shift = kept };
        return true;
    }

    // The control's transform of the kind whose cycle replaces that of the entry (HeidenhainChain.FamilyOf); -1 for
    // none.
    private int IndexOfKind(TransformKind kind)
    {
        return Control.FindIndex(active => HeidenhainChain.FamilyOf(active.Kind) == HeidenhainChain.FamilyOf(kind));
    }

    // The axes of an active shift that a cycle 7 of another shift leaves as they are, each with its value: those it
    // does not name, where the shift is not 0 (an omitted axis is 0 in NCX, language 4.2; unchanged on the control,
    // controllers heidenhain.md 3).
    private static Dictionary<string, decimal?> Without(TransformEntry active, TransformEntry other)
    {
        var kept = new Dictionary<string, decimal?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, decimal?> shift in active.Shift)
        {
            if (!other.Shift.ContainsKey(shift.Key) && shift.Value != 0m)
            {
                kept[shift.Key] = shift.Value;
            }
        }

        return kept;
    }
}
