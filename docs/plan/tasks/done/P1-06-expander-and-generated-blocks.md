# P1-06 Expander and generated blocks

Phase: 1 | Milestone: M2 | Depends on: `P1-05` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The stage between parser and VM that turns expansion rules into ordinary NCX blocks (D63).

## Scope

- `Expander` over the parsed program: for every block, the rules of the machine configuration (`pre`, `post`, `requires`, `restore` on functions, tool change and catalog cycles; phase 2 loads them, a hand-built rule set serves the tests now) and the `IProgramRewriter` hook (the interface in `Ncx.Core` with its caller, together with its signature types `RewriteResult` (`Unchanged`, `Replace`, `Surround(before, after, reason)`) and the `RewriteContext` abstraction, D106; the loader and the concrete context come in phase 7) produce generated blocks with their origin.
- The placeholder `{position:NAME}` in a `pre` or `post` block expands to the axis words of the named entry of the machine's `[positions]` table before the block is parsed (machine-config 5a, D100); an unknown name is an ERROR on the rule.
- The pseudo-words `@SAVE=key` and `@RESTORE=key` (VM section 3.10), the value a state key `KEY[:ADDR]` naming a state variable of the channel, parsed only under the option the expander uses for generated text (D95): a restore stack per state variable, re-applied as if the program had written the words; pseudo-words in a user file are an ERROR.
- Generated blocks execute in both modes, count for analytics, show in trace and annotate with their origin, are never written by `format`.

## References

- ncx-virtual-machine.md sections 1, 3.10
- ncx-language.md section 4.15
- machine-config.md section 5a
- architecture.md section 5.5

## Done when

- The coolant clutch example: `COOLANT:THROUGH=ON` with `requires = { SPINDLE = "OFF" }` and `restore = ["SPINDLE"]` produces stop, coolant, restart with the previous speed, and the VM state after it equals the state without the rule plus the coolant.
- `HOME Z` before a tool change from `[tool_change] pre`.
- `pre = ["RAPID {position:tool_change} FRAME=MACHINE"]` with `tool_change = { X = 0, Z = -120 }` inserts `RAPID X=0 Z=-120 FRAME=MACHINE` (D100).

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

Claude (agent), 2026-09-13. Built on `main` at c228dcb (P1-03), rebased onto aaded8b (P1-04) in the second review fix (below). All of section P1-06 of `implementation/11-phase-1-virtual-machine.md`. Decisions implemented: D61, D63, D64, D95, D100, D106, and D98 for the origin line of generated blocks. The specification is unchanged; everything below is a reading of it.

- Files:
  - `src/Ncx.Core/Expander/`:
    - `Expander` with `Expand(program, machine, rewriters)`, which returns a new `NcxProgram`: the diagnostics are copied, a program with a parser ERROR comes back unexpanded, and the sections are remapped.
    - The plugin surface: `IProgramRewriter`, `RewriteResult`, `RewriteKind`, `RewriteContext`, with `BlockRewriteContext` as the expander's own context.
    - `ExpansionRules` and `TriggeredRule`: which rules a block triggers.
    - `RuleBlocks`: `@SAVE`, requires, pre, the block, post, `@RESTORE`.
    - `StateKeys`: the role of the default resource.
    - `PositionPlaceholder` (D100), `GeneratedText` (parsing under D95, origin, diagnostics), `ProgramRewriters`, `LimitClamp` (D64), `BlockExpansion`, and a README.
  - The restore stack: `src/Ncx.Core/VirtualMachine/VirtualMachine.Restore.cs` and `RestoreRules.cs`.
  - In `Model/`: `GeneratedBlock`, `GeneratedPlacement`, `NumberValues`, and `DiagnosticCodes.Expander.cs` with VM650-VM653, VM670, VM680 and VM681.
