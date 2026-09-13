# Catalog

The word catalog, the schema of the language (architecture 4; language 4): one `WordDefinition` per word, with the values it takes, whether it is a verb, what its address names, its scope and its rank in the canonical order. One file per table of language 4, so `SPINDLE` is in `SpindleWords.cs`: `FileWords` (4.1), `FrameWords` (4.2), `MotionWords` (4.3), `ToolWords` (4.4), `SpindleWords` (4.5), `FunctionWords` (4.6), `CycleWords` (4.7), `ChannelWords` (4.8), `FlowWords` (4.9), `ResourceWords` (4.10), `LatheWords` (4.11), `PseudoWords` (4.15, D95).

Start with `WordCatalog.cs`: `Lookup(key)`, and the two rules for keys the catalog does not list, the machine axis words of D93 (`TryMachineAxis`) and the native cycle parameters of D94 (`IsNativeParameterAllowed`). Then `WordDefinition.cs` and one group file. `CanonicalRanks.cs` is the rank table of D90 (language 5 rule 6) in one file, `CanonicalOrder.cs` sorts the words of a block by it, `WordCheck.cs` checks the address and the value of a word against its entry (`PAR150` to `PAR154`).

A new word is one entry in the file of its table with a rank from `CanonicalRanks.cs`. `WordCatalogTableTests` in `tests/Ncx.Core.Tests/Catalog/` then rewrites `docs/spec/generated/word-catalog.md`, so the change shows in the diff next to the specification.

Never here: the block rules of language 5 (`../Parsing/BlockRules.cs`), what a word does to the state (`../VirtualMachine/Handlers/`), anything from a machine file: the canonical order never depends on one (D91, D93).
