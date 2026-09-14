namespace ShopRules;

/// <summary>
/// A reader rule: on the shop's mills M456 switches the coolant on, a code the machine file does not name, so the
/// rule reads it as COOLANT=ON (architecture 9, ISourceRule; D40, D66).
/// </summary>
public sealed class ShopCoolantCode : ISourceRule
{
    public bool Read(SourceBlock block, SourceState state, NcxBuilder builder)
    {
        if (block.Find("M")?.Number != 456m)
        {
            return false;
        }

        builder.Begin(block.Line).Word("COOLANT", null, new IdentValue("ON")).End();
        return true;
    }
}
