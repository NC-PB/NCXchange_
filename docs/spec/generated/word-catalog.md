# Word catalog

Generated from the word catalog in `src/Ncx.Core/Catalog/` by the test `WordCatalogTableTests` (P0-03);
change the catalog and run the tests instead of editing this file. The table is the reference for the
canonical order of language 5 rule 6 (D90): a word of lower rank stands first. It lists every word of the
tables of section 4 of `../ncx-language.md` with the section it comes from, the pseudo-words of 4.15
(internal, D95), and every form with a rank of its own: the addresses of a fixed set (`OFFSET:LEN`,
`CENTER:IX`, `TOLERANCE:ROTARY`), the reset form of `SHIFT`, `TILT` and `TILT_AXIS`, the machine axis words
(D93) and the native cycle parameters (D94). Words of one key with an open address (a role, a channel, a
variable) sort under their rank by the address text. The ten verbs share one rank; a block has at most one.

| Rank | Word | Value | Verb | Axis words | Address | Scope | Section | Meaning |
|---:|---|---|---|---|---|---|---|---|
| 10 | `SKIP` | none, integer |  |  |  | block | 4.1 | Optional block skip, bare or with the number 1 to 9 of the block skip switch (D53). |
| 20 | `FILE` | `BEGIN`, `END` |  |  |  | file | 4.1 | First block of every file, FILE=BEGIN with NCX=1, and its last block, FILE=END. |
| 30 | `NCX` | integer |  |  |  | file | 4.1 | Format version, NCX=1, in the FILE=BEGIN block. |
| 40 | `PROGRAM` | `BEGIN`, `END` |  |  |  | program | 4.1 | Start of a program, PROGRAM=BEGIN, and its executed end, PROGRAM=END, exactly once and its last block. |
| 50 | `SUB` | `BEGIN`, `END` |  |  |  | file | 4.9 | Start of a subprogram section of the file, SUB=BEGIN with NAME, and its end, SUB=END, the return to the caller (D89). |
| 60 | `NAME` | string; with `SUB=BEGIN`: integer, string, identifier |  |  |  | with partner | 4.1 | Program name with PROGRAM=BEGIN, name of a subprogram section with SUB=BEGIN (4.9, 4.13), optional program selector with START_CHANNEL (4.8); an identifier or an integer names a subprogram section only (D90). |
| 70 | `NUMBER` | integer |  |  |  | with partner | 4.1 | Program number (Fanuc O0001), with PROGRAM=BEGIN. |
| 80 | `CHANNEL` | integer |  |  |  | header | 4.1 | Channel the program runs on, a header word after PROGRAM=BEGIN; default 1 (4.14). |
| 90 | `SHIFT` | none, `RESET` | verb | carries |  | modal | 4.2 | Datum shift in the active frame, appended to the frame chain; SHIFT=RESET removes the shifts and what follows them. |
| 90 | `TILT` | none, `RESET` | verb | carries |  | modal | 4.2 | Tilted working plane by the spatial angles A, B, C, appended to the chain; TILT=RESET removes it and what follows. |
| 90 | `TILT_AXIS` | none, `RESET` | verb | carries |  | modal | 4.2 | Tilted working plane by the rotary axis positions of this machine, appended to the chain; TILT_AXIS=RESET removes it and what follows (D82). |
| 90 | `SETPOS` | none | verb | carries |  | block | 4.2 | Declares that the current position has these coordinates in the active workpiece frame; nothing moves (D55, D101). |
| 90 | `RAPID` | none | verb | carries |  | block | 4.3 | Rapid positioning (G0, FMAX) to the target given by the axis words. |
| 90 | `LINE` | none | verb | carries |  | block | 4.3 | Linear interpolation at the active feed (G1, L with F). |
| 90 | `ARC` | `CW`, `CCW` | verb | carries |  | block | 4.3 | Circular interpolation in the working plane to the target, with CENTER or R, or over a sweep ANGLE around CENTER. |
| 90 | `RETRACT` | none, number, expression | verb |  |  | block | 4.3 | Retract along the tool axis, bare to the axis limit, with a value by that distance (D83). |
| 90 | `HOME` | none | verb | carries |  | block | 4.3 | Reference point return for the axes named bare in the block, machine axes included (D93, D100). |
| 90 | `CYCLE_CALL` | none | verb | carries |  | block | 4.7 | Executes the active cycle at the position of the axis words, or at the current position without them (CYCL CALL, M99). |
| 100 | `X` | none, number, expression |  | is one |  | block | 4.3 | Absolute target coordinate of the axis X; bare as an axis name of HOME. |
| 110 | `Y` | none, number, expression |  | is one |  | block | 4.3 | Absolute target coordinate of the axis Y; bare as an axis name of HOME. |
| 120 | `Z` | none, number, expression |  | is one |  | block | 4.3 | Absolute target coordinate of the axis Z; bare as an axis name of HOME. |
| 130 | `A` | none, number, expression |  | is one |  | block | 4.3 | Absolute target coordinate of the axis A; bare as an axis name of HOME. |
| 140 | `B` | none, number, expression |  | is one |  | block | 4.3 | Absolute target coordinate of the axis B; bare as an axis name of HOME. |
| 150 | `C` | none, number, expression |  | is one |  | block | 4.3 | Absolute target coordinate of the axis C; bare as an axis name of HOME. |
| 160 | machine axis words (D93) | number, expression; none under `HOME` |  | is one |  | block | 4.3 | A key of the form `[XYZABCUVW][0-9]{0,2}` that is no catalog word, in a block whose verb carries axis words; machine axes sort by letter, then by number: `C2`, `W`, `Z2`. |
| 170 | `IX` | number, expression |  | is one |  | block | 4.3 | Incremental target of the axis X, the current position plus the value. |
| 180 | `IY` | number, expression |  | is one |  | block | 4.3 | Incremental target of the axis Y, the current position plus the value. |
| 190 | `IZ` | number, expression |  | is one |  | block | 4.3 | Incremental target of the axis Z, the current position plus the value. |
| 200 | `IA` | number, expression |  | is one |  | block | 4.3 | Incremental target of the axis A, the current position plus the value. |
| 210 | `IB` | number, expression |  | is one |  | block | 4.3 | Incremental target of the axis B, the current position plus the value. |
| 220 | `IC` | number, expression |  | is one |  | block | 4.3 | Incremental target of the axis C, the current position plus the value. |
| 230 | incremental machine axis words (D93) | number, expression |  | is one |  | block | 4.3 | `I` followed by a machine axis name: `IC2`, `IW`, `IZ2`, in the order of the absolute words. |
| 240 | `TX` | number, expression |  | is one |  | block | 4.3 | X part of the tool axis direction at the end of a LINE under TCPM=ON, a unit vector (D81). |
| 250 | `TY` | number, expression |  | is one |  | block | 4.3 | Y part of the tool axis direction at the end of a LINE under TCPM=ON, a unit vector (D81). |
| 260 | `TZ` | number, expression |  | is one |  | block | 4.3 | Z part of the tool axis direction at the end of a LINE under TCPM=ON, a unit vector (D81). |
| 270 | `NX` | number, expression |  | is one |  | block | 4.3 | X part of the surface normal at the end of the LINE, only together with TX TY TZ (D81). |
| 280 | `NY` | number, expression |  | is one |  | block | 4.3 | Y part of the surface normal at the end of the LINE, only together with TX TY TZ (D81). |
| 290 | `NZ` | number, expression |  | is one |  | block | 4.3 | Z part of the surface normal at the end of the LINE, only together with TX TY TZ (D81). |
| 300 | `CENTER:X` | number, expression |  | is one | `X` | block | 4.3 | Arc center on a plane axis, CENTER:X absolute, CENTER:IX incremental from the start point of the arc (the I J K of Fanuc and Siemens). |
| 310 | `CENTER:Y` | number, expression |  | is one | `Y` | block | 4.3 | Arc center on a plane axis, CENTER:X absolute, CENTER:IX incremental from the start point of the arc (the I J K of Fanuc and Siemens). |
| 320 | `CENTER:Z` | number, expression |  | is one | `Z` | block | 4.3 | Arc center on a plane axis, CENTER:X absolute, CENTER:IX incremental from the start point of the arc (the I J K of Fanuc and Siemens). |
| 330 | `CENTER:C` | number, expression |  | is one | `C` | block | 4.3 | Arc center on a plane axis, CENTER:X absolute, CENTER:IX incremental from the start point of the arc (the I J K of Fanuc and Siemens). |
| 340 | `CENTER:IX` | number, expression |  | is one | `IX` | block | 4.3 | Arc center on a plane axis, CENTER:X absolute, CENTER:IX incremental from the start point of the arc (the I J K of Fanuc and Siemens). |
| 350 | `CENTER:IY` | number, expression |  | is one | `IY` | block | 4.3 | Arc center on a plane axis, CENTER:X absolute, CENTER:IX incremental from the start point of the arc (the I J K of Fanuc and Siemens). |
| 360 | `CENTER:IZ` | number, expression |  | is one | `IZ` | block | 4.3 | Arc center on a plane axis, CENTER:X absolute, CENTER:IX incremental from the start point of the arc (the I J K of Fanuc and Siemens). |
| 370 | `CENTER:IC` | number, expression |  | is one | `IC` | block | 4.3 | Arc center on a plane axis, CENTER:X absolute, CENTER:IX incremental from the start point of the arc (the I J K of Fanuc and Siemens). |
| 380 | `R` | number, expression |  | is one |  | block | 4.3 | Arc radius, not 0, positive for an arc of 180 degrees or less, negative for more; the key belongs to arcs only (D96). |
| 390 | `ANGLE` | number, expression |  | is one |  | block | 4.3 | Sweep angle of an ARC in degrees, greater than 0, in the direction of its verb, instead of the plane end point (D84). |
| 400 | `F` | number, expression |  |  |  | modal | 4.3 | Feed in the active feed mode. |
| 410 | `FEED_MODE` | `PER_MIN`, `PER_REV` |  |  |  | modal | 4.3 | Feed per minute (G94) or per spindle revolution (G95); default PER_MIN. |
| 420 | `PRELOAD` | integer, string |  |  | role, optional | modal | 4.4 | Prepares a tool in the magazine, modal until the change consumes it; PRELOAD=0 clears a pending preload (D47). |
| 430 | `TOOL` | none, integer, string |  |  | role, optional | modal | 4.4 | The spindle or the addressed holder now carries this tool, TOOL=0 empties it; a bare TOOL changes to the preloaded tool (D47, D91). |
| 440 | `OFFSET` | integer |  |  | offset kind, optional | modal | 4.4 | Combined offset register; OFFSET:LEN the tool length offset register (G43 H), OFFSET:RAD the tool radius offset register (D); 0 cancels. |
| 450 | `OFFSET:LEN` | integer |  |  | `LEN` | modal | 4.4 | Combined offset register; OFFSET:LEN the tool length offset register (G43 H), OFFSET:RAD the tool radius offset register (D); 0 cancels. |
| 460 | `OFFSET:RAD` | integer |  |  | `RAD` | modal | 4.4 | Combined offset register; OFFSET:LEN the tool length offset register (G43 H), OFFSET:RAD the tool radius offset register (D); 0 cancels. |
| 470 | `COMP` | `LEFT`, `RIGHT`, `OFF` |  |  |  | modal | 4.4 | Cutter radius compensation (G41, G42, G40), from the motion of the same block on. |
| 480 | `SPINDLE` | `CW`, `CCW`, `OFF` |  |  | role, optional | modal | 4.5 | Spindle on clockwise, counterclockwise, off (M3, M4, M5); without an address the machine's default spindle. |
| 490 | `RPM` | number, expression |  |  | role, optional | modal | 4.5 | Spindle speed, per spindle role. |
| 500 | `CSS` | `ON`, `OFF` |  |  | role, optional | modal | 4.11 | Constant surface speed (G96, G97): the spindle follows VC while it is on. |
| 510 | `VC` | number, expression |  |  | role, optional | modal | 4.11 | Cutting speed for CSS in m/min or ft/min (the S of G96 S140). |
| 520 | `RPM_MAX` | number, expression |  |  | role, optional | modal | 4.11 | Speed limit under CSS (G50 S, LIMS). |
| 530 | `SPINDLE_MODE` | `SPINDLE`, `AXIS` |  |  | role, optional | modal | 4.5 | Work spindle as rotating spindle or as positioning C axis (M70, SPOS); in AXIS mode its axis is driven with C. |
| 540 | `ORIENT` | number, expression |  |  | role, optional | block | 4.5 | Oriented spindle stop in degrees (M19, SPOS=); the spindle is stopped afterwards. |
| 550 | `SPINDLE_SYNC` | list, `OFF` |  |  |  | modal | 4.5 | Synchronous spindles, a list of two spindle roles of which the second follows the first; OFF ends it (D56). |
| 560 | `PHASE` | number, expression |  |  |  | with partner | 4.5 | Angular offset in degrees of a phase-synchronous run; only with SPINDLE_SYNC. |
| 570 | `COOLANT` | `ON`, `OFF` |  |  | coolant channel, optional | modal | 4.6 | Default coolant channel (M8, M9) or a named channel of the machine configuration, modal per channel. |
| 580 | `FUNC` | identifier |  |  | function name, required | block | 4.6 | Named machine function of the machine configuration with its state as the value, the normal way to write machine functions (D9). |
| 590 | `MFUNC` | integer |  |  |  | block | 4.6 | Raw M function by number, for functions the machine configuration does not name; the compiler warns. |
| 600 | `UNITS` | `MM`, `INCH` |  |  |  | modal | 4.1 | Measurement units, required before the first motion. |
| 610 | `WORKPLANE` | `XY`, `ZX`, `YZ` |  |  |  | modal | 4.2 | Working plane with the tool axis perpendicular to it (G17, G18, G19). |
| 620 | `ORIGIN` | integer |  |  |  | modal | 4.2 | Workpiece datum number, G54 to G59 as 1 to 6; starts an empty frame chain (D31). |
| 630 | `DIAMETER` | `ON`, `OFF` |  |  |  | modal | 4.2 | Lathe diameter programming, the X words are diameters; default OFF. Listed among the lathe words of 4.11 as well (D28, D60, D90). |
| 640 | `WORKPIECE` | identifier |  |  |  | modal | 4.10 | From here on the program machines the part held by this holder role; ORIGIN, SHIFT and A B C refer to it (D57). |
| 650 | `FRAME` | `MACHINE` |  |  |  | block | 4.2 | The coordinates of this block refer to the machine datum (G53, M91). |
| 660 | `SHIFT=RESET` | `RESET` |  |  |  | modal | 4.2 | Removes the entry of `SHIFT` and what follows it from the frame chain; a frame word, not the verb. |
| 670 | `ROTATE` | number, expression, `RESET` |  |  |  | modal | 4.2 | Rotation of the working plane about the tool axis in degrees, appended to the chain; ROTATE=RESET removes it and what follows. |
| 680 | `MIRROR` | list, identifier |  |  |  | modal | 4.2 | Mirrors the named axes, an axis list or a single axis, appended to the chain; MIRROR=OFF. |
| 690 | `TILT=RESET` | `RESET` |  |  |  | modal | 4.2 | Removes the entry of `TILT` and what follows it from the frame chain; a frame word, not the verb. |
| 700 | `TILT_AXIS=RESET` | `RESET` |  |  |  | modal | 4.2 | Removes the entry of `TILT_AXIS` and what follows it from the frame chain; a frame word, not the verb. |
| 710 | `MOVE` | `TURN`, `MOVE`, `STAY` |  |  |  | block | 4.2 | How the machine reaches the tilted plane, with TILT or TILT_AXIS; default STAY (D82). |
| 720 | `ROT` | `TABLE`, `COORD` |  |  |  | block | 4.2 | On table kinematics, whether the table turns or only the coordinate system, with TILT or TILT_AXIS; default TABLE (D82). |
| 730 | `POINT` | integer |  |  |  | block | 4.3 | The reference point HOME moves to, POINT=2 for the second (G30 P2); only with HOME. |
| 740 | `CYLINDER` | number, expression, `OFF` |  |  |  | modal | 4.2 | Cylinder surface transformation, on with the reference radius as the value, off with OFF; there is no ON form (D54, D96, D102). |
| 750 | `POLAR` | `ON`, `OFF` |  |  |  | modal | 4.2 | Face transformation, Cartesian programming of the face with X and C (G12.1, TRANSMIT; D54, D102). |
| 760 | `TCPM` | `ON`, `OFF` |  |  |  | modal | 4.2 | Tool center point control for 5-axis simultaneous motion (G43.4, TRAORI, M128; D54). |
| 770 | `ROTARY_PATH` | `SHORTEST`, `FULL` |  |  |  | modal | 4.2 | Rotary axes take the shortest way (M126) or turn exactly as programmed (M127); default FULL (D86). |
| 780 | `ROTARY_FEED` | `MM_MIN`, `DEG_MIN` |  |  |  | modal | 4.2 | Feed of rotary axes in length per minute at the tool tip (M116) or in degrees per minute (M117); default DEG_MIN (D86). |
| 790 | `TOLERANCE` | number, expression, `OFF` |  |  | tolerance kind, optional | modal | 4.1 | Path tolerance in the active units, OFF for the control's default; TOLERANCE:ROTARY is the orientation tolerance of the rotary axes in degrees (D85). |
| 800 | `TOLERANCE:ROTARY` | number, expression |  |  | `ROTARY` | modal | 4.1 | Path tolerance in the active units, OFF for the control's default; TOLERANCE:ROTARY is the orientation tolerance of the rotary axes in degrees (D85). |
| 810 | `TOLERANCE_MODE` | `FINISH`, `ROUGH` |  |  |  | modal | 4.1 | What the control optimizes for under the tolerance, accuracy or speed; default FINISH (D85). |
| 820 | `CYCLE` | identifier; with an address: integer |  |  | controller, optional | modal | 4.7 | Defines the active cycle by name (DRILL, a catalog name, OFF), or natively with a controller address and the cycle number (CYCLE:HEIDENHAIN=251, D94). |
| 830 | `AXIS` | identifier |  |  |  | with partner | 4.7 | Drilling axis of the cycle, an axis name; default the tool axis of the active WORKPLANE (D59). |
| 840 | `SURFACE` | number, expression |  |  |  | with partner | 4.7 | Absolute coordinate of the workpiece surface along the drilling axis (Q203). |
| 850 | `CLEARANCE` | number, expression |  |  |  | with partner | 4.7 | Absolute coordinate of the clearance plane along the drilling axis (Fanuc R, Q203 + Q200). |
| 860 | `DEPTH` | number, expression |  |  |  | with partner | 4.7 | Absolute coordinate of the hole bottom along the drilling axis (Q203 + Q201). |
| 870 | `SAFE` | number, expression |  |  |  | with partner | 4.7 | Absolute coordinate of the safe plane along the drilling axis (G98 initial level); optional. |
| 880 | `CYCLE_RETRACT` | `CLEARANCE`, `SAFE` |  |  |  | with partner | 4.7 | Where the tool ends after each hole (G99, G98); default CLEARANCE (D83, D87). |
| 890 | `PECK` | number, expression |  |  |  | with partner | 4.7 | Peck depth (Q, Q202). |
| 900 | `CYCLE_F` | number, expression |  |  |  | with partner | 4.7 | Plunge feed of the cycle in the active feed mode, its own word so that the motion feed F is never touched (D29). |
| 910 | `CYCLE_DWELL` | number, expression |  |  |  | with partner | 4.7 | Dwell at the bottom in seconds (P, Q211; D29). |
| 920 | `PITCH` | number, expression |  |  |  | with partner | 4.7 | Thread pitch for TAP (Q239). |
| 930 | `CONTOUR` | integer, string, identifier |  |  |  | with partner | 4.7 | Contour reference of a multiple repetitive turning cycle, the NAME of a SUB section of the file (D65, D90). |
| 940 | native cycle parameters (D94) | number, expression |  |  |  | with partner | 4.7.1 | Every key the catalog does not know in a block that carries `CYCLE:<controller>=n`, in source order. |
| 950 | `SYNC` | integer |  |  |  | block | 4.8 | Rendezvous mark: the channel waits until every participating channel has reached it (M100 to M199, WAITM). |
| 960 | `WITH` | integer, list |  |  |  | block | 4.8 | Participating channels of the SYNC in the same block; default all channels of the job. |
| 970 | `START_CHANNEL` | integer |  |  |  | block | 4.8 | Starts the program of that channel (Siemens START); an optional NAME selects the program. |
| 980 | `WAIT_CHANNEL` | integer |  |  |  | block | 4.8 | Waits until that channel has finished (Siemens WAITE). |
| 990 | `VAR` | number, expression, string |  |  | variable name, required |  | 4.9 | Assigns a variable, creating it if needed; the address is the variable name. |
| 1000 | `LABEL` | integer, identifier |  |  |  |  | 4.9 | Jump target and repeat start, unique per program or subprogram; END is reserved (D88). |
| 1010 | `JUMP` | integer, identifier |  |  |  | block | 4.9 | Continues at the label, or with JUMP=END at the PROGRAM=END of the current program; conditional with IF (D88). |
| 1020 | `CALL` | integer, string, identifier |  |  |  | block | 4.9 | Calls a subprogram of the file by its NAME, or an external program by file name as a string. |
| 1030 | `ARG` | number, expression |  |  | argument name, required | block | 4.9 | Argument of the CALL in the same block (G65 P9010 A1); the callee sees it as a local variable. |
| 1040 | `TIMES` | integer, expression |  |  |  | block | 4.9 | Repeat count of the CALL or REPEAT in the same block. |
| 1050 | `REPEAT` | integer, identifier |  |  |  | block | 4.9 | Repeats the blocks from the label to this block TIMES more times. |
| 1060 | `RETURN` | none |  |  |  | block | 4.9 | Returns to the caller before SUB=END is reached; in a program a WARNING, treated as JUMP=END. |
| 1070 | `IF` | expression |  |  |  | block | 4.9 | Condition of the JUMP or CALL in the same block, which executes when the expression is not 0. |
| 1080 | `STOP` | `PROGRAM`, `OPTIONAL` |  |  |  | block | 4.1 | Program stop M0, STOP=PROGRAM, or optional stop M1, STOP=OPTIONAL. |
| 1090 | `DWELL` | number, expression |  |  |  | block | 4.1 | Dwell in seconds (G4). |
| 1100 | `RAW` | string |  |  | controller, required | block | 4.1 | Source text NCX could not express, kept verbatim; the address is the controller or builder dialect, and the text compiles to that one only (D5). |
| 1110 | `COMMENT` | string |  |  |  | block | 4.1 | Program comment, kept in the output. |
| 1120 | `SECTION` | string |  |  |  | block | 4.1 | Structuring comment (Heidenhain * -), a plain comment on other controllers. |
| 1130 | `@SAVE` (internal) | state key |  |  |  | block | 4.15 | Pushes the value of the state variable its state key names on the restore stack; generated blocks only (virtual machine 3.10, D95). |
| 1140 | `@RESTORE` (internal) | state key |  |  |  | block | 4.15 | Pops that value and applies it again as if the program had written the word; generated blocks only (virtual machine 3.10, D95). |
