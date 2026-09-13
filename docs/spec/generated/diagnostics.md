# Diagnostics

Generated from the table of the validation in `src/Ncx.Core/VirtualMachine/Validation/` by the test
`DiagnosticTableTests` (P1-04); change the table and run the tests instead of editing this file. It lists every
code of `Ncx.Core`: the `PAR` codes of the lexer, the parser and the word catalog, and the `VM` codes of the
virtual machine, family by family in the order of the validation list of `../ncx-virtual-machine.md` section 5.
An ERROR stops the run, a WARNING is reported and the run continues (D98); a diagnostic reads
`file(line): ERROR VM042: message`. A rule of section 5 that another stage raises stands in its family with that
stage named, and without a code where the code belongs to that stage's own project. The codes of the other
projects (`CFG`, `RDR`, `CMP`, `ANA`, `PLG`, `CLI`) are not listed here.

Suppressed inside a subprogram that no program of the file calls, because the state they check belongs to a caller that does not exist (virtual machine 3.9, D99): `VM040`, `VM041`, `VM042`, `VM043`, `VM044`, `VM200`, `VM201`, `VM202`, `VM227`, `VM250`, `VM251`, `VM480`, `VM500`.

## Structure

The file frame, the programs and subprograms, and the words of a block (language 3, 4.1, 4.13, 5; VM 3 step 1, 5). The parser reports them before the virtual machine runs.

| Code | Severity | Rule | Section |
|---|---|---|---|
| `PAR001` | WARNING | A line ends with LF where the first line break of the file is CRLF, or the other way round. | language 3 |
| `PAR002` | ERROR | A word that is not KEY, KEY=VALUE or KEY:ADDR=VALUE. | language 3 |
| `PAR003` | ERROR | A value that is none of the value types: wrong value type. | language 3; VM 3 step 1 |
| `PAR004` | ERROR | A string without its closing quote. | language 3 |
| `PAR005` | ERROR | A backslash in a string that escapes neither a quote nor a backslash. | language 3 |
| `PAR006` | ERROR | An expression without its closing brace. | language 3 |
| `PAR007` | ERROR | A pseudo-word (@SAVE, @RESTORE) in a user file. | language 3; VM 3.10, 5; D95 |
| `PAR008` | ERROR | A number too large to be kept as a number. | language 3 |
| `PAR010` | ERROR | Unknown key: neither a catalog word, nor a machine axis word, nor a native parameter of a CYCLE:controller=n block. | VM 3 step 1, 5; D93, D94 |
| `PAR011` | ERROR | Two verbs in one block. | language 5 rule 1; VM 5 |
| `PAR012` | ERROR | Axis word without verb, or under a verb that does not carry it: RETRACT with other axis words. | language 5 rule 2; VM 3.1a, 5 |
| `PAR013` | ERROR | A word in a HOME block that is not a bare axis name. | language 4.3, 5 rule 2 |
| `PAR014` | ERROR | An axis word without a value outside a HOME block. | language 4.3, 5 rule 2; D93 |
| `PAR015` | ERROR | Duplicate key: a key twice in one block with one address. | language 5 rule 4; VM 5 |
| `PAR016` | ERROR | IF, ARG, TIMES, WITH, FRAME, MOVE, ROT, POINT or PHASE without its verb or partner word in the block. | language 5 rule 5; VM 5 |
| `PAR020` | ERROR | Missing or misplaced FILE=BEGIN: it is the first block of the file and stands nowhere else. | language 4.1; VM 5; D92 |
| `PAR021` | ERROR | FILE=BEGIN without NCX, the format version. | language 4.1; VM 5 |
| `PAR022` | ERROR | NCX names a format version other than 1. | language 4.1 |
| `PAR023` | ERROR | Misplaced NCX, in a block other than FILE=BEGIN. | language 4.1; VM 5 |
| `PAR024` | ERROR | Missing or misplaced FILE=END: it is the last block of the file, and no block follows it. | language 4.1; VM 5; D92 |
| `PAR025` | ERROR | A block of the file or section frame with a word its grammar does not give it (FILE=END UNITS=MM). | language 3 EBNF, 4.1 |
| `PAR026` | ERROR | A block outside every section. | language 4.13; VM 3.6, 5 |
| `PAR027` | ERROR | A SUB inside a PROGRAM. | language 4.9, 4.13; VM 3.6, 5 |
| `PAR028` | ERROR | A file without a program. | language 4.1, 4.13; VM 5 |
| `PAR029` | ERROR | Duplicate SUB: two sections of the file with one name. | VM 3.6, 5 |
| `PAR030` | ERROR | Missing PROGRAM=END: a program without it, or a PROGRAM=BEGIN before it. | language 4.13; VM 5 |
| `PAR031` | ERROR | Misplaced PROGRAM=END, outside a program. | language 4.13; VM 5 |
| `PAR032` | ERROR | Missing SUB=END: a subprogram without it, or another section before it. | language 4.13; VM 5 |
| `PAR033` | ERROR | Misplaced SUB=END, outside a subprogram. | language 4.13; VM 5 |
| `PAR034` | ERROR | SUB=BEGIN without NAME. | language 4.9, 4.13 |
| `PAR150` | ERROR | A word written with an address that it does not take (LINE:X). | language 3, 4 |
| `PAR151` | ERROR | A word that takes an address written without one (CENTER=5). | language 4 |
| `PAR152` | ERROR | An address outside the fixed set of its word (OFFSET:LENGTH). | language 4 |
| `PAR153` | ERROR | Unknown value: a value its word does not accept (SPINDLE=UP). | language 3, 4; VM 5; D96 |
| `PAR154` | ERROR | SKIP=n with a switch outside 1 to 9. | language 4.1 |
| `VM400` | WARNING | RAW present: source text kept verbatim, which compiles only to its own controller or builder. | language 4.1; VM 5 |
| (none) | ERROR | RAW, or a CYCLE:controller=n block with its native parameters, compiled for another controller family. Raised by the compiler, at compile time. | language 4.1, 4.7.1; VM 5; D5, D94 |

