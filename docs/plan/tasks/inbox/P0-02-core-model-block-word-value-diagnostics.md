# P0-02 Core model: Block, Word, Value, Diagnostics

Phase: 0 | Milestone: M1 | Depends on: `P0-01` | Size: S (S: a day or two, M: up to a week, L: more)

## Goal

The immutable program model that every other component consumes.

## Scope

- `NcxProgram` (file name, blocks, sections of programs and subprograms, file begin and end blocks, diagnostics), `Section` (kind, name, number, channel, first and last block), `Block` (line, words, verb, skip and skip number, source text, `Find(key, addr)`, `Has(key)`, `Has(key, addr, value)`), `Word` (key, address, value), the value types (integer, decimal, identifier, list, string, expression, state key) as a small closed hierarchy or a discriminated record.
- `Block` keeps its trailing `Comment` and `NcxProgram` keeps `Trivia` (comment-only and blank lines with their line number and text) (D92).
- `Diagnostics` as a result object holding `Diagnostic` records with severity (ERROR, WARNING, INFO), file, line, code, message and `OriginLine` for a diagnostic on a generated block (D98, architecture 4); never exceptions for user-facing problems (guidelines section 6).
- `decimal` for coordinates and feeds (no `double` in the model, guidelines section 7), `CultureInfo.InvariantCulture` everywhere a number becomes text or text becomes a number.
- Records for the model, classes for state; `sealed` and `internal` by default.

## References

- architecture.md section 4 (core model class diagram)
- ncx-language.md sections 3 (lexical rules) and 4.13 (files, programs, subprograms)
- code-guidelines.md sections 6, 7

## Done when

- Unit tests construct blocks and words and read them back; a `Find` with an address distinguishes `OFFSET:LEN` from `OFFSET:RAD`.
- A diagnostic renders as `file(line): ERROR PAR012: message` (D98); one on a generated block as `file(line, from 12): ...`.

## Notes

Do not add behaviour to the model; the VM and the writers act on it.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
