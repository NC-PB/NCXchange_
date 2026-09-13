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

Claude (agent), 2026-09-13, part one (P2-02a: parse, render and match). Built on `main` with P0-01, P0-02, P2-04a and P2-03a merged; P2-01 (the loader and the records of `Ncx.Core.Machine`) is written in parallel, so this part works on template text only. Files in `src/Ncx.Config/Templates/`: `Template`, `Placeholder`, `TemplateValues`, `TemplatePattern` (internal, the reader side) and `DiagnosticCodes.Templates.cs` (`namespace Ncx.Config`, `public static partial class DiagnosticCodes`, codes CFG100-CFG149). Tests in `tests/Ncx.Config.Tests/Templates/`. The P0-01 skeleton `src/Ncx.Config/Placeholder.cs` is deleted as its TODO asks; it would also have hidden `Ncx.Config.Templates.Placeholder` from every namespace under `Ncx.Config`. Decisions implemented: D105 (codes compared by number), D98 (the codes, the ERROR on the block with its origin line), D100 (`{position:NAME}` parsed), D107 (the template text is parsed in `Ncx.Config`). The specification is unchanged; what follows is a reading of it.

- `new Template(text, line, diagnostics)` parses once (code-guidelines 7) into the literal text between the placeholders and a `Placeholder` list (`Name`, `Format` as written, `Width`, `IsText`). A line break stays in the literal text: `Render` writes it, and the pattern needs a line break there (CR LF accepted), so a two-line template matches two lines and not one. Braces that are not a placeholder are an ERROR of the machine file, reported on the line given, and stay in the text as literal braces: CFG101 `TemplatePlaceholderMalformed` (a `{` never closed, a `}` never opened, braces around no name), CFG102 `TemplateFormatUnknown` (a suffix other than a zero followed by the width).
- `{position:NAME}` is a placeholder named `position:NAME` with no format, because one expansion rule may name two positions; its value is the axis words. Any other name is accepted, the unlisted `{point}`, `{order}`, `{channel}`, `{radius}`, `{pair}`, `{ratio}` of machine-config 3 and 5 and of the example files included.
- `TemplateValues` holds numbers (`decimal`, an `int` passes through the same `Set`) and words by placeholder name; no `object` in a public signature (code-guidelines 7).
- `Render(values, block, diagnostics)` writes words as given and numbers with the invariant point, padded with zeros to the width of the suffix (`{tool:02}` 4 gives `04`, `{tool:02}.` gives `04.`). A missing value is CFG100 `TemplateValueMissing` on the block (so a generated block carries its origin line, D98), once per placeholder name, naming the placeholder and the template, and `Render` returns null: the template is unusable (machine-config introduction). Messages quote the template the way the TOML file writes it (`\n`, `\"`), so the user can search for it.
- `Matches(text, out captured)` matches the whole text, anchored at both ends, and gives the values back as `TemplateValues` (architecture 6 draws a string dictionary; numbers come back as numbers, so `M0106` gives the mark 106). Blanks in the template match any run of blanks or tabs, none included (`G96S120`, `G96  S120`); blanks at the ends are ignored (`G411F12. `, `M106 `). An M or G code in the literal text (the letter at the start of a word, not after a letter or underscore, followed by digits) matches by number with any leading zeros, and a G code keeps its decimal part (`G7.1`). A padded placeholder reads exactly its width of digits (so `T0101` splits 01 and 01); a placeholder whose value is words reads as few characters as possible up to the next literal text on the same line; every other placeholder reads a signed decimal (`70.`, `-.534`, `+10`). A placeholder that stands twice must read the same value.
- Open questions, marked `TODO(question)` in the files: whether malformed template text is an ERROR (the documents name only the missing value); the `Literal` member that architecture 6 draws on `Placeholder`; placeholder names the introduction does not list; which placeholders are words (the documents give no value kind, and the phase file's "a signed decimal group otherwise" cannot capture `{name}`, `{axes}`, `{move}`, `{channels}`; words are `name`, `axis`, `axes`, `move`, `channels`, `position:*`, and `{kind}` is a number like both example mappings); a padded placeholder against a source without its leading zero (Fanuc `T101`) and a number wider than its suffix, which renders unpadded and does not match back. `TODO:` in `TemplatePattern`: the number group reads the decimal point only; the comma of Heidenhain files (heidenhain.md 1, 7) needs the separator of `[format]`, which `TemplateSet` can pass in part two.

Done when:

- `G340 T{tool:02}{offset:02}. A{next:02}.` renders `G340 T0101. A02.` and matches it: holds (`Render_NakamuraToolChange_WritesPaddedNumbersAndLiteralPoints`, `Matches_NakamuraToolChange_CapturesToolOffsetAndNext`).
- A table value `M8` matches `M8`, `M08` and `M008`; `G1` matches `G01` (D105): holds for `Template.Matches` (`Matches_M8_MatchesEverySpellingOfTheNumber`, `Matches_G1_MatchesEverySpellingOfTheNumber`); `TemplateSet.FindFunctionByCode` waits for part two.
- Round trip over every template in the five example files: waits for part two (it needs P2-01's loader). Meanwhile every template the specification text writes out (machine-config introduction, 3, 5, 5a, 7, with the forms of the example files) renders from sample values and matches back to them (`Matches_TemplateOfTheSpecification_ReadsBackWhatItRenders`), and lines copied from `NAKAMURA_WY250L_O1000.path1.nc` match with their spacing and trailing blanks (`TemplateNakamuraLinesTests`).

Remains for part two (after P2-01 is merged): `TemplateSet` (the parsed templates of one machine built from the text of its records, `For(text)`, `FindFunctionByCode`, D105, D107), reporting template errors with the line of the machine file, the per-role tables and the `channel`/`channels` binding kept as data (D56), the decimal separator of `[format]` for the pattern, and the table-driven round trip over the five loaded machine files.

Gate: `dotnet build -warnaserror` with 0 warnings, 255 tests passing (177 in `Ncx.Config.Tests`), `dotnet format --verify-no-changes` clean.