## Frame

SETPOS, the path tolerance and the tilted plane (language 4.1, 4.2; VM 2.1, 3.4, 5). MOVE and ROT without TILT or TILT_AXIS are PAR016 of the structure.

| Code | Severity | Rule | Section |
|---|---|---|---|
| `VM050` | ERROR | SETPOS with an axis unknown in every frame, except directly after a HOME of that axis that found no reference point. | VM 3.4, 5; D100, D101 |
| `VM420` | ERROR | TOLERANCE:ROTARY or TOLERANCE_MODE without an active TOLERANCE. | language 4.1; VM 5; D85 |
| `VM421` | ERROR | SETPOS without an axis word. | language 4.2; VM 5 |
| `VM422` | WARNING | ROT on a target without table kinematics: the machine file has no rotary axis on a table. Not checked without a machine file. | language 4.2; VM 5; D82 |

## Motion

Linear motion and the machine limits of a motion (language 4.3; VM 3.1, 5; machine-config 4; D64, D100).

| Code | Severity | Rule | Section |
|---|---|---|---|
| `VM200` | ERROR | Motion before UNITS. Suppressed inside a subprogram that no program of the file calls (D99). | language 4.1; VM 3.1, 5 |
| `VM201` | ERROR | LINE without feed. Suppressed inside a subprogram that no program of the file calls (D99). | VM 3.1, 5 |
| `VM202` | ERROR | IX from an unknown position. In a subprogram nothing calls the position becomes unknown instead. Suppressed inside a subprogram that no program of the file calls (D99). | VM 3.1, 5; D99 |
| `VM203` | ERROR | Two forms of one axis in one block (X=10 IX=5). | language 4.3 |
| `VM440` | WARNING | F in a RAPID block. | VM 5 |
| `VM441` | WARNING | F above an axis max_feed. | VM 5; machine-config 4; D64 |
| `VM442` | WARNING | A target beyond the axis limits, compared in the MACHINE frame and not checked while the machine position is unknown. | VM 5; machine-config 4; D64, D100 |

## Arc

