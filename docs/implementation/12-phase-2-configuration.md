# Phase 2: machine configuration

Status: written 2026-09-11; closed 2026-09-18 (Log). Milestone M3. Tasks P2-01 to P2-04. Closed when the five example machine files (with `millturn1.toml`, D104) load, the five examples check with no ERROR with their machine files (D103), a template renders from an NCX block and matches back to the same words, and a bad file reports the line (`../plan/phases.md`).

## Entry state

Split in two (`20-schedule.md`): P2-01 and P2-02 start right after phase 0 and finish before P1-01, because the VM reads the configuration from its first task (F16, F27). P2-03 and P2-04 follow phase 1, because P2-04's acceptance runs `ncx check` (P1-07). The phase closes after P2-04.

Checked 2026-09-18, at the close of phases 0 and 1. P2-01 (aee9b21) was on `main` before P1-01, as planned, but before phase 0 closed, since P0-04 and P0-06 came later the same day. P2-02 came in two parts: a1703a0 before P1-01 and 83f0207 right after it. The data parts of P2-03 and P2-04 did not wait for phase 1: the cycle catalogs (d4442ce) and the D100 machine data (54ffdbe) landed on 2026-09-13 before P1-01, and the catalog code (6ba04e4) right after P1-01. P2-04 part two (789ab3d, 2026-09-14) followed P1-07 (0c81e4e), as its acceptance needs `ncx check`. Phase 1 closed formally on 2026-09-18 (`11-phase-1-virtual-machine.md`, Log).

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

Files: `src/Ncx.Config/Templates/TemplateSet.cs` (the parsed templates of one machine, built from the template text of its records, with `FindFunctionByCode`, D105, D107), `src/Ncx.Config/Templates/Template.cs` (parsed once in the constructor: literal text and `Placeholder`s with name and format suffix; `\n` splits into lines), `Placeholder`, `TemplateValues` (a string-keyed bag of decimals, ints and strings), `Template.Render(values)` (the format suffix pads numbers, `{tool:02}` gives `04`, `{tool:02}.` keeps the literal point; a missing value is a `CFG` ERROR naming the placeholder and the template, machine-config introduction), `Template.Matches(text, out captured)` (the same template turned into a regular expression with a named group per placeholder: an integer group for padded placeholders, a group of words for the six placeholders whose value is words (`{name}`, `{axis}`, `{axes}`, `{move}`, `{channels}`, `{position:NAME}`; machine-config introduction, 3 and 5), a signed decimal group otherwise, literal text escaped, whitespace tolerant between words; an M or G code in the literal text is matched by number, so that the normalized template `M8` matches a source `M08` and `G1` matches `G01` (D105); the reader tries the tables of its machine before its generic rules).

Cite: machine-config introduction (placeholders and suffixes), 3 (the templates found in the manuals), 5; architecture 6 (`Template.Matches`).

Tests first: every template of the five example files (with `millturn1.toml`, D104) renders from a sample value set and matches back to the same values (a table-driven test over the loaded configurations); `G340 T{tool:02}{offset:02}. A{next:02}.` renders `G340 T0101. A02.` and matches it; `T{tool} M6` matches `T4 M6` and not `T4`; `M{mark} P{paths}` matches `M106 P12`; `M8` matches `M08` and `M008`, and `M{mark} P{paths}` matches `M0106 P12` (D105); a template with a `\n` renders two lines; a missing placeholder value is the ERROR of the introduction.

### P2-03 Cycle catalogs

