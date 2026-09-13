# P0-07 Folder READMEs and `reading-the-code.md`

Phase: 0 | Milestone: M1 | Depends on: `P0-06` | Size: S (S: a day or two, M: up to a week, L: more)

## Goal

The path through the code for the next person, who may not be a programmer.

## Scope

- One `README.md` per project folder: what lives here, the two or three files to open first, what never goes here.
- `docs/reading-the-code.md`: a guided tour from `Program.cs` through one `ncx format` run (extended to `convert` and `compile` in phase 3), file by file.
- The three extension levels (configuration, plugin, source) named on the front page of `docs/`.

## References

- code-guidelines.md section 10 (code for readers who are not programmers)

## Done when

- A reader who has never seen the repository can name the file that parses a word after ten minutes with the READMEs.
- The tour is checked against the code in the acceptance test project by a test that asserts the mentioned files exist.

## Notes

May be skipped for M1 per D78 and written at the end of phase 1 instead; not later.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
