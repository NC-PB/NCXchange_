# CoolantClutch

The coolant clutch rule of `../../../docs/architecture/code-guidelines.md`, section 11, as a finished plugin: an `IProgramRewriter` that saves and stops the spindle before `COOLANT:THROUGH=ON` and restores it afterwards, for a machine whose coolant clutch engages only while the spindle stands (virtual machine 3.10). It is the plugin that `ncx plugin new` makes from `../../../templates/ncx-plugin/`, built with the repository instead of against the folder of `ncx`.

Open `CoolantClutchRule.cs`, then `CoolantClutch.Tests/CoolantClutchRuleTests.cs`: a program in, the program the virtual machine runs out. `ncx compile` of a program with `COOLANT:THROUGH=ON` on line 12 reports what the rule did as the INFO `plugin CoolantClutch: inserted 3 blocks at line 12` (D98; the count is an open question, see `../../../src/Ncx.Plugins/PluginRewriter.cs`).

The same rule can be written as an expansion rule of the machine file (`../../../docs/spec/machine-config.md`, section 5a); a plugin is for what the machine file cannot say (`../../../docs/plugins.md`).
