# Ncx.Config

Loads the TOML files of a machine into the records of `Ncx.Core.Machine` (architecture 6; D107): the machine file (machine-config 1 to 9), the job manifest and the vars file (machine-config 8), the cycle catalogs (`Cycles/`, machine-config 6), and parses the templates of a machine once (`Templates/`, machine-config introduction and 5). `DefaultMachine.cs` is the built-in machine of D103 that `check`, `analyze`, `trace` and `annotate` use without a machine file. Every mistake in a file is a `CFG` diagnostic on its line; an unknown key is a WARNING that names the nearest known key.

## Open first

1. `MachineConfigLoader.cs`: `Load(path, diagnostics)` and `LoadText(text, diagnostics)`, then one part per group of sections: `MachineConfigLoader.Identity.cs` (machine-config 1 to 3), `MachineConfigLoader.Resources.cs` (4), `MachineConfigLoader.Functions.cs` (5, 5a), `MachineConfigLoader.Variables.cs` (6, 7, 9).
2. `ConfigTable.cs`: one TOML table as a loader walks it, with the typed reads that report a wrong type, a missing key or an unknown key on its line.
3. `DefaultMachine.cs`.

`TomlDocument.cs` is the one place that talks to Tomlyn; `NearestKey.cs` finds the key a typo was meant to be; `FunctionValues.cs` spells M and G codes without leading zeros (D105). The loader parses every template of a machine file on the line of its key (`ConfigTable.Template`, wave-1 question #61), so that a template that cannot be parsed is an ERROR on that line. `JobManifestLoader.cs` and `VarsFile.cs` load the two files of machine-config 8, `ProjectSettingsLoader.cs` loads `ncx.toml`, the settings of the working directory (machine-config 10, P2-04). The codes are `CFG001` to `CFG199`, one part of `DiagnosticCodes` per component with its range (`DiagnosticCodes.cs`, D98). The files it loads are in `../../machines/` and `../../cycles/`.

## Never here

- The records themselves: they are in `../Ncx.Core/Machine/`, so that the virtual machine needs no `Ncx.Config` (D107).
- Binding TOML to records by reflection: the loaders walk the tables by hand, so that every key keeps its line (`MachineConfigLoader.cs`).
- The meaning of a program: this project knows the machine, not the program.
