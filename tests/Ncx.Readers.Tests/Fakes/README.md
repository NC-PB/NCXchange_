# Fakes

Small hand-written doubles for the reader framework (code-guidelines 8): `FakeTokenizer.cs` cuts a Fanuc-like syntax into source blocks (`( )` comments, `/` and `/n`, `%`, `GOTO n`, `* - title`), `FakeReader.cs` is a reader of a controller family on top of `ReaderBase` (the structure of `O`, `N`, `M30`, `M2`, `M99`, `GOTO` and the calls of `M98 P`; `G0`, `G1`, `G28`, `G90`, `G91`, `F`, `M3`, `M5`, `M98`; the machine's functions; `MFUNC`; `RAW` for `G10` and the builder codes), `CodeRule.cs` is a reader rule as a plugin writes one, and `FakeRead.cs` reads a snippet against a small Fanuc machine and checks that the result formats to itself.

Never here: the real Fanuc reader (`src/Ncx.Readers/Fanuc/`, P3-02).