- Decided while doing it (code structure, no behaviour question):
  - `GeneratedBlock` holds the origin block, the source (the rule's table or the rewriter's type name), the reason and the placement (Before, InPlace, After). It sits in `Model/` next to `Block`, which carries it as `Block.Generated`; the phase file listed it under `Expander/`. `IsGenerated` and `OriginLine` stay and are set with it.
  - When a rewriter (Replace) or `limits = "clamp"` rewrites the words of a block of the file, the rewritten block stands in its place as a generated block with the placement InPlace. `NcxWriter` writes its origin when generated blocks are not asked for, so format of the expanded program equals format of the original after a rewrite too. A second rewrite of the same block keeps the origin and appends its source and reason.
  - `RewriteContext` is an abstract class. A C# interface would have to be called `IRewriteContext`, and code-guidelines 11 and D106 use the name `RewriteContext`. The expander passes the machine name, the channel of the section (1 outside every section), the line and empty settings. P7-01 can wrap a plugin's rewriter to hand it the context with its settings. `Replace(block, reason)` takes one block of NCX text; `Surround(before, after, reason)` is as code-guidelines 11 writes it.
  - Generated text is parsed with `AllowPseudoWords`. The parser's diagnostics are reported again with the origin line, and the message starts with the rule key or the rewriter and the text: `[tool_change] pre "FOO=1": ...`.
  - Each `@SAVE`, requires condition, pre or post text and `@RESTORE` becomes one block. Pseudo-words stand in blocks of their own, as the recommendation of D167 assumes.
  - A restore list is saved before the requires words and the pre blocks even when the rule has no requires. Machine-config 5a: restore puts back what `@SAVE` kept. The flowchart of architecture 5.5 draws the `@SAVE` only in the requires branch, and every example there has requires. The `@RESTORE` blocks follow the order of the restore list.
  - A requires or restore key without an address gets the role of the default resource of its word: SPINDLE becomes SPINDLE:MAIN, as in architecture 5.5 (virtual machine 3.8 rule 2). A key with an address, and a key whose word addresses no role (COOLANT, F), stay as written.
  - An unknown `{position:NAME}` is VM650, an ERROR on the rule. It is reported once per rule text and gives no block.
  - Generated blocks may not stand before a BEGIN block or after an END block, and no rewriter may replace such a block (VM652, language 4.13). A generated text with FILE, NCX, PROGRAM or SUB is VM653; a text that holds no block is VM651.
  - In the VM, `ExecutePseudoWords` runs between step 1 and step 2, one inserted call in `Execute`. `@SAVE` pushes a `RestoreEntry`, which gained `Variable` (SPINDLE:S1) and `UnknownKeys`. `@RESTORE` pops the newest entry of its own variable, so there is one stack per variable in one list. Its words are appended to the block, so steps 2 to 7 (and the events of P1-05) see `SPINDLE:MAIN=CW RPM:MAIN=1500` where the block said `@RESTORE=SPINDLE:MAIN`.
    - A value that was UNKNOWN comes back UNKNOWN (virtual machine 1), and a value that was none comes back none.
    - `@RESTORE` with nothing saved is VM680; a key the stack does not keep is VM681. Both are ERRORs, because code-guidelines 6 allows a WARNING only where the specification does.
    - A pseudo-word that reaches the VM in a block of the file is PAR007, the parser's code for the same rule, and does nothing.
