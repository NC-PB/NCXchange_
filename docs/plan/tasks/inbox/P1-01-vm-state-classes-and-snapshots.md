# P1-01 VM state classes and snapshots

Phase: 1 | Milestone: M2 | Depends on: `P0-06`, `P2-01`, `P2-02` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The mutable state of one channel exactly as section 2 of the virtual machine lists it, with a cheap snapshot for Before and After.

## Scope

- `ChannelState` with `ProgramState`, `FrameState` (units, workplane, origin, the transform chain as an ordered list of `TransformEntry`, setpos shifts, diameter, cylinder (the reference radius or OFF, D96), polar, tcpm, rotary path and feed, tolerance, workpiece holder, machine-frame block flag), `MotionState` (positions with frame and known flag, feed and mode, compensation, block verb, tool vector and surface normal), `HolderState` (the tool as a number or a name, language 4.4), `SpindleState`, `CycleState`, `FlowState`, `VariableStore`, coolant and function tables.
- `Snapshot()` returns an immutable copy (records or a copy-on-write list), used for `Before`/`After` on every event.
- Initial values per the tables of section 2, from the machine configuration where the table says so (the `MachineConfig` loaded by P2-01, or the `DefaultMachine` of D103 when no file is given).

## References

- ncx-virtual-machine.md section 2 (every table)
- architecture.md section 5 (VM class diagram)

## Done when

- A test sets every variable and reads it back from a snapshot while the live state changes.
- The start position rule holds: an axis with `home` in the configuration starts known in the MACHINE frame at that reference point and unknown in the workpiece frame; an axis without `home` starts unknown in every frame (VM 2.2, 3.4, D35, D100).

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