Files: `cycles/fanuc.toml`, `cycles/heidenhain.toml`, `cycles/siemens.toml` per machine-config 6 and controller-mapping 5; `CycleCatalog`, `CycleEntry` (Name, Native, Params with their kinds and native names, `AbsoluteFromSurface`, `Modal`, `Contour` (F25, D65), the `ExpansionRule` keys, `CycleF` and `CycleDwell` placement, `Axis` handling); `CycleCatalogLoader`; the built-in drilling family (`DRILL`, `DRILL_DWELL`, `PECK`, `CHIP_BREAK`, `TAP`, `REAM`, `BORE`) defined in code and overridable per machine (machine-config 6); `CYCLE:<controller>=n` passing through to the same family as native (D45, D94). Entries beyond the drilling family as far as the corpus needs them: Fanuc `TURN_OD`, `THREAD`, `FACE` (the modal `G90`/`G92`/`G94`; `G90` turns outer and inner diameters alike and is the one cycle `TURN_OD`, language 4.7.1 and controller-mapping 5), `ROUGH_TURN`, `ROUGH_FACE`, `FINISH`, `GROOVE`, `COMPOUND_THREAD` (`G70`..`G76` with `Contour`); Heidenhain 200 to 209, 240, 241, 251 to 254, 256, 257; Siemens `CYCLE81` to `CYCLE86`, `CYCLE830`, `CYCLE840` with `_AXN`.

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

2026-09-18, the phase closes. Claude (agent), phase bookkeeping BK-01, on `main` at c2b683f, with .NET SDK 10.0.400 on macOS.

Built, by task; what was decided in each is in its log in `../plan/tasks/done/`:

- P2-01 TOML schema and loading: aee9b21.
- P2-02 Templates both ways: a1703a0 (parse, render, match), 83f0207 (`TemplateSet`).
- P2-03 Cycle catalogs: d4442ce (the three catalogs), 6ba04e4 (the code).
- P2-04 Example machines and `machines/`: 54ffdbe (the D100 data and the three mills), 789ab3d, which closed M3.
- Since then: FU-06 (e7456a3) applied the answered questions of wave 1 to the loader, the templates, the default machine and the four builder machine files. FU-07 (c2b683f) applied those of wave 2 to the loader of `ncx.toml`.

Decisions implemented: D100, D103, D104, D105 and D107, together with the earlier decisions that each task log names. The questions the tasks recorded are listed in `03-open-questions.md`. Those that need an answer are the open entries D115, D128, D129, D137 to D141, D144, D147, D150 to D164, D172 to D182, D194, D206, D207 and D238 of `../decisions/rationale.md`. Until they are answered the code and the machine files keep their `TODO(question)` workarounds (`20-schedule.md` 4, "if unanswered").

Exit checklist (`20-schedule.md` 6):

1. The row of `../plan/phases.md`, as amended to eight machine files, run as the commands below:
   - The machine files load: holds. Each of the eight files in `machines/` loads through `ncx check` of a five-line program, with exit 0 and no diagnostic. The five example machine files there equal those of `docs/spec/examples/machines/` byte for byte.
   - The five examples check with no ERROR with their machine files: holds for the eight pairs of P2-04. Each exits 0 and equals its expected file. The only WARNINGs are one `VM540` for `PATTERN_LOOP` on both mills and two `VM500` for `MILLTURN_TRANSFER` on `millturn1`.
   - A template renders from NCX words and matches back to the same words: holds. `RoundTrip_EveryTemplateOfTheMachineFiles_MatchesBackToItsSampleValues` passes with 447 rows over the eight files. On the command line, a program of `PRELOAD=4`, `TOOL=4`, `SPINDLE=CW RPM=1200`, `COOLANT=ON`, `COOLANT=OFF` and `SPINDLE=OFF` compiles for `fanuc-mill-30i` through its templates (`T4`, `T4 M6`, `S1200 M3`, `M8`, `M9`, `M5`) and converts back to the same text byte for byte.
   - A bad file reports the line: holds. An unknown table gives a WARNING on its line with the nearest known table, and a wrong value type gives an ERROR on its line with exit 1 (output below).
