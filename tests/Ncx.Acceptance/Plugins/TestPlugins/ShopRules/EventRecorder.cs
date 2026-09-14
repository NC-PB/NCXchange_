using System.Globalization;

namespace ShopRules;

/// <summary>
/// A listener that records every change of a spindle's direction it reads from Before and After of an event
/// (virtual machine 7), and gives the record back as its text, one line per event: STATE_CHANGE(4): S1 Off ->
/// Clockwise.
/// </summary>
public sealed class EventRecorder : IVmListener
{
    private readonly List<string> _lines = [];

    public void On(VmEvent vmEvent)
    {
        foreach (KeyValuePair<string, SpindleSnapshot> spindle in vmEvent.After.Spindles)
        {
            if (!vmEvent.Before.Spindles.TryGetValue(spindle.Key, out SpindleSnapshot? before)
                || before.Direction == spindle.Value.Direction)
            {
                continue;
            }

            SpindleDirection after = spindle.Value.Direction;
            _lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"{vmEvent.Kind}({vmEvent.Block.Line}): {spindle.Key} {before.Direction} -> {after}"));
        }
    }

    public override string ToString()
    {
        return string.Join('\n', _lines);
    }
}
