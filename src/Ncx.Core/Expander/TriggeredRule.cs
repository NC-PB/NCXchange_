using Ncx.Core.Machine;

namespace Ncx.Core.Expander;

/// <summary>
/// An expansion rule a block triggers, with the table of the machine file it stands in (machine-config 5a).
/// </summary>
/// <param name="Rule">The pre, post, requires and restore of the table.</param>
/// <param name="Source">The table, as a generated block names its origin: "[tool_change]", "[coolant] THROUGH",
/// "cycle PECK".</param>
internal sealed record TriggeredRule(ExpansionRule Rule, string Source);
