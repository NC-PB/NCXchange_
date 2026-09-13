# SINUMERIK 840D and 840D sl

The Siemens language is DIN 66025 G-code extended by a high-level language: addresses with an equals sign, identifiers, typed variables, frames as first-class objects, subprograms with parameters, structured control flow, and cycles that are ordinary subprogram calls with positional parameters. The 840D (powerline, 1990s to 2010) and the 840D sl (solution line, 2005 on, software 2.x to 4.9x) share the language; the sl added the trailing mode parameters on the cycles, `CYCLE800`, `CYCLE832` and the ShopMill/ShopTurn cycle set. SINUMERIK ONE (2019 on) keeps the language. Version 1.0 of NCXchange reads and writes generic 840D sl programs; the GILDEMEISTER structure programming of DMG lathes (`L7xx` cycles, `TC(...)`) is documented in `machine-builders.md` and not implemented (decision D68).

## 1. Program units and files

```
%_N_SHAFT_MPF
;$PATH=/_N_WKS_DIR/_N_SHAFT_WPD
N10 G0 G40 G90 G94
N20 CYCLE800()
N30 G54
N40 T="SF8-ST-NL30-SL21" D1
N50 M6
...
N600 M30
```

- A unit is a main program (`MPF`) or a subprogram (`SPF`); in a file exported from the control it starts with `%_N_NAME_MPF` and a `;$PATH=` comment that names the workpiece directory; on the control itself the header is not part of the text. A job archive holds several units in one file (`%_N_1_0_MPF`, `%_N_TH1_HS_01_SPF`, a tool list `%_N_1_3_MPF`, an `_INI` unit), one after another: the NCX file structure with several programs and subprograms per file exists for this.
- Program names: letters, digits, underscore, up to 24 characters; the first two characters should be letters or an underscore and a letter, because a name that starts with a digit can only be called through `CALL`. Names are case-insensitive, except tool names in `T="..."`.
- Blocks end with the line feed, `N` numbers optional (they must be unique for the block search), at most 512 characters per block; `;` starts a comment at the end of a block or a comment block; `/` or `/0` skips the block on the first skip level, `/1`..`/9` on the others.
- The recommended order in a block is `N G X Y Z F S T D M H`, but the control does not require it; some addresses may appear several times (`G`, `M`, `H`).
- Ends: `M30` (rewind), `M2`, `M17` (subprogram return, also accepted in a main program), `RET` (return without an output to the PLC). `M0`, `M1` (written `M01` by some posts) stop.
- Values: `X10` for a single-letter address with a constant; `X=10` when the value is an expression or a variable (`X=R1*2`, `Z=R40`); the `=` is required after every multi-letter address and after an address with a numeric extension (`CR=5`, `AR=90`, `S2=1000`, `X1=10`, `M2=3`). Separators after the address are allowed (`F 100`).
- Address extensions select an object: `S2=` spindle 2, `M2=3` spindle 2 clockwise, `T2=` tool selection for spindle 2, `X1=` machine axis X1 in `G74`, `C4=` a fourth C axis (the corpus drives spindle 4 as a C axis this way), `I1=` the intermediate point of `CIP`.

## 2. G groups and modality

Every G command belongs to a numbered group; one per group is active. The groups the readers need (manual chapter 4.3):

