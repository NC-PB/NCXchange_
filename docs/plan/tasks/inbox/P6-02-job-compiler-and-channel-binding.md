# P6-02 Job compiler and channel binding

Phase: 6 | Milestone: M9 | Depends on: `P6-01`, `P3-06` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

Compiling a job for a multi-path machine: the wait codes in the machine's form and the words that only one path may write.

## Scope

- Per channel a compile with the shared `TargetState` awareness; `SYNC` into `M1xx` with `P` path lists or bitmasks (`[sync] paths`), `start_mark`, groups; `WAITM(mark, channels)` on Siemens.
- Channel-bound functions (`channel = n`, `channels = "all"`): move the word to the owning channel's program or duplicate it into every channel behind a generated `SYNC` (D56); a single-channel compile of such a word is an ERROR.
- Output: one file per channel with the machine's naming (`O1000`, `O1000.P-2`; `_C1`/`_C2`).

## References

- ncx-virtual-machine.md section 3.8 rule 2a
- controller-mapping.md section 7
- machine-config.md sections 4 and 5 (`[sync]`, `[spindle_sync]`)

## Done when

- The Nakamura pair compiles back with its wait codes in the same order and the spindle synchronization words in the path that owns them (M9).
- This closes phase 6.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
