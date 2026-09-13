# P0-03 Word catalog

Phase: 0 | Milestone: M1 | Depends on: `P0-02` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The schema of the language as code: for every key its value kind, whether it is a verb, its address rule, its scope and its rank in the canonical order (D76).

## Scope

- `WordDefinition` and `WordCatalog` with one entry per word of `ncx-language.md` section 4 (all tables), including the axis words (the standard axes `X Y Z A B C` and their `I` incremental forms; machine axes such as `Z2` are not entries, they are accepted by the pattern rule of the next item, D93), `CENTER:*`, `TX TY TZ`, `NX NY NZ`, `ANGLE`, the cycle words with `CONTOUR` (D90), the flow words, `FILE`, `PROGRAM`, `SUB`, `RAW:*`, and the pseudo-words `@SAVE`/`@RESTORE` marked as internal, with a state key `KEY[:ADDR]` as their value (D95).
- Catalog methods for the two rules that accept keys the catalog does not list: the machine-axis form `[XYZABCUVW][0-9]{0,2}`, or `I` followed by that, in a block whose verb takes axis words (D93), and a native parameter in a block that carries `CYCLE:<controller>=n` (D94).
- Value validation per definition (identifier sets such as `CW`/`CCW`, `ON`/`OFF`, `TURN`/`MOVE`/`STAY`; numbers; lists; strings; expressions); `CYLINDER` takes the reference radius or `OFF`, there is no `ON` form (D96).
- Canonical rank per section 5 rule 6 of the language: every word has a rank, the rank table of D90 is the rule.
- A generated Markdown table from the catalog, `docs/spec/generated/word-catalog.md` written by a test (D90), so that the catalog and the specification can be compared by eye.

## References

- ncx-language.md section 4 (every table) and section 5 (block rules)
- architecture.md section 4 (WordDefinition, WordCatalog)
- code-guidelines.md section 5 (table-driven dispatch)

## Done when

- Every word used in the five examples and in the language examples of section 6 has a definition.
- A test enumerates the catalog and asserts that no two words share a key with a different meaning and that every verb is marked.
- The rank order equals the table of D90.

## Notes

Write the catalog as data in code (a static list), not as a TOML file: the catalog is the language, the cycle catalogs are data.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

Claude (agent), 2026-09-13. Built on the P0-02 model on `main`. `src/Ncx.Core/Catalog/`: `WordDefinition` with its enums, `WordCatalog` (`Lookup`, `All`, `IsStandardAxis`, `TryMachineAxis`, `IsNativeParameterAllowed`, `IsVerb`), `MachineAxisWord`, `CanonicalRanks` (the rank table of D90 in one file), `CanonicalOrder` (`RankOf`, `Compare`, `Sort`), `WordCheck` (the address and the value of a word against its entry), and one class per table of language 4: `FileWords`, `FrameWords`, `MotionWords`, `ToolWords`, `SpindleWords`, `FunctionWords`, `CycleWords`, `ChannelWords`, `FlowWords`, `ResourceWords`, `LatheWords`, `PseudoWords`, 107 entries. Diagnostic codes `PAR150`-`PAR154` in `src/Ncx.Core/Model/DiagnosticCodes.Catalog.cs` (the part file the P0-02 header prescribes). `docs/spec/generated/word-catalog.md` is written by `WordCatalogTableTests`. Tests in `tests/Ncx.Core.Tests/Catalog/`. Decisions implemented: D90 (every word has a rank, steps of ten, the buckets with their inner order; `CONTOUR` a cycle word; one `DIAMETER` entry in 4.2; `NAME` with `SUB=BEGIN` takes an identifier, an integer or a string, the EBNF line of `sub`, while with `PROGRAM=BEGIN` and `START_CHANNEL` it stays the string of the 4.1 row), D93 (`TryMachineAxis`, the ranks of absolute and incremental machine axes, letter then number), D94 (`IsNativeParameterAllowed`, the native-parameter rank, source order), D95 (`@SAVE` and `@RESTORE` internal, with a state key), D96 (`CYLINDER` takes a number or `OFF`, no `ON`). The specification is unchanged.

