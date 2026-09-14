using System.Globalization;
using System.Text;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine.Events;

/// <summary>
/// STATE_CHANGE: the modal state variables a block changed, one event each with the old and the new value, from the
/// Before and After of the block (virtual machine 4, 6, 7; architecture 5.1).
/// </summary>
internal sealed class StateChanges
{
    // Tool 0 is the empty spindle (language 4.4).
    private static readonly ToolRef s_emptySpindle = new(0);

    private readonly Block _block;
    private readonly ChannelSnapshot _before;
    private readonly ChannelSnapshot _after;
    private readonly List<StateChangeEvent> _changes = [];

    private StateChanges(Block block, ChannelSnapshot before, ChannelSnapshot after)
    {
        _block = block;
        _before = before;
        _after = after;
    }

    /// <summary>
    /// The STATE_CHANGE events of a block, in the order of the tables of virtual machine 2: frame, motion, tool,
    /// spindle, machine functions, cycle.
    /// </summary>
    // STATE_CHANGE is raised on any modal change (virtual machine 7), and trace writes one row per changed state
    // variable (6). The variables are the modal items of the modal summary (4) and the position per axis, the tool
    // vector and the surface normal (2.2), which trace shows as well; the variables have VAR_CHANGE, and the program,
    // flow and channel rows (2.1, 2.7, 2.8) their events of their own; the block items (verb, FRAME, SKIP) end with
    // their block and never differ between Before and After.
    // TODO(question): virtual machine 7 names "any modal change" and 6 "every changed state variable" without a list;
    // STATE_CHANGE covers the variables above, without the program rows, lastHolder and the flow and channel rows,
    // until that is answered.
    public static List<StateChangeEvent> Between(Block block, ChannelSnapshot before, ChannelSnapshot after)
    {
        var changes = new StateChanges(block, before, after);
        changes.CompareFrame();
        changes.CompareMotion();
        changes.CompareHolders();
        changes.CompareSpindles();
        changes.CompareMachineFunctions();
        changes.Compare("CYCLE", Cycle(before.Cycle), Cycle(after.Cycle));
        return changes._changes;
    }

    // A variable whose value reads differently after the block than before it changed (virtual machine 6).
    private void Compare(string variable, string oldValue, string newValue)
    {
        if (oldValue == newValue)
        {
            return;
        }

        _changes.Add(new StateChangeEvent
        {
            Channel = _after.ChannelId,
            Block = _block,
            Before = _before,
            After = _after,
            Variable = variable,
            OldValue = oldValue,
            NewValue = newValue,
        });
    }

    // The frame rows of virtual machine 2.1; the setpos shift per axis (3.4).
    private void CompareFrame()
    {
        FrameSnapshot before = _before.Frame;
        FrameSnapshot after = _after.Frame;
        Compare("UNITS", EventText.Ident(before.Units), EventText.Ident(after.Units));
        Compare("WORKPLANE", EventText.Ident(before.Workplane), EventText.Ident(after.Workplane));
        Compare("ORIGIN", Integer(before.Origin), Integer(after.Origin));
        Compare("CHAIN", Chain(before.Chain), Chain(after.Chain));
        foreach (string axis in after.SetposShift.Keys)
        {
            string key = "SETPOS:" + axis;
            Compare(key, EventText.Number(Shift(before, axis), _before, key),
                EventText.Number(Shift(after, axis), _after, key));
        }

        Compare("DIAMETER", EventText.OnOff(before.Diameter), EventText.OnOff(after.Diameter));
        Compare("CYLINDER", OffOr(before.Cylinder, _before, "CYLINDER"), OffOr(after.Cylinder, _after, "CYLINDER"));
        Compare("POLAR", EventText.OnOff(before.Polar), EventText.OnOff(after.Polar));
        Compare("TCPM", EventText.OnOff(before.Tcpm), EventText.OnOff(after.Tcpm));
        Compare("ROTARY_PATH", EventText.Ident(before.RotaryPath), EventText.Ident(after.RotaryPath));
        Compare("ROTARY_FEED", EventText.Ident(before.RotaryFeed), EventText.Ident(after.RotaryFeed));
        Compare("TOLERANCE", OffOr(before.Tolerance.Value, _before, "TOLERANCE"),
            OffOr(after.Tolerance.Value, _after, "TOLERANCE"));
        Compare("TOLERANCE:ROTARY", EventText.Number(before.Tolerance.Rotary, _before, "TOLERANCE:ROTARY"),
            EventText.Number(after.Tolerance.Rotary, _after, "TOLERANCE:ROTARY"));
        Compare("TOLERANCE_MODE", EventText.Ident(before.Tolerance.Mode), EventText.Ident(after.Tolerance.Mode));
        Compare("WORKPIECE", before.WorkpieceHolder ?? "", after.WorkpieceHolder ?? "");
    }