2. CI green on `main` on both operating systems: cannot be checked, because nothing is pushed (`10-phase-0-foundations.md`, Log, item 2).
3. Task files: P2-01 to P2-04 are in `done/` with their logs.
4. Decisions: D100, D103, D104, D105 and D107 have their rows in `decisions.md` and their answers in `rationale.md`. Machine-config 4, 5, 8 and 11 were amended when D100, D104 and D105 were applied, and by the document fixes F23 and F24 of P2-01. `../plan/tasks/README.md` and `../plan/phases.md` carry the corrected dependencies (F27). The phase took no other decision; the open entries above are unanswered.
5. The five examples: `ncx format --check` and `ncx check` without a machine file pass (phases 0 and 1), and `ncx check` with their machine files reports no ERROR (item 1).
6. The generated tables equal the code (phase 1, item 6). The `CFG` codes of this phase are not part of `diagnostics.md`.
7. `01-findings.md`: F16, F17, F22 and F27 were already resolved. This bookkeeping completed F13 (the machine data, 54ffdbe) and F25 (`CycleEntry.Contour`, 6ba04e4), and filled in F23 and F24 with their commits.
8. The entry state of phase 3 was checked against the commits and corrected.

Commands and results (the build, the formatting and the test run of the gate as in phase 0):

```sh
for m in dmg-ctx-840d doosan-puma-2600sy millturn1 mori-ntx1000-mapps nakamura-ntjx; do
  cmp docs/spec/examples/machines/$m.toml machines/$m.toml                          # equal, each
done
# load.ncx: FILE=BEGIN NCX=1, PROGRAM=BEGIN NAME="LOAD", UNITS=MM, PROGRAM=END, FILE=END
for m in machines/*.toml; do
  dotnet run --no-build --project src/Ncx.Cli -- check load.ncx --machine $m        # exit 0, no diagnostic, each of the eight
done
for pair in 2.5D_FRAESEN:heidenhain-itnc530 2.5D_FRAESEN:fanuc-mill-30i PATTERN_LOOP:heidenhain-itnc530 \
    PATTERN_LOOP:fanuc-mill-30i INCREMENTAL_SUB:heidenhain-itnc530 INCREMENTAL_SUB:fanuc-mill-30i \
    MILLTURN_TRANSFER:millturn1 POLAR_FACE:nakamura-ntjx; do
  f=${pair%%:*}; m=${pair##*:}
  dotnet run --no-build --project src/Ncx.Cli -- check docs/spec/examples/$f.ncx --machine $m   # exit 0, = Expected/$f.$m.check.txt
done
dotnet test tests/Ncx.Config.Tests --no-build \
  --filter "FullyQualifiedName~RoundTrip_EveryTemplateOfTheMachineFiles_MatchesBackToItsSampleValues"   # 447 passed
# tpl.ncx: one program with NUMBER=1234, the header block, PRELOAD=4, TOOL=4, SPINDLE=CW RPM=1200, COOLANT=ON,
# COOLANT=OFF, SPINDLE=OFF
dotnet run --no-build --project src/Ncx.Cli -- compile tpl.ncx --machine fanuc-mill-30i --output out   # exit 0
dotnet run --no-build --project src/Ncx.Cli -- convert out/tpl.nc --machine fanuc-mill-30i > back.ncx  # exit 0
cmp tpl.ncx back.ncx                                                                                   # equal
# bad-typo.toml: fanuc-mill-30i.toml with [format] written as [formt] on line 20
# bad-type.toml: fanuc-mill-30i.toml with decimals = "3" on line 22
dotnet run --no-build --project src/Ncx.Cli -- check load.ncx --machine bad-typo.toml   # exit 0
dotnet run --no-build --project src/Ncx.Cli -- check load.ncx --machine bad-type.toml   # exit 1
```

```text
bad-typo.toml(20): WARNING CFG002: Unknown table [formt]; the nearest known table is [format] (machine-config).
bad-type.toml(22): ERROR CFG003: decimals in [format] must be a table, not the string "3" (machine-config 2).
```
