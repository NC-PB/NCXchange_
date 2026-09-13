# P0-06 Canonical writer and `ncx format`

Phase: 0 | Milestone: M1 | Depends on: `P0-05` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The one way an NCX program is written, so that two programs that mean the same thing are the same text (D43).

## Scope

- `NcxWriter`: canonical word order per section 5 rule 6 (the rank table of D90), numbers untouched, strings escaped, the block's comment last at column 57 (three spaces when the words end at column 54 or later), trivia lines written back as read (D92), `SUB` sections and programs in file order, generated blocks never written; a provisional machine axis word (D93) has no catalog entry and takes the rank of bucket 3 behind `X Y Z A B C`, absolute before incremental, alphabetical by letter then number, so the canonical text never depends on a machine file.
- `NcxBuilder` (fluent, used by readers later): `Begin(line).Verb(...).Word(key, addr, value)...End()`.
- `ncx format <file> [--check]`: parse, write, nothing else; it takes no machine file and does not run the VM, a bare `TOOL` stays bare (D91); exit codes per D97: 0 without ERROR, 1 with an ERROR, 2 for a usage error or an unreadable input (the missing-machine-file case of D97 cannot occur here); `--check` exits 1 when the output differs from the input; the shared `--strict` option (a WARNING exits 1) arrives with P1-07 and applies to `format` as well.
- The CLI project with System.CommandLine, the `ncx` root command (`format` has no `--machine` option, D91), exit codes and diagnostic output to stderr.

## References

- ncx-language.md section 5 rules 6 and 7, section 2 rule 7
- architecture.md sections 4 (NcxBuilder) and 10 (CLI table)

## Done when

- The five examples format to themselves byte for byte (line endings per the file); they were rewritten into the rank order and the comment of `PATTERN_LOOP.ncx:15` realigned in the D90 commit (D90, D92), so `ncx format` must produce an empty diff over `docs/spec/examples/*.ncx`.
- A hand-written block in free order formats into the canonical order and back to itself.

## Notes

This closes M1.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
