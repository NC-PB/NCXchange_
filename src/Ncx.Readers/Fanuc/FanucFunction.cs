using Ncx.Core.Model;

namespace Ncx.Readers.Fanuc;

/// <summary>
/// The NCX word an M code of the machine's function tables stands for: SPINDLE=CW for M3, COOLANT=ON for M8,
/// FUNC:RIGID_TAP=ON for M29 (machine-config 5).
/// </summary>
/// <param name="Key">The key, "SPINDLE".</param>
/// <param name="Addr">The address; null for the default spindle and the default coolant channel.</param>
/// <param name="Value">The value.</param>
/// <param name="SpindleRole">The role of the spindle a SPINDLE or ORIENT word selects, which owns a bare S of the block
/// and the S after it (controller-mapping 4); null for the other words.</param>
internal sealed record FanucFunction(string Key, string? Addr, Value Value, string? SpindleRole);
