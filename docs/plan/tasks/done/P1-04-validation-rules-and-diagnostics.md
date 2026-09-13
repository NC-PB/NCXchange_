# P1-04 Validation rules and diagnostics

Phase: 1 | Milestone: M2 | Depends on: `P1-03` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

Every ERROR and WARNING of section 5 of the virtual machine, each with a `VM` diagnostic code (D98) and a test.

## Scope

- The ERROR list (run stops) and the WARNING list (run continues) implemented where the state is known, each rule a `VM` code in `DiagnosticCodes` (D98) listed in the generated `docs/spec/generated/diagnostics.md` that the documentation can cite; the "spindle OFF before a `LINE`" rule names the holder's spindle.
- Inside a subprogram that no program of the file calls the caller-dependent rules are suppressed: `LINE` without feed, motion before `UNITS`, tool and offset rules, cycle rules, incremental word from an unknown position, spindle OFF before a `LINE` (D99).
- A role, function or machine axis the built-in default machine lacks, when no machine file is given, is the WARNING "not checked: no machine file", once per name, and the word runs against a resource created on the spot (a work spindle with a rotary axis of its own); with a machine file it stays the ERROR of VM 5 (D103).
- Machine limits (`rpm_min`/`rpm_max`, `max_feed`, axis `limits`) as WARNING, or clamped by the expander under `limits = "clamp"` with the WARNING saying so (D64).
- Unreachable block after an unconditional `JUMP` (D89), `JUMP=END` from inside a subprogram, `RETURN` in a program, `SYNC` in a single-channel job.

## References

- ncx-virtual-machine.md section 5
- ncx-language.md section 4.13 (rules)

## Done when

- One test per rule, named after the rule, with the smallest input that triggers it.
- The five examples check with no ERROR without a machine file (D103); the WARNINGs are those the notes and D100/D103 name.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

