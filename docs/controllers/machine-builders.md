# Machine builders

The controller manual is never the whole story. Every builder adds M codes, G macros and cycles, and two machines with the same control differ in the spindle numbering, the wait codes and the tool change. This is why the machine configuration (`../spec/machine-config.md`) is template based and why readers have a `RAW:BUILDER` word and reader rules. This document says, per builder, what the machine is, what its dialect looks like and where it goes in the configuration. The code-by-code table is section 8 of `../spec/controller-mapping.md`; the example machine files are in `../spec/examples/machines/`.

## 1. Fanuc-based lathes and turn-mill centers

### Nakamura-Tome (NTJX, NTY3, WY, AS-200L; Fanuc 18i-TB, 0i-TF, 31i-B, G-code system A)

Twin-turret and multi-spindle turning centers with two, three or four paths (upper turret L, upper turret R on the NTY3, lower turret, a tool spindle with an ATC on the NTJX). Their programming manual 4810004E documents the whole dialect, called "NT NURSE" macros: `G340 T0101. A02.` changes the ATC tool (two-digit tool and offset with a literal decimal point, `A` the next tool), `G341` prepares, `G419 A` orients the tool spindle, `G360`/`G361` mirror Z for the sub spindle side (or a separate datum `G59` with `M427`), `M428`/`M427` say which spindle the turret works on and are written with the datum (`G54 M428`), `M91`/`M41` and `M491`/`M441` switch the C axes, `M96`/`M92`/`M97`/`M93` synchronize the spindles (from the path that owns the second spindle), `G496`/`G497` the cycle form, `M100`..`M199` are the wait marks with `P` path lists or bitmasks, `M199` synchronizes the program start, `M800`..`M849` a second wait group on the WTW, `G411 x1. I{n}` jumps on the part status, `G471`..`G486` and `G480` are branch commands (read as `JUMP` with `IF`), `G300` checks the cut-off, `G131` soft approach, `G333` resets modals, `G330` goes to reference points 2..4, `G10 L2 P` writes datums, `M200 P` calls a program from the memory card, and the second C axis of a path is `C2=`/`H2=` (NTX II) or `A` (SC series). Files of one job carry the path in the name (`O1000`, `O1000.P-2`). Example file: `nakamura-ntjx.toml`; example programs: `../spec/examples/sources/NAKAMURA_WY250L_O1000.path1.nc` and `.path2.nc`.

### Mori Seiki (NL, NT, NTX; Fanuc under MAPPS, system A)

The NT and NTX are turn-mill centers with a tool spindle (HEAD 1) and a lower turret (HEAD 2), each with its own program. Tool change of the tool spindle in two steps: `T9001` brings the tool to the change position, `G361 B{angle} C{check} D{kind} T{wash}` changes it (`D0.` rotary tool, `D1.`/`D2.` turning tool with the clamp `M436`/`M437`); the turret uses `T0101` with the offsets of spindle 2 shifted by 30 (`T0131`). Spindle 2 has its own codes (`M203`..`M205`, `M245`/`M246` C axis, `M303`/`M304` for the side the turret works on), the sub spindle sits on the `A` axis (`G0 A`, `G53 G0 A`, `G330` reference, `G38 A K F Q` push check, `M80` cut-off detection), the spindle synchronization is `M35` (speed), `M34` (phase), `M36` (off), written in both programs after a wait code, `M480`/`M481` synchronize the C axes, `M395`/`M396` mirror Z, `M582`/`M583` switch to radius programming (which fits NCX `DIAMETER` with `programming = "switchable"`), `G10.9` switches diameter and radius dynamically, `M101`..`M197` are the wait marks (the ESPRIT postprocessor counts up from 101), `G68`/`G69` on the NT is the balance cut of turret 2, `M198` calls an external program. Example file: `mori-ntx1000-mapps.toml`.

### Doosan (Puma 2600SY; Fanuc, single channel, system A)

A single-channel lathe with a sub spindle and a milling spindle selected by a `P` suffix on the spindle M code (`M03 P11` turning spindle, `M03 P12` milling spindle, `M315` stop without check), `M34`/`M134` for the side the turret works on, `M35`/`M135` C axes, `M203`..`M206` and `M213`/`M214` spindle synchronization with phase, `M291`/`M292` select the retract or chip-breaking behaviour of `G83`/`G88` before the cycle (an expansion rule in the catalog entry), `M68`/`M69`, `M168`/`M169` chucks, `M116` ejector, `G300`/`G301` sub spindle as tailstock, `G350` cut-off check, polygon `M28`. Example file: `doosan-puma-2600sy.toml`.

