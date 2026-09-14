namespace MyShopRules;

/// <summary>
/// Stops the spindle before the through-spindle coolant is switched on and starts it
/// again afterwards. For machines whose coolant clutch only engages while the spindle
/// stands still.
/// </summary>
public sealed class CoolantClutchRule : IProgramRewriter
{
    // Every block of the program passes through here once, before the virtual machine
    // executes it. Return Unchanged for blocks this rule is not about.
    public RewriteResult Rewrite(Block block, RewriteContext context)
    {
        // Only the block that switches the through-spindle coolant on needs the sequence.
        // Change the three words below to match your machine file.
        if (!block.Has(key: "COOLANT", addr: "THROUGH", value: "ON"))
        {
            return RewriteResult.Unchanged;
        }

        // Save the spindle, stop it, keep the block, start the spindle again. The virtual
        // machine fills in the direction and speed from its own state, so this rule does
        // not need to know them (virtual machine 3.10).
        return RewriteResult.Surround(
            before: ["@SAVE=SPINDLE:MAIN", "SPINDLE:MAIN=OFF"],
            after: ["@RESTORE=SPINDLE:MAIN"],
            reason: "the coolant clutch needs a standing spindle");
    }
}
