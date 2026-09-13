# Phase 2: machine configuration

Status: 2026-09-11, not started. Milestone M3. Tasks P2-01 to P2-04. Closed when the five example machine files (with `millturn1.toml`, D104) load, the five examples check with no ERROR with their machine files (D103), a template renders from an NCX block and matches back to the same words, and a bad file reports the line (`../plan/phases.md`).

## Entry state

Split in two (`20-schedule.md`): P2-01 and P2-02 start right after phase 0 and finish before P1-01, because the VM reads the configuration from its first task (F16, F27). P2-03 and P2-04 follow phase 1, because P2-04's acceptance runs `ncx check` (P1-07). The phase closes after P2-04.

## Decisions needed first

D105 (function values as strings) and D103 (the built-in `DefaultMachine`) before P2-01; D104 (`millturn1.toml`) and D100 (the reference and dynamics data in the machine files) before P2-04. Document fixes F23, F24, F25 in the tasks named; F27 was made on 2026-09-11.

## Tasks

### P2-01 TOML schema and loading

Files in `src/Ncx.Core/Machine/` (the records, D107) and `src/Ncx.Config/` (the loader):

- One record per table of `machine-config.md` in `src/Ncx.Core/Machine/`, one file each, `sealed`, `required` properties, loaded once and never changed (code-guidelines 7): `MachineConfig`, `MachineIdentity` (`[machine]`), `OutputFormat` (`[format]`), `ToolChangeConfig`, `HomeConfig`, `SetposConfig`, `ResourceDef` (`[[resource]]`, with `channel`, F23), `AxisDef` (`[[axis]]`, with `home`, `home2`, `limits`, `rapid`, `max_feed`, `acceleration`, `programming`, `clamp`; `home` and `limits` in machine coordinates), `PositionsTable` (`[positions]`: named machine-frame positions, each a set of axis values, `tool_change = { X = 0, Z = -120 }`, D100), `DynamicsConfig`, `DiameterConfig`, `FunctionTable` (`[spindle.ROLE]`, `[spindle_mode.ROLE]`, `[spindle_sync]`, `[coolant]`, `[func]`, with `channel`, `channels`, and the `ExpansionRule` keys `pre`, `post`, `requires`, `restore` of 5a), `FuncMeta`, `WorkpieceConfig`, `SyncConfig`, `TransformTable`, `RetractTable`, `ToleranceTable`, `RawTable` (F23), `CyclesConfig`, `VariablesConfig`, `SystemVariables`, `KinematicTree` (`[[node]]`, loaded, not interpreted), `LimitPolicy`.
- `MachineConfigLoader.Load(path)`: Tomlyn parsed into its document model and walked table by table (not reflection binding), so that an unknown key reports its line and the nearest known key, a wrong type reports the line, a missing required key reports the table. Function values per D105: strings, a bare integer meaning `M{n}`, and the loader normalizes every M or G code to its canonical spelling without leading zeros (`8`, `08`, `M8`, `M08` all become `M8`; `G01` becomes `G1`), so that the compiler always writes the normalized form. Unknown keys are WARNINGs (the schema is a sketch until the compiler exists, P2-01), missing required keys and wrong types are ERRORs, more than one resource of a kind without a default is an ERROR (VM 3.8 rule 2), a resource that names an undeclared axis is an ERROR (F23), `CFG` codes.
- `JobManifest` and `JobManifestLoader` (`<name>.ncxjob.toml`: `[job] name`, `machine`; `[[channel]] id`, `file`, optional `program` (D48); `[shared] spindles`, `axes` (D20, F24)); `VarsFile` (`<name>.vars.toml`, numbers and strings into a `VariableStore` seed).
- `DefaultMachine` (D103): the in-code configuration used when no file is given.
- `MachineConfig.ResolveRole`, `ResolveAxis`, `FindFunction(name, state)` as methods on the records, so that readers, compilers and the VM ask the same object, and `TemplateSet.FindFunctionByCode(native)` (compared by number, D105) in `Ncx.Config` (architecture 6, D107).

Cite: machine-config 1 to 9 (every table), 11; VM 3.8; architecture 6; D15, D20, D48, D103, D105.