### Biglia (B545, B565, Smart Turn; Fanuc 18i-T, G-code system B)

Twin-spindle lathes on the B system (`G90`/`G91`, `G94`/`G95`, `G92 S` limit, cycles `G77`/`G78`/`G79`): counter spindle `M203`..`M205`, driven tools `M73`..`M75`, `M64`/`M65` for the side, `M10`/`M11` C axis, `M61`/`M62`/`M63` spindle synchronization, `M42` wait before a tool change, `M19` and `M10`..`M15` spindle orientation in 30 degree steps, `M24`/`M25` chuck, `M53` collet, `M85` ejector, `G101`..`G103` B-axis programs, `G131` tailstock cycle, `G183` deep hole drilling with decreasing pecks, the Easy Biglia cycles of volume 3.

### Mazak (EIA), Matsuura, Hyundai WIA, Hermle ISO

Milling machines whose ISO programs are in the sample corpus: Mazak EIA with `G43.4`, `G41.2` (5-axis radius compensation), `G68.2`/`G53.1`, `G5 P2`; Matsuura with `G120`/`G121`/`G400` (precision modes) and `G153`/`G154`, `G254`/`G255`; Hermle in ISO mode with `M128`-like functions as M codes. They read as Fanuc with their extra G codes as catalog entries or `RAW:BUILDER`. Mazatrol (the conversational format) and Okuma OSP are out of scope (D69).

## 2. Siemens-based machines

### DMG MORI, GILDEMEISTER structure programming (CTX, NTX, CLX, GMX; Siemens 840D sl)

DMG's lathes are programmed with a house structure on top of the 840D: two channels (`CHAN 1`, `CHAN 2`) with start programs `L1001`/`L2001` that write the datums from `RG` parameters, spindles numbered 4 (main), 3 (counter), 1 and 2 (tool spindles), 5 (slide 3) and addressed as `M4=3`, `S4=1500`, `G96 S4=200 LIMS=3500`; `SETMS(n)` selects the master spindle; `M814`/`M813` say which spindle the slide works on; C-axis modes by cycles `L707({angle})`/`L708` (spindle 4), `L705`/`L706` (spindle 3), `L701`..`L704` (tool spindles); clamps `M412`/`M413`, `M312`/`M313`, `M112`/`M113`; spindle synchronization `L726({angle})`/`L727`, polygon `L725`/`L729`; tool change `T="NAME"` or `T1` then `TC(D, direction, place, kind, B1 angle, C1 angle)`; tool change points `L710`..`L713`; transformations `TRACYL_S4({d})`, `TRANSMIT_S4`, `TRANS_5A`, `TRANS_OFF` (the DMG form of `TRAFOOF`), `CYCLE800("TC1")`; chucks `M436`/`M437` (4), `M336`/`M337` (3); `L730`..`L733` fixed stop and cut-off check; `BARLOAD`, `RL_POS`, `RECEPTAC`, `UNLOAD_R` for the bar loader and the unloader; `WAITM(mark, WAIT_K1, WAIT_K2)` with marks 0..99 (95 taken by `BARLOAD_SYNC`); `L770`..`L772` measuring, `L781`/`L782` tool monitoring, `CHECK_MAG`, `SETPIECE`; labels `NNnnnn:`; `STOPRE`; `M99` restart with cycle time check. The DMC mills write `T="NAME" M6`, `L_FREI` (retract) and `TRAORI`. Documented for the record (D68: not implemented in 1.0, nothing may prevent it later). Example file: `dmg-ctx-840d.toml`.

### DMG CLX 350 and 550 with the DLL postprocessor (Siemens 840D sl)

Programs written by the maintainer's own postprocessor for the CLX: a `;PROJEKT =` header, `WORKPIECE(,,,"CYLINDER",...)`, `MSG("OP1 - BOHREN")` per operation, `T="DREHBOHRER_D30"` then `TC(1)`, `G54`, `SETMS(4)`, `LIMS=`, `G94 G97 S3600 M3` or `G95 G96 S180 M3`, `DIAMON`, `M108`/`M109` for the driven tools, `M067` at the end, `M30`. Generic Siemens plus the `TC` and `SETMS` of the structure programming.

### Monforts RNC 700 (Siemens 840D)