The three forms of an arc in its working plane (language 4.3; VM 3.2, 5; D36, D84, D102).

| Code | Severity | Rule | Section |
|---|---|---|---|
| `VM220` | ERROR | ARC without CENTER, R or ANGLE. | language 4.3; VM 3.2, 5 |
| `VM221` | ERROR | CENTER on one plane axis only. | VM 3.2 |
| `VM222` | ERROR | CENTER on an axis outside the working plane. | VM 3.2 |
| `VM223` | ERROR | ARC with both CENTER and R. | language 4.3 |
| `VM224` | ERROR | ANGLE with R. | language 4.3; VM 3.2, 5; D84 |
| `VM225` | ERROR | ANGLE with plane end-point words. | language 4.3; VM 3.2, 5; D84 |
| `VM226` | ERROR | ANGLE without CENTER. | language 4.3; VM 3.2; D84 |
| `VM227` | ERROR | ARC from a start that is not known in the working plane. Suppressed inside a subprogram that no program of the file calls (D99). | VM 3.2; D102 |
| `VM230` | ERROR | Inconsistent center: the start and the end lie at radii from CENTER that differ by more than the arc tolerance. | VM 3.2, 5; D36 |
| `VM231` | ERROR | Radius too small: the end lies farther from the start than twice R plus the arc tolerance. | VM 3.2, 5; D36 |
| `VM232` | ERROR | Full circle with R. | language 4.3; VM 3.2, 5 |
| `VM233` | ERROR | R=0. | language 4.3 |
| `VM234` | ERROR | ANGLE not greater than 0. | language 4.3; VM 5; D84 |
| `VM460` | ERROR | COMP change in an ARC block. | VM 5 |

## Vector

The tool vector and the surface normal under TCPM (language 4.3; VM 3.1, 5; D81).

| Code | Severity | Rule | Section |
|---|---|---|---|
| `VM210` | ERROR | Vector words without TCPM=ON. | language 4.3; VM 3.1, 5; D81 |
| `VM211` | ERROR | Vector words mixed with rotary words. | language 4.3; VM 3.1, 5; D81 |
| `VM212` | ERROR | Incomplete vector words: TX TY TZ all three, NX NY NZ all three and only with TX TY TZ. | language 4.3; VM 5; D81 |
| `VM213` | ERROR | Vector words not of unit length within the arc tolerance. | VM 3.1, 5; D36, D81 |

## Retract and home

RETRACT along the tool axis and HOME to the reference point (language 4.3; VM 3 step 5, 3.1a, 5; D83, D100). RETRACT with other axis words is PAR012 of the structure.

| Code | Severity | Rule | Section |
|---|---|---|---|
| `VM060` | WARNING | HOME on an axis without a reference point in the configuration, once per run and axis; the axis is unknown in every frame afterwards. | VM 3 step 5, 5; D100 |
| `VM061` | ERROR | HOME without an axis name. | VM 5 |
| `VM240` | ERROR | RETRACT with feed. | VM 3.1a, 5; D83 |

## Tool

The tool change of the two-word model and the offsets (language 4.4; VM 2.3, 3.5, 5; D42).

| Code | Severity | Rule | Section |
|---|---|---|---|
| `VM040` | WARNING | Preload of the tool already in the spindle, a no-op on the machine. Suppressed inside a subprogram that no program of the file calls (D99). | VM 3.5, 5 |
| `VM041` | ERROR | TOOL without preload. Suppressed inside a subprogram that no program of the file calls (D99). | language 4.4; VM 3.5, 5 |
| `VM042` | WARNING | A different tool preloaded than called; the magazine has to cycle twice. Suppressed inside a subprogram that no program of the file calls (D99). | VM 3.5, 5; D42 |
| `VM043` | WARNING | TOOL while a cycle is active; the change ends the cycle. Suppressed inside a subprogram that no program of the file calls (D99). | VM 3.5, 4, 5 |
| `VM044` | WARNING | TOOL while compensation is on. Suppressed inside a subprogram that no program of the file calls (D99). | VM 3.5, 5 |
| `VM480` | ERROR | OFFSET mixed with OFFSET:LEN or OFFSET:RAD in one program. Suppressed inside a subprogram that no program of the file calls (D99). | language 4.4; VM 5 |

