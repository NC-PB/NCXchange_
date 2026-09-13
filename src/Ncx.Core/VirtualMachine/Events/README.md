# Events

The events of the virtual machine (virtual machine 7; architecture 5.3): one record per row of the table of section 7, each a `VmEvent` with the channel, the block and the snapshots `Before` and `After`, and `IVmListener`, the one-method interface that analytics, trace, the kinematics module and plugins implement (D106). A listener subscribes with `VirtualMachine.Subscribe` and receives every event in the order raised; `Before` and `After` are read-only snapshots, so a listener observes and never changes the state (D61).

Start with `VmEvent.cs` and `IVmListener.cs`, then `BlockEvents.cs`: the events of one block in the order the block acts (what it opens, its state words, its motion, its flow words, the state it changed, what it closes), raised in step 7 by `RaiseEvents` in `../VirtualMachine.cs`. `../VirtualMachine.Events.cs` keeps the listeners and the chain from one block's `After` to the next block's `Before`, and raises `FILE_BEGIN` and `FILE_END`, whose blocks no walk executes.

| File | Event or rule |
|---|---|
| `FileEvent.cs`, `ProgramEvent.cs`, `SubEvent.cs`, `SectionEvent.cs` | `FILE_BEGIN`/`END`, `PROGRAM_BEGIN`/`END` with the run statistics, `SUB_BEGIN`/`END` with the caller, `SECTION` |
| `ToolEvent.cs`, `PreloadEvent.cs` | `TOOL_BEGIN`/`END` with the distance and the blocks under the tool, `PRELOAD` (virtual machine 3.5) |
| `MotionEvent.cs`, `MotionEvents.cs`, `CycleCallEvent.cs` | `MOTION` with its length, one per motion of an expanded cycle, and `CYCLE_CALL` (virtual machine 3.2, 3.3, 8; D37, D84, D94) |
| `StateChangeEvent.cs`, `StateChanges.cs`, `VarChangeEvent.cs` | `STATE_CHANGE`, one per changed variable named by its state key (virtual machine 3.10, 4, 6), and `VAR_CHANGE` |
| `FlowEvent.cs`, `FlowKind.cs` | `JUMP`, `CALL`, `RETURN`, `REPEAT` |
| `DwellEvent.cs`, `StopEvent.cs`, `FunctionEvent.cs` | `DWELL`, `STOP`, `FUNCTION` for `FUNC`, `MFUNC` and `COOLANT` |
| `SyncEvent.cs`, `BlockWriteEvent.cs` | declared here, raised by the job scheduler (P6-01) and the compilers (P3-03) |
| `RunStatistics.cs`, `EventText.cs`, `EventPhase.cs` | the distance and blocks under a tool and a program, the text `KIND(line): payload` of an event, begin and end |

Never here: a change of the state of the channel, and a listener that writes a report (the analytics are in `Ncx.Analytics`).