A twin-spindle lathe programmed by hand and by a post that writes a restart structure: header `;1_OG_SP2_RNC700`, `G54`, `LIMS=1500`, a tool list in `R201`..`R212`, then `AUTOSUCH`, `GOTOF WERKZEUG`, `WERKZEUG_0_0:` and for every operation `STOPRE`, a label `WERKZEUG_<tool>_<step>:`, `WKZ_NR=1 WKZ_STEP=1`, `MSG("...")`, the technology block `G97 T1 D1 S286 M4 M41` (tool, edge, speed, direction, gear range) and the motion; `M108`/`M109` driven tools, `M8`/`M9`, `MCALL CYCLE81(11, 6, 5, -50,)` with the feed set by `F0` or `F.08` before it, `PROGENDE:` and `M067` `M30` at the end. Everything beyond the generic words is `RAW:BUILDER` and reader rules (the restart labels are `LABEL`s, `AUTOSUCH` a `FUNC`).

### STAMA MT 724 2C (Siemens 840D sl, two channels)

A twin-spindle vertical turn-mill center with two channels (`_C1`, `_C2` files) and a builder framework: `ZYK_START`, `UHRAUS`/`UHREIN` (timers), `TI_PROG(1001)` and `TI_ON(1)`/`TI_OFF` (tool inspection), `ZYK_S832(,,1)`, `ZYK_S832(2)`, `ZYK_S832(99)`, `ZYK_S832` (the STAMA wrapper of `CYCLE832` and its marks), `MTWARM11_C1` (warm-up), the header block `G153 G0 G17 G90 G40 G60 G451 G601 CFC NORM`, `NPL(2, GC1_XDREMI, GC1_YDREMI, GC1_ZDREMI)` (datum from global variables), `T1 D1` then `L6` (tool change), `TRANS X0. Y0. Z=R40`, `TURNH()`/`TURNV()` (turning modes with `SPOS[1]=180`), `DREH(0, 0, R40, 90, 0)` plus `TRANS X=R43 Y=R42 Z=R41` (the tilt as a builder cycle plus a shift from variables), `TRACYL_ON`, `AROT X180` on the second spindle, the second channel's axes `AA`, `BB`, `XX`, `SPOS[2]`, and a recipe block `<PROG_BEGIN_C1>` ... `<PROG_END_PAR>` with `GREZ_*`, `GL_*`, `GG2_*`, `GML_*` loader parameters that is not NC syntax (kept as `RAW`). A Doosan VCF 850 LSR program written with the same post exists in the corpus.

### Burkhardt+Weber MCX 1200 (Siemens 840D sl)

A horizontal machining center with a B table: datum writes `$P_UIFR[1, X, TR]=-56.667` (and `FI` fine parts) guarded by `IF $P_SEARCH OR $P_SIM GOTOF BEGINN`, `NEWCOORD(54, B 0, 0, 0, 0, 0)` (a builder datum cycle), `L798`, `L706(, 1, , "1")` (tool change), `G0 SUPA Z1800 D0` (retract), `B=DC(270)`, `G0 D1 X.. Y.. S828 M3 M40 F39`, `MCALL CYCLE81(3, 1, 2, -50,)` with positions, `M40`, `M854`. One of the files is a TopSolid postprocessor template with `//[DEFINE_...]` lines and is not NC code.

### Pittler (Rollstar, with Y axis; Siemens 840D sl)

A vertical lathe with a B-axis tool carrier: `WZW("15226", "BLIND2", 1)` (tool change by name), `B_TC(90, 1, 1)` (tool carrier position), `TOWSTD`/`TOWWCS` (tool orientation reference), `SETMS(1)`/`SETMS(3)`, `G25 S20`, `G26 S700`, `LIMS=700` before every operation, `G641` with `ADIS=0 ADISPOS=0.5` in the header, `AROT RPL=45` between `G18` and `G17`, `B225 C11=-90` (a second C axis), `ROT` to cancel, `G53 Z1700 D0` retracts, `M27`, `M80` at the end.

### Hermle C22 U on 840D sl

A 5-axis machining center with an A/C table: `CYCLE800(0, "HERMLE", 0, 39, 0, 0, 0, 180, 0, -90, 0, 0, 0, -1,)` with axis-wise angles (mode 39) for the 3+2 operations and `CYCLE800` to cancel, `G53 Z330 D0`, `G53 X0 Y0` retracts, `T1` then `M6` then `G54 D1`, and for the simultaneous operations `A35.732 C36.87` positioning followed by `TRAORI`, `G54` and the point list, `TRAFOOF` at the end, `T=0` before `M30`. The Hermle programs in Klartext (on Heidenhain controls) use the OEM cycles 3xx (`CYCL DEF 361 Standard TFB`, 331, 335, 336).

