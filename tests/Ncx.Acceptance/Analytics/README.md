# Analytics tests

The tests of `src/Ncx.Analytics/` until it has a test project of its own (implementation 00-method 4). `AnalyticRuns` runs a program as `ncx analyze` runs it, INTERPRETED with `ExpandCycles` and one analytic subscribed, and `AnalyticMachines` builds the three-axis mill whose numbers make the times easy to compute by hand, and the five-axis mills with an A/C table and with a B head over a C table.

- `MotionProfileTests`, `RuntimeAnalyticTests`: the runtime estimate of virtual machine 8 and D64 on small programs, each time computed by hand in the comment above the test: one block, the block-time floor, exact stop and continuous path mode, max_feed, acceleration, rapid, feed per revolution, CSS, accel_time, dwells, cycles, the fallback without `[dynamics]`, the totals, the block range.
- `ToolListAnalyticTests`: the tool list of `2.5D_FRAESEN` (one tool, tool 1, with the distances computed by hand from the example) and of `INCREMENTAL_SUB` (tools 2 and 3), the block range, the preload.
- `SegmentAnalyticTests`: the segment length (P4-03) on three blocks with known lengths and one arc, the motions skipped and why, the ten shortest with their lines, the block range, INCH, the configurable bins, the report.
- `ToolVectorAnalyticTests`: the tool vector change (P4-03) on two `LINE` blocks under `TCPM=ON` with vectors 30 degrees apart, the same with `A` and `C` words on an A/C table, the order of the axes, a B head over a C table, the `WORKPLANE` normal, the motions skipped and why (the polar plane among them), the block range, the bins, the report with the convention in its header.
- `SegmentAndVectorReferenceTests`: both analytics through `ncx analyze` on every example, the references of `3D_FRAESEN` from both sources, and the 5-axis pair of the corpus under `FiveAxisCorpusFactAttribute` (skipped with a message until `NCX_CORPUS` holds its folder `5X`).
- `TextTableTests`, `BlockRangeTests`, `AnalyticsRegistryTests`, `HistogramTests`, `WorstMotionsTests`: the tables, the range of D67, the registry, the bins, the ten worst.

`AnalyticsReference` compares a report with its reference in `../Expected/analytics/`; the command itself is tested in `../Cli/AnalyzeCommandTests`.