| Group | Commands | Meaning |
|---|---|---|
| 1 | `G0 G1 G2 G3 CIP CT ASPLINE BSPLINE CSPLINE POLY G33 G331 G332 G34 G35 INVCW INVCCW` | modal motion |
| 2 | `G4 G63 G74 G75 REPOSL REPOSQ ... G147 G148 G247 G248 G347 G348 G340 G341 G5 G7` | non-modal motion, dwell, reference point, fixed point, approach and retreat |
| 3 | `TRANS ROT SCALE MIRROR ATRANS AROT ASCALE AMIRROR ROTS AROTS CROTS G25 G26 G110 G111 G112 G58 G59` | programmable frames, working area limits, pole |
| 4 | `STARTFIFO STOPFIFO FIFOCTRL` | block buffer |
| 6 | `G17 G18 G19` | plane |
| 7 | `G40 G41 G42` | radius compensation |
| 8 | `G500 G54 G55 G56 G57 G505 ... G599` | settable datum |
| 9 | `G53 G153 SUPA G60 ...` | frame suppression (non-modal) |
| 10 | `G60 G64 G641 G642 G643 G644 G645` | exact stop or continuous path |
| 11 | `G9` | exact stop, non-modal |
| 12 | `G601 G602 G603` | block change criterion under exact stop |
| 13 | `G70 G71 G700 G710` | inch or metric (`G70`/`G71` geometry only, `G700`/`G710` also feeds and offsets) |
| 14 | `G90 G91` | absolute or incremental |
| 15 | `G93 G94 G95 G96 G961 G962 G97 G971 G972 G973` | feed type and constant surface speed |
| 16 | `CFC CFTCP CFIN` | feed override on inner and outer curves |
| 17 | `NORM KONT KONTC KONTT` | approach and retreat with radius compensation |
| 18 | `G450 G451` | corner behaviour with radius compensation |
| 21 | `BRISK SOFT DRIVE` | acceleration profile |
| 22 | `CUT2D CUT2DF CUT3DC CUT3DF CUT3DFF CUT3DCC CUT3DCCD CUT2DD CUT2DFD` | tool offset type |
| 24 | `FFWOF FFWON` | feed forward |
| 25 | `ORIWKS ORIMKS` | reference system of the tool orientation |
| 29 | `DIAMOF DIAMON DIAM90 DIAMCYCOF` | radius or diameter programming |
| 30 | `COMPOF COMPON COMPCURV COMPCAD COMPSURF` | block compression |
| 45 | `SPATH UPATH` | path reference of the `FGROUP` axes |
| 49 | `CP PTP PTPG0` | point-to-point motion |
| 50 | `ORIEULER ORIRPY ORIVIRT1 ORIVIRT2 ORIAXPOS ORIRPY2` | orientation programming |
| 51 | `ORIVECT ORIAXES ORIPATH ORIPLANE ORICONCW ...` | orientation interpolation |
| 52 | `PAROT PAROTOF` | workpiece-related frame rotation |
| 53 | `TOROTOF TOROT TOROTZ TOROTY TOROTX TOFRAME TOFRAMEZ ...` | tool-related frame rotation |
| 59 | `DYNNORM DYNPOS DYNROUGH DYNSEMIFIN DYNFINISH` | dynamic mode |

Words of other groups (`G500`, `SUPA`, `DIAMON`, `CFC`, `NORM`, `G451`, `G601`) are what the posts write in their first block: `G153 G0 G17 G90 G40 G60 G451 G601 CFC NORM` is a typical STAMA header. The reader keeps the state per group; the words NCX has no meaning for stay `RAW` modal words and are written back by the Siemens compiler in place.

## 3. Motion

