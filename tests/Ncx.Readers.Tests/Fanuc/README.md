# Fanuc

The tests of the Fanuc reader (`src/Ncx.Readers/Fanuc/`): a snippet of Fanuc code in, the NCX blocks out, one test per row of the Fanuc column of `controller-mapping.md`, named after what the reader does with the words (`G81_WithG99_BecomesDrillCycleWithClearanceRetract`, `T5Alone_BecomesPreload`, `G91G28Z0_BecomesHomeZ`, `G52_ReplacesEarlierShift_EmitsReset`).

`FanucRead.cs` reads a snippet framed with `%`, `O0001` and `M30` against a small mill or a small system A lathe of the builder "nakamura", both with the Fanuc cycle catalog of `cycles/`, and gives the blocks between the header and `PROGRAM=END`. The files follow the sections of controller-mapping: `FanucProgramTests` (1, program and frame), `FanucFrameTests` (1, the frames), `FanucMotionTests` (2), `FanucToolTests` (3 and 4), `FanucBuilderTests` (4, 7 and 9), `FanucCycleTests` (5), `FanucMacroTests` (6), `FanucSubprogramTests` (6, the state a subprogram runs with and leaves, virtual machine 3.9); `FanucTokenizerTests` the syntax.

The example files end to end are in `../../Ncx.Acceptance/Examples/FanucReaderTests.cs`.
