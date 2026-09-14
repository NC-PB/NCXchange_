# P7-01 Plugin interfaces and loading

Phase: 7 | Milestone: M10 | Depends on: `P1-06`, `P1-05`, `P3-01`, `P3-03` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

`Ncx.Plugins`: what only plugins need behind the four hooks, and safe loading of user DLLs.

## Scope

- The four interfaces already live with their callers, one method each: `IProgramRewriter` and `IVmListener` in `Ncx.Core`, `ISourceRule` in `Ncx.Readers`, `IBlockWriter` in `Ncx.Compilers` (D106). `RewriteResult` (`Unchanged`/`Replace`/`Surround(before, after, reason)`) and the `RewriteContext` abstraction exist in `Ncx.Core` next to `IProgramRewriter` since P1-06; `Ncx.Plugins` holds the loader, the concrete `RewriteContext` built from the plugin's own settings dictionary of D80 (machine name, channel, line, settings, no VM) and the diagnostics helpers; it references Core, Readers and Compilers, so that a plugin project references `Ncx.Plugins` alone.
- Loading with an `AssemblyLoadContext` per plugin that shares every `Ncx.*` assembly with the host and isolates everything else the plugin brings (D106), from `plugins/` and the `[plugins]` section of `ncx.toml`, with the plugin's own `[plugins.<name>]` settings passed through the context (D80); a failing plugin reports its name and never takes the CLI down.
- The expander calls the rewriters, the readers call the source rules, the VM the listeners, the compilers the block writers; diagnostics from plugins carry the plugin name.

## References

- architecture.md section 9 (plugin table)
- ncx-virtual-machine.md section 7 (the four places)
- code-guidelines.md sections 10 and 11

## Done when

- The coolant clutch sample as a plugin (`CoolantClutchRule`) produces the same generated blocks as the configuration rule of phase 1.
- A plugin that throws is reported and the run continues without it.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