- `G0` rapid (linear interpolation or per axis by machine data, `RTLION`/`RTLIOF`), `G1` at `F`, `G2`/`G3` arcs.
- Absolute or incremental: `G90`/`G91` modal, `X=AC(10)` and `X=IC(5)` per word; rotary axes `C=DC(90)` shortest way (also for `SPOS`), `C=ACP(90)` positive direction, `C=ACN(90)` negative direction; `G91` with `IC` may turn a modulo axis by more than 360 degrees.
- Arcs: end point with `I J K` (center incremental from the start point; `I=AC(50)` absolute; the corpus writes `I=AC() J=AC()` throughout), `CR=5` radius (negative for the arc over 180 degrees; a full circle needs the center), `AR=90` opening angle with the end point or the center, `CIP X Y I1= J1=` through an intermediate point, `CT X Y` tangential; `TURN=2` on a helix adds two full turns before the end point; arcs outside the working plane are possible when the end point names the axes. `INVCW`/`INVCCW` involutes.
- Polar: `G110`/`G111`/`G112` define the pole (relative to the last position, the workpiece zero, the last pole), then `G0`/`G1`/`G2`/`G3 AP=30 RP=50`; switching back to Cartesian happens by writing Cartesian words. The pole stays until the program end.
- Chamfer and rounding: `CHF=2` (chamfer length), `CHR=2` (chamfer width in the original direction), `RND=4` (radius), `RNDM=4` modal rounding (`RNDM=0` off), with `FRC=` (blockwise) or `FRCM=` (modal) feeds; contour definitions with `ANG=` (a line by angle) and two- or three-block forms that the control completes.
- Feed: `F` (one per block), `G94` per minute, `G95` per revolution of the master spindle, `G93` inverse time, `G95 FZ=` per tooth, `FGROUP(X, Y, Z, A)` names the path axes the feed refers to and `FGREF[A]=r` the effective radius of a rotary axis, `FL[Y]=500` a velocity limit, `FB=` blockwise feed, `OVR=` override, `ACC[X]=` acceleration limit, `FA[X]=` feed of a positioning axis.
- Path control: `G60` exact stop (with `G601`/`G602`/`G603` criteria), `G9` blockwise, `G64` continuous, `G641 ADIS=0.5 ADISPOS=2` smoothing by distance, `G642` by axis tolerance, `G643` inside the block, `G644` by dynamics, `G645` also at tangential transitions; `CTOL=`, `OTOL=`, `ATOL[X]=` contour, orientation and axis tolerances; `COMPON`/`COMPCURV`/`COMPCAD`/`COMPSURF` compressors; `CYCLE832(tol, mode, otol)` sets all of this for HSC programs (mode 1 finish, 2 semi-finish, 3 rough, 4 precision, plus 10 for an orientation tolerance in the third parameter; `CYCLE832(0, 0, 1)` off).
- Radius compensation: `G41`/`G42`/`G40` with `D`; `NORM` (straight approach), `KONT` (around the corner), `KONTC`/`KONTT` (continuous); `G450` (transition circle at outer corners, `DISC=`) or `G451` (intersection); `OFFN=` contour allowance; `CUT2D`.. `CUT3DC` offset types; `G147`/`G148`/`G247`/`G248`/`G347`/`G348` smooth approach and retreat (`RAW`).
- Machine frame: `G53` (one block, programmable and settable frames off), `G153` (also the base frame), `SUPA` (also handwheel and external shifts, `PRESET`); the posts write `G0 G53 Z360 D0` (retract with the tool offset cancelled in the same block) and `G0 SUPA Z1800 D0`.
- `G74 X1=0 Y1=0 Z1=0`: reference point approach with machine axis names, own block, no transformation active; `G75 X0 Z0 FP=2`: fixed point 1..4.
- `G4 F1.5` dwell in seconds, `G4 S10` in revolutions of the master spindle, `G4 S2=10` of spindle 2; the `F` and `S` of a dwell block do not touch the modal feed and speed.
- `PRESETON(C, 0)` declares the current position of an axis (loses the reference status), `PRESETONS` keeps it.
- `WORKPIECE(, "", , "CYLINDER", 0, 1, -108, 0, 53.5)`: blank definition for the simulation (name, clamping, datum, shape `CYLINDER`/`PIPE`/`RECTANGLE`/`BOX`/`N_CORNER`, then the dimensions), header information only.

## 4. Frames