Tests first (`tests/Ncx.Config.Tests/`): the five example files (with `millturn1.toml`, D104) load without ERROR and the WARNINGs are listed in a committed expected file (each one names a gap in a file, which P2-04 fixes, or in the schema, which this task fixes by editing `machine-config.md`); a file with `[formt]` reports the line and suggests `[format]`; `decimals = "3"` reports a type error with the line; two work spindles without `default_spindle` is an ERROR; `ON = 8`, `ON = "08"` and `ON = "M08"` all load as `M8` (D105); a job manifest with two channels and a `program` loads; a vars file seeds `Q1 = 10`.

Document fixes in this task: F23 (schema completed from the files: `[raw]`, `channel` on resources, the function states used), F24 (job manifest names, `[shared] axes`, `JobManifest.Machine`); F27 was made on 2026-09-11.

### P2-02 Templates both ways

Files: `src/Ncx.Config/Templates/TemplateSet.cs` (the parsed templates of one machine, built from the template text of its records, with `FindFunctionByCode`, D105, D107), `src/Ncx.Config/Templates/Template.cs` (parsed once in the constructor: literal text and `Placeholder`s with name and format suffix; `\n` splits into lines), `Placeholder`, `TemplateValues` (a string-keyed bag of decimals, ints and strings), `Template.Render(values)` (the format suffix pads numbers, `{tool:02}` gives `04`, `{tool:02}.` keeps the literal point; a missing value is a `CFG` ERROR naming the placeholder and the template, machine-config introduction), `Template.Matches(text, out captured)` (the same template turned into a regular expression with a named group per placeholder: an integer group for padded placeholders, a signed decimal group otherwise, literal text escaped, whitespace tolerant between words; an M or G code in the literal text is matched by number, so that the normalized template `M8` matches a source `M08` and `G1` matches `G01` (D105); the reader tries the tables of its machine before its generic rules).

Cite: machine-config introduction (placeholders and suffixes), 3 (the templates found in the manuals), 5; architecture 6 (`Template.Matches`).

Tests first: every template of the five example files (with `millturn1.toml`, D104) renders from a sample value set and matches back to the same values (a table-driven test over the loaded configurations); `G340 T{tool:02}{offset:02}. A{next:02}.` renders `G340 T0101. A02.` and matches it; `T{tool} M6` matches `T4 M6` and not `T4`; `M{mark} P{paths}` matches `M106 P12`; `M8` matches `M08` and `M008`, and `M{mark} P{paths}` matches `M0106 P12` (D105); a template with a `\n` renders two lines; a missing placeholder value is the ERROR of the introduction.

### P2-03 Cycle catalogs

Files: `cycles/fanuc.toml`, `cycles/heidenhain.toml`, `cycles/siemens.toml` per machine-config 6 and controller-mapping 5; `CycleCatalog`, `CycleEntry` (Name, Native, Params with their kinds and native names, `AbsoluteFromSurface`, `Modal`, `Contour` (F25, D65), the `ExpansionRule` keys, `CycleF` and `CycleDwell` placement, `Axis` handling); `CycleCatalogLoader`; the built-in drilling family (`DRILL`, `DRILL_DWELL`, `PECK`, `CHIP_BREAK`, `TAP`, `REAM`, `BORE`) defined in code and overridable per machine (machine-config 6); `CYCLE:<controller>=n` passing through to the same family as native (D45, D94). Entries beyond the drilling family as far as the corpus needs them: Fanuc `TURN_OD`, `TURN_ID`, `THREAD`, `FACE` (the modal `G90`/`G92`/`G94`), `ROUGH_TURN`, `ROUGH_FACE`, `FINISH`, `GROOVE`, `COMPOUND_THREAD` (`G70`..`G76` with `Contour`); Heidenhain 200 to 209, 240, 241, 251 to 254, 256, 257; Siemens `CYCLE81` to `CYCLE86`, `CYCLE830`, `CYCLE840` with `_AXN`.

Cite: language 4.7, 4.7.1; machine-config 6; controller-mapping 5; controllers `fanuc.md` 6, `heidenhain.md` 5, `siemens.md` 7; D29, D45, D59, D65, D94.

