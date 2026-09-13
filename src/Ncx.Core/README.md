# Ncx.Core

Everything that knows what an NCX program says and what it means: the model of a program, the word catalog, the lexer and the parser, expressions, the canonical writer, the geometry of arcs, the records of the machine model, and the virtual machine (architecture 3 to 6; D107). It depends on nothing but the .NET base library and reads no file: a caller hands it text and a loaded machine.

## Open first

1. `Model/NcxProgram.cs` and `Model/Block.cs`: a program is a list of blocks, a block a list of words (language 3, architecture 4).
2. `Parsing/Parser.cs`: NCX text in, an `NcxProgram` with its diagnostics out.
3. `VirtualMachine/VirtualMachine.cs`: the steps of virtual machine 3, one method per step.

## Folders

| Folder | What | Specification |
|---|---|---|
| `Model/` | `NcxProgram`, `Block`, `Word`, the values, `Diagnostics`, and the `PAR` and `VM` codes of the whole project | language 3, 4.13; architecture 4; D92, D98 |
| `Catalog/` | the word catalog: one entry per word, one file per table, the rank table of the canonical order | language 4, 5 rule 6; D90, D93 to D96 |
| `Parsing/` | lexer, parser, block rules, structure pass | language 3, 4.1, 4.13, 5; virtual machine 3 step 1, 5 |
| `Expressions/` | the expression parser and the evaluator of INTERPRETED mode | language 4.12; virtual machine 3.6 |
| `Writing/` | the canonical writer, and the builder the readers will use | language 2 rule 7, 5 rules 6 and 7; D90 to D93 |
| `Geometry/` | `Vec3`, planes, angles, arcs, in `double` | virtual machine 3.2; architecture 4.2; D62, D84 |
| `Machine/` | the records a machine file is loaded into, the cycle catalog, the job manifest | machine-config 1 to 9; architecture 6; D107 |
| `VirtualMachine/` | the virtual machine, the state of a channel (`VirtualMachine/State/`), the word handlers (`VirtualMachine/Handlers/`) | virtual machine 1 to 4; architecture 5 |

The expander of P1-06 (`Expander/`), the events of P1-05 and, with them, the plugin interfaces `IProgramRewriter` and `IVmListener` join this project (D106).

## Never here

- A file, the console, TOML: `Ncx.Config` loads the machine file into `Machine/`, `Ncx.Cli` reads the program (code-guidelines 4, dependency inversion; D107).
- The syntax of a controller: `G81`, `CYCL DEF 200` and `CYCLE81` are read in `Ncx.Readers` and written in `Ncx.Compilers`. The native names of the built-in drilling family in `Machine/DrillingFamily.*.cs` are catalog data, not syntax (machine-config 6).
- A reference to another project of the solution.
- State that changes, outside the virtual machine: the model, the catalog and the machine records are immutable (code-guidelines 7).
