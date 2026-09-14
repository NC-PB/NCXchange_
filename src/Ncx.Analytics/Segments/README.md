# Segment length

The segment length of virtual machine 8 and architecture 9 (implementation 14, P4-03): per `MOTION` in the workpiece frame whose positions are known, the distance between its start and its end point, the arc length for an `ARC`, with the minimum, maximum and mean per verb, a histogram over configurable bins, the ten shortest motions with their lines and the motions skipped and why, over the block range of D67.

Start with `SegmentAnalytic.cs`, the listener: `Measure` takes the length the `MOTION` event carries (virtual machine 7, 8) and skips the motions of the machine frame; `CountUnknown` says why a motion has no length. `SegmentAnalytic.Report.cs` writes the report. The statistics, the histogram and the list of the ten shortest are `../VerbStatistics.cs`, `../Histogram.cs` and `../WorstMotions.cs`, which the tool vector change shares, and `../MotionPositions.cs` gives the reason a position is not known.

Never here: a length computed again from the positions; the virtual machine measures it once, and the tool list and the runtime estimate read the same number.
