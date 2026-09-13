# P6-01 Job scheduler and `SYNC`

Phase: 6 | Milestone: M9 | Depends on: `P4-01` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

Several channels in one run, advanced in rounds, with rendezvous marks (D39).

## Scope

- One VM per channel program from the job manifest; rounds where every channel that is neither finished nor waiting executes one block; `SYNC=m` with `WITH` (default all), marks matched in execution order, `WAIT_CHANNEL`, `START_CHANNEL`; deadlock ERROR naming the marks; shared resources from `[shared]` with the WARNING for two channels on one spindle between marks.
- `ncx analyze --job <ncxjob.toml>` and `ncx check --job`.

## References

- ncx-virtual-machine.md section 3.7
- architecture.md section 5.4 (scheduler flowchart)
- machine-config.md section 8

## Done when

- A two-channel test job with three marks in different orders passes; a job whose channels wait at different marks reports the deadlock with both marks.
- The Nakamura WY250L pair reads (phase 3 reader plus the builder tables) and analyzes without deadlock.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
