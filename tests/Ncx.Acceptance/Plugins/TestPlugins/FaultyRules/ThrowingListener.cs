namespace FaultyRules;

/// <summary>
/// A listener that throws on TOOL_BEGIN.
/// </summary>
public sealed class ThrowingListener : IVmListener
{
    public void On(VmEvent vmEvent)
    {
        if (vmEvent is ToolEvent && vmEvent.Kind == "TOOL_BEGIN")
        {
            throw new InvalidOperationException("the tool list is full");
        }
    }
}
