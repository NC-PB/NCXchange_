# P2-04 Example machines and `machines/` folder

Phase: 2 | Milestone: M3 | Depends on: `P2-03`, `P1-07` | Size: S (S: a day or two, M: up to a week, L: more)

## Goal

The five example machine files become the shipped machine files, verified against the loader and the templates.

## Scope

- Verify `millturn1.toml` (the fifth example machine, a generic SINUMERIK 840D sl mill-turn for `MILLTURN_TRANSFER.ncx`, written for D104) against the loader and the templates like the other four; copy the five files from `docs/spec/examples/machines/` to `machines/`, fix what the loader reports, keep the copies identical (a test compares them).
- Every axis gets `home` and `limits` in machine coordinates and the dynamics keys (`rapid`, `max_feed`, `acceleration`, `[dynamics]`, `rpm_min`, `rpm_max`, `accel_time`) with plausible values marked "not verified on the machine", and every file a `[positions]` table (D100).
- A plain `fanuc-mill-30i.toml`, `heidenhain-itnc530.toml` and `siemens-840dsl-mill.toml` for the readers and compilers of phases 3 and 5 (generic words only).
- `ncx` finds machine files by name in `machines/` and by path.

## References

- machine-config.md section 11
- controllers/machine-builders.md section 3

## Done when

- `ncx check examples/2.5D_FRAESEN.ncx --machine heidenhain-itnc530` runs.
- The five examples check with no ERROR with their machine files (`MILLTURN_TRANSFER` with `millturn1`, `POLAR_FACE` with `nakamura-ntjx`) (D103, D104).
- This closes M3 and phase 2.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