    // The motion rows of virtual machine 2.2 without the block items: the position per axis, named by the axis,
    // feed, feed mode, compensation, and the tool vector and surface normal by the keys that set them (D81).
    private void CompareMotion()
    {
        MotionSnapshot before = _before.Motion;
        MotionSnapshot after = _after.Motion;
        foreach (KeyValuePair<string, AxisPosition> position in after.Position)
        {
            AxisPosition old = before.Position.TryGetValue(position.Key, out AxisPosition known)
                ? known
                : AxisPosition.Unknown;
            Compare(position.Key, EventText.Position(old), EventText.Position(position.Value));
        }

        Compare("F", EventText.Number(before.Feed, _before, "F"), EventText.Number(after.Feed, _after, "F"));
        Compare("FEED_MODE", EventText.Ident(before.FeedMode), EventText.Ident(after.FeedMode));
        Compare("COMP", EventText.Ident(before.Comp), EventText.Ident(after.Comp));
        CompareVector(["TX", "TY", "TZ"], before.ToolVector, after.ToolVector);
        CompareVector(["NX", "NY", "NZ"], before.SurfaceNormal, after.SurfaceNormal);
    }

    // The tool rows of virtual machine 2.3 per holder; a holder created on the spot during the block (D103) starts
    // from the values the table gives.
    private void CompareHolders()
    {
        foreach (KeyValuePair<string, HolderSnapshot> holder in _after.Holders)
        {
            string id = holder.Key;
            HolderSnapshot? before = _before.Holders.TryGetValue(id, out HolderSnapshot? known) ? known : null;
            HolderSnapshot after = holder.Value;
            Compare("TOOL:" + id, (before?.SpindleTool ?? s_emptySpindle).ToString(), after.SpindleTool.ToString());
            Compare("PRELOAD:" + id, before?.Preloaded?.ToString() ?? "", after.Preloaded?.ToString() ?? "");
            Compare("OFFSET:LEN:" + id, Integer(before?.OffsetLen ?? 0), Integer(after.OffsetLen));
            Compare("OFFSET:RAD:" + id, Integer(before?.OffsetRad ?? 0), Integer(after.OffsetRad));
            Compare("OFFSET:" + id, Integer(before?.OffsetCombined ?? 0), Integer(after.OffsetCombined));
        }
    }

    // The spindle rows of virtual machine 2.4 per spindle; a spindle created on the spot during the block (D103)
    // starts from the values the table gives.
    private void CompareSpindles()
    {
        foreach (KeyValuePair<string, SpindleSnapshot> spindle in _after.Spindles)
        {
            string id = spindle.Key;
            SpindleSnapshot? before = _before.Spindles.TryGetValue(id, out SpindleSnapshot? known) ? known : null;
            SpindleSnapshot after = spindle.Value;
            Compare("SPINDLE:" + id, EventText.Ident(before?.Direction ?? SpindleDirection.Off),
                EventText.Ident(after.Direction));
            CompareNumber("RPM", id, before?.Rpm ?? 0m, after.Rpm);
            Compare("SPINDLE_MODE:" + id, EventText.Ident(before?.Mode ?? SpindleMode.Spindle),
                EventText.Ident(after.Mode));
            CompareNumber("ORIENT", id, before?.Orientation, after.Orientation);
            Compare("SPINDLE_SYNC:" + id, before?.SyncPartner ?? "", after.SyncPartner ?? "");
            CompareNumber("PHASE", id, before?.SyncPhase, after.SyncPhase);
            Compare("CSS:" + id, EventText.OnOff(before?.Css ?? false), EventText.OnOff(after.Css));
            CompareNumber("VC", id, before?.Vc, after.Vc);
            CompareNumber("RPM_MAX", id, before?.RpmMax, after.RpmMax);
        }
    }

