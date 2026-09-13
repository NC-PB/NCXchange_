# Validation

The validation list of virtual machine 5, one file per family of the list, each with the rules of its family as rows of the table of the validation (`../../../../docs/spec/generated/diagnostics.md`, written by `DiagnosticTableTests`) and the checks of the rules that block execution and motion do not raise themselves (D98, D99).

Start with `RunValidation.cs`: what the virtual machine calls during a run. `CheckFile` is the pre-pass over the file before execution (labels, jump targets, unreachable blocks, and the rules about a block as it is written, once per block), `BeforeBlock` and `AfterBlock` wrap the steps of each block, `EndRun` reports the expressions STATIC mode left unresolved. Then `DiagnosticTable.cs`, the families in the order of the list.

| File | Family |
|---|---|
| `StructureValidation.cs` | the file frame, the sections, the words of a block (the parser's PAR codes), RAW |
| `FrameValidation.cs` | SETPOS, the path tolerance, ROT against the machine |
| `MotionValidation.cs` | UNITS, feed, IX; F in a RAPID block, `max_feed`, the axis `limits` (D64, D100) |
| `ArcValidation.cs`, `VectorValidation.cs`, `RetractAndHomeValidation.cs`, `CycleValidation.cs` | the rules of `ArcRules`, `ToolVectorRules`, `RetractRules`, `HomeRules` and `CycleRules`; COMP in an ARC block |
| `ToolValidation.cs` | the tool change table of 3.5, the offset forms of a program |
| `SpindleValidation.cs` | spindle rules 4 and 5 of 3.8, spindle OFF before a LINE, `rpm_min` and `rpm_max` |
| `FlowValidation.cs` | labels, jump targets, calls, JUMP=END from a subprogram, RETURN in a program, unreachable blocks (D89) |
| `ExpressionValidation.cs` | the expression grammar, the evaluator's ERRORs, unresolved expressions in STATIC mode |
| `ResourceValidation.cs` | roles, functions, axes with and without a machine file (D103), MFUNC, WORKPIECE while a spindle runs |
| `ChannelValidation.cs` | SYNC in a single-channel job, and the rules of the job scheduler |

A rule that depends on the state of a caller reports to `BlockContext.CallerRuleDiagnostics`, which a subprogram that no program of the file calls never reads, and its row says so (`SuppressedInUncalledSub`, D99).

Never here: executing a block, or a rule whose state only another stage knows; such a rule has its row with the stage that raises it (the compiler, the job scheduler, INTERPRETED mode).
