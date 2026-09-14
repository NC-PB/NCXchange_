# P7-02 Plugin template and `ncx plugin` commands

Phase: 7 | Milestone: M10 | Depends on: `P7-01` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The lowest hurdle for an NC programmer: a working plugin to change one method in.

## Scope

- `templates/ncx-plugin/` per code-guidelines section 11: the project, one `.cs` with a rewriter that does something visible, one test, a README with the six steps, `ncx.toml` snippet; `dotnet new` template packaging optional.
- `ncx plugin new <name>` copies and renames; `ncx plugin build` builds into `plugins/` and registers the line in `ncx.toml`; `ncx plugin check <dll>` loads and lists the interfaces; `ncx plugin test` runs the plugin's tests.
- `samples/plugins/`: the coolant clutch rule, a `BLOCK_WRITE` writer that puts Z on its own line, a source rule that folds `M5`, `M51`, `M3 S` into `COOLANT:THROUGH=ON`.
- `docs/plugins.md`: the six steps, the four interfaces with one example each, what a plugin may not do (D61).

## References

- code-guidelines.md section 11
- architecture.md sections 9 and 10

## Done when

- The template builds and runs unchanged after `ncx plugin new`; the three samples build and their tests pass; `ncx compile` prints the INFO diagnostic (D98) `plugin <name>: inserted 2 blocks at line 12` for the clutch sample (M10).

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

Claude (agent), 2026-09-14. `templates/ncx-plugin/` written per code-guidelines 11: `MyShopRules.csproj` (`EnableDynamicLoading`, no package, the global usings of the Ncx namespaces, its tests left out of its files), `CoolantClutchRule.cs` as printed there, `ZOnItsOwnLine.cs` the Z writer of the sample commented out, `README.md` with the six steps, the commands to copy and the line of `ncx.toml`, and `MyShopRules.Tests/CoolantClutchRuleTests.cs`, one string-in string-out test (a program in, the expanded program out), with its project file `MyShopRules.Tests.csproj`, which the listing of the guidelines leaves out. The build of `Ncx.Cli` copies the template next to the tool (`templates/ncx-plugin/`). `src/Ncx.Cli/Commands/PluginCommand.cs` with `PluginNew`, `PluginBuild`, `PluginCheck` and `PluginTest` (helped by `PluginFolder`, `PluginProject`, `DotnetProcess`, `DotnetResult`, `NcxTomlPlugins`, `PluginCommandSettings`), the codes `CLI550` to `CLI559` (`DiagnosticCodes.Plugin.cs`), one registration line in `Program.cs`. `samples/plugins/CoolantClutch/`, `ZOnItsOwnLine/` and `ThroughCoolantSourceRule/`, each with a test project, in the solution under a folder `samples`. `docs/plugins.md`: the six steps, the four interfaces with one example each, what a plugin may not do (D61), its settings (D80), where ncx finds plugins, how a plugin is followed with `check`, `trace` and `annotate`, the commands with their codes. The optional `dotnet new ncx-plugin` packaging is not done.