- Settable: `G54`..`G57`, `G505`..`G599`, `G500` off (or `G500` with a value); written from the program through `$P_UIFR[n, X, TR]=` (translation), `[.., FI]` (fine), `[.., RT]` (rotation) as the Burkhardt+Weber posts do.
- Programmable, modal, group 3: `TRANS X Y Z` absolute shift (replacing), `ATRANS X Y Z` additive; `ROT X.. Y.. Z..` rotation about the geometry axes (Euler order), `ROT RPL=30` about the normal of the plane, `AROT` additive; `ROTS`/`AROTS`/`CROTS` rotations by two spatial angles; `SCALE`/`ASCALE`; `MIRROR X0`/`AMIRROR X0` (the value is meaningless); `G58 X..`/`G59 X..` replace the absolute or the additive translation part per axis. Every replacing instruction (`TRANS`, `ROT`, `SCALE`, `MIRROR`, with or without values) deletes all earlier programmable frame instructions; the additive ones build on the current frame. `TRANS` alone (and `ROT`, `SCALE`, `MIRROR` alone) clears the programmable frame.
- `TOFRAME`/`TOROT`/`PAROT` and their `OF` forms align the coordinate system with the tool or the workpiece after a swivel; `CYCLE800` (below) is what the posts write.
- Frame variables: `$P_PFRAME`, `$P_UIFR[n]`, `$P_BFRAME`, `$P_ACTFRAME`; frame arithmetic (`CTRANS(X, 10)`, `CROT`, `CMIRROR`, `CSCALE`, `CFINE`) and `MEAFRAME` are `RAW`.

`CYCLE800(_FR, _TC, _ST, _MODE, _X0, _Y0, _Z0, _A, _B, _C, _X1, _Y1, _Z1, _DIR, _FR_I, _DMODE)` swivels the plane on a machine whose kinematics is set up under a name (`_TC`, `"HERMLE"`, `"DMG"`, `"TC1"`; `""` when there is one, `"0"` deselects): `_FR` retract before swivelling (0 none, 1 machine Z, 2 Z then XY, 4 in tool direction to the limit, 5 by `_FR_I`); `_ST` ones digit 0 new or 1 additive, tens digit 1 tracks the tool tip (`TRAORI`), hundreds digit aligns the tool instead of the plane; `_MODE` is bit coded: bits 7 and 6 select axis-wise angles (00, with the rotation order in bits 5..0: 27 = Z Y X, 39 and 57 in the corpus), spatial angles (01), projection angles (10) or rotary axes direct (11); `_X0 _Y0 _Z0` the reference point before and `_X1 _Y1 _Z1` after the rotation; `_A _B _C` the angles; `_DIR` -1 or +1 positions the rotary axes (the smaller or larger solution), 0 only computes the frame; `CYCLE800()` cancels. A `TRAORI` after a plain `G0 A.. C..` positioning (Hermle) is a rotary motion followed by the transformation, not a swivel.

## 5. Tools and spindles

- `T1`, `T=1`, `T="DRILL_D8"` select a tool (by number, or by name and duplo under tool management; a locked tool is replaced by the search strategy); on turret lathes the selection is the change (manual 2.4.1); on mills `M6` performs the change (2.4.2), so `T1` before it is a preload; `T2=5` selects for spindle 2 where the machine allows the extension; `T0` unloads. `D1`..`D8` select the cutting edge of the active tool with its length and radius (one register, NCX `OFFSET=`); `D0` cancels the offsets (written in the retract blocks); `TOFFL`/`TOFF`/`TOFFR`/`TOFFLR` programmable offsets; `TCARR=`, `TCOABS`, `TCOFR` for orientable tool carriers.
- Spindles: `S1000` and `M3`/`M4`/`M5` for the master spindle, `S2=1000` and `M2=3` for spindle 2 (at most three `S` per block; `S0=` is the master spindle); `SETMS(2)` in its own block makes spindle 2 the master spindle that `S`, `M3`, `G95`, `G96` and `G4 S` refer to, `SETMS` alone returns to the configured one; `M70`/`M2=70` switches a spindle to axis mode, `SPOS=90`/`SPOS[2]=90` positions (`SPOSA` without waiting, `WAITS(1,2)` waits), `M19`, `SPOS=DC(45)` with a direction; `G96 S200` constant surface speed (switches `G95` on), `G961` with feed per minute, `G962` either, `G97`/`G971`/`G972` off, `G973` off without limit; `LIMS=3000` (and `LIMS[2]=`) the speed limit under `G96`; `G26 S1400 S2=350` upper and `G25 S20` lower limits for all spindles, kept beyond the program end; `SVC=` cutting speed for milling tools; `SCC[Y]` another reference axis for `G96`.
- Synchronous spindles: `COUPDEF(S2, S1, 1, 1, "NOC", "DV")` defines a coupling, `COUPON(S2, S1, 113.5)` switches it on with an angular offset, `COUPONC` keeps the current offset, `COUPOF` off, `COUPDEL` deletes; the DMG cycles wrap them.
- Coolant, chucks, clamps and everything else are the builder's M functions (`M8`/`M9` almost everywhere, `M108`/`M109` for the driven tools on the Monforts and the CLX, `M40`/`M41`/`M42` gear ranges).