Decided while doing it (readings of the specification, no decision of the log):

- The group files are internal static classes named after their files (`SpindleWords.Definitions`) rather than parts of a partial `WordCatalog`, so that each file holds one type named like the file (code-guidelines 3.3); `WordCatalog` collects them in the order of language 4.
- `WordDefinition` gains, next to the P0-02 shape: `Section` (the section the word comes from, shown in the generated table; `Description` is the meaning), `IsAxisWord` (the axis words of rule 2), `IsAddrRequired` (`CENTER`, `FUNC`, `VAR`, `ARG`, `RAW`; a role address is optional by 4.10), `AddrRanks` (the fixed address sets with their ranks: `OFFSET` `LEN` `RAD`, `CENTER` `X` `Y` `Z` `C` `IX` `IY` `IZ` `IC`, `TOLERANCE` `ROTARY`; the word accepts no other address), `AddressedValueKinds` (`CYCLE:<controller>` takes an integer, `TOLERANCE:ROTARY` a number), `ValueKindsWithPartner` (a list of `PartnerValueKinds`, the value types a partner word in the block gives: `NAME` is a string, and with `SUB=BEGIN` an identifier, an integer or a string, language 4.1, D90) and `ResetRank` (`SHIFT=RESET`, `TILT=RESET`, `TILT_AXIS=RESET` rank in bucket 12 and are not the verb, as the 4.2 rows and bucket 12 of D90 have them; `WordCatalog.IsVerb` says so).
- `AddrKind` gains `Function` and `Argument` (the rows of `FUNC` and `ARG` name them) and `ToleranceKind` (open question 3). `Scope` follows the Scope column of language 4: `Program` for `PROGRAM`, `WithPartner` for the rows "with `CYCLE`", "with `PROGRAM=BEGIN`", "with `SPINDLE_SYNC`", `None` for the empty rows of `VAR` and `LABEL`; "until consumed", "per channel" and "part of the chain" are `Modal`. This settles the P0-02 TODO(question) on `Scope` by the language column; the one on `AddrKind` keeps its tolerance part. The enumeration lists of architecture 4 are now shorter than the code; that document is not edited here.
- An expression is accepted where a row says "number" (language 3: "allowed wherever a number is allowed", "number" being the catalog term of the same table) and on an integer row only where the row names it (`TIMES`). `X` to `C` also take no value, as axis names of `HOME`; rule 2 narrows that in the parser (P0-04). `POINT` has no row of its own and takes the scope "block" of virtual machine 4.
- `TOLERANCE:ROTARY` takes a number, not `OFF`, as its row says; `SPINDLE_SYNC` a list or `OFF`; `MIRROR` a list or any identifier (one axis, `MIRROR=X`, or `OFF`); `WITH` a list or one integer; list items are not checked.
- The ten verbs share the rank of bucket 2; `R` and `ANGLE`, `COMMENT` and `SECTION` rank in the order D90 writes them.
- `CanonicalOrder.Sort(block)` is rule 6 for the writer (P0-06): by rank, machine axes by letter then number, words of one key by the address text, otherwise source order (a stable insertion sort, which keeps the native parameters of D94 as written). A key the catalog does not know outside a native cycle block ranks with the native parameters; the parser reports it.
- `WordCheck.Accepts(word, definition, block, diagnostics)` reports `PAR150` (an address on a word that takes none), `PAR151` (the address missing), `PAR152` (an address outside the fixed set), `PAR153` (a value missing or not accepted, `CYLINDER=ON` of D96 among them), `PAR154` (`SKIP=n` outside 1 to 9). The block rules of language 5 stay with P0-04.
- The tests read the examples with a small tokenizer inside the test (`ExampleBlocks`); the audit list of seventy forms is not in the repository. The snippets of language 6 and the Word column of the tables of language 4 are read from `docs/spec/ncx-language.md` through `Fixture.RepositoryRoot()`; a reverse test checks that the catalog invents no word.

