# Virtual machine tests

Block execution, one test per rule: `ToolChangeTests` (one per row of virtual machine 3.5 and per transition of architecture 5.2), `ModalSummaryTests` (one per row of virtual machine 4), `FrameChainTests` and `SetposTests` (2.1, 3.4; D31, D101), `HomeTests` (D100), `ResourceResolutionTests` (3.8; D103), `DiameterRulesTests` (D60), `StaticRunTests` (D99, D53), `WordHandlersTests` (every state word has a handler), and `ExampleRunTests` (the five examples run STATIC without a machine file and give no ERROR).

`VmHarness` executes blocks one by one or runs a whole file; `VmMachines` builds the machines of these tests by hand, because this project loads no TOML. The tests of the state tables themselves are in `State/`.
