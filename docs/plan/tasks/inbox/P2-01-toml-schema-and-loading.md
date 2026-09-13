# P2-01 TOML schema and loading

Phase: 2 | Milestone: M3 | Depends on: `P0-04` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

`Ncx.Config`: the machine file loaded with Tomlyn into the typed records of `Ncx.Core` (D107), every mistake reported with its line.

## Scope

- Records for every table of `machine-config.md`, in `Ncx.Core` (namespace `Ncx.Core.Machine`) and loaded by `Ncx.Config` (D107): `[machine]`, `[format]`, `[tool_change]`, `[home]`, `[setpos]`, `[roles]`, `[[resource]]`, `[[axis]]` with `programming`, `max_feed`, `acceleration`, `home` and `limits` in machine coordinates, `letter`, the `[positions]` table of named machine-frame positions (`tool_change`, `program_end`, ...) that expansion rules reach through `{position:NAME}` (D100), `[dynamics]`, `[diameter]`, `[spindle.ROLE]`, `[spindle_mode.ROLE]`, `[spindle_sync]`, `[workpiece]`, `[sync]`, `[coolant]`, `[func]`, `[func_meta]`, `[transform]` with the tilt, retract and rotary keys, `[retract]`, `[tolerance]`, the expansion rule keys, `[cycles]` and `[[cycle]]`, `[variables]`, `[system_variables]`, the optional `tool_table` and `[[node]]` tree (loaded, not interpreted).
- Validation: unknown keys are WARNINGs (the schema is a sketch until the compiler exists), missing required keys and wrong types are ERRORs, more than one resource of a kind without a default is an ERROR (VM 3.8).
- `DefaultMachine` (D103): a `MachineConfig` built in code with one work spindle `MAIN` with the C axis, one tool holder `TOOL` with the tool spindle `TOOL`, axes `X Y Z A B C` without limits and without reference points, coolant channel `STANDARD`, no named functions, the arc tolerance of D36 and units `MM`; `check`, `trace`, `annotate` and `analyze` use it when no machine file is given.
- Function values are strings; a bare integer is accepted and means `M` followed by the number (the one exception to the wrong-type ERROR above). The loader normalizes every M or G code to its canonical spelling without leading zeros (`8`, `08`, `M8`, `M08` become `"M8"`, `G01` becomes `"G1"`), the compiler always writes the normalized form, readers compare codes by number (D105).
- The job manifest (`<name>.ncxjob.toml`) with `[job]` naming the machine, `[shared]` listing the shared spindles and axes, and `file` and optional `program` per channel (D48), and the vars file.

## References

- machine-config.md sections 1 to 9
- architecture.md section 6 (MachineConfig class diagram)

## Done when

- The five example machine files (with `millturn1.toml`, D104) load without ERROR; the WARNINGs, if any, name real gaps in the files (fix the files).
- A file with a typo in a key reports the line and the nearest known key.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
