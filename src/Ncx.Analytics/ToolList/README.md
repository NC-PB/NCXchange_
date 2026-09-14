# Tool list

The tool list of virtual machine 8 and architecture 9 (implementation 14, P4-02): from `TOOL_BEGIN` and `TOOL_END` one row per tool use, with the rpm and the feeds it cuts with, the offset registers of its holder, the cutting distance (`LINE`, `ARC`, cycle plunges) and the rapid distance, the motions of unknown length, the blocks under it, the estimated time and whether it was preloaded before its change.

Start with `ToolListAnalytic.cs`, the listener: `Begin` opens a use at `TOOL_BEGIN`, `AddMotion` sums the distances, `CountBlocks` the blocks, and the runtime estimate of `../Runtime/` charges the time. `ToolUse.cs` is one row, `ToolListAnalytic.Report.cs` writes the table.
