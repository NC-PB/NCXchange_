# ThroughCoolantSourceRule

A reader rule (`ISourceRule`, `../../../docs/architecture/architecture.md`, section 9) for the case of D66: a machine whose coolant clutch engages only while the spindle stands writes `COOLANT:THROUGH=ON` as the three blocks `M5`, `M51`, `M3 S1500` (`../../../docs/spec/machine-config.md`, section 5a), and a program read back from them would stop and start its spindle twice when it is compiled again. The rule folds the three blocks back into the one word.

`Fold(block, following, builder)` holds the rule, and `ThroughCoolantSourceRule.Tests/ThroughCoolantSourceRuleTests.cs` shows it with the three blocks: blocks with nothing but the three codes, a block number and the speed are folded; a stop with a move, or a stop that another code follows, is not.

`Read` cannot use it yet. The reader offers a rule only the blocks its tables leave undecided, and a machine file names all three codes (D231); and a rule sees the one block it is offered and none after it (D232). Until these are answered, `Read` claims nothing and the reader reads the blocks as it always does.