Claude (agent), 2026-09-14. `src/Ncx.Plugins/` written, the placeholder removed: `PluginLoader` (`Files` gives the DLLs of the `[plugins]` list of `ncx.toml` and of `plugins/` in load order, `Load` loads each into a `PluginLoadContext` of its own and makes an object of every public class of the four interfaces, `PluginClasses`), `PluginLoadContext` (every `Ncx.*` assembly from the host, the rest the plugin brings from its folder, D106), `PluginSet` (rewriters, reader rules, listeners, block writers in load order), `PluginRewriteContext` (machine name, channel, line, the plugin's own settings, no VM; D61, D80), `PluginDiagnostics` and `DiagnosticCodes` (`PLG001` the INFO of inserted blocks, `PLG002` not loaded, `PLG003` failed in a method, `PLG004` a block that does not parse, `PLG005` no plugin class; every message begins "plugin <name>: "). The composition root loads the set per run (`src/Ncx.Cli/RunPlugins.cs`) for check, trace, annotate and analyze (`Pipeline`), convert and compile, and the TODOs of P1-06, P3-02c and P3-03 are gone: rewriters into every expander (external programs of an INTERPRETED run included), reader rules into the reader of convert, listeners into the VM runs of the pipeline and of convert's check, block writers and rewriters into `CompileOptions`.

Decided while doing it (implementation, no specification change):
- Every class of a plugin sits behind an entry of `PluginSet` (`PluginRewriter`, `PluginSourceRule`, `PluginListener`, `PluginBlockWriter`) that catches what it throws (`PluginFaults`: everything but running out of memory), reports it with the plugin's name and drops the whole plugin for the rest of the run. The plugin's diagnostics go straight into the diagnostics of the command, not into the lists the stages test with `HasErrors`, so a `PLG` ERROR sets exit code 1 while expansion, VM and compiler run to their end without the plugin, which is how "reported as a PLG ERROR and dropped for the run" and "the run continues without it" hold together with virtual machine 2.9. compile writes no file after such an ERROR (D205 as recommended, TODO(question) in `CompileCommand`).
- A reader rule writes into an `NcxBuilder` of its own; its blocks and comment lines are copied into the reader's builder (`ClaimedBlocks`) only when it claimed the block without throwing, so a rule that throws with a block begun leaves nothing and the reader reads the block (D5). A block writer that throws or leaves a null line gets its lines put back as it found them.
- A rewriter's answer is parsed under `AllowPseudoWords` before the expander sees it; any parser ERROR rejects the whole answer with `PLG004` on the block's line (the plugin is not dropped). A null answer is a failure (`PLG003`).
- The plugin's name is the DLL name without `.dll`, the key of its `[plugins.<name>]` section. The blocks of a loaded plugin carry that name as their origin, as architecture 9 says trace shows "the plugin's name": `Ncx.Core` gains the one-member interface `INamedRewriter`, which `ProgramRewriters` asks for the name before falling back to the type name (a shared-file change, test `NamedRewriterTests`).
- Assemblies are read into memory (`LoadFromStream`, with the `.pdb` when there is one), so that no DLL of `plugins/` is locked on Windows and `ncx plugin build` can replace one another run has loaded; a plugin's `Assembly.Location` is therefore empty.
- The tests load two plugin projects built with the solution, `tests/Ncx.Acceptance/Plugins/TestPlugins/ShopRules` (the `CoolantClutchRule` of code-guidelines 11 as printed, a settings rewriter, a reader rule, a listener, a Z writer) and `FaultyRules` (a class per failure, each on a word of its own); `Ncx.Acceptance.csproj` references them with `ReferenceOutputAssembly="false"` and copies the DLLs into `TestPlugins/` of its output folder (not `plugins/`, so no command a test runs there loads them). `ReferenceGraphTests` lists the two references; the solution holds both projects.

Open questions recorded as TODO(question): the count of the INFO line (the clutch rule inserts three blocks where D98, code-guidelines 11 and P7-02 print "inserted 2 blocks"; every inserted block is counted), the load order and where a listed name is found (the list first, then `plugins/` by name; a listed name that is no file of the working directory is looked for in `plugins/`), and a DLL without a plugin class (a WARNING, `PLG005`). D238 (list and sections in one `ncx.toml`) and D205 (no NC file after an ERROR) are cited where they apply. Plain TODO: what a plugin reports about a block of an external program names the file of the run.

Done when: the coolant clutch sample as a plugin produces the same generated blocks as the configuration rule and the same state afterwards (`CoolantClutchPluginTests`), holds; a plugin that throws is reported and the run continues without it, in each of the four places (`FailingPluginTests`), holds. The other tests of the phase file hold as well: a text that does not parse is reported with its line, the settings of `[plugins.<name>]` arrive in the context (`PluginLoaderTests`, `PluginWiringTests`), a listener sees Before and After and the snapshot types are immutable records (`ListenerPluginTests`).

Review fix, Claude (agent), 2026-09-14. `ncx compile` subscribes the listeners of the plugins to its STATIC run too, as virtual machine 7 ("as a listener it reads every event"), architecture 9 (`IVmListener` "on every VM event") and machine-config 10 (the plugin assemblies "subscribe to VM events") have it, and the TODO(question) on the listeners in compile is gone. `CompileOptions` gains `Listeners` (a type of P3-03, a shared-file change), which `CompilerBase.Compile` subscribes after its `StepRecorder` (also P3-03's); `CompileCommand` passes the listeners of the set with the rewriters and the block writers. In compile a listener also reads the BLOCK_WRITE the virtual machine raises for the compiler, with a copy of the words and the lines still empty; the compiler hands copies of its own to the block writers, so nothing a listener does reaches the output (D61). A listener that throws is a `PLG003` ERROR on the line of its event, and compile writes no file (D205 as recommended). Tests: `CompileListenerTests` in `Ncx.Compilers.Tests` (named in its README), `ListenerPluginTests.IVmListener_ListenerOfAPluginInACompile_ReadsTheEventsOfTheStaticRun`, `PluginWiringTests.Compile_ListenerOfAPluginThatThrows_IsReportedByNameAndNoFileIsWritten`. The branch was rebased onto main after P4-03. Both "Done when" criteria still hold.