Tests first: the three catalogs load; `G81`, `CYCLE81` and `CYCL DEF 200` map the same NCX words (`SURFACE`, `CLEARANCE`, `DEPTH`, `SAFE`, `CYCLE_F`, `CYCLE_DWELL`, `CYCLE_RETRACT`); the Heidenhain entry converts `Q203`-relative depths to absolute through `AbsoluteFromSurface`; the Fanuc turning entries are marked modal; a `G71` entry carries `Contour`; a catalog entry with a `pre` rule loads into an `ExpansionRule`.

Document fix in this task: architecture 6 and 13 (`CycleEntry.Contour`).

### P2-04 Example machines and the `machines/` folder

Files: `machines/nakamura-ntjx.toml`, `doosan-puma-2600sy.toml`, `mori-ntx1000-mapps.toml`, `dmg-ctx-840d.toml` copied from `docs/spec/examples/machines/` and kept identical (the fixture-identity test extends to them; the fixes below are made in `docs/spec/examples/machines/` and copied); `millturn1.toml` (D104) treated the same way: fixed in `docs/spec/examples/machines/` and copied; `fanuc-mill-30i.toml`, `heidenhain-itnc530.toml`, `siemens-840dsl-mill.toml` as plain single-channel mills with generic words only, for the readers and compilers of phases 3 and 5; every `[[axis]]` gets `home`, `limits`, `rapid`, `max_feed`, `acceleration`, every spindle table `rpm_min`, `rpm_max`, `accel_time`, every file a `[dynamics]` table and a `[positions]` table with `tool_change` and, where the machine has one, `program_end` in machine coordinates (D100), values marked "not verified on the machine" in a comment; `nakamura-ntjx.toml` declares axis `C2` (F23). `Ncx.Cli` resolves `--machine <name>` in `machines/` (the folder of `ncx.toml`, then the working directory, then the tool's own folder) or by path; `ncx.toml` read from the working directory (`machine`, `machines`, `cycles`, `out`, `plugins` per machine-config 10).

Cite: machine-config 10, 11; controllers `machine-builders.md` 3; D100, D104.

Tests first: the copies equal the originals; the eight files load without ERROR and without WARNING (every WARNING of P2-01's expected file is gone); `ncx check docs/spec/examples/2.5D_FRAESEN.ncx --machine heidenhain-itnc530` exits 0; the five examples check with no ERROR with their machine files: `2.5D_FRAESEN`, `PATTERN_LOOP`, `INCREMENTAL_SUB` with `heidenhain-itnc530` and with `fanuc-mill-30i`, `MILLTURN_TRANSFER` with `millturn1`, `POLAR_FACE` with `nakamura-ntjx` (the second reading of the phase 1 acceptance under D103).

Acceptance run:

```
dotnet run --project src/Ncx.Cli -- check docs/spec/examples/POLAR_FACE.ncx --machine nakamura-ntjx
```

## Risks and open ends

- The schema is finalized by the compilers (machine-config status line); P2-01 will not get every key right and P3-04, P3-06, P5-02 will add keys. Keep the loader strict on types and lenient on unknown keys until P5-02, then turn unknown keys into ERRORs in a decision of its own.
- `Template.Matches` on free-form builder lines (`G340 T0101. A02.`) must tolerate the spacing and the trailing whitespace the Nakamura files carry (`G411F12. `); test with lines copied from `NAKAMURA_WY250L_O1000.path1.nc`.
- The dynamics values of D100 are guesses; the runtime estimate of P4-02 is only as good as they are, and the report says so.

## Exit checklist

- `phases.md` row 2 as amended: the eight machine files load; templates render and match; a bad file reports the line; the five examples check with no ERROR with their machine files.
- D104, D105 rows present; machine-config 4, 5, 8, 11 read as amended; `tasks/README.md` and `phases.md` carry the corrected dependencies.
- P2-01 to P2-04 in `done/`; F13 (machine data part), F16, F17, F22 to F25, F27 marked resolved.

## Log

(filled when the phase starts and when it closes)
