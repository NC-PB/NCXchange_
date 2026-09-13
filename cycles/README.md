# Cycles

The cycle catalogs per controller family in TOML, `fanuc.toml`, `heidenhain.toml` and `siemens.toml` (`../docs/spec/machine-config.md`, section 6); task P2-03 fills this folder.

`ncx` finds the catalog that `[cycles] catalog` of a machine names in the cycle folder that `ncx.toml` names, then in `cycles/` of the working directory, then in `cycles/` of the tool's own folder, where the build copies this folder, and nowhere else: unlike `--machine`, the value is no path from the working directory, and a value with a folder in it (`shop/fanuc.toml`) is looked for below each cycle folder. It loads the catalog beneath the `[[cycle]]` entries of the machine file (P2-04, `../src/Ncx.Cli/RunMachine.cs`).
