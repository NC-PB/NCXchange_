# History

The two history outputs of the virtual machine, `ncx trace` and `ncx annotate` (virtual machine 6; architecture 1, 9; D16): the VM never stores history in the program, so both are listeners on the events of a run (virtual machine 7) and the text they write from them.

Start with `TraceRow.cs`: one state variable that one executed block changed, from a `STATE_CHANGE` or a `VAR_CHANGE`, with the old and the new value as NCX writes them, empty when unknown. Then:

| File | What it does |
|---|---|
| `TraceListener.cs`, `TraceTable.cs`, `TraceFormat.cs` | trace: every row of the run in the order raised, so a subprogram walked at several calls appears once per walk (D99), written as aligned text or CSV |
| `AnnotationListener.cs`, `AnnotatedText.cs` | annotate: the values of the first walk of each block, appended to its comment in a copy of the program through the canonical writer (D92) |
| `GeneratedOrigin.cs` | how both name the origin of a generated block (virtual machine 3.10) |

Never here: a rule of the virtual machine (the rows come from its events) or a second copy of the canonical writer (a block is written by `NcxWriter.WriteBlock`).
