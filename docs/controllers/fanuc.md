# Fanuc and the ISO dialects

The Fanuc family is the largest one NCXchange reads and writes: Fanuc Series 0i, 16i/18i/21i, 30i/31i/32i and the controls that speak the same language with their own extensions, which the trade calls "ISO" or "EIA" programming: Mazak EIA, Hyundai WIA, Matsuura, Mori Seiki MAPPS (a Fanuc 31i under Mori Seiki's user interface), Doosan, Nakamura-Tome, Biglia, and the ISO mode of a Siemens (`G290`). The core is DIN 66025 G-code with Fanuc's custom macro B; everything beyond that is the machine builder's, and the reader treats it as such (`machine-builders.md`).

## 1. The program file

```
%
O0001 (2.5D FRAESEN)
N10 G0 G40
N20 G80 G90 G94 G98
N30 G54
(SIDE MILL D10 L35 SD10)
N40 T1 M6
...
N410 M2
%
```

- `%` opens and closes the file (a tape convention; the closing `%` is the file end, not the program end). A file may hold several `O` programs one after another; the control stores each under its `O` number.
- `Oxxxx` (four digits, eight on 30i) names the program; a comment in parentheses after it is the program name shown on the screen. Nakamura names the files of a two-path job by suffix (`O1000` and `O1000.P-2`).
- `N` block numbers are optional and need not be in order; they matter as jump targets (`GOTO 300`) and as `P`/`Q` references of the turning cycles.
- `( ... )` is a comment, anywhere in the block. Nothing else is. Some controls restrict the character set to ASCII capitals.
- `/` or `/n` at the block start is the optional block skip: the block runs unless switch n is on.
- `M30` ends and rewinds, `M2` ends; `M99` in a main program jumps back to its first block (this is how bar-work programs loop). `M0` stops, `M1` stops when the optional stop switch is on.
- Subprograms are `O` programs of their own (after the main program's `M30` in the same file, or in their own file) that end with `M99`; `M98 P0100` calls, `M98 P0100 L3` calls three times (older form `M98 P30100`: three times program 0100), `M99 P30` returns to block 30 of the caller. `M198` calls a program from external memory; `G65 P9010 A1 B2` calls a macro with letter arguments (section 7).
- One `G` per group and up to three `M` codes per block on 30i (one on older controls). The words in a block execute together; `S` and `M3` in a motion block start the spindle before the motion.

## 2. Numbers

- Decimal point programming: `X70.` (trailing dot) and `I-.534` (leading dot) are normal output of CAM systems and must be accepted and, on the writing side, may be written that way or padded (`70.0`), per the `[format]` table of the machine configuration.
- A number without a decimal point is, on a control with "calculator-type decimal point input" off, a count of least input increments (`X70` = 0.070 mm). Modern machines have the calculator type on, and every CAM program writes points, so the readers take `X70` as 70 mm; a machine flag for the other behaviour is a matter for the configuration.
- Angles in degrees, rotary axes usually modulo 360 with a roll-over setting that decides the direction of the shortest way.
- Feed `F` per minute or per revolution, in mm or inch per the units; `S` in rpm or m/min under `G96`.

## 3. Modality and the G groups

Every G belongs to a group; one value per group is active until another G of the same group appears. The groups that matter for the readers:

| Group | Codes | Meaning |
|---|---|---|
| 01 | `G0 G1 G2 G3`, `G33` (thread), `G73 G74 G76 G80..G89` (cycles belong to group 09 on mills) | motion |
| 02 | `G17 G18 G19` | plane |
| 03 | `G90 G91` | absolute or incremental (systems B and C; system A lathes use `U W` addresses instead) |
| 05 | `G94 G95` (mill; lathe B and C), `G98 G99` (lathe system A) | feed per minute or per revolution |
| 06 | `G20 G21` (`G70 G71` in lathe system C) | inch or metric |
| 07 | `G40 G41 G42` | cutter radius compensation |
| 08 | `G43 G44 G49` | tool length offset (mills) |
| 09 | `G73 G74 G76 G80 G81..G89` | canned cycles (mills); `G80` cancels |
| 10 | `G98 G99` | return to the initial level or the R level after a cycle (mills; the same numbers mean feed mode on lathe system A) |
| 12 | `G54..G59`, `G54.1 Pn` | workpiece coordinate system |
| 13 | `G61 G62 G63 G64` | exact stop, tapping, cutting mode |
| 14 | `G66 G67` | modal macro call |
| 16 | `G68 G69` | coordinate rotation (mills); on the Mori Seiki NT the same numbers are the balance cut of turret 2 |
| 17 | `G15 G16` | polar coordinate command |
| 00 | `G4`, `G10`, `G28 G30`, `G52`, `G53`, `G65`, `G92` (`G50` in system A), `G5.1`, `G8`, `G68.2`, `G53.1`, `G43.4`, `G43.5` | one-shot |

The reader keeps a source-side state of every group (its own small virtual machine) so that a block with only coordinates gets the verb of the active group 01 code, the absolute or incremental words of group 03, and so on. The compiler writes a G only when the group changes, unless the configuration asks for the G on every block.

Lathe G-code systems: Fanuc lathes come in three G-code systems selected by a parameter. System A (Nakamura, Doosan, Mori Seiki in the corpus): no `G90`/`G91`, incremental by `U W H V` (X Z C Y), `G98`/`G99` feed per minute or per revolution, `G50` for the spindle limit and the coordinate set, `G90`/`G92`/`G94` are the simple turning, threading and facing cycles. System B (Biglia): `G90`/`G91`, `G94`/`G95`, `G92` for the limit and the coordinate set, cycles `G77`/`G78`/`G79`. System C: like B with `G70`/`G71` for inch and metric and cycles `G20`/`G21`/`G24`. The machine configuration carries the system (`gcode_system` in `[machine]`) and the readers select their tables by it.

## 4. Motion and frames

- `G0` rapid, `G1` at `F`, `G2`/`G3` arcs with the end point and `I J K` (center, incremental from the start point, in the plane of `G17`/`G18`/`G19`) or `R` (radius; negative for the arc over 180 degrees; a full circle needs `I J`). A third axis in the arc block makes a helix.
- `G90`/`G91` modal (B, C, mills). `G91 G28 Z0` is the idiom for a reference point return of Z (the `0` is an incremental distance of zero on the way to the reference point); `G28 U0 W0` the lathe form; `G30 P2` the second reference point. `G28` with a real distance moves to the intermediate point first.
- `G53` in the block: machine coordinates for that block, also with macro variables (`G53 X#528`).
- `G54`..`G59` workpiece coordinate systems, `G54.1 P1`..`P48` (or `P300`) the additional ones; `G10 L2 P1 X.. Z..` writes them from the program (a data setting the reader keeps as `RAW`).
- `G52 X Y Z` local coordinate system (a shift inside the active `G54`); a new `G52` replaces the old one, `G52 X0 Y0 Z0` cancels.
- `G92 X Z` (B, C) or `G50 X Z` (A) declares the current position to be these coordinates without moving; `G50 C0` after `G28 H0` is the common C-axis reset before polar interpolation.
- `G68 X Y R30` rotates the coordinate system about a point, `G69` cancels; `G51.1 X0` mirrors, `G50.1` cancels; `G51 P scale`.
- `G68.2 X Y Z I J K` tilted working plane by Euler angles (`P` selects other angle conventions), `G53.1` positions the rotary axes to it, `G69` cancels; `G68.1` rotates the coordinate system about an axis (3D coordinate conversion, on Mazak and Mori Seiki).
- `G43 H1` tool length offset plus, `G44` minus, `G49` cancel; `G43.4` tool center point control (type 1), `G43.5` (type 2, with the tool vector as `I J K`); `G41.2` 5-axis radius compensation on Mazak.
- `G41`/`G42` `D1` cutter compensation, `G40` off, applied from the motion of the same block.
- `G12.1`/`G13.1` polar coordinate interpolation (face milling on a lathe with X and C as Cartesian), `G7.1 C30` cylindrical interpolation, `G7.1 C0` off. Builders alias them (`G112`/`G113`, `G107`).
- `G4 P1000` dwell (milliseconds or seconds by parameter; `G4 X1.` or `G4 U1.` seconds on lathes).
- `G5.1 Q1` AI contour control on, `Q0` off; `G5 P10000` high precision mode; `G8 P1` look-ahead; `G61` exact stop, `G64` cutting mode; the tolerance itself sits in a parameter, so NCX `TOLERANCE` reaches Fanuc only as the on and off switch.
- `G96 S200` constant surface speed with the limit `G50 S3000` (A) or `G92 S3000` (B, C), `G97 S1500` rpm.
- Polar coordinates `G16`/`G15` with the radius in X and the angle in Y: converted to Cartesian by the reader.
- Chamfer and corner rounding attached to a motion block, `,C2` and `,R4` (with a comma), are expanded by the reader into the line and arc they stand for.

## 5. Tools

The `T` word means different things per machine kind, and the two-word model of NCX (`PRELOAD`, `TOOL`) covers all of them:

- Mill with a magazine: `T5` alone brings tool 5 to the change position (preload); `M6` performs the change to the preloaded tool; `T4 M6` in one block changes to 4 directly (the control preloads and changes). `T5` after the `M6` in the same block is illegal on most controls. `M6` without any preload is an error in the source.
- Lathe with a turret: `T0404` indexes station 4 and activates offset 4 in one word; `T0656` is station 6 with offset 56; `T0100` cancels the offset of station 1; six-digit forms `T001001` exist on 120-tool ATCs. A `T` word alone in its own block moves the axes by the difference of the offsets, at rapid under `G0`, which is why the builders recommend writing it inside a `G0` block; the reader emits the offset change and no motion.
- Mill-turn ATCs wrap the change in a builder macro: Nakamura `G340 T0101. A02.` (change to 1 with offset 1, prepare 2), Mori Seiki `T9001` then `G361 B0 D1.`, DMG on Siemens `T="NAME"` then `TC(...)` (`machine-builders.md`).
- `G43 H` and `D` carry the offsets on mills; on lathes the offset is inside the `T` word. `H` and `D` of the same tool usually have the same number (a CAM habit, not a rule).

Spindles: `M3`/`M4`/`M5` with `S`. Fanuc has one `S` address; on a machine with several spindles the `S` belongs to the spindle whose M code is in the same block (Nakamura: `M88 S1000` for the rotary tool, `M3 S500` for the main spindle), otherwise to the spindle selected last. The compiler therefore writes speed and direction together; the reader assigns a bare `S` with its source-side state. Doosan selects the spindle with a `P` suffix (`M03 P11`, `M03 P12`), Mori Seiki and Nakamura with offset M numbers (`M203`, `M53`).

## 6. Cycles

Mill canned cycles are one block that defines and calls: `G81 G99 Z-21.732 R5. F565` drills at the current X Y to Z-21.732 (absolute under `G90`), starting the feed at `R5.` and returning to R (`G99`) or to the initial level (`G98`); every following block with a position calls the cycle again at that position; `G80` (or a `G0`/`G1` on some controls) cancels. `G82` adds a dwell `P` (milliseconds), `G83` pecks with `Q`, `G73` chip-breaks, `G84` taps (`M29 S` before it makes it rigid; the pitch is `F`, or `F`/`S` in per-minute mode), `G85` reams, `G86` bores, `G76` fine bores with a shift, `G74` left-hand taps, `G87`..`G89` back boring and variants. `K3` on the cycle block repeats it three times at incremental steps under `G91`. The reader converts `R` and `Z` to the absolute `CLEARANCE` and `DEPTH` of NCX and keeps the modal call behaviour by emitting a `CYCLE_CALL` per position.

Lathe drilling cycles: `G83`/`G84`/`G85` drill along Z at a `C` position on the face, `G87`/`G88`/`G89` along X on the circumference (NCX `AXIS=X`); Mori Seiki adds deep-hole variants `G83.5`/`G87.5`. Turning cycles: `G90` (OD/ID), `G92` (thread), `G94` (face) in system A (`G77`/`G78`/`G79` in B) are modal like the drilling cycles, a following block with only `X`, `Z`, `R` (or `U`, `W`) repeats them; `G70`..`G76` (finish, rough OD, rough face, pattern repeat, face cut-off, OD groove, compound thread) are one-shot blocks whose `P` and `Q` name the block range of the contour that follows, which NCX carries as a subprogram section referenced by `CONTOUR=`.

## 7. Custom macro B

- Variables `#1`..`#33` are local to a macro call level, `#100`..`#199` common (cleared at power-off), `#500`..`#999` permanent, `#1000` and above system variables (`#5021`..`#5025` current machine position, `#5041`.. workpiece position, `#4001`.. modal G codes, `#3000` alarm, `#3001` timer, `#2001`.. and `#11001`.. offset and wear registers on lathes: `#11099` is the Z wear register 99 in the Nakamura transfer program). `#0` is empty (vacant).
- Expressions in brackets: `#1 = [#2 + 3.5] * SIN[#3]`; functions `SIN COS TAN ATAN SQRT ABS ROUND FIX FUP LN EXP`; operators `+ - * / MOD`, comparisons `EQ NE GT GE LT LE`, logic `AND OR XOR`.
- Flow: `GOTO n` (to block `Nn`), `IF [cond] GOTO n`, `IF [cond] THEN #1 = 2`, `WHILE [cond] DO1` ... `END1` (three nesting levels), `M99` returns from a macro.
- Calls: `G65 P9010 A1. B2. Z-5.` calls program 9010 once with letter arguments; `G66 P9010` makes it modal (called after every motion block, like a cycle) until `G67`; the letters map to local variables by the standard table: `A`=#1, `B`=#2, `C`=#3, `I`=#4, `J`=#5, `K`=#6, `D`=#7, `E`=#8, `F`=#9, `H`=#11, `M`=#13, `Q`=#17, `R`=#18, `S`=#19, `T`=#20, `U`=#21, `V`=#22, `W`=#23, `X`=#24, `Y`=#25, `Z`=#26 (`G`, `L`, `N`, `O`, `P` cannot be arguments). NCX names them `V1`..`V33` and `#100` becomes `V100`, because `#` is not a legal character in an NCX name.
- Builder macros are called through G codes bound to programs (`G340`, `G411`, `G300` on Nakamura; `G361` on Mori Seiki) or through M codes that run hidden subprograms; the reader cannot see inside them, which is why the machine configuration and the reader rules decide what they mean.

## 8. Several paths

A two-path lathe runs one program per path (turret, or head and turret). The paths synchronize with wait M codes in a parameter-defined range (`M100`..`M199` on Nakamura, `M101`..`M197` on Mori Seiki): both programs must reach the same M number before either continues. On three- and four-path machines a `P` word names the participants as a path list (`P12`, `P123`) or a bitmask (`P3` = paths 1 and 2, `P7` = all), selected by a parameter; without `P` all paths wait. `M199` at the start synchronizes the program start on Nakamura. The same mark may be used many times in program order. Balance cut (`G68`/`G69` on the Mori Seiki NT) lets two turrets cut the same contour from both sides.

## 9. Rules for the reader

1. Resolve every block against the source-side state: verb from group 01, absolute or incremental from group 03 or the `U W` addresses, plane from group 02, feed mode from group 05 or `G98`/`G99` on system A.
2. `T`, `M6` and the offsets per section 5; `S` binds to the spindle of the M code in the same block, else to the last selected spindle.
3. `G91 G28 Z0` and `G28 U0 W0` are `HOME`; `G30 P2` is `HOME POINT=2`; `G53` is `FRAME=MACHINE` on the block.
4. `M99` in the main program is a jump to a label after the header; `/M30` is `SKIP JUMP=END`; `GOTO` to a block below `M30` and back becomes a section in front of `PROGRAM=END` behind `JUMP=END`.
5. `G81`..`G89` define and call; following position blocks call; `G80` cancels; `R` and `Z` become absolute `CLEARANCE` and `DEPTH`; `K` repeats expand.
6. `G52` replaces the previous `G52`, so the reader writes `SHIFT=RESET` before the new `SHIFT`.
7. `G10`, `G65`/`G66` calls of unknown macros, probing (`G31`, `G38`), builder G macros not in the configuration: `RAW` with a WARNING, never dropped.
8. Numbers are taken as written; a trailing or leading dot is accepted.

## 10. Rules for the writer

1. Write `G0`/`G1` only when the verb changes (a `[format]` option may ask for the G on every block); write `G90` once in the header and use absolute values, or write `G91` blocks where the NCX block had incremental words (a machine setting).
2. Write `M3 S1000` in one block, never `S1000` alone after another spindle's M code (section 5).
3. Program end from `program_end` (`M30` or `M2`); subprograms after it as `O` programs ending with `M99`, or as separate files when the machine wants that.
4. Wait marks from the `[sync]` table with the path form the machine uses (list or bitmask).
5. Decimal point on every real number per `[format]` (`decimals`, `trailing_zeros`); comments in parentheses, transliterated to ASCII (`comment_charset`).
6. Numbers, names and file names per `[format]`; block numbers per `format.block_numbers`.
