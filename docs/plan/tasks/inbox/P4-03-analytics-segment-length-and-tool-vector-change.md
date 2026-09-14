# P4-03 Analytics: segment length and tool vector change

Phase: 4 | Milestone: M7 | Depends on: `P4-02` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The two 5-axis analytics: distance between consecutive end points and the angle between consecutive tool vectors.

## Scope

- Per MOTION: the segment length (workpiece frame, known positions only), a histogram and the minimum, maximum and mean over the range.
- Tool vector change: from `TX TY TZ` when present, else from the rotary axis positions with the machine's rotary axes as given in `[[axis]]` (A/C table, B head: a fixed convention until the kinematics module exists, documented in the report), the angle between consecutive vectors, histogram and extremes.
- Both take `--from`/`--to`; both list the ten worst blocks with their line numbers.

## References

- architecture.md section 9
- ncx-virtual-machine.md section 8

## Done when

- Results on `3D_FRAESEN` and on a 5-axis program of the corpus (ask the maintainer for the 5X pair) are recorded as reference files in the acceptance project (M7).
- This closes phase 4.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)

Claude (agent), 2026-09-14, P4-03 Analytics: segment length and tool vector change. Built on `main` at 857a3b3 (P4-02: `IAnalytic`, `AnalyticOptions`, `RangeFilter`, `TextTable`, `ReportText`, `ncx analyze`) and on the MOTION event of VM 7 with its length, its From and To positions and TX TY TZ (P1-05). Decisions implemented: D24 (no kinematics in NCX: the tool-vector convention of implementation 14, documented in the report header), D67 (`--from`/`--to`), D81 (TX TY TZ as the tool vector), D37 (analyze expands cycles, so their motions are measured), D35 and D100 (the frames of the positions). No decision of the log was needed and no document was edited.

