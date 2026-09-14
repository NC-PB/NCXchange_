# Fakes

Small hand-written doubles for the compiler framework (code-guidelines 8): `FakeCompiler.cs` is a compiler of a controller family on top of `CompilerBase` (`%`, `O` programs and subprograms, `program_end` and `sub_end`, `RAW` verbatim, `CYCL DEF n` with the native parameters, `G0` and `G1` with the feed written on change, the tool change and the preload of the framework, `M98 P`, comments in parentheses), `FakeMachines.cs` gives it a three-axis mill with the `[format]` and `[tool_change]` tables a test needs, `FakeCompile.cs` compiles an NCX text for such a machine, and `ZOnItsOwnLine.cs` is a block writer as a plugin writes one (code-guidelines 11).

Never here: the real compilers (`src/Ncx.Compilers/Heidenhain/` P3-04, `Fanuc/` P3-06).
