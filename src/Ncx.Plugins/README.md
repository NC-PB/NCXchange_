# Ncx.Plugins

What a plugin project references (architecture 9; D106): the plugin loader, which loads each plugin DLL into its own `AssemblyLoadContext` that shares the `Ncx.*` assemblies with the host, the concrete `RewriteContext` built from the plugin's own settings (D80), and the diagnostics helpers for plugins. The four plugin interfaces are not here: `IProgramRewriter` and `IVmListener` live in `Ncx.Core`, `ISourceRule` in `Ncx.Readers`, `IBlockWriter` in `Ncx.Compilers`, each with its caller, and a plugin gets them through the references of this project (D106).

Empty so far: `Placeholder.cs` gives the project something to compile until P7-01. The template that `ncx plugin new` copies is `templates/ncx-plugin/` at the repository root (P7-02, code-guidelines 11).

Never here: a way into the state of the virtual machine (D61); a rule the machine configuration can express (code-guidelines 10.1).
