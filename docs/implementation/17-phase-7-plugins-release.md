# Phase 7: plugins and release 1.0

Status: written 2026-09-11; open on 2026-09-18. P7-01 is in `../plan/tasks/done/`. P7-02 is on `main` (87b8917) and waits for the answer on the block count of the plugin INFO line. P7-03, the release, waits for the maintainer. Milestone M10. Tasks P7-01 to P7-03. Closed when the template builds and runs unchanged, the coolant clutch sample expands and restores through a plugin, a sample plugin puts Z on its own line, and a tagged 1.0 with release notes exists (`../plan/phases.md`).

## Entry state

For P7-01: P1-06 (the expander calls `IProgramRewriter`), P3-01 (the readers call `ISourceRule`), P3-03 (the compilers raise `BLOCK_WRITE` to `IBlockWriter`), P1-05 (`IVmListener`); the four interfaces exist in their home projects since those tasks (D106), so P7-01 adds the loader and the plugin-facing types, not the hooks. For P7-03: everything.

## Decisions needed first

D106 answered (it shapes the template's project reference and the load context). D80 (plugin settings) governs the context.

## Tasks

### P7-01 Plugin interfaces and loading

Files in `src/Ncx.Plugins/`: `PluginLoader` (one `AssemblyLoadContext` per DLL from `plugins/` and the `[plugins]` list of `ncx.toml`; the context resolves every `Ncx.*` assembly from the host so that types are identical and isolates everything else the plugin brings; discovers the types implementing the four interfaces; a plugin that fails to load or throws in a method is reported with its name as a `PLG` ERROR and dropped for the run, never taking the CLI down), `PluginSet` (the rewriters, source rules, listeners and block writers in load order), `PluginRewriteContext` (the concrete `RewriteContext` of `Ncx.Core`: machine name, channel, line, the plugin's own `[plugins.<name>]` settings as a string dictionary, D80; no VM; `RewriteResult` itself is in `Ncx.Core` since P1-06, and inserted NCX text that does not parse under the D95 option is rejected here with a `PLG` ERROR), `PluginDiagnostics` (every diagnostic from a plugin carries the plugin name; the INFO line `plugin <name>: inserted 2 blocks at line 12`); the composition root of `Ncx.Cli` wires the set into the expander, the readers, the VM and the compilers.

Cite: architecture 9 (the plugin table), 10; VM 7 (the four places); code-guidelines 5 (plugin isolation), 10, 11; D61, D80, D106.

Tests first (`tests/Ncx.Acceptance/Plugins/`, with a small test plugin project built as part of the test run): the `CoolantClutchRule` of code-guidelines 11 as a plugin produces the same generated blocks as the configuration rule of P1-06 and the same state afterwards; a plugin whose `Rewrite` throws is reported by name and the run continues without it; a plugin that inserts text that does not parse is reported with the line; the settings of `[plugins.MyShopRules]` arrive in the context; a listener plugin sees `Before` and `After` and cannot mutate them (compile-time: the snapshot types are immutable records).

### P7-02 Plugin template and `ncx plugin` commands

Files: `templates/ncx-plugin/` exactly per code-guidelines 11 (`MyShopRules.csproj` referencing `Ncx.Plugins` only, `EnableDynamicLoading`, no other packages; `CoolantClutchRule.cs` as printed there; `ZOnItsOwnLine.cs` as a commented-out `IBlockWriter`; `README.md` with the six steps and the commands to copy; `MyShopRules.Tests/CoolantClutchRuleTests.cs` with one string-in string-out test), optionally packaged as a `dotnet new ncx-plugin` template; `src/Ncx.Cli/Commands/PluginCommand.cs` (`ncx plugin new <name>` copies and renames the template into `./<name>/`; `ncx plugin build [<folder>]` runs `dotnet build`, copies the DLL into `plugins/` and adds the line to `ncx.toml`; `ncx plugin check <dll>` loads it in isolation and lists the interfaces it implements; `ncx plugin test [<folder>]` runs `dotnet test`); `samples/plugins/CoolantClutch/`, `samples/plugins/ZOnItsOwnLine/`, `samples/plugins/ThroughCoolantSourceRule/` (folds `M5`, `M51`, `M3 S` back into `COOLANT:THROUGH=ON`, the D66 case), each with a test; `docs/plugins.md` (the six steps, the four interfaces with one example each, what a plugin may not do per D61, the settings of D80, how a plugin is debugged with `trace`).

Cite: code-guidelines 10, 11; architecture 9, 10; D61, D66, D80.

Tests first: `ncx plugin new Sample` then `ncx plugin build` in a temporary folder produces a DLL that `ncx plugin check` lists as `IProgramRewriter`; the three samples build and their tests pass; `ncx compile` of a program with `COOLANT:THROUGH=ON` and the clutch plugin prints `plugin CoolantClutch: inserted 2 blocks at line 12` (M10); the Z writer splits `L X+10 Z-5` into two Heidenhain lines.

### P7-03 Release 1.0

Files and steps: `Ncx.Cli` packed as a .NET global tool (`PackAsTool`, `ToolCommandName` `ncx`, `dotnet tool install --global NCXchange.Cli` from the package); self-contained single-file binaries for `win-x64`, `osx-arm64`, `osx-x64`, `linux-x64` published by CI on a `v*` tag; `CHANGELOG.md` started with 1.0; release notes assembled from `decisions.md` (D1 to the last row), the milestone table and the known limits; `docs/reading-the-code.md` final; every folder README checked; the front page of `docs/README.md` states the known limits (no kinematics, Heidenhain turning and GILDEMEISTER structure programming read as `RAW`, the runtime is an estimate, the machine files are sketches not verified on the machines); a definition-of-done sweep over the tree (every non-trivial method cites its sentence; the generated tables equal the code; the analyzers are quiet); tag `v1.0.0` on `main` with green CI.

Cite: architecture 12; code-guidelines 12; D68, D69, D72, D75.

Tests first: a fresh machine (a clean CI job) installs the tool from the package and runs the acceptance commands of the release notes (`format`, `check`, `convert`, `compile`, `analyze` on the examples) with exit code 0.

## Risks and open ends

- `AssemblyLoadContext` with shared `Ncx.*` assemblies: a plugin built against an older `Ncx.Plugins` will load until it calls a changed member; from 1.0 on the public surface of Core, Readers, Compilers and Plugins is versioned and `ncx plugin check` reports the version it was built against.
- `ncx plugin build` shells out to `dotnet`; the SDK must be on the path, which is step one of the README. The command says so when it is not.
- Single-file publish and `Tomlyn` or `System.CommandLine` trimming: publish without trimming in 1.0.

## Exit checklist

- `phases.md` row 7: the template builds and runs unchanged; the clutch sample expands and restores through a plugin; a sample plugin puts Z on its own line; `v1.0.0` tagged with release notes.
- `docs/plugins.md` exists; the plugin commands are in the CLI table.
- Every task file of phases 0 to 7 in `done/` with a log; `PL-01` and `PL-02` remain in `inbox/`.
- `01-findings.md` every row marked resolved or carried into a `later` note.

## Log

(filled when the phase starts and when it closes)