Claude (agent), 2026-09-13, branch `p1-04-validation`: all of section P1-04 of `implementation/11-phase-1-virtual-machine.md`, on the block execution of P1-02 and P1-03. Decisions implemented: D64 (machine limits as WARNINGs; the `limits = "clamp"` rewrite is the expander's, P1-06), D89 (unreachable blocks), D98 (a code per rule, the table), D99 (every suppression named in the table), D100 (limits compared in the MACHINE frame), D103 (no ERROR without a machine file); the spindle-OFF rule as VM 5 words it since the F30 fix of 2026-09-11. The specification is unchanged; everything below is a reading of it.

- Files in `src/Ncx.Core/VirtualMachine/Validation/`:
  - One per family of VM 5: `StructureValidation`, `FrameValidation`, `MotionValidation`, `ArcValidation`, `VectorValidation`, `RetractAndHomeValidation`, `ToolValidation`, `SpindleValidation`, `CycleValidation`, `FlowValidation`, `ExpressionValidation`, `ResourceValidation`, `ChannelValidation`. Each holds the rows of its family (`ValidationRule`, `ValidationFamily`) and the checks of the rules that P1-02 and P1-03 do not raise.
  - `DiagnosticTable` (the families in the order of the list) and `RunValidation`, what the VM calls: `CheckFile` before execution, `BeforeBlock` and `AfterBlock` around steps 3 to 5, `EndRun`.
  - New codes VM400-VM572 in `Model/DiagnosticCodes.Validation.cs`; the range table in `DiagnosticCodes.cs` is the allocation now in force.
- The table has a row for every code of `Ncx.Core`, the PAR codes of the parser and the VM codes of P1-02, P1-03 and the evaluator included, each exactly once (a test). `DiagnosticTableTests` writes `docs/spec/generated/diagnostics.md` from it: code, severity, rule, section, family by family, each D99 suppression named in its row and listed at the top. A code added later needs its row, or that test fails.
- The rules of VM 5 that another stage raises have their row with that stage:
  - Block cap exceeded: INTERPRETED mode. P4-01 part two owns VM750-VM849 and builds it now, so no code was taken for it here.
  - RAW or a `CYCLE:<controller>=n` block compiled for another controller family: the compiler at compile time (P3-03, with a CMP code).
  - Deadlock at SYNC and two channels on one spindle between marks: the job scheduler (P6-01). VM571 and VM572 are reserved for it in the channel family.
- Where the rules run:
  - The pre-pass over the file (VM 3.6). Duplicate LABEL and missing jump target (JUMP, REPEAT) per program and subprogram are ERRORs that stop the run before its first block. The rules about a block as written come once per block, however often the walks of D99 pass it: JUMP=END from a subprogram, RETURN in a program, unreachable blocks, SYNC in a single-channel job, RAW, F in a RAPID block, MFUNC and ROT against the machine.
  - After step 5, the rules that read the state the block leaves, at every walk as the rules of P1-02 and P1-03 do: SETPOS without an axis word, TOLERANCE:ROTARY and TOLERANCE_MODE without TOLERANCE, F above max_feed, a target beyond the limits, COMP change in an ARC block, OFFSET forms mixed, spindle OFF before a LINE, the RPM limits, WORKPIECE while the old holder's spindle runs. The caller rules report to `CallerRuleDiagnostics` (D99).
  - At the end of a run that no ERROR stopped: the expressions STATIC mode left unresolved, each expression word counted once, reported on the block of the first.
- Readings decided while doing it:
  - Unreachable (D89): a JUMP is unconditional without IF and without SKIP, and RETURN in a program counts as JUMP=END (VM 3.6). A LABEL makes its block and those after it reachable. PROGRAM=END, the target of JUMP=END, is never unreachable. Each unreachable block gets its own WARNING (language 4.13, "a block ... gets a WARNING").
  - Spindle OFF before a LINE: the current tool holder is lastHolder (VM 2.3), and its spindle is `[[resource]] spindle`, else default_spindle. A SPINDLE word in the LINE block counts (language 5 rule 3).
  - OFFSET forms "in one program": the program with the subprograms it calls, the memory reset at PROGRAM=BEGIN. A COMP change is a COMP word whose value differs from the compensation before the block.
  - MFUNC: an M code of any template of the machine file (format, tool change, HOME, SETPOS and every table of machine-config 5), compared as a word of the template after the normalization of D105.
  - Limits: compared after every motion block on each linear axis whose position changed, through the machine position of an axis known in the MACHINE frame or through the record of its setpos shift (`FrameRules.MachineCoordinate`, added). F under UNITS=INCH is converted to mm/min for max_feed. RPM is compared with the `[spindle.ROLE]` table of a role naming the spindle, and not while CSS is on (language 4.11).
- MILLTURN_TRANSFER: the spindle-OFF WARNING falls at lines 13 and 14, not at LINE C=90 as the phase file expects, because the default spindle of the default machine is the tool spindle TOOL (wave-1 question #53). This is the case drafted as D129, whose recommendation amends the phase-file expectation to lines 13 and 14; the expected file follows the code.
- Open questions, marked `TODO(question)` in the files:
  - What table kinematics is in a machine file for ROT, and whether ROT is checked without one (`FrameValidation`).
  - Which axes F is compared with for max_feed, and how a feed per revolution compares (`MotionValidation`).
  - Which rotary axes have travel limits rather than a modulo display range; rotary limits are not compared (`MotionValidation`).
  - Only LINE is checked for the spindle OFF, not ARC and CYCLE_CALL (`SpindleValidation`).
  - Unreachable blocks in a subprogram are not checked (`FlowValidation`).
  - Which run is a single-channel job when a file is checked without a job manifest (`ChannelValidation`).
- Shared files changed:
  - `VirtualMachine.cs`: the field `_validation` and two calls in `Execute`, before step 3 and after step 5.
  - `VirtualMachine.Static.cs`: the pre-pass and `EndRun` in `Run`.
  - `FrameRules.cs`: `MachineCoordinate`.
  - `DiagnosticCodes.cs`: the range table.
  - The P1-02 and P1-03 tests that cut a LINE with the tool spindle off now start it in their setup: `MotionTargetTests`, `PolarCylinderTests`, `SetposMotionTests`, `VectorWordTests`. `ExampleRunTests` expects the new WARNINGs of PATTERN_LOOP and MILLTURN_TRANSFER.
  - The READMEs of `src/Ncx.Core/Model/`, `src/Ncx.Core/VirtualMachine/`, `tests/Ncx.Core.Tests/`, its `VirtualMachine/`, `tests/Ncx.Acceptance/Examples/`, and the new `tests/Ncx.Acceptance/Expected/README.md`.

Done when:

- One test per rule, named after the rule, with the smallest input that triggers it: holds for every ERROR and WARNING of VM 5 that the parser or the virtual machine of phase 1 raises. The tests are in `tests/Ncx.Core.Tests/VirtualMachine/Validation/`; each asserts exactly one diagnostic, with the code and the severity of the table. `UncalledSubSuppressionTests` shows each D99 suppression both ways. The four rules of other stages are in the scope and the "Done when" of their own tasks: the block cap in P4-01 part two, RAW at compile time in P3-03 ("`RAW:FANUC` ... fails for Heidenhain"), the deadlock and two channels on one spindle in P6-01 ("reports the deadlock with both marks").
- The five examples check with no ERROR without a machine file (D103); the WARNINGs are those the notes and D100/D103 name: holds (`tests/Ncx.Acceptance/Examples/ExampleCheckTests.cs` against `tests/Ncx.Acceptance/Expected/<name>.check.txt`, compared as whole files through the parser and the VM; the CLI comes with P1-07).
  - 2.5D_FRAESEN: none.
  - PATTERN_LOOP: one WARNING for its four unresolved expressions.
  - INCREMENTAL_SUB: the three HOME WARNINGs (D100).
  - POLAR_FACE: HOME C (D100).
  - MILLTURN_TRANSFER: the five "not checked" WARNINGs (D103), and spindle OFF before the LINEs of lines 13 and 14 (D129, above).

Gate: `dotnet build -warnaserror` with 0 warnings; `dotnet test` with 2647 tests passing, 1639 of them in `Ncx.Core.Tests` and 10 new in `Ncx.Acceptance`; `dotnet format --verify-no-changes` clean.
