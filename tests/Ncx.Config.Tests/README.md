# Ncx.Config.Tests

The tests of `src/Ncx.Config`: a machine file loaded table by table with every mistake reported on its line (`MachineConfigLoaderTests`, machine-config 1 to 9), its templates parsed on the lines of their keys (`MachineConfigLoaderTemplateTests`, wave-1 question #61), the eight machine files of the repository (`ExampleMachinesTests`: the five examples of machine-config 11 and the three mills, all eight in `machines/`), the default machine (`DefaultMachineTests`, D103), the job manifest and the vars file (machine-config 8), `ncx.toml` (`ProjectSettingsLoaderTests`, machine-config 10), the spelling of M and G codes (`FunctionValuesTests`, D105), and the nearest key a WARNING names (`NearestKeyTests`). `Templates/` and `Cycles/` mirror their source folders.

Open first: `MachineConfigLoaderTests.cs` for one short TOML text per table, then `ExampleMachinesTests.cs` for the real files. The examples come through `Fixture` (`../README.md`), the files of `machines/` through `Fixture.RepositoryRoot()`; `Fixtures/` holds the one fixture of this project.

Never here: the lookups of the loaded records (`../Ncx.Core.Tests/Machine/`), a path relative to the working directory.
