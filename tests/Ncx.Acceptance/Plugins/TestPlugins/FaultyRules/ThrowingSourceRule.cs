namespace FaultyRules;

/// <summary>
/// A reader rule that begins a block for M457 and throws before it ends it, halfway through its work.
/// </summary>
public sealed class ThrowingSourceRule : ISourceRule
{
    public bool Read(SourceBlock block, SourceState state, NcxBuilder builder)
    {
        if (block.Find("M")?.Number != 457m)
        {
            return false;
        }

        builder.Begin(block.Line).Word("COOLANT", null, new IdentValue("ON"));
        throw new InvalidOperationException("the M457 table is missing");
    }
}
