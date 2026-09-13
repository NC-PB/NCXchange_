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

Claude (agent), 2026-09-13. Built on `main` with the model of P0-02, the word catalog of P0-03 and the expression parser of P0-05. Files in `src/Ncx.Core/Parsing/`: `Parser` (public: `Parse(text, fileName, options)` for a file, and `ParseBlock(text, line, options, diagnostics)` for one line, which the expander needs for the text of generated blocks), `ParserOptions` (public record: `AllowPseudoWords`, false by default; `KeepSourceText`, true by default), `Lexer` (the line ending, the lines, the comment and the words of a line), `WordLexer` (one word into key, address and value form), `LexedLine`, `LexedWord`, `BlockRules` (rules 1, 2, 4, 5 of language 5), `StructurePass` and `FileStructure`. Codes PAR001-PAR035 in `src/Ncx.Core/Model/DiagnosticCodes.Parsing.cs`, the part file that the header of `DiagnosticCodes.cs` prescribes. Tests in `tests/Ncx.Core.Tests/Parsing/`. Decisions implemented: D90 (the `NAME` of `SUB=BEGIN`, through the catalog), D92 (trivia, the trailing comment), D93 (machine axis words), D94 (native parameters), D95 (pseudo-words under `AllowPseudoWords`, `StateKeyValue`), D96 (`CYLINDER`, through the catalog), D98 (the codes). The specification is unchanged; everything below is a reading of it.

- The lexer tells the value type of a value by its form (integer, decimal, identifier, list, string, expression, and a state key on a pseudo-word only), as one flag of `ValueKinds`; the parser converts it into the value record and checks the word with `WordCheck.Accepts`, which applies `ValueKindsOf` with the address and the partner word of the block (the P0-03 log).
- `ExprValue.Text` holds the canonical print of the tree (`ExprNode.ToCanonical()` of P0-05), so `ncx format` normalizes the spacing inside braces; an expression the grammar does not take keeps its text as written, with no tree, after the ERROR of the expression parser.
- A malformed word, a pseudo-word in a user file and a number out of range are reported and left out of the words; a key the catalog does not know stays in the words without a definition, as `CanonicalOrder` expects. A block with an ERROR keeps its source text whatever `KeepSourceText` says (architecture 4.1). `Block.SourceText` and `Trivia.Text` are the whole line without its line ending.
- The first LF of the file decides the line ending (CRLF when a CR stands before it); a later line that ends otherwise is one WARNING, PAR001, on that line. A text without a line break has none (null; the writer takes LF). A CR that is not part of a CRLF is whitespace.
- A backslash in a string that escapes neither a quote nor a backslash is PAR005, because the EBNF of language 3 names only these two escapes. A number with a plus sign or with a leading or trailing dot is PAR003 (language 2 rule 5, the number forms of language 3).
- A machine axis word and a native parameter take no address and a number or an expression; they report the catalog codes of the same rules, PAR150 and PAR153. A key of the machine-axis form under no verb that carries axis words is an ERROR of rule 2, PAR012, never an unknown key, because the phase plan counts it as an axis word.
- Rule 2: an axis word needs a verb that carries axis words (`TakesAxisWords`); RETRACT carries none (PAR012). SHIFT, TILT, TILT_AXIS and SETPOS carry their own axis words, the words that name an axis (`X Y Z A B C`, `IX` to `IC`, the machine axis words of D93); `CENTER`, `R`, `ANGLE`, `TX TY TZ` and `NX NY NZ` name no axis and require a motion verb under them as well (PAR012). Under HOME every axis word is a bare axis name, `X Y Z A B C` or a machine axis name without the `I` (PAR013); under any other verb an axis word has a value (PAR014).
- Rule 5: FRAME needs a motion verb, `RAPID`, `LINE`, `ARC`, `RETRACT`, `HOME` or `CYCLE_CALL`; MOVE and ROT accept `TILT` and `TILT_AXIS` in any form, their reset forms included.
- Structure: the blocks of the file frame and of the section frames hold the words of the EBNF only (PAR025): `FILE=BEGIN` with `NCX`, `FILE=END` alone, `PROGRAM=BEGIN` with the header words `NAME`, `NUMBER` and `CHANNEL` of 4.1, `PROGRAM=END` alone, `SUB=BEGIN` with `NAME`, `SUB=END` alone. A section still open when another BEGIN block, `FILE=END` or the end of the file comes is closed before it, with PAR030 or PAR032 on that line; a `SUB=BEGIN` inside a program is PAR027 and the subprogram is kept as a section of its own; an END block outside its section is PAR031 or PAR033. Programs and subprograms share one set of names (`NAME=100` and `NAME="100"` are one name), because `CALL` finds a section by its name and a `CALL` of a program is an ERROR (virtual machine 3.6). `Number` and `Channel` of a section come from its `PROGRAM=BEGIN` block only; a subprogram has neither.
- The diagnostics of the blocks come in line order; those of the file structure follow them.

