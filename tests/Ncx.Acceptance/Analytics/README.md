# Analytics tests

The tests of `src/Ncx.Analytics/` until it has a test project of its own (implementation 00-method 4). `AnalyticRuns` runs a program as `ncx analyze` runs it, INTERPRETED with `ExpandCycles` and one analytic subscribed, and `AnalyticMachines` builds the three-axis mill whose numbers make the times easy to compute by hand.

- `MotionProfileTests`, `RuntimeAnalyticTests`: the runtime estimate of virtual machine 8 and D64 on small programs, each time computed by hand in the comment above the test: one block, the block-time floor, exact stop and continuous path mode, max_feed, acceleration, rapid, feed per revolution, CSS, accel_time, dwells, cycles, the fallback without `[dynamics]`, the totals, the block range.
- `ToolListAnalyticTests`: the tool list of `2.5D_FRAESEN` (one tool, tool 1, with the distances computed by hand from the example) and of `INCREMENTAL_SUB` (tools 2 and 3), the block range, the preload.
- `TextTableTests`, `BlockRangeTests`, `AnalyticsRegistryTests`: the tables, the range of D67, the registry.

`AnalyticsReference` compares a report with its reference in `../Expected/analytics/`; the command itself is tested in `../Cli/AnalyzeCommandTests`.
