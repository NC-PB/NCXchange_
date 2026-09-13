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
