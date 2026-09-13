# Machine

The machine model: the records a machine file is loaded into, one per table of machine-config 1 to 9, loaded once and never changed, template values kept as text (code-guidelines 7; architecture 6). They live in `Ncx.Core` so that the virtual machine, the expander and the job scheduler read them without depending on `Ncx.Config`, which loads them (D107). `JobManifest.cs` and `ChannelProgram.cs` are the job manifest of machine-config 8.

Start with `MachineConfig.cs`: every table as a property, and the lookups that readers, compilers and the virtual machine share, `ResolveRole`, `ResolveAxis`, `ResolveDefaultSpindle`, `ResolveDefaultHolder`, `FindFunction` (virtual machine 3.8). Then `MachineIdentity.cs` (`[machine]`, machine-config 1), `ResourceDef.cs` and `AxisDef.cs` (`[[resource]]` and `[[axis]]`, machine-config 4), `FunctionTable.cs` with `ExpansionRule.cs` (machine-config 5, 5a).

The cycle catalog is `CycleCatalog.cs` with its `CycleEntry.cs` records (machine-config 6, language 4.7.1), over the built-in drilling family of `DrillingFamily.cs`, one part per controller family as controller-mapping 5 maps it.

Never here: TOML, reading a file, parsing a template (`../../Ncx.Config/` does all three); state that changes (`../VirtualMachine/State/`).
