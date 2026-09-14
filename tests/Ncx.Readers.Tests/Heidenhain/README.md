# Heidenhain

The tests of the Heidenhain reader (`src/Ncx.Readers/Heidenhain/`): a snippet of Klartext in, the NCX blocks out, one test per rule of `controllers/heidenhain.md` 7 and per row of the Heidenhain column of `controller-mapping.md`, named after what the reader does with the words (`ToolCall_WithAxisAndSpeed_BecomesToolWithBothOffsetsAndRpm`, `CcAndC_BecomeOneArcWithAnAbsoluteCenter`, `CyclDef7_ReplacesTheEarlierShiftWithReset`).

`HeidenhainRead.cs` reads a snippet framed with `BEGIN PGM`, `M30` and `END PGM` against the iTNC 530 of `machines/heidenhain-itnc530.toml` (or, through `MillWith`, that file with one text replaced) with the cycle catalog `cycles/heidenhain.toml`, and gives the blocks between the header and `PROGRAM=END`. The files follow the rules of heidenhain 7: `HeidenhainProgramTests` (rules 4, 8, 9; controller-mapping 1), `HeidenhainToolTests` (rule 2), `HeidenhainFlowTests` (rule 3, the Q parameters, the state of a subprogram), `HeidenhainMotionTests` (rules 1 and 5), `HeidenhainCornerTests` (`CHF` and `RND` expanded per D58, next to lines and arcs), `HeidenhainFrameTests` (rule 6, the tolerance and the dwell), `HeidenhainCycleTests` (rule 7), `HeidenhainFunctionTests` (the M functions); `HeidenhainTokenizerTests` the syntax.

The example files end to end are in `../../Ncx.Acceptance/Examples/HeidenhainReaderTests.cs`.
