# P2-02 Templates both ways

Phase: 2 | Milestone: M3 | Depends on: `P2-01` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

Render a template from NCX words and match native text back to NCX words with the same template.

## Scope

- `Template` with the placeholders of the machine-config introduction (`{tool}`, `{offset}`, `{next}`, `{kind}`, `{rpm}`, `{axis}`, `{axes}`, `{angle}`, `{b}`, `{c}`, `{name}`, `{mark}`, `{channels}`, `{paths}`, `{r}`, `{d}`, `{value}`, `{index}`, `{a}`, `{x}`, `{move}`, `{dir}`, `{distance}`, `{tol}`, `{rotary}`, `{mode}`) and the format suffixes (`{tool:02}`, literal decimal points).
- `Render(values)` for the compilers and `Matches(text)` for the readers (a template becomes a regular expression with named groups; an M or G code in the literal text matches by number, so the normalized `M8` of the table matches a source `M08` (D105); the reader tries the tables of its machine before its generic rules).
- Per-role tables (`[spindle.MAIN] CW = "M3"`), the `channel`/`channels` binding kept as data for the job compiler (D56).

## References

- machine-config.md introduction (placeholders), sections 3 and 5
- architecture.md section 6

## Done when

- Round trip: every template in the five example files (with `millturn1.toml`, D104) renders from a sample word set and matches back to it.
- `G340 T{tool:02}{offset:02}. A{next:02}.` renders `G340 T0101. A02.` and matches it.
- A table value `M8` matches the source lines `M8`, `M08` and `M008`; `G1` matches `G01` (D105).

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
