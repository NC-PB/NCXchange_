using System.Globalization;
using Ncx.Core.Expander;
using Ncx.Core.Model;

namespace Ncx.Compilers.Tests.Jobs;

/// <summary>
/// A program rewriter of a plugin (architecture 9, Surround) that writes FUNC:DOOR=OPEN before every DWELL block and
/// records the channel its context names for it (D106).
/// </summary>
internal sealed class DwellRewriter : IProgramRewriter
{
    /// <summary>
    /// The channel of the context of every DWELL block, in the order the expander asked.
    /// </summary>
    public List<string> Channels { get; } = [];

    public RewriteResult Rewrite(Block block, RewriteContext context)
    {
        if (!block.Has("DWELL"))
        {
            return RewriteResult.Unchanged;
        }

        Channels.Add(context.Channel.ToString(CultureInfo.InvariantCulture));
        return RewriteResult.Surround(["FUNC:DOOR=OPEN"], [], "door before the dwell");
    }
}