## 6. Transformations and orientation

- `TRAORI` or `TRAORI(1)` switches the 5-axis transformation on (positions refer to the tool tip, rotary axis moves are compensated), `TRAORI(1, x, y, z, a, b)` with a tool direction and rotary offsets; `TRAFOOF` off; `TRACYL(d)` cylinder surface transformation (with `TRACYL(d, n)` and the groove-wall variant), `TRANSMIT` (`TRANSMIT(n)`) face transformation, `TRACON` chained transformations, `TRAANG` inclined axis, `TRAFOON` with kinematic chains (SINUMERIK ONE).
- Orientation under `TRAORI`: rotary axis positions `A B C`; Euler or RPY angles `A2= B2= C2=` (`ORIEULER`, `ORIRPY`, `ORIRPY2` select the convention); direction vector `A3= B3= C3=` (the tool axis in workpiece coordinates); surface normals `A4= B4= C4=` at the block start and `A5= B5= C5=` at the end; `LEAD=` and `TILT=` angles relative to the path (`ORIPATH`); `ORIAXES` (linear interpolation of the axes) or `ORIVECT` (great circle interpolation of the vector); `ORIWKS` (the vector is meant in the workpiece system, the default) or `ORIMKS` (in the machine system); `ORISON`/`ORISOF` smoothing; `ORIRESET(a, b)` basic position.
- `CUT3DC`/`CUT3DF`/`CUT3DFF` 3D radius compensation, `ORIC`/`ORID` behaviour at outer corners.

## 7. Cycles

A cycle is a subprogram call with positional parameters; empty positions take defaults. The 840D sl set (manual chapter 3.25), with the trailing `_GMODE`, `_DMODE`, `_AMODE` integers that the sl added (geometry mode, display mode with the plane in the ones digit, alternative mode: whether the depth is absolute or incremental and the dwell in seconds or revolutions; omitted values mean the old behaviour):

