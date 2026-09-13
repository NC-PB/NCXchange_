using System.Globalization;
using Ncx.Core.Expander;
using Ncx.Core.Model;
using Ncx.Core.Writing;

namespace Ncx.Core.Tests.Expander;

/// <summary>
/// A rewriter that changes nothing and records every call: the machine name, channel and line of the context and the
/// block it was asked about.
/// </summary>
internal sealed class ContextRecorder : IProgramRewriter
{
    public List<string> Calls { get; } = [];

    public RewriteResult Rewrite(Block block, RewriteContext context)
    {
        Calls.Add(string.Create(CultureInfo.InvariantCulture,
            $"{context.MachineName} {context.Channel} {context.Line} {NcxWriter.WriteBlock(block)} {context.Settings.Count}"));
        return RewriteResult.Unchanged;
    }
}
