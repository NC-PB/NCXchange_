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

Claude (agent), 2026-09-13. Built on the P0-01 skeleton on `main`. `src/Ncx.Core/Model/`, one type per file: `NcxProgram`, `Section`, `SectionKind`, `Trivia`, `LineEnding`, `Block`, `Word`, `Value` with `NoValue`, `IntegerValue`, `DecimalValue`, `IdentValue`, `ListValue`, `StringValue`, `ExprValue`, `StateKeyValue`, `Diagnostics`, `Diagnostic`, `Severity`, `DiagnosticCodes`, `ToolRef`. By assignment, the shapes the parallel tasks start from, in their folders: `src/Ncx.Core/Catalog/WordDefinition.cs` with `WordKind`, `ValueKinds`, `AddrKind`, `Scope` and no entries (P0-03 fills them), and the abstract `src/Ncx.Core/Expressions/ExprNode.cs` (P0-05 adds the nodes). Tests in `tests/Ncx.Core.Tests/Model/`. `src/Ncx.Core/Placeholder.cs` is deleted, as its TODO asked. Decisions implemented: D92 (`Trivia`, `Block.Comment`), D95 (`StateKeyValue`, `WordDefinition.IsInternal`), D98 (`Diagnostic` with `OriginLine`, the rendering, the severities, `DiagnosticCodes`), D106 (`Find` and `Has` as public lookups for plugins). The specification is unchanged; everything below is a reading of it or a repository convention.

- The model types are public sealed records, because plugins and the other projects consume them (D106); `Diagnostics` is a sealed class, because it collects (code-guidelines 3.4: a class for state that changes); `ToolRef` is a readonly record struct with a number or a name, `ToString()` writing the number or the name as an NCX string. `StateKeyValue` is public like the rest; "internal" (D95) is the language's word for a value only generated blocks carry, and the parser enforces it.
- `NcxProgram.Sections` is the stored list in file order; `Programs` and `Subs` of the architecture 4 diagram are lookups over it. `Section.FirstBlock` and `LastBlock` are indexes into `Blocks`, both inclusive; `Name` is the content of a string, an identifier or the digits of an integer; `Number` is null without `NUMBER`; `Channel` is 1 without `CHANNEL` (language 4.1). `LineEnding` is `Lf` or `CrLf`, null for a program built by a reader.
- `Block.Verb` is stored, because the parser decides which word is the verb (`SHIFT=RESET` ranks as a state word, D90 bucket 12); `Skip` and `SkipNumber` are read from the `SKIP` word among `Words`, which has its own rank (D90 bucket 1), so the writer finds it there and the block does not hold it twice. `Find(key)` is the first word of the key whatever its address; `Find(key, addr)` the word with exactly that address, null meaning none; `Has(key, addr, value)` compares the value as canonical NCX writes it, numbers as written (`"10.50"` is not `"10.5"`), a string with its quotes, a bare word as the empty text.
- `Block.Comment` is the comment as the EBNF of language 3 defines it, from the semicolon to the end of the line, the semicolon included, as read; `Trivia.Text` is the whole line as read.
- `Value.ToCanonical()` is the text after `=` for every value (architecture 4 draws `Value.Text`; the phase file's `ToCanonical()` covers it, and `Text` stays on `IntegerValue`, `DecimalValue` and `ExprValue`). `ListValue` compares by its items. `ExprValue` holds `Text` and a nullable `Tree` and writes `{Text}`, so whether `format` normalizes the spacing of an expression is decided by what P0-04 stores as `Text` (P0-05's canonical print or the source).
- `Diagnostics` is constructed with the file name. `Error`, `Warning` and `Info` take a line (architecture 4) or a block; the block forms copy `Block.OriginLine` into `Diagnostic.OriginLine`, which is where the copy rule of D98 lives. `Add(Diagnostic)` takes one about another file (a called program, virtual machine 2.9). `ToText()` ends every line with LF.
- `DiagnosticCodes` is a public static partial class. Its main part defines no code, because the model raises none and the rendering tests use literal codes; its header comment gives the D98 form and the ranges: PAR001-PAR099 lexer, parser and structure pass (P0-04), PAR100-PAR149 expressions (P0-05), PAR150-PAR199 word catalog (P0-03), VM001-VM199 block execution (P1-02), VM200-VM399 motion, arcs, retract, home, cycles (P1-03), VM400-VM899 validation and the rest of phase 1 (P1-04 onward), VM900-VM949 expression evaluation (P4-01). Each component adds `DiagnosticCodes.<Component>.cs` next to it. `DiagnosticCodesTests` checks every constant of every part for `PAR` or `VM` and three digits, and that no two share a code.
- `WordDefinition` has the shape of the phase file (Key, Group, ValueKinds, AllowedIdents, IsVerb, TakesAxisWords, AddrKind, Scope, CanonicalRank, Description) plus `IsInternal` of architecture 4 as amended by D95, an init property that the catalog sets on `@SAVE` and `@RESTORE`. `Group` is of the architecture's `WordKind`. `ValueKinds` is `[Flags]` with `Bare` for a word without a value (F8) and `Number`, `NumberOrExpr` as combinations; empty `AllowedIdents` means any identifier. CA1720 reads the members `Integer`, `Decimal`, `String` as .NET type names; they keep the names of language 3 and the rule is disabled around that enum with the reason.
- Open questions, marked `TODO(question)` in the files: `AddrKind` keeps the seven kinds of architecture 4, which have no kind for the addresses of `FUNC` (a function name, 4.6), `ARG` (an argument name, 4.9) and `TOLERANCE:ROTARY` (4.1); `Scope` keeps the four scopes of architecture 4, which have no "program" (4.1, `PROGRAM=BEGIN`, `PROGRAM=END`) and no form for the rows that name a partner word. The word catalog (P0-03) meets both when it records these words.

Done when:

- Unit tests construct blocks and words and read them back; a `Find` with an address distinguishes `OFFSET:LEN` from `OFFSET:RAD`: holds (`BlockTests`, `WordTests`, `Find_OffsetLenAndOffsetRad_ReadsEachBackByItsAddress`).
- A diagnostic renders as `file(line): ERROR PAR012: message` (D98); one on a generated block as `file(line, from 12): ...`: holds (`DiagnosticsTests`).

Gate: `dotnet build -warnaserror` with 0 warnings, 79 tests passing (68 in `Ncx.Core.Tests`), `dotnet format --verify-no-changes` clean.
