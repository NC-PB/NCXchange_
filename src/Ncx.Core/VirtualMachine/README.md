# VirtualMachine

The virtual machine executes the blocks of a program against the state of a channel and reports what it finds, so that readers, compilers and analytics know what the program means at every block (virtual machine 1; architecture 5). STATIC mode walks every program of the file and follows the calls of its subprograms (D99); INTERPRETED mode comes with P4-01.

Start with `VirtualMachine.cs`: `Execute(block)` runs the seven steps of virtual machine 3, one private method each, `ParseAndValidate`, `ResolveRolesAndAxes`, `ApplyStateWords`, `ApplyFrameVerb`, `ExecuteMotion`, `ResetBlockScope`, `RaiseEvents`, with `ApplyFlowWords` for the flow words of architecture 5.1 between steps 5 and 6. `VirtualMachine.Static.cs` is the STATIC walk behind `Run`. Then `WordHandlers.cs`, the table from a key to the handler that applies the word in step 3, and `ToolChangeRules.cs`, the tool change of virtual machine 3.5 as the transition table of architecture 5.2.

| File | Rule |
|---|---|
| `ResourceResolver.cs` | roles, default resources, axis names, functions, coolant channels, spindle rules 4 and 5 (virtual machine 3.8); the default machine without a file (D103) |
| `FrameRules.cs` | the frame chain and the setpos shifts (virtual machine 2.1, 3.4; D31, D35, D55, D101) |
| `HomeRules.cs` | `HOME` (virtual machine 3 step 5, D100) |
| `DiameterRules.cs` | diameter programming (language 4.2, D60) |
| `ProgramEndRules.cs` | what `PROGRAM=END` resets (virtual machine 4) |
| `BlockContext.cs`, `BlockFlow.cs` | one block while it executes, and where the flow goes after it |
| `VmOptions.cs`, `SkipBlocks.cs`, `ExecutionMode.cs`, `RunResult.cs` | the options of a run and how it ended (D53) |

`State/` holds the state of a channel and its snapshots, `Handlers/` the word handlers, one class per group. `ExecuteMotion` executes `HOME`; the other motion verbs are P1-03's, the validation list P1-04's, the events P1-05's.

Never here: parsing (the program arrives parsed), the output of a controller, reading a machine file (the caller passes a loaded `MachineConfig`, or `DefaultMachine` of `Ncx.Config` without a file, D103).
