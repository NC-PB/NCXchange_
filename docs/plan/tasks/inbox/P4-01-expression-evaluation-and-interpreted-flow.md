# P4-01 Expression evaluation and interpreted flow

Phase: 4 | Milestone: M7 | Depends on: `P1-07` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The INTERPRETED mode: variables evaluated, jumps and calls followed.

## Scope

- Evaluator over `ExprNode` with `decimal` arithmetic, the function set of section 4.12, `MOD` and `INT` semantics, comparisons to 1 and 0, division by zero ERROR, unassigned variables ERROR unless `unassigned = 0` (D38), `$SYS_*` through the configuration mapping with indexes (D51).
- Flow of section 3.6: `JUMP` with `IF`, `JUMP=END`, `CALL` with `ARG` and locals `V1`..`V33`, `SUB=END`/`RETURN`, `REPEAT`/`TIMES`, external programs, depth limit (default 8), block cap (default 1 000 000), `SKIP` per run option.
- Vars files (`<file>.vars.toml`) as start values.

## References

- ncx-virtual-machine.md sections 1, 2.7, 3.6, 3.9
- ncx-language.md sections 4.9, 4.12

## Done when

- `PATTERN_LOOP.ncx` runs the loop five times with the call positions X = 10, 30, 50, 70, 90 of its notes, and the trace shows Q1 and Q3 per iteration.
- `INCREMENTAL_SUB.ncx` calls the subprogram four times and ends at the expected position.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

Claude (agent), 2026-09-13, P4-01a Expression evaluator (P4-01 part one). Built on `main` at P1-01 (the tree of P0-05 in `src/Ncx.Core/Expressions/`, the `VariableStore` of P1-01). Part one is the evaluator of section P4-01 of `implementation/14-phase-4-interpreted-analytics.md`; the INTERPRETED loop, the flow words, `CALL` and `ARG`, and the vars files are part two. Decisions implemented: D33 (the `V` names read like any other), D38 (unassigned variables), D51 (SYS_ names with an index). No decision of the log was needed and the specification is unchanged; everything below is a reading of it.

