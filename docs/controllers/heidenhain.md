# Heidenhain Klartext (iTNC 530, TNC 640)

Heidenhain controls are programmed in Klartext (conversational format): every block is a complete statement whose first word says what it is, coordinates carry their own sign and prefix, and the cycles are named definitions with `Q` parameters. NCXchange reads and writes the Klartext of the iTNC 530 and the TNC 640 (`.h` files). The DIN/ISO mode of the same controls (`%NAME G71 *`, G codes with an asterisk at the block end) and the new TNC7 syntax are not covered by version 1.0.

## 1. The program file

```
0 BEGIN PGM 2.5D FRAESEN MM
1 CYCL DEF 247 INIT. REF.PKT ~
    Q339=1 ;REF.PUNKTNUMMER
2 BLK FORM 0.1 Z X0 Y0 Z-20
3 BLK FORM 0.2 X100 Y100 Z0
4 * - SIDE MILL D10 L35 SD10
5 TOOL CALL 1 Z S1592
6 TOOL DEF 0
7 M3
...
44 M5
45 END PGM 2.5D FRAESEN MM
```

- `BEGIN PGM name MM` (or `INCH`) opens and `END PGM name MM` closes; the name is the file name without `.h`. One program per file. Blocks are numbered consecutively from 0 by the editor; the numbers are part of the text and `LBL`, not block numbers, are the jump targets.
- `BLK FORM 0.1`/`0.2` describe the blank for the graphic (kept as header information, `RAW`).
- `;` starts a comment at the end of a block or a comment block; `* - TEXT` is a structuring block shown in the program outline.
- `~` at the end of a line continues the block on the next line (cycle definitions are written this way by CAM systems).
- `/` at the block start is the optional skip.
- `M30` ends the program (`M2` is accepted); `STOP` or `M0` stops, `M1` optionally. A missing `M30` before `END PGM` is tolerated by the control.
- Subprograms are `LBL 1` ... `LBL 0` sections after the `M30` inside the same program (`LBL "NAME"` on newer controls); `CALL LBL 1` calls once, `CALL LBL 1 REP 3` repeats the section between `LBL 1` and the call three more times (a program-part repeat, not a subprogram); `CALL PGM name` calls another file (with `Q` parameters set before the call); `FN 9`..`FN 12` jump to an `LBL` conditionally.
- The decimal separator in the file is the comma (`X-6,964`); the writer must produce it. Newer controls accept the dot in the editor, the files still carry the comma.

## 2. Motion

- `L X+10 Y-5 Z+2 R0 FMAX M3`: line to the target; `FMAX` is rapid, `F500` feed, `F AUTO` the feed of the tool table, `F` modal otherwise; `R0`/`RL`/`RR` radius compensation off, left, right; up to two `M` functions at the end.
- Coordinates carry a sign (`X+10`); `IX+30` is incremental (`IX IY IZ IA IB IC`), never modal.
- `CC X+50 Y+50` sets the arc center (absolute; `CC IX IY` incremental; `CC` alone takes the current position); `C X+70 Y+50 DR+` draws the arc about it, `DR+` counterclockwise, `DR-` clockwise; `CR X+2 Y+7 R+5 DR-` draws by radius (positive radius: arc of at most 180 degrees, negative: more); `CT X Y` a tangentially connecting arc.
- Polar: `LP PR+50 PA+30`, `CP IPA+737.956 IZ-5.4 DR+` (an incremental polar angle of more than a turn with an axial increment is how helices are written; `CP PA` absolute), `CC` is the pole.
- `LN X Y Z NX NY NZ TX TY TZ F` is the 5-axis line with the surface normal and the tool vector, used with `M128` or `FUNCTION TCPM`.
- `CHF 2` chamfer and `RND 4` rounding between two motion blocks; `APPR`/`DEP` approach and departure blocks (`APPR LT`, `DEP CT`...) are kept `RAW` in 1.0.
- `M91` in the block: coordinates from the machine datum; `M92` from a second datum.
- `M126`/`M127`: shortest path of rotary axes; `M128 F1000`/`M129`: tool center point control (with the feed of the compensating motion), `FUNCTION TCPM F TCP AXIS POS PATHCTRL AXIS` and `FUNCTION RESET TCPM` on the TNC 640; `M116`/`M117` rotary feed in mm/min; `M138` selects the tilting axes for `M128`; `M140 MB MAX` (or `MB50`) retracts along the tool axis to the limit or by a distance, `M140 MB MAX F..`; `M118` handwheel superimposition, `M120 LA` look-ahead for radius compensation, `M136`/`M137` feed per revolution.
- `CYCL DEF 32.0 TOLERANZ`, `32.1 T0.02`, `32.2 HSC-MODE:0 TA0.05`: contouring tolerance, mode (0 finish, 1 rough) and rotary tolerance.

## 3. Frames

