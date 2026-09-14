# Expander

The stage between the parser and the virtual machine (virtual machine 1, architecture 5.5, D63): it turns the expansion rules of the machine file (`pre`, `post`, `requires`, `restore` on the function tables, the tool change and the catalog cycles, machine-config 5a) and the program rewriters of the plugins into ordinary NCX blocks around the block that triggered them, and applies `limits = "clamp"` (D64). It returns a new `NcxProgram`; the virtual machine executes the generated blocks like the rest, `ncx format` never writes them.

Start with `Expander.cs`: `Expand(program, machine, rewriters)` walks the blocks, and for each block `ExpansionRules` finds the rules it triggers, `RuleBlocks` writes their blocks, `ProgramRewriters` asks the rewriters, `LimitClamp` rewrites what is beyond a machine limit. `GeneratedText` parses every generated text under the option of D95 and marks it with its origin (`Model/GeneratedBlock.cs`).

| File | Rule |
|---|---|
| `IProgramRewriter.cs`, `RewriteResult.cs`, `RewriteKind.cs`, `RewriteContext.cs` | the plugin surface of the expander: one method, three answers, a context without the VM (architecture 9, code-guidelines 11; D61, D80, D106) |
| `ExpansionRules.cs`, `TriggeredRule.cs` | which rules a block triggers (machine-config 5, 5a) |
| `RuleBlocks.cs` | `@SAVE`, `requires`, `pre`, the block, `post`, `@RESTORE` (architecture 5.5) |
| `StateKeys.cs` | `SPINDLE` of a rule becomes `SPINDLE:MAIN`, the role of the default spindle (virtual machine 3.8 rule 2) |
| `PositionPlaceholder.cs` | `{position:NAME}` becomes the axis words of `[positions]` (D100) |
| `GeneratedText.cs` | parsing, origin and diagnostics of a generated block (D95, D98) |
| `ProgramRewriters.cs`, `BlockRewriteContext.cs` | `Unchanged`, `Replace`, `Surround` applied to the block (architecture 9) |
| `INamedRewriter.cs` | a rewriter whose blocks carry a name of its own, the plugin's, instead of the name of its type (architecture 9, P7-01) |
| `LimitClamp.cs` | `limits = "clamp"` for `RPM`, `F` and machine-frame targets (D64) |
| `BlockExpansion.cs` | one block while it is expanded, and where generated blocks may stand (language 4.13) |

The other half of generated blocks is in the virtual machine: `../VirtualMachine/VirtualMachine.Restore.cs` executes `@SAVE` and `@RESTORE` with the restore stack of `../VirtualMachine/RestoreRules.cs` (virtual machine 3.10).

Never here: VM state (the expander never sees it, architecture 5.5), the plugin loader and the concrete context with the plugin settings (`Ncx.Plugins`, D106), native controller codes (rules are NCX text, machine-config 5a).
