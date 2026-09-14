# Ncx.Analytics

Listeners of the virtual machine that write text tables: tool list, runtime estimate, travel limits, segment length and tool vector change, loops, channel timeline (architecture 9; virtual machine 8). Each analytic is one class that subscribes to the events it needs and writes its report at the end; `ncx analyze` runs them (`../Ncx.Cli/Commands/AnalyzeCommand.cs`).

## Open first

1. `IAnalytic.cs`: an `IVmListener` with the block range of D67 (`BlockRange.cs`, `--from` and `--to`, NCX line numbers of the file) and `Report()`, the text it writes after the run.
2. `AnalyticOptions.cs`: what an analytic is created with, the machine of the run, the names its report gives the file and the machine, the range, the format, the mode; `AnalyticsRegistry.cs`: the analytics by the name `--analytic` takes, registered in `../Ncx.Cli/Program.cs`.
3. `TextTable.cs`: a table in aligned columns or CSV (virtual machine 8), `TableFormat.cs`; `ReportText.cs` writes the numbers and the title of a report; `RangeFilter.cs` decides whether an event stands in the range.
4. `ToolList/`: the tool list (P4-02). `Runtime/`: the runtime estimate of virtual machine 8 and D64 (P4-02), which the tool list uses for its times as well.

P4-03 adds segment length and tool vector change. The tests are in `../../tests/Ncx.Acceptance/Analytics/` until the project has enough to deserve its own (implementation 00-method 4). The analytics report in their text and raise no diagnostics of their own, so the project has no `DiagnosticCodes` class yet; its codes are `ANA` (D98).

Never here: a reference to anything but `Ncx.Core`; modal state of its own (an analytic reads the Before and After of each event, architecture 2 rule 1); a change to the program or the state (listeners observe, D61); a list of every event of a run (an analytic sums per event, so that a point list of millions of blocks runs, implementation 14 risks); generics of our own, LINQ chains of more than two calls (code-guidelines 10.2).