Open questions, marked `TODO(question)` in the files:

1. `CanonicalRanks.cs`: D90 sorts one key's words by the address text, which would put `CENTER:IX` before `CENTER:X`, while bucket 5 lists `CENTER:X`, `CENTER:Y`, `CENTER:Z`, then the incremental forms; the catalog follows bucket 5. `CENTER:C` and `CENTER:IC` (the X-C plane under `POLAR` and `CYLINDER`, D102, virtual machine 3.1 and 3.2) are accepted and ranked after `CENTER:Z` and `CENTER:IZ`; D90 does not list them.
2. `CanonicalRanks.cs`: D90 gives `@SAVE` and `@RESTORE` no bucket; they rank last.
3. `AddrKind.cs`: the address of `TOLERANCE:ROTARY` has no kind in language 3 or architecture 4; it is `ToleranceKind`, accepting `ROTARY` only.
4. `SpindleWords.cs`: the row of `SPINDLE_MODE` writes "addr = spindle role" without "optional", 4.10 makes every role address optional; kept optional.
5. `WordCatalog.cs`: the parameter names of a cycle catalog entry (`CYCLE=RECT_POCKET LENGTH=60 WIDTH=40`, 4.7.1) are neither catalog words nor, under D94, native parameters, and `ncx format` has no machine file (D91); only `CYCLE:<controller>=n` opens a block to keys the catalog does not know.

Done when:

- Every word used in the five examples and in the language examples of section 6 has a definition: holds (`ExampleWordsTests`; `Z2` of `MILLTURN_TRANSFER` resolves as a machine axis word by D93).
- A test enumerates the catalog and asserts that no two words share a key with a different meaning and that every verb is marked: holds (`WordCatalogTests.Catalog_TwoEntries_NeverShareAKey`, `BlockRule1_EveryVerb_IsMarkedIsVerb`, `BlockRule2_VerbsThatCarryAxisWords_AreMarkedTakesAxisWords`).
- The rank order equals the table of D90: holds (`CanonicalOrderTests.D90_FormsOfTheRankTable_StandInItsOrder`, `D90_Ranks_AreInStepsOfTen`; every block of the five examples and of language 6 stands in the order of the catalog).

Gate: `dotnet build -warnaserror` with 0 warnings, 247 tests passing (236 in `Ncx.Core.Tests`), `dotnet format --verify-no-changes` clean.

Claude (agent), 2026-09-13, review fixes. The review found that `NAME` took an identifier or an integer in every block. Language 4.1 gives `NAME="..."` a string with `PROGRAM=BEGIN` and an identifier, an integer or a string with `SUB=BEGIN`. D90 changed only the EBNF line of `sub`. With `START_CHANNEL`, `NAME` selects a program by its name (4.8), so it is a string too. What changed:

- The `NAME` entry now has `ValueKinds` string and `ValueKindsWithPartner` `SUB=BEGIN`: identifier, integer or string. `PartnerValueKinds` is a new record in `src/Ncx.Core/Catalog/`.
- `WordCheck.ValueKindsOf(word, definition, block)` gives the value types of a word in its block, counting its address and its partner word. `WordCheck.Accepts` checks against it. The parser should convert values by it, not by `ValueKinds` alone (P0-04), because `CYCLE:HEIDENHAIN=251` and `SUB=BEGIN NAME=100` need it.
- `PROGRAM=BEGIN NAME=SHAFT` and `START_CHANNEL=2 NAME=1` are `PAR153`. The message cites language 4.1 and says that an identifier or an integer names a `SUB=BEGIN` section only.
- A `NAME` with none of its three partners is checked as the string of the Word column. Reporting the missing partner is a block rule of the parser (P0-04).
- The generated table shows the partner rule in the Value column.

Gate: `dotnet build -warnaserror` with 0 warnings, 264 tests passing (253 in `Ncx.Core.Tests`), `dotnet format --verify-no-changes` clean. The three "Done when" criteria still hold.
