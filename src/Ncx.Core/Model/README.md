# Model

A parsed program as immutable records (architecture 4; language 3, 4.13): `NcxProgram` (the blocks, the programs and subprograms as `Section`s, the comment-only and blank lines as `Trivia`, the diagnostics of a file), `Block` with `GeneratedBlock`, the origin of a block the expander generated (language 4.15, virtual machine 3.10), `Word`, `Value` with its closed set of value types (`IntegerValue`, `DecimalValue`, `StringValue`, ...), `ToolRef`, and `Diagnostics` with `Diagnostic` and `Severity` (D92, D98). No behaviour beyond lookups: `Block.Find` and `Block.Has` are the lookups plugins use (D106).

Start with `NcxProgram.cs`, `Block.cs` and `Word.cs`, then `Value.cs` for the value types and `Diagnostics.cs` for how every stage reports what it finds.

`DiagnosticCodes.cs` lists the ranges of every `PAR` and `VM` code of `Ncx.Core`; each component keeps its codes in a part of its own in this folder, `DiagnosticCodes.Parsing.cs`, `DiagnosticCodes.Vm.cs`, `DiagnosticCodes.Validation.cs`, and a code is never renumbered (D98). Every code has its row, with severity, rule and section, in the table of the validation (`../VirtualMachine/Validation/`), written out as `docs/spec/generated/diagnostics.md`.

Never here: parsing, writing, checking a word against the catalog, executing a block. A block never sorts its words; the writer does.
