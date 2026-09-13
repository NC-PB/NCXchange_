# P0-04 Lexer and parser

Phase: 0 | Milestone: M1 | Depends on: `P0-03` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

Text to `NcxProgram`, validated against the catalog, with the file structure resolved.

## Scope

- Lexer per section 3 of the language: one block per line, whitespace-separated words, `KEY[:ADDR][=VALUE]`, `;` comments outside strings kept as the block's comment, comment-only and blank lines collected as trivia (D92), strings with escapes, `{...}` expressions kept as text for the expression parser, lowercase normalized; a key starting with `@` only under the option the expander uses for generated text, in a user file the ERROR "pseudo-word in a user file" (D95).
- Parser: an unknown key of the machine-axis form in a block whose verb takes axis words is a provisional machine axis word (D93), an unknown key in a block with `CYCLE:<controller>=n` is a native parameter kept in source order (D94), any other unknown key, wrong value kind, duplicate key, two verbs, axis words without a verb, `IF`/`ARG`/`TIMES`/`WITH`/`MOVE`/`ROT`/`POINT`/`PHASE` without their partner: ERROR with its `PAR` diagnostic code (D98).
- File structure pre-pass: `FILE=BEGIN NCX=1` as the first block and `FILE=END` as the last (comment-only and blank lines may stand before and after them as trivia, D92), `PROGRAM=BEGIN` ... `PROGRAM=END` and `SUB=BEGIN NAME=` ... `SUB=END` sections, a block outside every section, a `SUB` inside a `PROGRAM`, a file without a program, duplicate section names, `LABEL=END`: ERROR (VM section 5 lists them).
- The parser never throws for user input; it returns the program with diagnostics.

## References

- ncx-language.md sections 3, 4.1, 4.13, 5
- ncx-virtual-machine.md section 5 (validation list, the structural part)
- architecture.md section 4.1 (parsing)

## Done when

- The five examples parse without diagnostics.
- One test per structural ERROR of the validation list with a two-line input that triggers it.
- A file with two programs and one subprogram yields three sections with the right block ranges.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
