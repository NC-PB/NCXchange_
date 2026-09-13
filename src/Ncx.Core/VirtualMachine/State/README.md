# State

The state of one channel, one class per table of virtual machine 2, with the start values the tables give: `ChannelState` (2.3, 2.5, 2.8, and the parts below), `ProgramState` and `FrameState` (2.1), `MotionState` (2.2), `HolderState` (2.3, one per tool holder), `SpindleState` (2.4, one per spindle), `CycleState` (2.6), `FlowState` (2.1, 2.7, with the restore stack of 3.10), `VariableStore` (2.7). The classes are mutable and internal; only the virtual machine changes them (code-guidelines 7).

`ChannelState.Snapshot()` returns a `ChannelSnapshot`, an immutable deep copy made of the public records `ProgramSnapshot`, `FrameSnapshot`, `MotionSnapshot`, `HolderSnapshot`, `SpindleSnapshot`, `CycleSnapshot` and `FlowSnapshot`: the Before and After of every event, which a listener reads and never changes the machine through (virtual machine 7; architecture 5.3; D61, D106). The small immutable parts (`AxisPosition`, `TransformEntry`, `SetposRecord`, `ToleranceState`, `CallFrame`, `RepeatFrame`, `RestoreEntry`, `VariableValue`) and the enumerations of the closed sets (`Verb`, `Units`, `Workplane`, `SpindleDirection`, `ToolChangeState`, ...) are shared by the state and its snapshots.

Start with `ChannelState.cs`, then `HolderState.cs` next to `../ToolChangeRules.cs`, and `FrameState.cs` for the transform chain of D31.

Never here: the rules that change the state; they are in `../Handlers/` and in the rule files of `../`.
