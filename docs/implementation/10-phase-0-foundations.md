# Phase 0: foundations

Status: 2026-09-11, not started. Milestone M1. Tasks P0-01 to P0-07 of `../plan/tasks/inbox/`. Closed when the five `.ncx` examples parse and format to themselves byte for byte, the analyzers are quiet and CI runs the tests on every push (`../plan/phases.md`).

## Entry state

Nothing exists but the documentation. No git repository, no solution. .NET SDK 10.0.400 is installed.

## Decisions needed first

- Before P0-03 (the catalog is the rule): D90 canonical ranks, D92 comments and trivia, D93 machine axis words, D94 native cycle parameters, D95 pseudo-words, D96 `CYLINDER`. Batch 1 of `02-decisions-proposed.md`.
- Before P0-06 (the writer and the CLI): D91 `format` without the VM, D97 exit codes, D98 diagnostic codes. D91, D97 and D98 were answered as recommended on 2026-09-11 (`../decisions/decisions.md`); no log line is needed.
- P0-01 needs D106 for the project reference graph (Plugins references Core, Readers, Compilers); it was answered 2026-09-11 as recommended, so the references are set in P0-01 and the graph test asserts them from the first commit.

Batch 1 was asked and answered 2026-09-11, every decision as recommended (`../decisions/rationale.md`); nothing in this phase waits for a decision any more, and P0-03 is written against the recommendations as answered.

## Tasks

### P0-01 Repository skeleton and solution

Files:

- `git init`; commit 1 "docs: version 1.0 as handed over" with `docs/` unchanged; commit 2 the skeleton below.
- `NCXchange.sln`; `global.json` (`10.0.400`, `latestPatch`); `Directory.Build.props` (code-guidelines 9: `net10.0`, nullable, implicit usings, warnings as errors, `AnalysisLevel` latest-recommended, `EnforceCodeStyleInBuild`, `InvariantGlobalization`, `LangVersion` latest, `CA1305` as error); `Directory.Packages.props` (Tomlyn, System.CommandLine, xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, Microsoft.CodeAnalysis.NetAnalyzers if not implicit); `.editorconfig` from `dotnet/docs` plus the three rules; `.gitignore` (VS, Rider, `bin/`, `obj/`, `out/`); `LICENSE` (MIT, D75); root `README.md` (one paragraph, the three uses, a link to `docs/`).
- `src/Ncx.Core`, `src/Ncx.Config`, `src/Ncx.Readers`, `src/Ncx.Compilers`, `src/Ncx.Analytics`, `src/Ncx.Plugins`, `src/Ncx.Cli` (exe, `PackAsTool`, tool command `ncx`); `tests/Ncx.Core.Tests`, `tests/Ncx.Config.Tests`, `tests/Ncx.Readers.Tests`, `tests/Ncx.Compilers.Tests`, `tests/Ncx.Acceptance` (xUnit). Each project a `.csproj` and one placeholder type so that the build has something to compile; `InternalsVisibleTo` for its test project.
- References: Config to Core; Readers and Compilers to Core and Config; Analytics to Core; Plugins to Core, Readers, Compilers (D106); Cli to everything. A test in `Ncx.Acceptance` reads the `.csproj` files and asserts exactly this graph, so a wrong reference fails the build the day it is added.
- Folders `machines/`, `cycles/`, `templates/ncx-plugin/`, `samples/plugins/`, each with a one-sentence `README.md` saying what goes there and which task fills it.
- `.github/workflows/ci.yml`: on push and pull request, matrix ubuntu-latest and windows-latest, `dotnet restore`, `dotnet build --no-restore -warnaserror`, `dotnet format --verify-no-changes`, `dotnet test --no-build`.
- `tests/Ncx.Acceptance/Fixtures/`: a build target copies `docs/spec/examples/*.ncx`, `docs/spec/examples/sources/*`, `docs/spec/examples/machines/*.toml` into the fixture folder as embedded resources; a test asserts each copy equals its original byte for byte (the specification folder stays the source of truth).

Cite: architecture 3 (layout, dependencies), code-guidelines 3, 9, 10.

Tests first: the reference-graph test; the fixture-identity test.

Acceptance run:

```
dotnet build && dotnet test
```

Clean checkout, no warnings, CI green on the first push.

### P0-02 Core model: Block, Word, Value, Diagnostics

Files, all in `src/Ncx.Core/Model/`, one type per file, records, `sealed`:

