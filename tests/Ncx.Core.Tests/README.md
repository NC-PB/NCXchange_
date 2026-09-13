# Ncx.Core.Tests

The tests of `src/Ncx.Core`, one folder per source folder: NCX text in, the expected text, state or diagnostics out (architecture 11; code-guidelines 8). A test is named `Rule_Scenario_Expectation` after the rule of the specification it checks, `BlockRule2_AxisWordWithoutVerb_IsError`, so the list of test names reads as the list of rules.

## Open first

1. `Parsing/ExampleParsingTests.cs`: the five examples parse without a diagnostic.
2. `Parsing/BlockRulesTests.cs` or `VirtualMachine/ToolChangeTests.cs`: one short NCX text per rule and the expected result under it.
3. `VirtualMachine/VmHarness.cs`: how a test runs blocks through the virtual machine.

| Folder | Tests | Helpers |
|---|---|---|
| `Model/` | blocks, words, values, diagnostics | |
| `Catalog/` | the word catalog against language 4 and the examples; writes `docs/spec/generated/word-catalog.md` | `ExampleBlocks`, `LanguageDocument`, `WordCatalogTable` |
| `Parsing/` | lexical rules, value forms, block rules, structure, trivia, pseudo-words | `ParseText` |
| `Expressions/` | the grammar of language 4.12, the canonical text, the evaluator | `ExprStructure`, `ExpressionTexts`, `EvaluationChannel` |
| `Writing/` | the canonical writer and the builder | |
| `Geometry/` | vectors, planes, angles, the three forms of an arc | `GeometryAssert` |
| `Machine/` | the lookups of the machine model and the cycle catalog | |
| `VirtualMachine/` | block execution, frames, tool change, resources, STATIC runs | `VmHarness`, `VmMachines` |
| `VirtualMachine/State/` | the start values and snapshots of every state table | `StateMachines` |
| `VirtualMachine/Validation/` | one test per rule of the validation list of virtual machine 5; writes `docs/spec/generated/diagnostics.md` | `RuleAssert`, `ValidationMachines`, `DiagnosticsDocument` |
| `Expander/` | expansion rules, program rewriters, `limits = "clamp"`, generated blocks and their origin | `ExpanderHarness`, `ExpanderMachines`, the rewriters `CoolantClutchRule`, `ReplacingRewriter`, `SurroundingRewriter`, `ContextRecorder` |

## Never here

- TOML: this project loads no machine file. The machines of its tests are built by hand (`VmMachines`, `StateMachines`); loading is tested in `../Ncx.Config.Tests/`.
- A path relative to the working directory: examples come through `Fixture` (`../README.md`).
- A mocking framework (code-guidelines 8).