- `CYCL DEF 247 INIT. REF.PKT Q339=1` activates preset 1 of the preset table (the datum).
- `CYCL DEF 7.0 NULLPUNKT`, `7.1 X+60`, `7.2 Y+40`, `7.3 Z-5`: datum shift, axes in any order, an omitted axis is unchanged, a new cycle 7 replaces the previous one; `CYCL DEF 7.1 #5` takes line 5 of a datum table.
- `CYCL DEF 8.0 SPIEGELN`, `8.1 X Y` mirror; `CYCL DEF 10.0 DREHUNG`, `10.1 ROT+30` rotation; `CYCL DEF 11` scaling; `CYCL DEF 26` axis-specific scaling.
- `PLANE SPATIAL SPA+0 SPB+45 SPC+0 TURN MB MAX FMAX SEQ- TABLE ROT`: tilted working plane by spatial angles; `PLANE AXIAL A-90 C+180 STAY` by rotary axis positions (the dominant form in CAM output); `PLANE EULER`, `PLANE PROJECTED`, `PLANE VECTOR`, `PLANE POINTS`, `PLANE RELATIV` are the other forms; `PLANE RESET STAY` cancels. `TURN` positions the rotary axes (with the retract from `MB`), `MOVE` positions them while the tool tip stays on the workpiece (`ABST` distance), `STAY` only rotates the coordinate system; `TABLE ROT`/`COORD ROT` decide whether a rotary table turns the workpiece or only the coordinates rotate; `SEQ+`/`SEQ-` selects the solution. The older `CYCL DEF 19.0 BEARBEITUNGSEBENE`, `19.1 A.. B.. C..` is the same tilt by axis angles, cancelled by `19.1` without axes.
- `M91` (above) is the machine frame per block.

## 4. Tools and spindle

- `TOOL CALL 4 Z S1592 F500 DL+0.1 DR-0.05`: tool 4 in the spindle, Z as the tool axis (which also sets the working plane), speed 1592, optionally the feed and delta offsets; the length and radius come from the tool table implicitly, which is why NCX readers emit both offset words with the number of the tool. `TOOL CALL S2000` without a number only changes the speed on newer controls; `TOOL CALL 0` empties the spindle. `TOOL DEF 5` prepares the next tool (preload). `TOOL CALL "NAME"` by name on the TNC 640.
- `M3`/`M4`/`M5` spindle, `M13`/`M14` spindle plus coolant, `M8`/`M9` coolant, `M6` tool change on older machines (usually inside the `TOOL CALL` macro), `M19` oriented stop is the builder's.
- Turning on the TNC 640 (`FUNCTION MODE TURN`, back with `FUNCTION MODE MILL`): X values are diameters, radial infeeds are radius values; `FUNCTION TURNDATA SPIN VCONST:ON S200` constant surface speed with `SMAX`; cycles 800 (adapt the coordinate system), 801, 810..8xx (turning contours, grooves, threads). Mill-turn programs of this kind exist in the corpus; Heidenhain turning is not part of version 1.0, the words are documented for the reader that comes later.

## 5. Cycles

Drilling cycles are defined with `CYCL DEF` and called separately:

```
CYCL DEF 200 BOHREN ~
    Q200=+2    ;SICHERHEITS-ABST. ~
    Q201=-21.732 ;TIEFE ~
    Q206=+565  ;VORSCHUB TIEFENZ. ~
    Q202=+21.732 ;ZUSTELL-TIEFE ~
    Q210=+0    ;VERWEILZEIT OBEN ~
    Q203=+0    ;KOOR. OBERFLAECHE ~
    Q204=+50   ;2. SICHERHEITS-ABST. ~
    Q211=+0    ;VERWEILZEIT UNTEN
L X+10 Y+10 R0 FMAX M99
CYCL CALL
```

- `Q200` safety clearance above the surface, `Q201` depth (incremental, negative), `Q202` infeed depth, `Q203` surface coordinate (absolute), `Q204` second clearance (retract height after the cycle), `Q206` feed, `Q210`/`Q211` dwells, `Q207` tapping feed, `Q239` pitch (cycle 207), `Q256` retract for chip breaking (203), `Q257` chip breaking depth, `Q208` retraction feed. The NCX reader converts to absolute coordinates along the tool axis: `SURFACE = Q203`, `CLEARANCE = Q203 + Q200`, `DEPTH = Q203 + Q201`, `SAFE = Q203 + Q204`.
- Cycles 200 (drilling), 201 (reaming), 202 (boring), 203 (universal drilling with chip breaking and decreasing pecks), 204 (back boring), 205 (universal pecking), 206/207 (tapping with and without a compensating chuck; 207 rigid), 208 (bore milling), 209 (tapping with chip breaking), 240 (centering), 241 (single-lip deep hole).
- `CYCL CALL` calls at the current position; `M99` in a positioning block calls at that position; `CYCL CALL PAT` calls at every point of a `PATTERN DEF`; `CYCL CALL POS X Y Z` calls at a position. The definition stays active until the next `CYCL DEF`; there is no cancel word, so the NCX reader emits `CYCLE=OFF` before the next non-cycle motion.
- Pockets and studs: 251 (rectangular pocket), 252 (circular pocket), 253 (slot), 254 (circular slot), 256/257 (studs), 233 (face milling), 262..267 (thread milling); contour cycles: `CYCL DEF 14 KONTUR` names the label subprograms, 20 (contour data), 21 (pilot drilling), 22 (rough-out), 23 (floor finishing), 24 (side finishing), 25 (contour train), 270/271; `CYCL DEF 9.0 VERWEILZEIT`, `9.1 V.ZEIT 1.5` dwell; 12 (`PGM CALL`), 13 (spindle orientation).
- Probing cycles `TCH PROBE 4xx`, OEM cycles 3xx (Hermle), and the turning cycles 8xx are catalog entries or `RAW`.