## Spindle

The spindles, their mode and synchronization, and their speed limits (language 4.5, 4.11; VM 2.4, 3.8 rules 4 and 5, 5; machine-config 5; D64).

| Code | Severity | Rule | Section |
|---|---|---|---|
| `VM010` | ERROR | SPINDLE on a spindle in AXIS mode. | VM 3.8 rule 4, 5 |
| `VM011` | ERROR | C= on a spindle in SPINDLE mode. | VM 3.8 rule 4, 5 |
| `VM012` | ERROR | SPINDLE_SYNC with a spindle that is not in SPINDLE mode. | VM 3.8 rule 5 |
| `VM013` | WARNING | RPM or SPINDLE of the following spindle while it runs synchronized. | VM 3.8 rule 5 |
| `VM014` | ERROR | SPINDLE_SYNC with more or fewer than two spindle roles. | language 4.5; VM 3.8 rule 5 |
| `VM500` | WARNING | Spindle OFF before a LINE: the spindle of the current tool holder, the default spindle when the holder has none. Suppressed inside a subprogram that no program of the file calls (D99). | VM 2.3, 5 |
| `VM501` | WARNING | RPM above the spindle's rpm_max. | VM 5; machine-config 5; D64 |
| `VM502` | WARNING | RPM below the spindle's rpm_min. | VM 5; machine-config 5; D64 |

## Cycle

The cycle call (language 4.7; VM 3.3, 5; D59, D94).

| Code | Severity | Rule | Section |
|---|---|---|---|
| `VM250` | ERROR | CYCLE_CALL without cycle. Suppressed inside a subprogram that no program of the file calls (D99). | VM 3.3, 5 |
| `VM251` | ERROR | CYCLE_CALL of a built-in drilling cycle without DEPTH or CLEARANCE; a catalog or CYCLE:controller=n cycle carries its own parameters. Suppressed inside a subprogram that no program of the file calls (D99). | VM 3.3, 5; D94 |
| `VM252` | ERROR | AXIS naming an axis that is not a linear axis of the machine. | language 4.7; VM 3.3, 5; D59 |

## Flow

Labels, jumps, calls and the reach of the blocks of a program (language 4.9, 4.13; VM 2.7, 3.6, 3.9, 5; D89, D99).

| Code | Severity | Rule | Section |
|---|---|---|---|
| `PAR035` | ERROR | LABEL=END; END is reserved for JUMP=END. | language 4.9; VM 2.7, 5 |
| `VM070` | ERROR | Call depth exceeded; the subprogram is not entered. | VM 3.6, 3.9, 5; D99 |
| `VM071` | ERROR | CALL of a program. | language 4.13; VM 3.6, 5 |
| `VM072` | ERROR | Missing call target. | VM 3.6, 5 |
| `VM520` | ERROR | Duplicate LABEL in one program or subprogram. | language 4.9; VM 3.6, 5 |
| `VM521` | ERROR | Missing jump target: a JUMP or REPEAT to a label its program or subprogram does not hold. | language 4.9; VM 3.6, 5 |
| `VM522` | WARNING | JUMP=END from inside a subprogram: it ends the program from a call. | VM 3.6, 5 |
| `VM523` | WARNING | Unreachable block of a program after an unconditional JUMP that no LABEL makes reachable. | language 4.13; VM 3.9, 5; D89 |
| `VM524` | WARNING | RETURN in the main program; it is treated as JUMP=END. | language 4.9, 4.13; VM 3.6, 5 |
| (none) | ERROR | Block cap exceeded: possible endless loop. Raised by INTERPRETED mode. | VM 3.6, 5 |

## Expression

Expressions and variables (language 4.9, 4.12; VM 1, 2.7, 3.6, 5; D38, D51). The ERRORs of evaluation come in INTERPRETED mode, which evaluates.