    // The coolant channels and the named functions of virtual machine 2.5; a channel or function created on the spot
    // (D103) starts OFF and without a state.
    private void CompareMachineFunctions()
    {
        foreach (KeyValuePair<string, bool> channel in _after.Coolant)
        {
            bool before = _before.Coolant.TryGetValue(channel.Key, out bool on) && on;
            Compare("COOLANT:" + channel.Key, EventText.OnOff(before), EventText.OnOff(channel.Value));
        }

        foreach (KeyValuePair<string, string?> function in _after.Functions)
        {
            string before = _before.Functions.TryGetValue(function.Key, out string? state) ? state ?? "" : "";
            Compare("FUNC:" + function.Key, before, function.Value ?? "");
        }
    }

    // A number of a spindle, empty when none or UNKNOWN under its state key, RPM:S1 (virtual machine 1).
    private void CompareNumber(string key, string spindle, decimal? before, decimal? after)
    {
        string stateKey = key + ":" + spindle;
        Compare(stateKey, EventText.Number(before, _before, stateKey), EventText.Number(after, _after, stateKey));
    }

    private void CompareVector(string[] keys, IReadOnlyList<decimal>? before, IReadOnlyList<decimal>? after)
    {
        for (int index = 0; index < keys.Length; index++)
        {
            Compare(keys[index], Component(before, index), Component(after, index));
        }
    }

    private static string Component(IReadOnlyList<decimal>? vector, int index)
    {
        return vector is not null && index < vector.Count ? EventText.Number(vector[index]) : "";
    }

    private static decimal? Shift(FrameSnapshot frame, string axis)
    {
        return frame.SetposShift.TryGetValue(axis, out decimal shift) ? shift : null;
    }

    private static string Integer(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    // A value that OFF switches off, CYLINDER and TOLERANCE: OFF for none, empty for UNKNOWN (virtual machine 1).
    private static string OffOr(decimal? value, ChannelSnapshot snapshot, string stateKey)
    {
        return value is null ? "OFF" : EventText.Number(value, snapshot, stateKey);
    }

    // The transform chain as its words, the entries in program order separated by a bar (virtual machine 2.1, D31).
    private static string Chain(IReadOnlyList<TransformEntry> chain)
    {
        var entries = new List<string>();
        foreach (TransformEntry entry in chain)
        {
            entries.Add(Entry(entry));
        }

        return string.Join(" | ", entries);
    }

    private static string Entry(TransformEntry entry)
    {
        return entry.Kind switch
        {
            TransformKind.Shift => "SHIFT" + AxisWords(entry.Shift),
            TransformKind.Rotate => "ROTATE=" + EventText.Number(entry.Angle),
            TransformKind.Mirror => "MIRROR=" + string.Join(",", entry.Mirrored),
            TransformKind.Tilt => "TILT" + TiltWords(entry),
            _ => "TILT_AXIS" + TiltWords(entry),
        };
    }

    private static string TiltWords(TransformEntry entry)
    {
        return AxisWords(entry.Angles) + " MOVE=" + EventText.Ident(entry.Move)
            + " ROT=" + EventText.Ident(entry.Rot);
    }

    // The axis words of a chain entry; a value from an expression is UNKNOWN and shown as ? (virtual machine 1).
    private static string AxisWords(IReadOnlyDictionary<string, decimal?> values)
    {
        var text = new StringBuilder();
        foreach (KeyValuePair<string, decimal?> value in values)
        {
            text.Append(' ').Append(value.Key).Append('=').Append(EventText.Number(value.Value));
        }

        return text.ToString();
    }

    // The cycle row of virtual machine 2.6 as one value: its name, the controller of a native cycle, the drilling
    // axis and the parameter words; OFF without a cycle.
    private static string Cycle(CycleSnapshot cycle)
    {
        if (cycle.Name is not string name)
        {
            return "OFF";
        }

        string head = cycle.Controller is null ? name : name + " (" + cycle.Controller + ")";
        string parameters = cycle.Parameters.Count == 0 ? "" : " " + EventText.Words(cycle.Parameters);
        return head + " AXIS=" + cycle.Axis + parameters;
    }
}
