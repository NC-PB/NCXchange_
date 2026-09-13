# Validation tests

The validation list of virtual machine 5 (`../../../../src/Ncx.Core/VirtualMachine/Validation/`), one test file per family and one test per ERROR and WARNING of the list, each with the smallest input that breaks its rule: `StructureValidationTests`, `FrameValidationTests`, `MotionValidationTests`, `ArcValidationTests`, `VectorValidationTests`, `RetractAndHomeValidationTests`, `ToolValidationTests`, `SpindleValidationTests`, `CycleValidationTests`, `FlowValidationTests`, `ExpressionValidationTests`, `ResourceValidationTests`, `ChannelValidationTests`. `UncalledSubSuppressionTests` shows every suppression of D99 in both directions.

Open first: `DiagnosticTableTests`, which writes `docs/spec/generated/diagnostics.md` from the table of the validation (`DiagnosticsDocument`) and checks that every code of `Ncx.Core` has its row. `RuleAssert.Only` is how a rule test asserts: exactly one diagnostic, with its code and the severity of the table. `ValidationMachines` builds the machines with limits and a rotary table.

Never here: the rules of another stage (the job scheduler, the compiler, INTERPRETED mode), which have their row in the table and their tests with that stage.
