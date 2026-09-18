# Fanuc

The tests of the Fanuc compiler (`src/Ncx.Compilers/Fanuc/`): NCX blocks in, the Fanuc lines out. `FanucCompile.cs` compiles a program for a vertical mill written like `machines/fanuc-mill-30i.toml` (without block numbers, so that a test reads the lines alone) or for a two-path lathe of G-code system A, and `Body` gives the lines between the start block and `M30`.

Start with `FanucWriterRulesTests.cs`, one test per rule of section 10 of `controllers/fanuc.md` (`Rule1_G0AndG1_AreWrittenOnlyWhenTheVerbChanges`, `Rule2_SpeedAndDirection_StandInOneBlock`, ...), then the concerns: `FanucMotionTests` (arcs, turns, compensation, G53), `FanucToolWordsTests` (T M6, G43 H, CSS, M29 S), `FanucFramesTests` (G54, G52, G68, G51.1, G68.2, G28, G92, the transformations), `FanucCyclesTests` (G81 to G89, G98 and G99, G80, the lathe codes), `FanucFlowTests` (GOTO, IF, # variables, G65, M99), `FanucSystemATests` (the G-code system of the target).

The example sources end to end are in `../../Ncx.Acceptance/Examples/FanucCompilerTests.cs`.