- `NcxProgram` (FileName, LineEnding, Blocks, Sections, FileBegin, FileEnd, Trivia, Diagnostics). `Trivia` is the list of comment-only and blank lines with their line numbers (D92); the writer interleaves them with the blocks by line number.
- `Section` (Kind: Program or Sub, Name, Number, Channel, FirstBlock, LastBlock).
- `Block` (Line, Words, Verb, Skip, SkipNumber, Comment, SourceText, IsGenerated, OriginLine) with `Find(key)`, `Find(key, addr)`, `Has(key)`, `Has(key, addr, value)` (D106); `OriginLine` is the line of the block a generated block was made for and is copied into `Diagnostic.OriginLine` (D98). A block never sorts its words; the writer does.
- `Word` (Key, Addr, Value, Definition) with `ToCanonical()`.
- `Value` and its closed set: `NoValue`, `IntegerValue` (long, Text), `DecimalValue` (decimal, Text; the text is what the writer emits, design rule 5), `IdentValue`, `ListValue` (items as text), `StringValue` (content, `ToCanonical()` escapes `"` and `\`), `ExprValue` (tree, phase P0-05), `StateKeyValue` (internal, D95).
- `Diagnostics` (Items, HasErrors, `Error(line, code, message)`, `Warning(...)`, `Info(...)`, `ToText()`), `Diagnostic` (Severity, File, Line, OriginLine, Code, Message), `Severity`, `DiagnosticCodes` with the `PAR` codes as they are needed (D98).
- `ToolRef` (readonly record struct: Number or Name, `ToString()`), used from phase 1 on but defined here because `Value` conversion produces it.

Cite: architecture 4 (class diagram, amended per D92 and D98), language 3 (value types), 4.13, code-guidelines 6, 7.

Tests first (`tests/Ncx.Core.Tests/Model/`): construct a block with `OFFSET:LEN=1 OFFSET:RAD=1` and read both back by address; `Has("COOLANT", "THROUGH", "ON")`; a diagnostic renders as `2.5D_FRAESEN.ncx(12): ERROR PAR003: message`; a generated block's diagnostic renders with `from 12`; `DecimalValue` keeps `-7.025` and `0.05` as text; `StringValue` round-trips `"SIDE \"MILL\" D10"`.

No behaviour in the model beyond lookups (P0-02 notes).

### P0-03 Word catalog

Files in `src/Ncx.Core/Catalog/`:

- `WordDefinition` (Key, Group, ValueKinds as flags, AllowedIdents, IsVerb, TakesAxisWords, AddrKind, Scope, CanonicalRank, Description with the language section).
- `WordCatalog`: `Lookup(key)`, `IsStandardAxis(key)`, `TryMachineAxis(key)` (the D93 pattern), `IsNativeParameterAllowed(block)` (D94), `All()`.
- One static partial file per group so that a reader finds `SPINDLE` in `SpindleWords.cs`: `FileWords.cs` (4.1), `FrameWords.cs` (4.2), `MotionWords.cs` (4.3), `ToolWords.cs` (4.4), `SpindleWords.cs` (4.5), `FunctionWords.cs` (4.6), `CycleWords.cs` (4.7, with `CONTOUR`), `ChannelWords.cs` (4.8), `FlowWords.cs` (4.9), `ResourceWords.cs` (4.10), `LatheWords.cs` (4.11), `PseudoWords.cs` (`@SAVE`, `@RESTORE`, internal, D95). Every entry's description carries the section it comes from, so the generated table can be laid next to the specification.
- `CanonicalRank` values are assigned in steps of ten in the order of D90 so that a later word fits between two others without renumbering.

Cite: language 4 (every table), 5 rule 6 as amended by D90, D93, D94, D95, D96; architecture 4; code-guidelines 5 (table-driven dispatch).

Tests first (`tests/Ncx.Core.Tests/Catalog/`):

- Every key and `KEY:ADDR` form of the five examples resolves (the list of seventy forms from the audit is the fixture); every word in the snippets of language 6 resolves.
- No two definitions share a key; every verb of language 5 rule 1 is marked `IsVerb`; every word that rule 2 says carries axis words is marked `TakesAxisWords`.
- The rank order is strictly increasing in the order of D90; `TOOL` before `RPM`; `CYCLE_RETRACT` before `CYCLE_F`; `UNITS` after `COOLANT`.
- A test writes `docs/spec/generated/word-catalog.md` (key, value kinds, verb, address kind, scope, rank, section) and fails when the committed file differs, so the table is reviewed in every diff that changes the catalog.

### P0-04 Lexer and parser

Files in `src/Ncx.Core/Parsing/`:

- `Lexer`: splits a line into words and the comment (`;` outside strings and braces), detects the line ending of the file (first line break wins; mixed endings are a WARNING), normalizes lowercase keys, addresses and identifiers (language 3, Case), recognizes `KEY`, `KEY:ADDR`, `KEY=VALUE`, `KEY:ADDR=VALUE`, strings with `\"` and `\\`, `{...}` kept as text for the expression parser, lists by `,` without spaces. Never throws; a malformed token is a `PAR` ERROR with the column.
- `Parser`: values converted per `WordDefinition.ValueKinds` (integer, decimal, identifier from `AllowedIdents`, list, string, expression, bare); the block rules of language 5 with a `PAR` code each: one verb (rule 1), axis words need a verb (rule 2; the D93 provisional axes count as axis words), a key once per block with the address distinguishing (rule 4), partner words present in the block (rule 5: `IF` with `JUMP` or `CALL`, `ARG` with `CALL`, `TIMES` with `CALL` or `REPEAT`, `WITH` with `SYNC`, `FRAME` with a motion verb, `MOVE` and `ROT` with `TILT` or `TILT_AXIS`, `POINT` with `HOME`, `PHASE` with `SPINDLE_SYNC`); a key the catalog does not know is a provisional machine axis word when it has the form `[XYZABCUVW][0-9]{0,2}` or `I` plus that (`WordCatalog.TryMachineAxis`) and stands in a block whose verb takes axis words, with a number or expression value, bare only under `HOME` (D93); native parameters per D94; any other unknown key is an ERROR and the block is kept with its source text (architecture 4.1); pseudo-words per D95 only under `ParserOptions.AllowPseudoWords`.
- `StructurePass`: `FILE=BEGIN NCX=1` as the first block and `FILE=END` as the last (trivia allowed around them, D92), `PROGRAM=BEGIN` ... `PROGRAM=END` and `SUB=BEGIN NAME=` ... `SUB=END` into `Section`s, the structural ERRORs of VM 5 (a block outside every section, a `SUB` inside a `PROGRAM`, a file without a program, a duplicate section name, `PROGRAM=END` more than once or missing, `LABEL=END`, a `NAME` missing on `SUB=BEGIN`, an unknown `NCX` version).
- `ParserOptions` (AllowPseudoWords, KeepSourceText).

Cite: language 3 (every row, the EBNF), 4.1, 4.13, 5; VM 3 step 1, 5 (structural part); architecture 4.1.

Tests first (`tests/Ncx.Core.Tests/Parsing/`):

- The five examples parse with zero diagnostics (fixture files).
- One two-line input per structural ERROR, one per block rule, named after the rule (`BlockRule2_AxisWordWithoutVerb_IsError`).
- A file with two programs and one subprogram yields three sections with the right block ranges.
- Every value form of F8: `SUB=BEGIN NAME=100`, `NAME="SLOT ROW"`, `TOOL`, `TOOL="DRILL_D8"`, `RETRACT`, `RETRACT=50`, `SKIP`, `SKIP=3`, `TOLERANCE=OFF`, `MIRROR=X,Y`, `MIRROR=OFF`, `SHIFT X=1`, `SHIFT=RESET`, `HOME X Z`, `CYLINDER=30`.
- Trivia: comment lines before and after the file frame and blank lines are collected with their line numbers; a trailing comment is on the block.
- Lowercase input normalizes; a `;` inside a string does not end the block; CRLF input reports CRLF.
- `@SAVE=SPINDLE:MAIN` is an ERROR without the option and a `StateKeyValue` with it.

### P0-05 Expression parser

Files in `src/Ncx.Core/Expressions/`: `ExprParser` (recursive descent, one method per production of language 4.12), `ExprNode` records (`NumberNode` with text, `VariableNode` with name and optional index node, `CallNode` with function and arguments, `UnaryNode`, `BinaryNode` with operator), `ExprFunction` enum (the seventeen names of 4.12), `ExprNode.ToString()` producing the canonical text: one space around binary operators including `AND`, `OR`, `MOD`; `NOT` and unary minus followed by their operand without a space; `, ` between arguments; no spaces inside parentheses and brackets; numbers as written; `{` and `}` added by the writer.

Cite: language 4.12 (the grammar, degrees, `INT`, `ROUND`, `MOD`, comparisons yield 1 or 0); architecture 4 (Interpreter pattern in code-guidelines 5).

Tests first: every expression of `PATTERN_LOOP` and of language 6 parses and prints back identically; precedence `{1 + 2 * 3}` (sum of 1 and product), `{-2 ^ 2}` (unary minus of the power, per `unary = ["-"] power`), `{2 ^ 3 ^ 2}` (right associative per `power = primary ["^" unary]`), `{$Q1 < $Q2 AND NOT $Q3}`, `{$SYS_WEAR_Z[99]}`, `{MAX($A, 2) * SIN(30)}`; an unknown function, an unbalanced parenthesis and a missing operand give a `PAR` ERROR with the position inside the expression.

Evaluation is P4-01; only the tree is needed now.

### P0-06 Canonical writer and `ncx format`

Files:

- `src/Ncx.Core/Writing/NcxWriter`: for every line of the file, trivia by line number or a block; a block is its words sorted by `CanonicalRank` and then by address text; a provisional machine axis word (D93) has no definition and takes the rank of bucket 3 behind `X Y Z A B C`, absolute before incremental, alphabetical by letter then number, so the canonical text never depends on a machine file; each as `Word.ToCanonical()` (key, address, `=` and the value text as stored), then the comment at column 57 or after three spaces (D92); generated blocks skipped unless `WriterOptions.IncludeGenerated`; the line ending of the program, LF for a program that has none (built by a reader); a final line break always.
- `src/Ncx.Core/Writing/NcxBuilder`: `Begin(sourceLine)`, `Verb(key, value)`, `Word(key, addr, value)`, `Comment(text)`, `End()`, `Trivia(text)`, `Raw(controller, text)`, `Build()`; sorts at `End()`; used by the readers from phase 3.
- `src/Ncx.Cli/Program.cs` (composition root, code-guidelines 5) and `src/Ncx.Cli/Commands/FormatCommand.cs` with System.CommandLine: `ncx format <file> [--check]` (no `--machine` option: `format` takes no machine file, D91), output to stdout or `--output`, diagnostics to stderr in the D98 form, exit codes per D97.

Cite: language 2 rule 7, 5 rules 6 and 7 as amended by D90 and D92; D91; architecture 4.1 (`NcxWriter`), 10 (CLI table); code-guidelines 3.4 (InvariantCulture).

Tests first:

- A hand-written block in free order (`Y=2 LINE COMP=LEFT X=7 F=200 ; note`) formats into the canonical order and is stable on a second pass.
- Every value form writes back as read; a string with a quote is escaped; the comment column rule on a short and a long block.
- The five examples format to themselves byte for byte (the `Ncx.Acceptance` test over the fixture copies).

The order of work inside this task: writer and builder first with the unit tests, then the CLI, then run `ncx format` over `docs/spec/examples/*.ncx` and inspect the diff. The expected diff is empty: the ten example blocks of F1 and the comment column of `PATTERN_LOOP:15` were rewritten in the D90 commit (D90, D92), together with the D90 and D92 rows in `decisions.md`, the answers in `rationale.md`, the amended language sections and the two snippets of language 6 (2026-09-11); any diff is a bug in the writer, not in the example. From that commit on the byte-for-byte test is the acceptance test of the whole phase.

Acceptance run:

```
dotnet run --project src/Ncx.Cli -- format docs/spec/examples/2.5D_FRAESEN.ncx --check
```

for each of the five files, exit code 0.

### P0-07 Folder READMEs and `reading-the-code.md`

The folder READMEs are written now (one per `src/` and `tests/` project: what lives here, the two or three files to open first, what never goes here; code-guidelines 10.3). The guided tour `docs/reading-the-code.md` from `Program.cs` through one `ncx format` run is written at the end of phase 1, when `check` exists and the tour can end in the VM, and is extended to `convert` and `compile` in P3-07 (D78 allows the delay to the end of phase 1, not later). The three extension levels of code-guidelines 10.1 go onto the front page of `docs/README.md` in the same commit. A test in `Ncx.Acceptance` asserts that every file the tour names exists.

## Risks and open ends

- The comment column rule (D92, answered as recommended) is the only normalization the writer performs on layout; the acceptance test holds with the examples realigned once in the D90 commit.
- `Directory.Build.props` with `TreatWarningsAsErrors` and `latest-recommended` analyzers will flag things in generated code and in test projects (xUnit analyzers); the test projects get a small `.editorconfig` override for the rules that fight the string-in string-out style (raw string literals with trailing whitespace inside NC text), recorded in the P0-01 log.
- The catalog test that regenerates `word-catalog.md` writes into `docs/`; it must find the repository root from the test assembly location (walk up to `NCXchange.sln`), never the working directory (code-guidelines 8).
- `System.CommandLine` API shape changed between its beta releases; pin the version in `Directory.Packages.props` and keep the CLI layer thin (one class per command, the pipeline in `Ncx.Core` and `Ncx.Cli/Pipeline`).

## Exit checklist

- `phases.md` row 0: the five examples parse and format to themselves byte for byte; analyzers quiet; CI runs the tests on every push.
- `docs/spec/generated/word-catalog.md` committed and equal to the catalog.
- D90 to D98 have rows in `decisions.md` and answers in `rationale.md`; the language sections 3, 4.1, 4.2, 4.3, 4.7, 4.7.1, 5, 6 read as amended; the examples are canonical.
- Task files P0-01 to P0-07 in `done/` with logs (P0-07 may stay in `inbox/` with the READMEs done and the tour pending, per D78).
- `01-findings.md` F1 to F11 marked resolved.

## Log

(filled when the phase starts and when it closes)