- Open questions, marked `TODO(question)` in the files:
  - A rule of a function table applies to every word that sets a state of its table, COOLANT:THROUGH=OFF as well; machine-config 5a says "every function state" and writes the keys once per table (`ExpansionRules`).
  - Several rules on one block nest: the rule of the first word in canonical order stands outermost, and the rewriters' blocks stand inside the rules' (`ExpansionRules`).
  - A catalog cycle's rule fires on the block that names the cycle (`CYCLE=name`, or the native form through `CycleCatalog.FindNative`), never on its `CYCLE_CALL` blocks. Machine-config 5a says only "around the triggering block", and language 4.7 defines a cycle once and executes it by `CYCLE_CALL` at each position; neither names the triggering block. A `post`, or `requires` with `restore`, on a catalog cycle therefore undoes the mode or the required state before the holes are drilled (`ExpansionRules`; pinned for `pre` and `post` in `ExpansionRuleTests`).
  - A generated block has the line of its origin, so a diagnostic reads `file(12, from 12)`; D98 does not say which line a generated block has (`GeneratedText`).
  - A generated block carries the SKIP of its origin, so `skip_blocks` skips them together (`GeneratedText`).
  - The restore stack keeps SPINDLE (with the speed), RPM, SPINDLE_MODE, CSS, VC, RPM_MAX, COOLANT, FUNC, F, FEED_MODE, COMP and DIAMETER; any other key is VM681 (`RestoreRules`).
  - F is clamped as written, against the smallest max_feed of the axes its block names; units and feed mode are VM state (`LimitClamp`).
  - Only targets of FRAME=MACHINE blocks are clamped; the machine position of a workpiece-frame target is VM state (`LimitClamp`).
  - Rotary targets are not clamped: modulo display range or travel limit, and no key tells which (`LimitClamp`).
  - Wave-1 questions cited, not asked again: #33 (D167), the order of a pseudo-word and the state words in a mixed block (`VirtualMachine.Restore.cs`); #4, the X values of `[positions]` are copied as written (`PositionPlaceholder`, `LimitClamp`); #69, the native form of a catalog cycle (`ExpansionRules`).
- Seams for the tasks of this wave and later:
  - P1-04 raises the limit WARNINGs of virtual machine 5 under `limits = "warn"`. Under "clamp" the expander rewrites the value and reports VM670, so the VM sees the clamped value. The merge may unify the two codes. VM650 to VM681 have their rows in P1-04's table of the validation and in `docs/spec/generated/diagnostics.md` since the second review fix.
  - P1-05 raises the events from the block `ExecutePseudoWords` returns.
  - P1-07's `Pipeline` calls `Expander.Expand` between parser and run, with the machine; trace and annotate read `Block.Generated`.
  - P4-01: INTERPRETED mode executes generated blocks through the same `Execute`. A JUMP to a LABEL whose block has generated blocks before it must enter at the first of them (plain TODO in `Expander.cs`).
  - P7-01 wraps the plugins' rewriters (settings, the PLG ERROR of a rewriter that throws, the INFO of D98).
- Seen outside this task and not changed: `ResourceResolver.EnsureInState` (P1-02) adds the axis id of a machine spindle (C1 on the mill-turn of the tests) to the position store, which is keyed by NCX name (C), the first time a spindle word resolves. A snapshot then holds a phantom axis C1. `RestoreStackTests` resolves the spindle before its snapshot.
- Shared files changed:
  - `Model/Block.cs`: `Generated`.
  - `Writing/NcxWriter.cs`: `WrittenBlock`, the origin of an InPlace block.
  - `VirtualMachine/VirtualMachine.cs`: one call in `Execute`.
  - `VirtualMachine/State/RestoreEntry.cs`: `Variable` and `UnknownKeys`.
  - The READMEs of `src/Ncx.Core/`, `Model/`, `VirtualMachine/`, `tests/Ncx.Core.Tests/` and `tests/Ncx.Core.Tests/VirtualMachine/`.

Done when:

- The coolant clutch example: holds (`CoolantClutchRuleTests`). With requires = { SPINDLE = "OFF" } and restore = ["SPINDLE"] the expander writes `@SAVE=SPINDLE:MAIN`, `SPINDLE:MAIN=OFF`, the coolant block, `@RESTORE=SPINDLE:MAIN`. The spindle stands while the coolant engages. The state after it equals the state without the rule plus the coolant, RPM:MAIN 1500 restored, and the STATIC run reports nothing. The `CoolantClutchRule` of code-guidelines 11, as a rewriter, gives the same blocks and state (`ProgramRewriterTests`).
- `HOME Z` before a tool change from `[tool_change] pre`: holds (`ExpansionRuleTests`). It stands before TOOL=1, a bare TOOL and TOOL=0, and before every TOOL block of 2.5D_FRAESEN, INCREMENTAL_SUB and MILLTURN_TRANSFER (`ExampleExpansionTests`).
- `pre = ["RAPID {position:tool_change} FRAME=MACHINE"]`: holds. It inserts `RAPID X=0 Z=-120 FRAME=MACHINE`; an unknown name is VM650 and the run stops.
- The other tests of the phase file: a pseudo-word in a user file is an ERROR; a diagnostic on a generated block names the originating line (`test.ncx(4, from 4): WARNING VM060`); format of the expanded program equals format of the original (`GeneratedBlockTests`).
- The rest of the scope:
  - Generated blocks run through the same `Execute` in both modes; STATIC is tested here, and INTERPRETED comes with P4-01.
  - They count for analytics through the events of P1-05 and P4-02.
  - `trace` and `annotate` show them with their origin in P1-07.
  - `format` never writes them: holds.

Gate:
- `dotnet build -warnaserror`: 0 warnings.
- `dotnet test`: 2556 tests passing, 1558 of them in `Ncx.Core.Tests`, 78 of those new.
- Review fix, Claude (agent), 2026-09-13: the triggering block of a catalog cycle's rule moved from the decisions to the open questions. It has a `TODO(question)` at the CYCLE case of `ExpansionRules.RuleOf`. The test comment no longer attributes the choice to machine-config 5a, the test is renamed `MachineConfig5a_CatalogCyclePre_StandsBeforeTheBlockThatNamesTheCycle`, and `MachineConfig5a_CatalogCyclePost_StandsDirectlyAfterTheBlockThatNamesTheCycle` pins the workaround for `post`.
- `dotnet format --verify-no-changes`: clean.
- A mutation check disabled six rules in turn, each in a scratch run with the file restored afterwards. Each mutation failed between one and six tests:
  - `@RESTORE` popping the oldest entry instead of the newest;
  - the writer dropping an InPlace block instead of writing its origin;
  - the role of the default spindle left off a state key;
  - the after blocks not nesting;
  - an UNKNOWN value not restored;
  - the SKIP of the origin not carried.

Second review fix, Claude (agent), 2026-09-13:
- The branch is rebased onto `main` at aaded8b (P1-04). The README conflicts of `tests/Ncx.Core.Tests/` and its `VirtualMachine/` keep the lines of both tasks.
- Every code of the expander has its row in the table of the validation, as P1-04's `DiagnosticTable_EveryCodeOfNcxCore_HasOneRow` requires (D98):
  - VM670 stands in the motion family after VM441 and VM442, because virtual machine 5 names the clamp in its sentence on the limits. The expander raises it.
  - VM650 to VM653, VM680 and VM681 stand in a new family, "Generated blocks" (`VirtualMachine/Validation/GeneratedBlockValidation.cs`, named after virtual machine 3.10), placed after the structure family: the list of virtual machine 5 names none of them. The expander raises VM650 to VM653; the restore stack raises VM680 and VM681.
  - Decided, a code-structure choice with no behaviour question: the rows document the codes. The severities are those the code already raised.
- The tests of the seven codes now assert through `RuleAssert.Only`, so each one checks the code and the severity of its row. With the rows missing, 13 tests failed; now they pass.
- `docs/spec/generated/diagnostics.md` is regenerated through `DiagnosticTableTests`.
- Shared files changed: P1-04's `VirtualMachine/Validation/DiagnosticTable.cs` (the new family in the list), `MotionValidation.cs` (the VM670 row) and `VirtualMachine/Validation/README.md` (the file table).
- Gate on the rebased branch:
  - `dotnet build -warnaserror`: 0 warnings.
  - `dotnet test`: 2725 tests passing, 1717 of them in `Ncx.Core.Tests`.
  - `dotnet format --verify-no-changes`: clean.
- Every "Done when" criterion still holds; the task file stays in `done/`.
