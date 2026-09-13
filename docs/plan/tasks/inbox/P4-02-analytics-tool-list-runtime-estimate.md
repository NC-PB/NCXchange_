# P4-02 Analytics: tool list, runtime estimate

Phase: 4 | Milestone: M7 | Depends on: `P4-01` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The first two analytics as event subscribers in `Ncx.Analytics`.

## Scope

- Tool list: per TOOL_BEGIN/TOOL_END the tool, holder, speed range, cutting distance, rapid distance, block count, time.
- Runtime estimate: the trapezoidal profile of section 8 of the virtual machine with `max_feed`, `acceleration`, `[dynamics]` block time and `path_mode`, spindle `accel_time`, dwell, cycle sequences (with `ExpandCycles`); per tool and total; rapid at the axis maximum.
- `ncx analyze <file> [--machine <toml>] --from <line> --to <line>` with a text report (D67; `--machine` optional as for `check`, D103).

## References

- ncx-virtual-machine.md section 8
- architecture.md section 9 (analytics table)

## Done when

- The tool list of `2.5D_FRAESEN.ncx` names two tools with the right distances.
- The runtime of `3D_FRAESEN.fanuc.nc` (converted) is within 10 percent of the value the maintainer measured on the machine (ask for it); the estimate is documented as an estimate.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
