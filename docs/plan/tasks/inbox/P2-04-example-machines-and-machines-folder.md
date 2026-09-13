# P2-04 Example machines and `machines/` folder

Phase: 2 | Milestone: M3 | Depends on: `P2-03`, `P1-07` | Size: S (S: a day or two, M: up to a week, L: more)

## Goal

The five example machine files become the shipped machine files, verified against the loader and the templates.

## Scope

- Verify `millturn1.toml` (the fifth example machine, a generic SINUMERIK 840D sl mill-turn for `MILLTURN_TRANSFER.ncx`, written for D104) against the loader and the templates like the other four; copy the five files from `docs/spec/examples/machines/` to `machines/`, fix what the loader reports, keep the copies identical (a test compares them).
- Every axis gets `home` and `limits` in machine coordinates and the dynamics keys (`rapid`, `max_feed`, `acceleration`, `[dynamics]`, `rpm_min`, `rpm_max`, `accel_time`) with plausible values marked "not verified on the machine", and every file a `[positions]` table (D100).
- A plain `fanuc-mill-30i.toml`, `heidenhain-itnc530.toml` and `siemens-840dsl-mill.toml` for the readers and compilers of phases 3 and 5 (generic words only).
- `ncx` finds machine files by name in `machines/` and by path.

## References

- machine-config.md section 11
- controllers/machine-builders.md section 3

## Done when

- `ncx check examples/2.5D_FRAESEN.ncx --machine heidenhain-itnc530` runs.
- The five examples check with no ERROR with their machine files (`MILLTURN_TRANSFER` with `millturn1`, `POLAR_FACE` with `nakamura-ntjx`) (D103, D104).
- This closes M3 and phase 2.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

- Claude (agent), 2026-09-13, part one (P2-04a, branch `p2-04a-machine-data`): the D100 data in the four builder files of `docs/spec/examples/machines/`: `home` and `limits` in machine coordinates, `rapid`, `max_feed`, `acceleration` on every `[[axis]]`, `rpm_min`, `rpm_max`, `accel_time` on every spindle table, a `[positions]` table with `tool_change` and `program_end`, a `[dynamics]` table, all marked as plausible and not verified on the machine; the header comment that deferred the data to P2-04 replaced; `nakamura-ntjx.toml` declares the axis `C2` of resource `S2` (F23). `machines/fanuc-mill-30i.toml`, `machines/heidenhain-itnc530.toml`, `machines/siemens-840dsl-mill.toml` written as plain three-axis mills with the generic words of their controller files and the D100 data (the reference point is the machine zero, so the `G91 G28 Z0` of the Fanuc sources and the `M91` moves of the Heidenhain sources end at the same place, 2.5D note 4). Every file parses. Nothing was decided; the gaps are `TODO(question)` comments in the files: whether `home`, `limits` and `[positions]` of a diameter-programmed X axis are diameters or radii; the address of the Nakamura sub spindle C axis on the 18i-TB; `{tool}` against `{next}` in the preload templates of machine-config 3; `{rotary}` of the tolerance templates on a machine without rotary axes; the role of a mill's tool holder; `TOOL CALL S` on the iTNC 530 and a generic `ORIENT` for Heidenhain; a template for named tools on Siemens; the Siemens variable map and the Heidenhain system variables. Remains for part two: copy the five example files to `machines/` with the identity test, the loader and `check` runs of the acceptance, `--machine` resolution in `Ncx.Cli`, `ncx.toml`. Found on the way: `doosan-puma-2600sy.toml` names `axis = "C2"` on resource `S2` without an `[[axis]]` `C2` (the F23 gap of the Nakamura file) and was left as it is.