- Files: `src/Ncx.Analytics/Segments/` (`SegmentAnalytic` with `.Report.cs`, README), `src/Ncx.Analytics/ToolVectors/` (`ToolVectorAnalytic` with `.Report.cs`, `ToolVectorConvention`, `ConventionAxis`, `ToolVector`, README); shared by both in `src/Ncx.Analytics/`: `Histogram`, `ValueStatistics`, `VerbStatistics`, `WorstMotions` with `WorstMotion`, `MotionPositions` with `PositionGap`; `ReportText` gains `Angle` and `VerbName`. Registered as `segments` and `vectors`. Tests in `tests/Ncx.Acceptance/Analytics/`: `SegmentAnalyticTests`, `ToolVectorAnalyticTests`, `HistogramTests`, `WorstMotionsTests`, `SegmentAndVectorReferenceTests` with `FiveAxisCorpusFactAttribute`; `AnalyticMachines` gains a five-axis mill with an A/C table and one with a B head over a C table; `AnalyticsReference` reports a missing reference instead of throwing. References: `tests/Ncx.Acceptance/Expected/analytics/3D_FRAESEN.fanuc-mill-30i.segments.txt`, `.vectors.txt` and the two `heidenhain-itnc530` ones.
- Segment length, readings: every MOTION of the range is measured (VM 8, "for every MOTION"): RAPID and the motions of an expanded cycle too, in a row per verb and over all. The length is the one the MOTION event carries (VM 7, 8), which the tool list and the runtime estimate read too, in mm as the tool list gives it. "In the workpiece frame" (phase file) is the frame (block) of the MOTION: HOME and FRAME=MACHINE moves are skipped. A motion under POLAR or CYLINDER has frame WORKPIECE and a known length in its plane, so it is measured. A skipped motion is counted by the first axis of its length that is not known at both ends in one frame: from an unknown position, to an unknown position, or from one frame into another. An ARC without a length whose axes are all known counts as an arc that did not resolve. The ten shortest keep ten entries while the run streams by (implementation 14, risks), equal values in the order of the run; a motion of length 0 is among them, as it is a repeated point. A bin holds the values from its lower bound up to, not including, its upper bound; the default bounds are 0.01, 0.1, 0.5, 1, 5, 10, 50 and 100 mm.
- Tool vector change, readings: per MOTION the angle between the vector at its start and at its end (VM 8, "before and after"; the "consecutive vectors" of the phase file are these). The start is the TX TY TZ of Before, else the convention on From; the end is the TX TY TZ of the event, else the convention on To. The WORKPLANE and the workpiece holder are those of After, because the state words of a block apply before its motion (VM 3, steps 3 and 5), so a WORKPLANE change between motions is no change of a motion. The convention is taken literally from the phase file: the tool axis is the WORKPLANE normal (Z, Y, X), each rotary axis turns it by the right-hand rule about the machine axis of its letter, in the order of `[[axis]]`, and a table axis turns it with the opposite sign. A motion that turns no rotary axis of the convention and has no vector words changes by 0, also where the rotary axes are unknown; the MOTION length follows the same rule for an axis unknown at both ends. An axis that turns must be known at both ends in one frame, and known as an angle: in the polar or cylinder plane its word is a length (VM 3.1, D102), and the motion is skipped with that reason. Angles are computed with atan2 from the cross and the dot product and rounded to three decimals, as the sweep of an arc is. The ten largest list only changes above 0, with the vectors at both ends. The default bounds are 0.01, 0.1, 0.5, 1, 2, 5, 10, 45 and 90 degrees. The report header states the convention and lists the axes it applies and what each turns.
- Reports: the title as in the other reports, the table per verb (motions, minimum, maximum, mean; `-` without a value), the histogram, the ten worst with their lines, and the skipped motions with their reasons; a range without a value writes a line instead of an empty table.
- Acceptance: 3D_FRAESEN converts from both sources and analyzes with exit 0. Both give the same statistics: 816 of 820 motions measured, 0.024 mm to 131.56 mm, mean 21.397 mm, the same histogram and the same ten shortest, whose lines differ by the two RAW lines of the Klartext header. These values were checked against an independent computation from the converted NCX text before the files were frozen; a test asserts the equal LINE and ARC rows. Two of the four skipped motions start from the reference point of the MACHINE frame (both machine files give `home`, D100); the other two end the program in the machine frame (G28 as HOME, M91 as FRAME=MACHINE). The tool vectors change by 0: the mills have no rotary axis.
- Corpus: the 5-axis A/C pair is not available. `FiveAxisCorpusFactAttribute` skips with a message unless `NCX_CORPUS` holds a folder `5X` with the programs and their machine files `heidenhain.toml` and `fanuc.toml`: no file of `machines/` has a rotary table. The first run writes the reports to the temporary folder `ncx-acceptance`; reviewed, they become `<part>.heidenhain.segments.txt`, `.vectors.txt` and the `fanuc` ones. That folder layout is the convention of the test, written in it and in `Expected/README.md`.
- No code of ANA100 to ANA199 was needed: the analytics report in their text and raise no diagnostic.
- Open questions, marked `TODO(question)`: where a user gives the bins of the histogram, since architecture 10 has no option for them and ncx.toml and the machine file no key (`SegmentAnalytic.DefaultBins`, `ToolVectorAnalytic.DefaultBins`; the analytics take bounds as a constructor parameter, and ncx analyze writes the defaults). Which rotary axes the convention applies (`ToolVectorConvention`): an axis without owner (B1 of the Mori Seiki and DMG files, A and B of the default machine) or one owned by a tool holder is taken as a head axis; a name with digits (C2) turns about the axis of its letter; a table axis counts only while its owner holds the workpiece (language 4.10).
- Shared files changed: `src/Ncx.Cli/Program.cs` (two registration lines and their usings), `src/Ncx.Cli/Commands/AnalyzeCommand.cs` (the description names the two analytics), `tests/Ncx.Acceptance/Cli/AnalyzeCommandTests.cs` (the usage error lists four analytics), `tests/Ncx.Acceptance/Expected/README.md`.

Done when:

- Results on `3D_FRAESEN` recorded as reference files: holds, for both sources, the segment length and the tool vector change.
- Results on a 5-axis program of the corpus: waits for the maintainer's 5X pair and a machine file with its rotary axes; its test is skipped with a message until then.
- "This closes phase 4": waits for the criterion above and for the measured cycle time of P4-02. The task file therefore stays in `inbox/`.

Gate: `dotnet build -warnaserror` with 0 warnings, 3578 tests passing and 4 skipped (360 in `Ncx.Acceptance`), `dotnet format --verify-no-changes` clean.
