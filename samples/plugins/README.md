# Sample plugins

Small finished plugins that double as documentation, one for each thing plugin authors ask for first, each with its tests, which run with the tests of the solution (implementation 17, P7-02; `../../docs/plugins.md`):

| Folder | Interface | What it does |
|---|---|---|
| `CoolantClutch/` | `IProgramRewriter` | the coolant clutch rule of code-guidelines 11: saves and stops the spindle before `COOLANT:THROUGH=ON` and restores it after (virtual machine 3.10) |
| `ZOnItsOwnLine/` | `IBlockWriter` | puts `Z` on a Heidenhain line of its own, down after the other axes and up before them (virtual machine 7, BLOCK_WRITE) |
| `ThroughCoolantSourceRule/` | `ISourceRule` | folds `M5`, `M51`, `M3 S` back into `COOLANT:THROUGH=ON` (D66); the reader does not yet offer it the blocks it needs (D231, D232) |

Each sample references `../../src/Ncx.Plugins/` alone, which brings `Ncx.Core`, `Ncx.Readers` and `Ncx.Compilers` with it (D106), and copies none of them next to the plugin; a plugin made with `ncx plugin new` references the same assemblies in the folder of `ncx` (`../../templates/ncx-plugin/`). The tests of each sample are a project of its own in the folder `<name>.Tests`, and `.editorconfig` here lets them keep the test names of code-guidelines 8.
