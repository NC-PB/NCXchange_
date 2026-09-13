using Ncx.Core.Expander;
using Ncx.Core.Model;

namespace Ncx.Core.Tests.Expander;

/// <summary>
/// The example rewriter of the plugin template (code-guidelines 11): stops the spindle before the through-spindle
/// coolant is switched on and starts it again afterwards, for a machine whose coolant clutch only engages while the
/// spindle stands still.
/// </summary>
internal sealed class CoolantClutchRule : IProgramRewriter
{
    public RewriteResult Rewrite(Block block, RewriteContext context)
    {
        // Only the block that switches the through-spindle coolant on needs the sequence.
        if (!block.Has(key: "COOLANT", addr: "THROUGH", value: "ON"))
        {
            return RewriteResult.Unchanged;
        }

        // Save the spindle, stop it, keep the block, start the spindle again (virtual machine 3.10).
        return RewriteResult.Surround(
            before: ["@SAVE=SPINDLE:MAIN", "SPINDLE:MAIN=OFF"],
            after: ["@RESTORE=SPINDLE:MAIN"],
            reason: "the coolant clutch needs a standing spindle");
    }
}
