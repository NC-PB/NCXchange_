# Source

The seven projects of the solution (`../docs/architecture/architecture.md`, section 3). Dependencies point inward: `Ncx.Core` depends on nothing but the .NET base library, `Ncx.Config` on `Ncx.Core` and the TOML parser, the readers and compilers on both, `Ncx.Analytics` on `Ncx.Core`, `Ncx.Plugins` on `Ncx.Core`, `Ncx.Readers` and `Ncx.Compilers` (D106), `Ncx.Cli` on everything. Any other reference fails the build (`../tests/Ncx.Acceptance/Repository/ReferenceGraphTests.cs`).

The guided tour through the code, from `Ncx.Cli/Program.cs` through one `ncx format` run and one `ncx check` run into the virtual machine, is `../docs/reading-the-code.md`; it is the place to start on the first afternoon.

| Project | What lives here |
|---|---|
| `Ncx.Core/` | what a program says and means: the model, the word catalog, the parser, expressions, the canonical writer, geometry, the machine model, the virtual machine |
| `Ncx.Config/` | the TOML files of a machine loaded into the machine model, templates, cycle catalogs, the built-in default machine |
| `Ncx.Readers/` | one reader per controller family: a controller program in, NCX out; so far the framework every reader shares (P3-01), the Fanuc, Heidenhain and Siemens readers come with P3-02, P3-05 and P5-01 |
| `Ncx.Compilers/` | one compiler per controller family: NCX in, the program for one machine out (empty until P3-03) |
| `Ncx.Analytics/` | listeners of the virtual machine that write text tables (empty until P4-02) |
| `Ncx.Plugins/` | the plugin loader, what a plugin project references |
| `Ncx.Cli/` | the command `ncx`: `format`, `check`, `trace` and `annotate` so far; `convert` and `compile` come in phase 3 |

## Looking for

| What | Where |
|---|---|
| a word of the language: its values, its address, its rank | `Ncx.Core/Catalog/`, one file per table of language 4: `SPINDLE` is in `SpindleWords.cs` |
| the file that parses a word, `KEY:ADDR=VALUE` | `Ncx.Core/Parsing/WordLexer.cs` reads it; `Ncx.Core/Parsing/Parser.cs` turns it into a `Word` against the catalog |
| a line cut into its words and its comment | `Ncx.Core/Parsing/Lexer.cs` |
| the block rules: one verb, axis words need a verb | `Ncx.Core/Parsing/BlockRules.cs` |
| an expression in braces | `Ncx.Core/Expressions/ExprParser.cs` |
| the canonical order and the comment column | `Ncx.Core/Catalog/CanonicalRanks.cs`, `Ncx.Core/Writing/NcxWriter.cs` |
| what a word does to the state of the channel | `Ncx.Core/VirtualMachine/Handlers/`: `SPINDLE` is in `SpindleHandlers.cs` |
| how the virtual machine executes one block | `Ncx.Core/VirtualMachine/VirtualMachine.cs`, `Execute`: the seven steps of virtual machine 3 |
| the tool change | `Ncx.Core/VirtualMachine/ToolChangeRules.cs` |
| an arc | `Ncx.Core/Geometry/ArcResolver.cs` |
| a rule of the validation list of virtual machine 5 | `Ncx.Core/VirtualMachine/Validation/`, one file per family |
| the events a listener receives | `Ncx.Core/VirtualMachine/Events/` |
| the blocks an expansion rule of the machine inserts | `Ncx.Core/Expander/Expander.cs` |
| a table of the machine file | `Ncx.Core/Machine/` for the record, `Ncx.Config/MachineConfigLoader.*.cs` for the loading |
| a template, `M{mark} P{paths}` | `Ncx.Config/Templates/Template.cs` |
| the skeleton every reader shares | `Ncx.Readers/ReaderBase.cs` |
| a diagnostic code | `PAR` and `VM` in `Ncx.Core/Model/DiagnosticCodes.*.cs`, each with its rule in `../docs/spec/generated/diagnostics.md`; `CFG` in `Ncx.Config/`, `RDR` in `Ncx.Readers/`, `CLI` in `Ncx.Cli/` |
| a command of `ncx` | `Ncx.Cli/Commands/` |
| what `ncx check`, `trace` and `annotate` run, stage by stage | `Ncx.Cli/Pipeline.cs` |

Every folder has a README of a few lines: what is in it, where to start reading, which section of the specification it implements (code-guidelines 10.3); a task that adds a folder adds its README, and a task that changes what a folder holds updates it (code-guidelines 12). The tests of each project are in `../tests/`, in folders of the same names.

Section references are written as the documents write them (`../docs/README.md`, Conventions): language 4.5 is `../docs/spec/ncx-language.md`, virtual machine 3.5 `../docs/spec/ncx-virtual-machine.md`, machine-config 5a `../docs/spec/machine-config.md`, controller-mapping 5 `../docs/spec/controller-mapping.md`, architecture 6 and code-guidelines 10 the two files of `../docs/architecture/`, and D90 a row of `../docs/decisions/decisions.md`.
