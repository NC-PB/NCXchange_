# P3-01 Reader framework and source-side state

Phase: 3 | Milestone: M4 | Depends on: `P1-07`, `P2-04` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

What every reader shares: tokenizing a controller file, keeping the source controller's modal state, emitting NCX through the builder, keeping what it cannot read as `RAW`.

## Scope

- `IReader` (`Read(SourceFile, MachineConfig) -> NcxProgram`), a `SourceBlock` tokenizer per family, a `SourceState` (the source-side small virtual machine: modal groups, positions, active tool and offsets, active cycle, master spindle) that the mapping rules consult.
- The `RAW:CONTROLLER` and `RAW:BUILDER` fallback with a WARNING per block; nothing is dropped (D5).
- The `ISourceRule` hook (plugins, phase 7) as a public interface in `Ncx.Readers`, where its caller is (D106): a rule sees the source blocks and the source-side state and may claim a sequence (D66).
- Comment and section words, block skip, the file structure (programs and subprograms as sections, jump-entered code moved in front of the end behind `JUMP=END`, D48, D89).

## References

- architecture.md section 7 (readers, with the sequence diagram)
- ncx-language.md section 4.13
- controller-mapping.md sections 1 and 6

## Done when

- A fake reader with three rules reads a three-line input into the expected NCX text.
- `RAW` blocks survive a `format` round trip.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
