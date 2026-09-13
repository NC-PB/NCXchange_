# P3-03 Compiler framework and number formatting

Phase: 3 | Milestone: M5 | Depends on: `P3-01` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

What every compiler shares: walking the program with the STATIC VM, deciding what the target needs written, formatting numbers, writing files.

## Scope

- `ICompiler` (`Compile(NcxProgram, MachineConfig) -> CompiledFiles`), a `TargetState` (what the target control has active, so a modal G is written only on change), the `BLOCK_WRITE` event and the `IBlockWriter` interface in `Ncx.Compilers`, where its caller is (D106), each `SUB` section emitted once, not once per `CALL`, although the STATIC walk follows every `CALL` (D99): the section is written from an unknown target state, so every modal word stands at its first use inside it and the text is right for every caller, and walks that would write different lines are an ERROR naming the section and the calls; under `program_layout = "file_per_program"` (Heidenhain) the section is written once per calling program (VM 3.9, language 4.13), the output layout (`out/<machine>/`, `program_layout` one file or file per program, D48).
- Number formatting per `[format]`: decimals per address, trailing zeros, decimal separator, culture-invariant; block numbers; line length; comment charset transliteration.
- The chain writer: frames in program order on every target (D31).
- `RAW` of another controller, and a `CYCLE:<controller>=n` block of another controller family (D94): ERROR at compile time.

## References

- architecture.md section 8 (compilers, sequence diagram)
- machine-config.md section 2
- ncx-language.md section 2 rule 5

## Done when

- A fake compiler writes three blocks with the formatting of two different `[format]` tables.
- `RAW:FANUC` compiles to Fanuc and fails for Heidenhain with the ERROR text of the specification.
- `CYCLE:HEIDENHAIN=251 Q215=0 Q218=60` compiles to Heidenhain with the native parameters in source order and fails for Fanuc with the same ERROR text (D94).

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