| Cycle | Signature (first parameters) | Meaning |
|---|---|---|
| `CYCLE81` | `(RTP, RFP, SDIS, DP, DPR, DTB, _GMODE, _DMODE, _AMODE)` | drilling, centering: retract plane, reference plane, safety distance (unsigned, added to the reference plane), depth absolute, depth incremental, dwell |
| `CYCLE82` | `(RTP, RFP, SDIS, DP, DPR, DTB, _GMODE, _DMODE, _AMODE, _VARI, ...)` | drilling with dwell, counterboring |
| `CYCLE83` | `(RTP, RFP, SDIS, DP, DPR, FDEP, FDPR, _DAM, DTB, DTS, FRF, VARI, _AXN, _MDEP, _VRT, _DTD, _DIS1, ...)` | deep hole drilling 1: first depth, degression, dwells, feed factor, `VARI` 0 chip breaking or 1 full retract, `_AXN` drilling axis (1, 2, 3 = first, second, third geometry axis), minimum depth, retract amount |
| `CYCLE830` | `(RTP, RFP, SDIS, _DP, FDEP, _DAM, DTB, DTS, FRF, VARI, ...)` | deep hole drilling 2 with pilot and through-hole options |
| `CYCLE84` | `(RTP, RFP, SDIS, DP, DPR, DTB, SDAC, MPIT, PIT, POSS, SST, SST1, _AXN, _PITA, _TECHNO, _VARI, _DAM, _VRT, ...)` | rigid tapping: direction after the cycle, metric size or pitch, spindle position before tapping, speeds, drilling axis |
| `CYCLE840` | `(RTP, RFP, SDIS, DP, DPR, DTB, SDR, SDAC, ENC, MPIT, PIT, _AXN, ...)` | tapping with a compensating chuck |
| `CYCLE85` | `(RTP, RFP, SDIS, DP, DPR, DTB, FFR, RFF, ...)` | reaming with its own feed and retract feed |
| `CYCLE86` | `(RTP, RFP, SDIS, DP, DPR, DTB, SDIR, RPA, RPO, RPAP, POSS, ...)` | boring with oriented spindle stop and lift-off |
| `CYCLE78` | thread milling of a drilled hole | |
| `HOLES1(SPCA, SPCO, STA1, FDIS, DBH, NUM, ...)`, `HOLES2(CPA, CPO, RAD, STA1, INDA, NUM, ...)`, `CYCLE801`, `CYCLE802` | row, circle, grid or frame, free positions | patterns that call the modal cycle at every position |
| `CYCLE61`, `POCKET3`, `POCKET4`, `CYCLE76`, `CYCLE77`, `CYCLE79`, `SLOT1`, `SLOT2`, `CYCLE899`, `LONGHOLE`, `CYCLE70`, `CYCLE60` | face milling, rectangular and circular pockets and spigots, polygon, slots, open slot, long hole, thread milling, engraving | milling cycles |
| `CYCLE62(_KNAME, _TYPE, _LAB1, _LAB2)`, `CYCLE72`, `CYCLE63`, `CYCLE64` | contour call by name, label pair or subprogram; path milling; contour pocket and spigot; pre-drilling | contour milling |
| `CYCLE951`, `CYCLE952(_PRG, _CON, _CONR, _VARI, _F, _FR, _RP, _D, ...)`, `CYCLE95(NPP, MID, FALZ, FALX, FAL, FF1, FF2, FF3, _VARI, DT, DAM, _VRT, ...)`, `CYCLE930`, `CYCLE940`, `CYCLE99`, `CYCLE98`, `CYCLE92` | stock removal of a corner, contour stock removal and grooving (with the contour named by `CYCLE62`), the older contour cycle with a subprogram name, groove, undercuts, thread turning, thread chain, cut-off | turning cycles |
| `CYCLE800`, `CYCLE832` | swivel, high speed settings | sections 4 and 3 |
| `CYCLE978`, `979`, `998`, ... | measuring cycles (own manual) | `RAW` |

Calling: `CYCLE81(11, 6, 5, -50,)` once at the current position; `MCALL CYCLE81(11, 6, 5, -50,)` makes it modal: every following block with a position calls it (`X-113.333 Y-226.667`, `X0`, ...) until `MCALL` alone switches it off. `MCALL` works with any subprogram. The cycle feed is the modal `F` (the posts write `F.08` in the block before the `MCALL`); `CYCLE85` has its own `FFR`. The older 840D cycles have the same leading parameters without the mode integers, so `CYCLE81(RTP, RFP, SDIS, DP, DPR)` reads on both.

## 8. Variables, flow and subprograms

