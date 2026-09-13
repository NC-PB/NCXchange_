# Handlers

The state words of step 3 of virtual machine 3, one class per group, each a list of `handlers["KEY"] = Apply...` lines in its `Register` method and one method per word (code-guidelines 5, table-driven dispatch): `FrameHandlers` (language 4.2, and the units and path tolerance of 4.1; virtual machine 2.1), `MotionHandlers` (`F`, `FEED_MODE`; 2.2), `ToolHandlers` (4.4; 2.3, 3.5), `SpindleHandlers` (4.5, and `CSS`, `VC`, `RPM_MAX` of 4.11; 2.4), `FunctionHandlers` (4.6; 2.5), `CycleHandlers` (the cycle definition of 4.7; 2.6), `VariableHandlers` (`VAR` of 4.9; 2.7), `ResourceHandlers` (`WORKPIECE` of 4.10). `../WordHandlers.cs` collects them.

To see what `SPINDLE` does, open `SpindleHandlers.cs` and search for `"SPINDLE"`. A new state word is its catalog entry plus one registration line and one method in the class of its group; the virtual machine does not change for it (code-guidelines 4, open/closed).

Never here: the verbs and their axis words (steps 4 and 5 in `../VirtualMachine.cs`), the flow words (`ApplyFlowWords` and the STATIC walk), resolving a role (step 2 did it; a handler reads the resource from `BlockContext`).
