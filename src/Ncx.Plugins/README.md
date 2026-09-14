# Ncx.Plugins

What a plugin project references (architecture 9; D106), and the loading of user DLLs (implementation 17, P7-01). The four plugin interfaces are not here: `IProgramRewriter` and `IVmListener` live in `Ncx.Core`, `ISourceRule` in `Ncx.Readers`, `IBlockWriter` in `Ncx.Compilers`, each with its caller, and a plugin gets them through the references of this project (D106).

Start with `PluginLoader.cs`: `Files(workingDirectory, listed)` gives the DLLs of a working directory in load order, the `[plugins]` list of `ncx.toml` first, then every DLL of `plugins/`; `Load(...)` loads each into a `PluginLoadContext` of its own and makes an object of every public class that implements one of the four interfaces (`PluginClasses.cs`). The result is a `PluginSet`, the rewriters, reader rules, listeners and block writers in load order, which the composition root of `Ncx.Cli` hands to the expander, the readers, the virtual machine and the compilers (`../Ncx.Cli/RunPlugins.cs`).

| File | Rule |
|---|---|
| `PluginLoader.cs`, `PluginClasses.cs` | the DLLs of `plugins/` and of the `[plugins]` list, the classes of the four interfaces, a plugin that fails to load reported and left out (P7-01) |
| `PluginLoadContext.cs` | one `AssemblyLoadContext` per DLL: every `Ncx.*` assembly from the host, everything else the plugin brings from its own folder (D106) |
| `PluginSet.cs`, `Plugin.cs` | the plugins by the place they act in; a plugin that failed is asked nothing more in the run |
| `PluginRewriter.cs` | a rewriter in the expander: its context with the settings, its answer checked to parse under the option of generated text (D95), the INFO of the blocks it inserted, its blocks named after the plugin |
| `PluginSourceRule.cs`, `ClaimedBlocks.cs` | a reader rule in a reader: its blocks reach the program only when it claimed the block without failing (D5) |
| `PluginListener.cs`, `PluginBlockWriter.cs` | a listener on the events of the virtual machine, a block writer on BLOCK_WRITE; the lines of a writer that fails stay as it found them |
| `PluginRewriteContext.cs` | the concrete `RewriteContext`: machine name, channel, line and the plugin's own `[plugins.<name>]` settings, no VM (D61, D80) |
| `PluginDiagnostics.cs`, `PluginFaults.cs`, `DiagnosticCodes.cs` | what the plugins report, every message beginning with the plugin's name, `PLG001` to `PLG005` (D98) |

The template that `ncx plugin new` copies is `templates/ncx-plugin/` at the repository root (P7-02, code-guidelines 11). The tests are in `../../tests/Ncx.Acceptance/Plugins/`, with the two plugins they load.

Never here: a way into the state of the virtual machine (D61); a rule the machine configuration can express (code-guidelines 10.1).