- `R1`..`R99` (more by machine data) arithmetic parameters, `R1=10`, `R[1]=10`, `X=R43`; `DEF INT COUNT = 0`, `DEF REAL LEN`, `DEF BOOL`, `DEF CHAR`, `DEF STRING[32] NAME`, `DEF AXIS`, `DEF FRAME` in the definition part at the program start; global user data (`GUD`) and the builder's variables (`GC1_XDREMI`, `XMW_1`) are declared outside the program.
- Operators `+ - * / DIV MOD`, `==`, `<>`, `>`, `>=`, `<`, `<=`, `AND OR NOT XOR`, bitwise `B_AND B_OR B_NOT B_XOR`, `<<` string concatenation; functions `SIN COS TAN ASIN ACOS ATAN2 SQRT ABS POT TRUNC ROUND LN EXP MINVAL MAXVAL BOUND`, string functions.
- Jumps: `GOTOF LABEL` (forward), `GOTOB LABEL` (backward), `GOTO LABEL` (both), `GOTOC` (no alarm when the label is missing), `GOTOS` (program start), all also with `IF cond`; the target is a label `NAME:` at the block start or a block number; `CASE(R1) OF 1 GOTOF A 2 GOTOF B DEFAULT GOTOF C`; a computed target `GOTOF "STEP_" << I_STEP` (INDEX).
- Structures: `IF cond` ... `ELSE` ... `ENDIF`, `LOOP` ... `ENDLOOP`, `FOR R1 = 1 TO 10` ... `ENDFOR`, `WHILE cond` ... `ENDWHILE`, `REPEAT` ... `UNTIL cond`; program-part repeats `REPEAT LABEL P=3`, `REPEAT START END P=3`, `REPEATB LABEL P=3`, `ENDLABEL:`.
- Subprograms: `PROC NAME` (optional when there are no parameters) ... `RET` or `M17`; `PROC NAME(REAL LENGTH=10, INT N, VAR REAL RESULT)` with typed call-by-value parameters (defaults allowed, up to 127) and `VAR` call-by-reference; `EXTERN NAME(REAL, INT)` announces a subprogram with parameters in the caller; `PROC NAME SBLOF DISPLOF` suppresses single-block stops and the block display inside; `SAVE` restores the modal G codes at the return.
- Calls: `L100` (leading zeros count: `L0100` is another program), `NAME` alone in its own block, `NAME(1, , 3)` with parameters (empty positions take the defaults), `NAME P3` three times, `MCALL NAME` modal, `CALL "NAME"` or `CALL VARIABLE` indirect, `CALL BLOCK "START" TO "END"`, `PCALL` with a path, `EXTCALL("path/NAME")` from an external drive, `ISOCALL` for an ISO program, `CALLPATH` extends the search path. `DEFINE NAME AS text` defines a macro.
- `STOPRE` stops the block preparation until the block is executed (needed before reading system variables that a previous block changes, and written before every restart label by the Monforts posts); `MSG("text")` shows a message on the screen, `MSG()` clears it; `SETAL(alarm)` raises an alarm; `WRITE`, `READ`, `DELETE`, `ISFILE` handle files.
- System variables: `$AA_IW[X]` workpiece position, `$AA_IM[X]` machine position, `$P_UIFR[n]` settable frames, `$P_PFRAME` programmable frame, `$P_TOOL`, `$P_TOOLNO`, `$TC_DP3[t, d]` tool length, `$P_SEARCH` (block search active), `$P_SIM` (simulation), `$AC_TIME`, `$A_DBB[n]`/`$A_DBW[n]` PLC bytes and words, `$AA_ETRANS` external shift. Machine and setting data `$MN_`, `$MC_`, `$MA_`, `$SC_`, `$SA_`.
- Synchronized actions: `ID=1 WHEN $AA_IW[X] > 100 DO M7`, `WHENEVER`, `FROM`, `EVERY`, `DO` with actions, `CANCEL(1)`; motion-synchronous and beyond the reach of a block-by-block reader: `RAW`.
- Interrupt routines `SETINT(1) PRIO=1 ASUP1`, `LIFTFAST` with `ALF` for a fast retract from the contour; `DISABLE`, `ENABLE`, `CLRINT`: `RAW`.

## 9. Several channels

- `INIT(2, "/_N_WKS_DIR/_N_SHAFT_WPD/_N_SHAFT2_MPF", "S")` selects a program in channel 2 (with synchronous acknowledgement), `START(2)` starts it, `WAITE(2)` waits for its end; `WAITM(1, 1, 2)` sets mark 1 in the own channel and waits until channels 1 and 2 have reached it (the previous block ends with exact stop; the mark is cleared after the synchronization); `WAITMC` brakes only when the others are not there yet; `SETM`/`CLEARM` set and clear marks without waiting (kept over reset). Marks 0..99, at most 10 set at once per channel; a single-channel system has mark 0 only. The own channel need not be listed.
- Axis exchange between channels: `RELEASE(X)`, `GET(X)`, `GETD(X)`, `AXTOCHAN`; `WAITP(X)` waits for a positioning axis. `RAW` in version 1.0.
- Multi-channel jobs on the control are a workpiece directory with one `MPF` per channel and a job list (`JOB` file); exported archives put the units into one file.

