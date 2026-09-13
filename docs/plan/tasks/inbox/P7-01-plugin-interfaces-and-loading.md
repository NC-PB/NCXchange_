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