Catalog fix (shared file): `PRELOAD` takes the optional holder role, as `TOOL` does (virtual machine 2.3, `PRELOAD[:r]`; 3.8 rule 2; language 4.10); the row of language 4.4 names no address, and without the role `PRELOAD:TURRET1=5` was an ERROR. `docs/spec/generated/word-catalog.md` is regenerated by `WordCatalogTableTests`.

Open questions, marked `TODO(question)` in the files:

1. `Lexer.cs`: language 3 lets a semicolon outside a string start the comment, while architecture 4.1 and the phase plan keep the braces of an expression together as well; the braces are kept together. A semicolon inside braces is an ERROR either way.
2. `BlockRules.cs`: the phase plan pairs `ARG` with `CALL` or `REPEAT`, language 4.9 with `CALL` only; `ARG` is accepted with either.
3. `BlockRules.cs`: the Scope column of language 4 names a partner for `NAME`, `NUMBER`, `CHANNEL`, the cycle parameters and `TOLERANCE:ROTARY`, which rule 5 and virtual machine 5 do not list; only the words of rule 5 are checked. (The P0-03 log expected the parser to report a `NAME` without its partner.)
4. Inherited from P0-03 (`WordCatalog.IsNativeParameterAllowed`): the parameter names of a cycle catalog entry, `CYCLE=RECT_POCKET LENGTH=60 WIDTH=40` of language 4.7.1, are unknown keys for the parser and an ERROR.
5. `BlockRules.cs` (review fix): rule 2 gives SHIFT, TILT, TILT_AXIS and SETPOS "their own axis words", and the rows of 4.2 name the spatial angles A, B, C for TILT and the rotary axis angles for TILT_AXIS; neither says whether a linear axis word under TILT or TILT_AXIS (`TILT X=1`) is an ERROR, nor whether the four take the incremental forms (`SHIFT IX=5`). Every axis name, absolute or incremental, standard or of the D93 form, is accepted under the four; only `CENTER`, `R`, `ANGLE` and the vector words are refused.

Done when:

- The five examples parse without diagnostics: holds (`ExampleParsingTests`, which also checks that every line of each example is one block or one trivia line).
- One test per structural ERROR of the validation list with a two-line input that triggers it: holds (`StructurePassTests`: missing and misplaced `FILE=BEGIN`, `NCX`, `FILE=END`, `PROGRAM=BEGIN`, `PROGRAM=END`, `SUB=BEGIN`, `SUB=END`, a block outside every section, a `SUB` inside a `PROGRAM`, a file without a program, a duplicate section name, `LABEL=END`, a `NAME` missing on `SUB=BEGIN`, an unknown `NCX` version, each with two lines; `PROGRAM=END` twice in one program needs three). One test per block rule in `BlockRulesTests`, the value forms of F8 in `ValueFormTests`, trivia in `TriviaTests`, the lexical rules in `LexicalRulesTests`, `@SAVE` with and without the option in `PseudoWordTests`.
- A file with two programs and one subprogram yields three sections with the right block ranges: holds (`StructurePassTests.Language413_TwoProgramsAndOneSubprogram_AreThreeSections`).

Gate: `dotnet build -warnaserror` with 0 warnings, 967 tests passing (684 in `Ncx.Core.Tests`), `dotnet format --verify-no-changes` clean.

Review fixes, Claude (agent), 2026-09-13: rule 2 under the frame verbs. SHIFT, TILT, TILT_AXIS and SETPOS accepted every axis word, `SHIFT R=5`, `TILT CENTER:X=1 B=45` and `SETPOS TX=1` among them; they now carry the words that name an axis only, and `CENTER`, `R`, `ANGLE`, `TX TY TZ` and `NX NY NZ` under them are PAR012 with a message that names the motion verbs `RAPID`, `LINE`, `ARC` and `CYCLE_CALL` (`BlockRule2_ArcOrVectorWordUnderAFrameVerb_IsError`; `SETPOS Z2=0` and `SHIFT Z2=-5` added to the accepted cases of D93). Whether TILT and TILT_AXIS also refuse a linear axis word, and whether the four take the incremental forms, is open question 5. The "Done when" criteria hold as above. Gate: `dotnet build -warnaserror` with 0 warnings, 974 tests passing (691 in `Ncx.Core.Tests`), `dotnet format --verify-no-changes` clean.