- Claude (agent), 2026-09-13, part one (P2-04a, branch `p2-04a-machine-data`): the D100 data in the four builder files of `docs/spec/examples/machines/`: `home` and `limits` in machine coordinates, `rapid`, `max_feed`, `acceleration` on every `[[axis]]`, `rpm_min`, `rpm_max`, `accel_time` on every spindle table, a `[positions]` table with `tool_change` and `program_end`, a `[dynamics]` table, all marked as plausible and not verified on the machine; the header comment that deferred the data to P2-04 replaced; `nakamura-ntjx.toml` declares the axis `C2` of resource `S2` (F23). `machines/fanuc-mill-30i.toml`, `machines/heidenhain-itnc530.toml`, `machines/siemens-840dsl-mill.toml` written as plain three-axis mills with the generic words of their controller files and the D100 data (the reference point is the machine zero, so the `G91 G28 Z0` of the Fanuc sources and the `M91` moves of the Heidenhain sources end at the same place, 2.5D note 4). Every file parses. Nothing was decided; the gaps are `TODO(question)` comments in the files: whether `home`, `limits` and `[positions]` of a diameter-programmed X axis are diameters or radii; the address of the Nakamura sub spindle C axis on the 18i-TB; `{tool}` against `{next}` in the preload templates of machine-config 3; `{rotary}` of the tolerance templates on a machine without rotary axes; the role of a mill's tool holder; `TOOL CALL S` on the iTNC 530 and a generic `ORIENT` for Heidenhain; a template for named tools on Siemens; the Siemens variable map and the Heidenhain system variables. Remains for part two: copy the five example files to `machines/` with the identity test, the loader and `check` runs of the acceptance, `--machine` resolution in `Ncx.Cli`, `ncx.toml`. Found on the way: `doosan-puma-2600sy.toml` names `axis = "C2"` on resource `S2` without an `[[axis]]` `C2` (the F23 gap of the Nakamura file) and was left as it is.
- Claude (agent), 2026-09-13, part two (P2-04b, branch `p2-04b-machines-folder`): the rest of section P2-04 of `implementation/12-phase-2-configuration.md`. Decisions implemented: D97 (exit codes of the new inputs), D98 (the codes, every diagnostic on its file and line), D100, D103, D104, D105, D107. The specification is unchanged apart from the two example machine files below; everything else is a reading of it.
  - The Doosan gap of part one was closed by P2-01 on main (`[[axis]]` `C2`, wave-1 question #11), so nothing was left to do there.
  - Example machines: fixed in `docs/spec/examples/machines/` and copied byte for byte to `machines/` (`FixtureTests.ExampleMachines_EveryCopyInMachines_EqualsItsExampleByteForByte`). The last WARNINGs of P2-01's committed list: the `CLAMP` of the spindle tables of `mori-ntx1000-mapps.toml` (`MAIN`, `SUB`, `TOOL`) and `dmg-ctx-840d.toml` (`MAIN`, `SUB`, `TOOL`, `TOOL2`) became the named functions `MAIN_CLAMP`, `SUB_CLAMP`, `TOOL_CLAMP`, `TOOL2_CLAMP` in `[func]`, and the Mori Seiki's `C_SYNC` left `[spindle_sync]` for `[func]`, as the Nakamura and Doosan files write them (machine-config 5: the states of a spindle table and of `[spindle_sync]` are the values of their word). The codes are unchanged. `tests/Ncx.Config.Tests/Fixtures/expected-machine-warnings.txt` keeps only its comments; no WARNING needed a question. The eight files of `machines/` load without ERROR and without WARNING (`ExampleMachinesTests.ShippedMachine_EveryFileOfMachines_LoadsWithoutErrorAndWithoutWarning`).
  - Templates are validated when the loader reads the file, as the disposition of wave-1 question #61 says: `Template.Check` parses a text for its mistakes alone, `TomlDocument.CheckTemplate` parses each text once and reports it on the first key that writes it, `ConfigTable.Template`, `FunctionValue` and `CheckTemplate` call it on the line of the key, and the `[system_variables]` entries are checked the same way. The NCX text of an expansion rule is no template (its braces are expressions). A machine file with a template that cannot be parsed now loads with an ERROR on the template's line (CFG101, CFG102) and gives no machine. `TemplateSet` still reports on line 1, but only for a machine built in code, which never passed the loader; its `TODO(question)` marker went, and its malformed-template test builds the machine in code.
  - `ncx.toml`: `ProjectSettings` and `ProjectSettingsLoader` in `Ncx.Config` read `machine`, `machines`, `cycles`, `out` and `plugins` (machine-config 10, architecture 10). They use the P2-01 codes: an unknown key is a WARNING CFG002, a wrong type an ERROR CFG003. A folder left out is `machines`, `cycles` or `out`, the names of the project layout, and every folder counts from the folder of `ncx.toml`, which is the working directory. `ProjectSettings.MachineLine` keeps the line of the `machine` key for the diagnostic of a machine that cannot be found.
  - `Ncx.Cli`:
    - `RunMachine` picks the machine of a run: `ncx.toml` of the working directory, then `--machine`, else the machine `ncx.toml` names, else `DefaultMachine` (architecture 10, D103).
    - It then loads the catalog file of `[cycles] catalog` with `CycleCatalogLoader.LoadText` and puts it beneath the machine's `[[cycle]]` entries with `WithCatalog`. This replaces P1-07's TODO in `Pipeline.LoadMachine`, which went.
    - `ProjectFolders` searches for a machine in the machine folder that `ncx.toml` names, then `machines/` of the working directory, then `machines/` of the tool's own folder; it searches for a catalog in the cycle folders alone, in the same order, because the path rules of `--machine` are none of its rules (review fix below). `.toml` is added to a name without it.
    - A file found by name is named in diagnostics by its path from the working directory when it lies below it, else by its full path. A path is named as given.
    - `RunSettings.WorkingDirectory` and `ToolFolder` default to the process's working directory and `AppContext.BaseDirectory`, so the tests pass folders of their own (`tests/Ncx.Acceptance/Cli/ProjectHarness.cs`).
    - Codes, all exit 2 before the run: CLI200 (a name in no machine folder, reported on the value of `--machine` or on the line of the `machine` key of `ncx.toml`), CLI201 (`ncx.toml` cannot be read), CLI202 (the catalog is in no cycle folder or cannot be read). An `ncx.toml` or catalog that loads with an ERROR stops the run with exit 1, like the machine file.
    - `ncx.toml` is read by `check`, `trace` and `annotate` only; `format` takes no machine file (D91, the architecture 10 flowchart).
    - The build copies `machines/` and `cycles/` next to the tool (`Ncx.Cli.csproj`), so the tool's own folder holds the shipped files.
  - Acceptance: `tests/Ncx.Acceptance/Examples/ExampleMachineCheckTests.cs` checks the eight pairs through `CheckCommand.Run`, with the repository as the tool's own folder. The expected files are `tests/Ncx.Acceptance/Expected/<example>.<machine>.check.txt`: empty for `2.5D_FRAESEN` and `INCREMENTAL_SUB` with both mills and for `POLAR_FACE` with `nakamura-ntjx` (every `HOME` axis has its reference point now, D100); the VM540 of `PATTERN_LOOP` with both mills; two VM500 for `MILLTURN_TRANSFER` with `millturn1` (question 4 below).
  - Open questions, each marked `TODO(question)`:
    1. `src/Ncx.Config/ProjectSettingsLoader.cs`: machine-config 10 writes `plugins = ["MyShop.NcxPlugins.dll"]` and D80 a `[plugins.<name>]` section in the same file; TOML cannot hold both under `plugins`. An array is read as the assemblies, a table as the per-plugin settings.
    2. `src/Ncx.Cli/ProjectFolders.cs`: which wins when a file exists at the path given and a machine of that name exists as well. The file at the path wins, which keeps P1-07's reading of every `--machine`.
    3. `src/Ncx.Cli/RunMachine.cs`: where ncx looks for the catalog of `[cycles] catalog`, and what a catalog that cannot be found means. It is searched in the cycle folders alone, in the order of the machine folders (a value with a folder in it below each of them, a full path in none), and one that cannot be found is an unreadable input, exit 2.
    4. `tests/Ncx.Acceptance/Examples/ExampleMachineCheckTests.cs`: VM 5 checks the spindle of the current tool holder before a `LINE`. `millturn1.toml` gives its turret the driven-tool spindle `S3`, so the turning lines 13 and 14 of `MILLTURN_TRANSFER` report VM500 although `MAIN` turns.
  - Not edited, for the merge step: architecture 10 still calls `--machine` "a machine file by path; without it the built-in default machine of D103". Under P2-04 it is a machine file by name in `machines/` or by path, and without it the machine that `ncx.toml` names.
  - Shared files:
    - `src/Ncx.Cli/Pipeline.cs` (P1-07): the machine of the run through `RunMachine`.
    - `RunSettings.cs`: two properties.
    - `Commands/RunOptions.cs`: the `--machine` description.
    - `InputFile.cs`: a `Read` overload that reads one path and names another.
    - `Ncx.Cli.csproj`: the copied folders.
    - The range lines of both `DiagnosticCodes.cs`.
    - P2-01/P2-02 files: `ConfigTable.cs`, `TomlDocument.cs`, `MachineConfigLoader.Variables.cs`, `Templates/Template.cs`, `Templates/TemplateSet.cs`.
    - Tests: `TemplateSetTests.cs`, `ExampleMachinesTests.cs`, `FixtureTests.cs`.
    - READMEs: `machines/`, `cycles/`, `src/Ncx.Cli/`, `src/Ncx.Config/`, and the test READMEs.
  - Done when:
    - `ncx check examples/2.5D_FRAESEN.ncx --machine heidenhain-itnc530` runs: holds. `CommandLine_ExampleWithItsMachineByName_ExitsZero` runs it through `Program.Run`, and `dotnet run --project src/Ncx.Cli -- check docs/spec/examples/2.5D_FRAESEN.ncx --machine heidenhain-itnc530` exits 0.
    - The five examples check with no ERROR with their machine files: holds for all eight pairs of the phase file (`Check_ExampleWithItsMachineByName_RaisesNoError`). The acceptance run `dotnet run --project src/Ncx.Cli -- check docs/spec/examples/POLAR_FACE.ncx --machine nakamura-ntjx` exits 0.
    - This closes M3 and phase 2: M3 holds (architecture 12: the machine files load, the templates render and match (P2-02), the five examples check with no ERROR with their machine files). The rest of the phase 2 exit checklist (`phases.md`, the findings marked resolved, the schedule) is the merge step's bookkeeping.
- Claude (agent), 2026-09-13, review fixes of part two (branch `p2-04b-machines-folder`):
  - The blocking finding on `src/Ncx.Cli/ProjectFolders.cs`: `FindCatalog` went through the lookup of `--machine`, whose first step takes a file of that name in the working directory, or a value with a folder in it, as a path. A machine file named after its catalog (`siemens.toml` with `catalog = "siemens.toml"`) was loaded as its own catalog, against the order that the `TODO(question)` of `RunMachine.WithCatalog`, `cycles/README.md` and this log state.
  - `FindCatalog` now looks in the cycle folders alone. A value with a folder in it is a path from each cycle folder in turn. A full path, or a value that climbs out with `..`, names no file of them and is CLI202. The path rules stay with `FindMachine`. This is a reading, not a decision: open question 3 above names it, with the workaround updated.
  - Tests in `RunMachineTests`: `CycleCatalog_FileOfItsNameInTheWorkingDirectory_DoesNotShadowTheCycleFolder` (the reviewer's reproduction), `CycleCatalog_ValueWithAFolder_IsLookedForBelowTheCycleFolders` and `CycleCatalog_FullPathOutsideTheCycleFolders_ExitsTwoWithCli202`. `MillWithCatalog` writes the catalog as a TOML literal string, so that a full path of any system is written as it is.
  - The "Done when" criteria hold as stated above.