## 6. Q parameters and FN functions

- `Q0`..`Q99` free, `Q100`..`Q199` reserved for the control's cycles (read by programs), `Q200`..`Q1199` cycle parameters, `Q1200`..`Q1399` reserved, `Q1400`..`Q1999` free again; `QL0`..`QL499` local to the program level, `QR` remanent (kept after power-off), `QS` strings.
- `FN 0: Q5 = +10` assign, `FN 1` add, `FN 2` subtract, `FN 3` multiply, `FN 4` divide, `FN 5` square root; the formula syntax `Q1 = Q2 + 3 * SIN Q3` with the functions `SIN COS TAN ASIN ACOS ATAN SQRT ABS INT FRAC SGN NEG LN LOG EXP` and `^` (power), `%` (modulo); `FN 9: IF +Q1 EQU +Q3 GOTO LBL 5`, `FN 10` (unequal), `FN 11` (greater), `FN 12` (less); `FN 14` error message, `FN 16: F-PRINT` output to a file, `FN 18: SYSREAD` system data (positions, tool data, cycle results), `FN 19` PLC values, `FN 20: WAIT FOR` synchronization with the PLC, `FN 26`..`FN 28` table access, `FN 29` PLC output, `FN 37` export.
- Expressions may stand wherever a number stands (`L X+Q1 Y-Q2`); the reader keeps `Q` names as NCX variable names (`Q5`, `QL5`, `QR5`, `QS5`).

## 7. Reading Klartext

1. Every block carries its verb; the reader still needs the state for the modal feed, the compensation, the active cycle definition and the active `CC` pole.
2. `TOOL CALL n Z S` gives `TOOL=n RPM=` and both offset words with n; a changed axis letter gives `WORKPLANE`; `TOOL DEF n` gives `PRELOAD=n`.
3. `LBL n` closed by `LBL 0` and called without `REP` is a subprogram (`SUB=BEGIN NAME=n` section of the file); an `LBL` used by `REP` or as an `FN 9`..`FN 12` target is a `LABEL`; `CALL LBL n REP k` is `REPEAT=n TIMES=k`.
4. `M30` and `END PGM` together are `PROGRAM=END`; a missing `M30` is a WARNING; sections after `M30` that are jumped into are moved in front of the end behind `JUMP=END`.
5. `CC` and `C` become one `ARC` block with an absolute `CENTER`; `CR` becomes an `ARC` with `R`; `CP IPA` beyond 360 degrees becomes `ARC` with `ANGLE`; `LP`/`CP`/`PA`/`PR` are converted to Cartesian with the pole.
6. `CYCL DEF 7` replaces the previous shift (`SHIFT=RESET` before the new `SHIFT`); `PLANE SPATIAL` is `TILT`, `PLANE AXIAL` and cycle 19 are `TILT_AXIS`, with `MOVE` and `ROT` from the options; other `PLANE` forms are `RAW`.
7. Cycle definitions become `CYCLE=` blocks with absolute coordinates; `CYCL CALL`, `M99` and `CYCL CALL PAT` become `CYCLE_CALL` blocks (a pattern expands to one call per point).
8. The comma is the decimal separator; the reader accepts both.
9. `BLK FORM`, `APPR`/`DEP`, `TCH PROBE`, `FN 16`/`FN 18`/`FN 19`, `PATTERN DEF` beyond the expanded calls, and OEM cycles are `RAW` with a WARNING.

## 8. Writing Klartext

1. Consecutive block numbers from 0, `BEGIN PGM name MM` from the program name and the units, `END PGM name MM` after `M30`.
2. Comma as the decimal separator, sign on every coordinate, `FMAX` for `RAPID`, `F` on `LINE` blocks when the feed changed (or on every block by option).
3. `TOOL CALL n axis S` assembled from `TOOL`, `WORKPLANE` and the `RPM` of the same or the following block; `TOOL DEF n` for `PRELOAD`.
4. Arcs as `CC` plus `C` (the canonical form) or `CR` when the NCX block had `R`; sweeps beyond a turn as `CP IPA`.
5. `HOME` as `L ... FMAX M91` to the reference coordinates (`home`, or `home2` for `POINT=2`) of the `[[axis]]` table (there is no reference return command); an axis without them is an ERROR of this compiler, not of `ncx check` (D100).
6. Subprograms as `LBL n` ... `LBL 0` after the `M30` of every program that calls them, or as separate `.h` files with `CALL PGM` when the machine setting says so.
7. Cycle blocks as `CYCL DEF` with the `Q` parameters in the control's order and `CYCL CALL` or `M99` for the calls; `CYCLE_RETRACT=SAFE` becomes `Q204`.