### INDEX (Siemens 840D sl, multi-channel job archive)

An INDEX turning center exported as one archive: `%_N_1_0_MPF` (the job frame with `GROUP_BEGIN(0, "Kanal-1 ", 0, 0)`, `L100`, `M1=28 H20`, `WAITM(0, 1, 2)`, `GX73`/`GZ73` retract macros, `G59 X=XMW_1 Z=ZMW_1 C=CMW_1` datum from variables, `SETMS(4)`, `G92=2000`, `T1 D0`, `H50=50`, `GOTOF "STEP_" << I_STEP` computed jumps, `STEP_0:`, `I_PRUN(4, 0, 1, XMW_1, ...)` step registration, `WAITM(2, 1, 2)`, `WAITM(8, 1, 2)`, `I_M392`, `M30`), `%_N_TH1_HS_01_SPF` (an operation: `L131` spindle selection, `L171("C80-Rhombic", 0, 0)`, `L172(110)`, `GXZ73`, `L184(3, 0, -25, ...)`, `G18`, `SETMS(4)`, `G95 S4=2000 M4=4 D1`, `M20=151`, `GZ73 M4=5`, `M01`, `M17`), `%_N_1_3_MPF` (the tool list: `MAG=1`, `PL=1 ID="C80-Rhombic" TYP=520`, `SN=1 D=1`, `Q= L= R=`), and `%_N_INDEX_INI` (`[MAFU]` machine function groups and the tool change positions). The INDEX framework is a reader-rule case: the step labels, the `I_PRUN` calls and the wait marks structure the job, the operations are generic Siemens.

### DMG 5-axis mills with TopSolid output (Siemens 840D sl)

Programs from the maintainer's TopSolid postprocessor for DMG mills: `%_N_1_MPF`, `;$PATH=`, `G0 G40 G90 G94`, `CYCLE800` (cancel), `G54`, `T="SF8-ST-NL30-SL21" D1`, `M6`, `T="next"` (preload), `S5968 M3`, `MSG("OP5 - POCKETING")`, `CYCLE800(1, "DMG", 200000, 27, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, , 0)` for 3+2 (mode 27 = axis-wise, order Z Y X), `TRAORI` for simultaneous operations, `I=AC() J=AC()` centers and `CR=` radii, `G75 Z0`, `G75 X0 Y0` fixed points at the end, `WORKPIECE(,,,"BOX",...)`, `M2`. The largest programs of the corpus (26 909 lines of arcs).

## 3. How a builder lands in the configuration

| The builder has | Configuration table | NCX side |
|---|---|---|
| its own M codes per spindle, C axis, chuck, clamp, coolant | `[spindle.ROLE]`, `[spindle_mode.ROLE]`, `[coolant]`, `[func]` templates per state | `SPINDLE:ROLE`, `SPINDLE_MODE:ROLE`, `COOLANT:name`, `FUNC:name=state` |
| a tool change macro with extra parameters | `[tool_change]` templates with `{tool}`, `{offset}`, `{next}`, `{kind}`, `{b}`, `{c}`, `{name}` | `TOOL`, `PRELOAD`, `OFFSET` |
| wait codes in a range with a path form | `[sync]` with `marks`, `paths = "list"` or `"bitmask"`, `start_mark`, `groups` | `SYNC`, `WITH` |
| codes that only one path may write, or that every path must write | `channel = n` or `channels = "all"` on the table | the job compiler moves or duplicates the words |
| a native "which spindle the turret works on" code | `[workpiece]` with `SUB_frame = "datum"` or `"mirror"` | `WORKPIECE=ROLE` |
| a mode M code before a cycle | expansion rule `pre = [...]` on the catalog entry | nothing in the program |
| a sequence rule (stop the spindle before the clutch) | expansion rule with `requires` and `restore` | generated blocks with `@SAVE`/`@RESTORE` |
| an M code that runs a hidden subprogram (a transfer, a datum cycle) | a reader rule in a plugin (`ISourceRule`) | the word the rule decides on |
| macros nobody can express (`G411`, `G300`, `L730`, `NPL`, `DREH`) | nothing | `RAW:BUILDER`, WARNING on read, ERROR on compile for another machine |