## 10. ISO mode

`G291` switches the control to its ISO dialect (Fanuc-compatible G-code with its own cycle set, `G290` switches back to Siemens mode); a machine that runs ISO programs is read with the Fanuc reader and the `dialect` of the machine file says so. Not to be confused with the ISO-style `CYCLE3xx` compatibility cycles of the Siemens cycle package.

## 11. Reading Siemens programs

1. Units: every `%_N_NAME_MPF` is a `PROGRAM`, every `%_N_NAME_SPF` and every `PROC` unit is a `SUB`; `;$PATH` lines are header comments; `_INI` units are `RAW` blocks of their own section.
2. Modality per G group with the source-side state; `G0`..`G3` verbs, `G90`/`G91` with `AC()`/`IC()` per word; `DC()`/`ACP()`/`ACN()` positions with the direction kept.
3. `T`, `M6`, `D` per section 5 and the machine's `[tool_change]` style; `S` and `M3` bind to the master spindle of the source-side state (`SETMS`), `S2=`/`M2=3` to spindle 2; the role comes from the machine file.
4. Frames: `ATRANS`/`AROT`/`AMIRROR` append, `TRANS`/`ROT`/`MIRROR`/`SCALE` cut the chain at its first entry first; `G58`/`G59` are shifts; `CYCLE800` is `TILT` or `TILT_AXIS` with `MOVE` from `_DIR` and `_ST`, and a `RETRACT` block from `_FR`; `$P_UIFR` writes are `RAW`.
5. Cycles per section 7 into `CYCLE=` blocks with absolute coordinates along the drilling axis (`_AXN` is `AXIS`), the modal `F` before an `MCALL` into `CYCLE_F`, `MCALL` positions into `CYCLE_CALL`, `MCALL` alone into `CYCLE=OFF`; patterns expand into calls.
6. Flow: structures are lowered to `LABEL`, `JUMP`, `IF`; `REPEAT` forms to `REPEAT`/`TIMES` or `SUB` sections; `PROC` parameters to `ARG` names; `M17`/`RET` to `SUB=END`; `M30`/`M2` to `PROGRAM=END`.
7. `WAITM` to `SYNC` with `WITH`; `INIT`/`START`/`WAITE` to the channel words.
8. `MSG`, `STOPRE`, `WORKPIECE`, `DEF` of non-numeric types, synchronized actions, interrupts, `ORI*` and the path-control words, and every builder cycle not in the configuration: `RAW` with a WARNING, kept in place.
9. Numbers as written; `=` and extensions parsed per section 1; expressions as values become NCX expressions.

## 12. Writing Siemens programs

1. One output file per NCX file with the `%_N_NAME_MPF`/`_SPF` headers when `program_layout = "one_file"`, else one file per unit; `PROC NAME` at the top of every subprogram and `RET` (or `M17`, `sub_end`) at its end; `M30` (`program_end`) at the program end.
2. `=` after every multi-letter address and extension; `I=AC()` for absolute centers, `CR=` for the `R` form, `TURN=` for sweeps beyond a turn (or split into turns when the target is an older 840D).
3. `T="NAME"` or `T1` and `M6` per `[tool_change]`, `D` from `OFFSET`; `SETMS(n)` in its own block when the master spindle changes; `S<n>=` and `M<n>=3` for the other spindles.
4. Frames in chain order as `TRANS` (first entry) and `ATRANS`/`AROT`/`AMIRROR` (the rest); `CYCLE800(...)` from the `[transform]` template with `{dir}` from `MOVE`.
5. Cycles with the full sl signature from the catalog; `MCALL` for repeated calls.
6. `WAITM(mark, channels)` from `[sync]`; `RAW:SIEMENS` blocks verbatim.
