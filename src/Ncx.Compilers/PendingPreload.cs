using Ncx.Core.Model;

namespace Ncx.Compilers;

/// <summary>
/// The preload of the next tool that auto_preload inserts under preload_position = "before_first_motion", waiting for
/// the step before whose lines it stands (machine-config 3; LookAhead.BeforeFirstMotion).
/// </summary>
/// <param name="Tool">The next tool.</param>
/// <param name="Before">The index of the step before whose lines it stands.</param>
internal sealed record PendingPreload(ToolRef Tool, int Before);