| Code | Severity | Rule | Section |
|---|---|---|---|
| `PAR100` | ERROR | A character that begins no part of an expression. | language 4.12 |
| `PAR101` | ERROR | A number without a digit before or after its decimal point. | language 2 rule 5, 3 |
| `PAR102` | ERROR | A $ without a variable name. | language 4.9, 4.12 |
| `PAR103` | ERROR | Nothing between the braces. | language 3 |
| `PAR104` | ERROR | An operator without its operand. | language 4.12 |
| `PAR105` | ERROR | A parenthesis opened and not closed, or closed and not opened. | language 4.12 |
| `PAR106` | ERROR | A bracket of an index opened and not closed, or closed and not opened. | language 4.12 |
| `PAR107` | ERROR | A call of a name that is none of the functions. | language 4.12 |
| `PAR108` | ERROR | A name without $ and without parentheses. | language 4.9, 4.12 |
| `PAR109` | ERROR | A part after a complete expression. | language 4.12 |
| `PAR110` | ERROR | A second comparison behind a comparison. | language 4.12 |
| `VM080` | ERROR | Assignment to a SYS_ variable. | VM 2.7, 5 |
| `VM540` | WARNING | Unresolved expression in STATIC mode, counted and reported once per run. | VM 1, 5 |
| `VM900` | ERROR | Division by zero. | language 4.12; VM 5 |
| `VM901` | ERROR | Unassigned variable, unless the configuration sets unassigned = 0. | VM 3.6, 5; D38 |
| `VM902` | ERROR | String where a number is required. | VM 5 |
| `VM903` | ERROR | A SYS_ name the configuration does not map, or an unknown state, read in INTERPRETED mode. | VM 2.7, 3.6; D51 |
| `VM904` | ERROR | A function with a number of arguments it does not take. | language 4.12 |
| `VM905` | ERROR | An operation without a real result. | language 4.12 |
| `VM906` | ERROR | A number beyond the range of the decimal arithmetic. | language 4.12 |
| `VM907` | ERROR | An index on a variable that is not a SYS_ name. | language 4.12; D51 |
| `VM908` | ERROR | An index that selects no register. | language 4.12; D51 |

## Resource

Roles, functions, coolant channels and machine axes, with a machine file and without one (language 4.6, 4.10; VM 3.8, 5; D93, D103).

| Code | Severity | Rule | Section |
|---|---|---|---|
| `VM001` | ERROR | Unknown role, when a machine file is given. | language 4.10; VM 3.8 rule 1, 5 |
| `VM002` | ERROR | A role that names a resource of the wrong type for its word. | VM 3.8 rule 1 |
| `VM003` | WARNING | A role, function, machine axis or coolant channel the built-in default machine lacks when no machine file is given: "not checked: no machine file", once per name. | VM 3.8, 5; D103 |
| `VM004` | ERROR | A word without a role address on a machine without a default resource of its kind. | VM 3.8 rule 2 |
| `VM005` | ERROR | A machine axis word not declared in [[axis]], when a machine file is given. | VM 3.8 rule 3, 5; D93 |
| `VM006` | ERROR | Unknown function, when a machine file is given. | language 4.6; VM 3.8, 5 |
| `VM007` | ERROR | Unknown value: FUNC with a state its function does not list. | machine-config 5; VM 5 |
| `VM008` | ERROR | COOLANT with a channel [coolant] does not name, when a machine file is given. | language 4.6; machine-config 5 |
| `VM550` | WARNING | MFUNC with a number that the configuration names. | language 4.6; VM 5; D105 |
| `VM551` | WARNING | WORKPIECE change while the old holder's spindle runs. | language 4.10; VM 5 |

## Channel

Channels and their synchronization in a job (language 4.8, 4.14; VM 3.7, 5).

| Code | Severity | Rule | Section |
|---|---|---|---|
| `VM570` | WARNING | SYNC in a single-channel job; the channel does not wait. | VM 3.7, 5 |
| `VM571` | ERROR | Deadlock at SYNC: every channel waits and no mark can be released; the ERROR names the marks. Raised by the job scheduler. | VM 3.7, 5 |
| `VM572` | WARNING | Two channels on one spindle between marks. Raised by the job scheduler. | VM 3.7, 5 |
