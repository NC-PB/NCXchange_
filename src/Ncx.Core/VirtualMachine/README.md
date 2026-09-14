# VirtualMachine

The virtual machine executes the blocks of a program against the state of a channel and reports what it finds, so that readers, compilers and analytics know what the program means at every block (virtual machine 1; architecture 5). STATIC mode walks every program of the file and follows the calls of its subprograms (D99); INTERPRETED mode executes the program that runs with its flow and evaluates its expressions (virtual machine 3.6).

Start with `VirtualMachine.cs`: `Execute(block)` runs the seven steps of virtual machine 3, one private method each, `ParseAndValidate`, `ResolveRolesAndAxes`, `ApplyStateWords`, `ApplyFrameVerb`, `ExecuteMotion`, `ResetBlockScope`, `RaiseEvents`, with `ApplyFlowWords` for the flow words of architecture 5.1 between steps 5 and 6. `VirtualMachine.Static.cs` is the STATIC walk behind `Run`, `VirtualMachine.Interpreted.cs` the INTERPRETED run behind `RunInterpreted`: the pc over the program that runs, `JUMP` with `IF`, `JUMP=END`, `RETURN`, `REPEAT`, the depth and the block cap, with the calls of subprograms and external programs in `VirtualMachine.Calls.cs` and the expressions of a block resolved before it executes in `ExpressionResolver.cs`. Then `WordHandlers.cs`, the table from a key to the handler that applies the word in step 3, and `ToolChangeRules.cs`, the tool change of virtual machine 3.5 as the transition table of architecture 5.2.

| File | Rule |
|---|---|
| `ResourceResolver.cs` | roles, default resources, axis names, functions, coolant channels, spindle rules 4 and 5 (virtual machine 3.8); the default machine without a file (D103) |
| `FrameRules.cs` | the frame chain and the setpos shifts (virtual machine 2.1, 3.4; D31, D35, D55, D101) |
| `MotionRules.cs` | `RAPID` and `LINE`: the targets of absolute and incremental words in the frame they program in, `UNITS` before the first motion, a feed for `LINE`, one form per axis (virtual machine 3.1; D35, D60, D99, D102) |
| `ArcRules.cs`, `ArcWords.cs`, `PlaneArc.cs` | `ARC` in its working plane with the geometry of `../Geometry/` (virtual machine 3.2; D36, D60, D84, D102) |
| `ToolVectorRules.cs` | the vector words `TX TY TZ`, `NX NY NZ` under `TCPM=ON` (virtual machine 2.2, 3.1; D81) |
| `RetractRules.cs` | `RETRACT` along the tool axis (virtual machine 3.1a, D83) |
| `CycleRules.cs`, `CycleMotion.cs` | `CYCLE_CALL`, the sequence of the drilling family and its motions under `ExpandCycles` (virtual machine 3.3; D37, D59, D94) |
| `HomeRules.cs` | `HOME` (virtual machine 3 step 5, D100) |
| `DiameterRules.cs` | diameter programming (language 4.2, D60) |
| `ProgramEndRules.cs` | what `PROGRAM=END` resets (virtual machine 4) |
| `VirtualMachine.Restore.cs`, `RestoreRules.cs` | the pseudo-words `@SAVE` and `@RESTORE` of generated blocks, between steps 1 and 2: one restore stack per state variable, the saved value re-applied as the words that set it (virtual machine 3.10, D95) |
| `VirtualMachine.Events.cs`, `Events/` | the events of step 7, `Subscribe` and the listeners, `Before` and `After` (virtual machine 7; architecture 5.3; D61, D106) |
| `BlockContext.cs`, `BlockFlow.cs`, `SectionExit.cs` | one block while it executes, where the flow goes after it, and how INTERPRETED mode leaves a program or subprogram |
| `VirtualMachine.Interpreted.cs`, `VirtualMachine.Calls.cs`, `ExpressionResolver.cs` | INTERPRETED mode: the flow, the calls with `ARG` and `TIMES`, external programs from the working directory, the expressions of a block (virtual machine 1, 2.7, 3.6, 3.9; D38, D51, D53) |
| `VmOptions.cs`, `SkipBlocks.cs`, `ExecutionMode.cs`, `RunResult.cs` | the options of a run and how it ended (D53) |
| `VirtualMachine.Steps.cs`, `StepResult.cs`, `SectionEnd.cs` | a run step by step: `Begin` starts one program on its channel, `Step` executes the next block the flow reaches; the walks of both modes hand out one step per executed block, also inside a call, so that the job scheduler of `../Jobs/` advances each channel by one block per round (virtual machine 3.7; architecture 5) |
| `VirtualMachine.Jobs.cs` | what the job scheduler asks of the virtual machine of a channel: the channels of the job for `SYNC` in a single-channel job, the waits at the marks with `SYNC_WAIT` and `SYNC_RELEASE`, the spindles and axes a block commands, the rest of a file after a STATIC job (virtual machine 2.8, 3.7, 7) |

`State/` holds the state of a channel and its snapshots, `Handlers/` the word handlers, one class per group, `Validation/` the validation list of virtual machine 5: one file per family with the rows of the table that `docs/spec/generated/diagnostics.md` is written from, and `RunValidation`, which checks the rules the steps do not raise themselves before and after each block and in a pre-pass over the file. `ExecuteMotion` hands each motion verb to its rules and keeps the resolved arc and the motions of an expanded cycle for the events of step 7 (`Events/`).

Never here: parsing (the program arrives parsed), the output of a controller, reading a machine file (the caller passes a loaded `MachineConfig`, or `DefaultMachine` of `Ncx.Config` without a file, D103).