Decided while doing it (implementation, no specification change):
- The template references the Ncx assemblies in the folder of ncx as DLLs, `Private="false"`: `Ncx.Plugins` and the three it brings, `Ncx.Core`, `Ncx.Readers`, `Ncx.Compilers`. A DLL reference carries no transitive references (checked: a project that references `Ncx.Plugins.dll` alone does not see `IProgramRewriter`, CS0246), a project reference needs the sources, and no package exists before P7-03, so the three stand beside `Ncx.Plugins`. The folder is the property `NcxFolder`, which `ncx plugin new` writes into both project files (the placeholder `NCX_FOLDER`, XML-escaped) and `ncx plugin build` and `test` pass as `-p:NcxFolder`, so that a plugin is always built against the assemblies of the ncx that loads it (D106). The tests of the template reference the same assemblies, copied next to them, and `Ncx.Config` for `DefaultMachine`, and pin the three test packages at the versions of `Directory.Packages.props`, since a copy of the template lies outside the repository.
- `ncx plugin new` copies every file of the template but `bin/` and `obj/`, replaces `MyShopRules` in paths and texts, and names every file written on the standard output. A name is ASCII letters, digits and underscores, parts joined by dots (machine-config 10 names `MyShop.NcxPlugins.dll`), no part beginning with a digit (`CLI550`); a folder that is there is `CLI551`, a missing template `CLI552`, each exit code 2.
- `ncx plugin build` runs `dotnet build <project> --nologo --disable-build-servers --output <plugin>/bin/ncx -p:NcxFolder=<folder of ncx>`: the place of the DLL is known, and no build server of dotnet stays behind in the folder of the user (nor holds the temporary folders of the tests on Windows). On a failure what dotnet wrote goes to the standard error before `CLI555`. The DLL and its `.pdb`, which the loader reads with it (P7-01), go into `plugins/` of the working directory, the folder `PluginLoader.PluginsFolder` names. `ncx.toml` is read before the build as every command reads it (`CLI201` exit code 2, an ERROR exit code 1).
- The line of `ncx.toml` (`NcxTomlPlugins`) changes the one line of the key only, so that comments and the other lines stay, and is kept only when the file loads with it and lists the plugin; the DLL is listed by its bare name, as machine-config 10 writes it and P7-01 finds it in `plugins/`.
- `ncx plugin check` loads through `PluginLoader.Load`, with the context and the diagnostics of a run, and lists the interfaces the `PluginSet` holds, with the number of classes, in the order of architecture 9; the version of the Ncx assemblies the plugin was built against (implementation 17, risks) is read from the metadata of the DLL with `System.Reflection.Metadata` of the base library. A DLL that is not there is `CLI002` (exit code 2), one that does not load `PLG002` of the loader (exit code 1).
- A dotnet that cannot be started is `CLI554` with exit code 1, since D97 gives exit code 2 only to a usage error, an unreadable input and a missing machine file.
- The samples reference `src/Ncx.Plugins` as `ShopRules` does (`Private="false"`, `ExcludeAssets="runtime"`), their tests `Ncx.Plugins` as well; `samples/plugins/.editorconfig` switches CA1707 off for their tests, as `tests/.editorconfig` does. The Z writer reads the direction of Z from `Before` and `After`, moves Z down after the other axes and up before them and repeats `FMAX`; a block whose Z is not known on both sides stays as the compiler wrote it. `ThroughCoolantSourceRule` holds its fold in `Fold(block, following, builder)`, the shape D232 recommends.
- `TemplateFilesTests` pins the template to code-guidelines 11 (its files, `CoolantClutchRule.cs` as printed) and the samples to the template.
- Document fix: the row of the plugin commands in architecture 10 names their arguments, `build [<folder>]`, `check <dll>`, `test [<folder>]` (F28).

Open questions recorded as TODO(question): the count of the INFO line (the clutch rule inserts three blocks where D98, code-guidelines 11 and this task print "inserted 2 blocks"; P7-01's question in `PluginRewriter`, cited in `TemplateTests` and the README of the template); the plugin that `ncx plugin build` and `test` take without a folder (`PluginFolder`: the working directory when it holds a project file, else the one folder of it that `ncx plugin new` made); what the plugin commands write on the standard output (`PluginCheck.Listing`); what `build` does with assemblies a plugin brings besides its own (`PluginBuild.CopyIntoPlugins`: only the plugin's DLL). D238 (the form of the line of `ncx.toml`, `NcxTomlPlugins.Add`) and D231, D232 (the fold of `ThroughCoolantSourceRule`) are cited where they apply. Plain TODO: the compile of `TemplateTests` runs with the `EchoCompiler` of the tests until a compiler of a controller family is registered in `Program.Compilers()` (P3-04, P3-06).

Done when: the template builds and runs unchanged after `ncx plugin new` (`TemplateTests`: new, build without a folder, check lists `IProgramRewriter`, the test of the template passes under `ncx plugin test`), holds; the three samples build and their tests pass, holds (the source rule folds in `Fold`; its `Read` claims nothing until D231 and D232 are answered); `ncx compile` prints the INFO of the clutch plugin at line 12, holds as `plugin CoolantClutch: inserted 3 blocks at line 12` through `CompileCommand` with the compiler of the tests, and waits for the answer to the count question for the text "inserted 2 blocks" and for P3-04 or P3-06 for `ncx compile` on the command line. The task stays in `inbox/` until then.
