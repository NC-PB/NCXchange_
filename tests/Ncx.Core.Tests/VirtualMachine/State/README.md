# State tests

One file per state class of `src/Ncx.Core/VirtualMachine/State/`: a row of the tables of virtual machine 2 has an `..._AtStart_...` test for its start value and an `..._ChangedAfterASnapshot_SnapshotKeeps...` test that a snapshot keeps its value while the live state changes. `StartPositionTests` checks the start position rule (2.2, 3.4; D35, D100), `VariableStoreTests` and `VariableValueTests` the variables of 2.7 and 3.6. `StateMachines` builds the machines of these tests by hand.
