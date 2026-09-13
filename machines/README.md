# Machines

The machine files that ship with `ncx` (`../docs/spec/machine-config.md`): the five example machines of machine-config 11, copied from `../docs/spec/examples/machines/` and kept identical byte for byte (a fix is made there and copied; `tests/Ncx.Acceptance/Repository/FixtureTests` compares them), and plain single-channel mills with the generic words of their controller for the readers and compilers of phases 3 and 5 (P2-04).

| File | Machine | The examples it checks |
|---|---|---|
| `fanuc-mill-30i.toml` | Fanuc 30i vertical mill | `2.5D_FRAESEN`, `PATTERN_LOOP`, `INCREMENTAL_SUB` |
| `heidenhain-itnc530.toml` | Heidenhain iTNC 530 vertical mill | `2.5D_FRAESEN`, `PATTERN_LOOP`, `INCREMENTAL_SUB` |
| `siemens-840dsl-mill.toml` | Siemens 840D sl vertical mill | |
| `millturn1.toml` | generic 840D sl mill-turn (D104) | `MILLTURN_TRANSFER` |
| `nakamura-ntjx.toml` | Nakamura-Tome NTJX, Fanuc 18i-TB | `POLAR_FACE` |
| `doosan-puma-2600sy.toml` | Doosan Puma 2600SY | |
| `mori-ntx1000-mapps.toml` | Mori Seiki NTX1000 under MAPPS | |
| `dmg-ctx-840d.toml` | DMG MORI CTX with structure programming (D68) | |

`ncx check <file> --machine nakamura-ntjx` finds `nakamura-ntjx.toml` in the machine folder that `ncx.toml` names, then in `machines/` of the working directory, then in `machines/` of the tool's own folder, where the build copies this folder; a value with a folder in it, or a file at the path given, is read by path (`../src/Ncx.Cli/ProjectFolders.cs`). Every file loads without ERROR and without WARNING (`tests/Ncx.Config.Tests/ExampleMachinesTests`). Every number under `[[axis]]`, `[positions]`, `[dynamics]` and the spindle limits is plausible and not verified on a machine (D100). The cycle catalogs the files name are in `../cycles/`.