- Files: `src/Ncx.Core/Expressions/Evaluator.cs` and `Evaluator.Functions.cs` (one internal sealed partial class: the nodes, the variables and the operators in the first file, the seventeen functions and the power in the second, so that each file stays one screen of related rules), `ExprResult.cs` (the value: a `decimal`, the string a variable holds, or UNKNOWN), the codes in `src/Ncx.Core/Model/DiagnosticCodes.Evaluation.cs`. Tests in `tests/Ncx.Core.Tests/Expressions/Evaluator*Tests.cs` with the helper `EvaluationChannel`.
- The entry point is `Evaluator.Evaluate(ExprNode expression, VariableStore vars, UnassignedVariable unassigned, Block block, Diagnostics diagnostics)`, shaped like `ExprParser.Parse`: it returns the value, or null after one VM ERROR on the block (with `OriginLine` on a generated block, D98), and stops at the first problem in reading order. It differs from the phase file's `Evaluate(ExprNode, VariableStore, SystemVariables)` in two ways: the `[system_variables]` mapping is not a parameter, because the store holds it and reads the SYS_ names through `GetSystem`; the policy for an unassigned variable, the block and the diagnostics are, as the assignment asks (P1-02 wires `VmOptions.Unassigned` to the parameter).
- Arithmetic in `decimal`: `MOD` is the remainder of `decimal`, which keeps the sign of the dividend, and `MOD` by 0 is the division by zero; `INT` is `decimal.Truncate`, `ROUND` rounds half away from zero, `FRAC` is x minus `INT(x)`. A whole-number power is the repeated product in decimal (by squaring), so `$A ^ 2` equals `$A * $A` to the last digit; a negative whole exponent is the reciprocal, and 0 to a negative power is the division by zero; a fractional exponent goes through `double`, and a negative base with one has no value. Comparisons, `AND`, `OR` and `NOT` yield 1 or 0, a value that is not 0 counting as true (language 4.9 `IF`; the phase file extends 4.12's "comparisons yield 1 or 0" to the three words).
- The functions that need `double` (SIN to ATAN2, SQRT, LN, EXP, a fractional power) come back as `decimal` through the .NET conversion, which rounds to 15 significant digits (checked on .NET 10: SIN(30) is 0.5, not 0.49999999999999994). SIN, COS and TAN reduce the angle to 0 to 360 degrees in decimal and are exact on the axes (COS(90) is 0, not 6.1E-17). TAN at 90 and 270, ASIN and ACOS outside -1 to 1, SQRT of a negative number, LN of 0 or less and ATAN2(0, 0) have no value: VM905. A number or a value beyond the range of `decimal` (about 7.9E28) is VM906, never an exception (code-guidelines 6).
- Variables are read through `VariableStore.Get` (the locals V1 to V33 of the call that runs, every other name) and `GetSystem(name, index)` for the SYS_ names; the index is evaluated first and must be a whole number. A SYS_ read that is UNKNOWN is the ERROR VM903 (VM 3.6, INTERPRETED mode); a plain variable holding UNKNOWN (set in STATIC mode, VM 1) makes the value UNKNOWN. A string passes through as the whole value (`{$QS1}`) and is VM902 wherever a number is required (VM 5).
- Codes VM900 to VM908: division by zero, unassigned variable, string where a number is required, SYS_ name unknown in INTERPRETED mode, wrong number of arguments, no value, beyond the range of decimal, index on a variable that is not a SYS_ name, index that selects no register.
- Open questions, marked `TODO(question)` in the files: an index on a variable that is not a SYS_ name (an ERROR); an index that is not a whole number (an ERROR); whether `AND` and `OR` evaluate their right operand when the left one decides (both are evaluated); whether `==` and `!=` compare strings (an ERROR); `FRAC` of a negative number (-0.7 for -2.7); the argument order, range and value at 0, 0 of `ATAN2` ((y, x), -180 to 180, an ERROR); the number of arguments per function (ATAN2 two, MIN and MAX one or more, every other one).
- Left as `TODO` for the tasks that own them: the unassigned setting is in the store (from the machine file, P1-01) and in the evaluator's parameter, and the store reports an unassigned variable only under "error", so P1-02 makes the two one setting when it wires `VmOptions.Unassigned`; the start value of a SYS_ register from `<file>.vars.toml` (VM 2.7) comes with the vars files of part two, which then names the vars file in the VM903 message (phase 4, risks).
- No existing shared file changed; the codes are a new part of the shared `DiagnosticCodes` class, in the file the header of `DiagnosticCodes.cs` asks for, and the evaluator's files are new files in `Expressions/`.

Done when (the criteria of the task wait for part two):

- `PATTERN_LOOP.ncx` runs the loop five times with the call positions X = 10, 30, 50, 70, 90, and the trace shows Q1 and Q3 per iteration: the evaluator's share holds (`EvaluatorPatternLoopTests`: the four expressions of the file, evaluated with Q1, Q2 and Q3 set by hand as the loop sets them, give X = 10, 30, 50, 70, 90, Q1 = 30 to 110 and Q3 = 1 to 5, and the condition ends the loop after the fifth pass). The run itself (`ncx trace --interpreted`) waits for part two, on the VM of P1-02 and the trace of P1-07.
- `INCREMENTAL_SUB.ncx` calls the subprogram four times and ends at the expected position: waits for part two (`CALL`, `TIMES`, `SUB=END`); the file holds no expression.
- The evaluator cases of the phase file hold: every operator and function of 4.12 by name, `{-7 MOD 3}` is -1, `{INT(-2.7)}` is -2, `{ROUND(2.5)}` is 3, `{ROUND(-2.5)}` is -3, `{1 / 0}` is an ERROR, `{$Q9}` is an ERROR and 0 under the option.

Remaining for part two: `VirtualMachine.RunInterpreted` (pc, labels, `JUMP` with `IF`, `JUMP=END`, `CALL` of a `SUB` section or an external program, `ARG` into the callee's locals, `SUB=END` and `RETURN`, `TIMES`, `REPEAT`, the depth limit, the block cap, `SKIP`), the vars files as start values (SYS_ registers included), resolving through this evaluator every value STATIC left UNKNOWN, and turning an `ExprResult` into the value `VAR` stores (whether a whole result is an integer, the text of a computed number).

Gate: `dotnet build -warnaserror` with 0 warnings, 1091 tests passing (808 in `Ncx.Core.Tests`), `dotnet format --verify-no-changes` clean.
