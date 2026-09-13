# NCX Language Specification

Status: Version 1.0 of the specification set, 2026-09-12 (draft 8 in the drafting history of `../decisions/decisions.md`). The language: purpose, design rules, lexical rules and grammar, the word catalog, block rules and canonical order, examples.

## 1. What NCX is for

NCX is a controller-independent NC program format. It exists for three uses:

1. **Conversion.** A program written for one controller is read into NCX and compiled for another: Fanuc G-code in, Heidenhain Klartext out, or any other pair. This is where NCX comes from.
2. **Authoring.** An advanced user writes NCX by hand and compiles it for whichever machine the part runs on.
3. **One postprocessor for all machines.** A CAM postprocessor emits NCX once; the NCX compiler produces the program for each machine from its machine configuration. The user maintains one postprocessor instead of one per machine.

All three uses share one requirement: an NCX file must mean exactly the same thing on every machine, and every machine specific detail must live in the machine configuration (TOML, see `machine-config.md`), never in the program.

## 2. Design rules

1. **One meaning.** Every word has one definition, independent of the controller the program came from or goes to. If two controllers disagree (arc center absolute or incremental, cycle depth from the surface or absolute), NCX picks one meaning and the reader and compiler translate.
2. **Explicit motion, modal state.** Every block that moves says how it moves (`RAPID`, `LINE`, `ARC`, `HOME`, `CYCLE_CALL`). State such as feed, tool, compensation, units and the active cycle is modal, as on every controller, and is resolved by the virtual machine (`ncx-virtual-machine.md`).
3. **Absolute by default, incremental by word.** `X=10` is absolute in the active workpiece frame. `IX=5` is incremental from the current position. There is no modal incremental switch.
4. **Explicit where controllers are implicit.** Heidenhain loads the tool offsets with the tool call, Fanuc needs `G43 H`; NCX always states the offsets. A reader fills in what the source controller implies.
5. **Numbers untouched.** NCX never rounds or formats a number. Decimal point, no thousands separators, no trailing dot. The compiler applies the number format of the target machine.
6. **Readable and typeable.** Bare words, one block per line, no brackets, short names that a CNC programmer recognizes.
7. **Canonical form.** `ncx format` and every writer emit blocks in the canonical word order (section 5), so that two programs that mean the same thing are the same text and `git diff` on regenerated CAM output shows real changes only.
8. **Lossless where it matters.** Comments are kept. Source that NCX cannot express is kept as `RAW` with a warning, so nothing disappears silently (decision D5).

## 3. Lexical rules

| Rule | Definition |
|---|---|
| Encoding | UTF-8, LF or CRLF line endings |
| Block | One line is one block. Blank lines, whitespace-only lines and comment-only lines are not blocks but trivia: they are kept in place and written back by every writer (D92). |
| Word | `KEY`, `KEY=VALUE` or `KEY:ADDR=VALUE`. Words are separated by whitespace. |
| Comment | `;` outside a string starts a comment to the end of the line. The parser keeps it, as trivia when the line holds nothing else or as the trailing comment of its block; it carries no meaning (program comments use the `COMMENT` word). D92. |
| KEY | `[A-Z][A-Z0-9_]*`. A key of the form `[XYZABCUVW][0-9]{0,2}`, or `I` followed by that, that is not a catalog word is a machine axis word in a block whose verb takes axis words (rule 2 of section 5; bare in a `HOME` block, otherwise with a number or expression value; 4.3, D93). In a block that carries `CYCLE:<controller>=n`, every key the catalog does not know, one of the machine-axis form included, is a native parameter with a number or expression value (4.7.1, D94). A key starting with `@` is a pseudo-word of a generated block (`@SAVE`, `@RESTORE`, 4.15) with a state key `KEY[:ADDR]` as its value, accepted only under the parser option the expander uses for generated text; in a user file it is the ERROR "pseudo-word in a user file", recognized by the lexer, not rejected as an unknown key (see below, D95). |
| ADDR | `[A-Z][A-Z0-9_]*`: an axis name, an offset kind, a coolant channel, a resource role, a variable name, or a controller family (`CYCLE:HEIDENHAIN`, `RAW:FANUC`; 4.7.1, D94) |
| VALUE | number, identifier, list, string or expression; a state key `KEY[:ADDR]` on the pseudo-words only (D95) |
| Case | Keys, addresses and identifiers are uppercase. Parsers may accept lowercase and normalize. |

Value types:

| Type | Syntax | Examples |
|---|---|---|
| integer | `-?[0-9]+` | `1`, `-5`, `1592` |
| decimal | `-?[0-9]+\.[0-9]+` | `10.5`, `-7.025` |
| number | integer or decimal; where the catalog says "number" both are accepted | |
| identifier | `[A-Z][A-Z0-9_]*` | `CW`, `PER_REV`, `LEFT` |
| list | identifiers or integers separated by `,` without spaces | `MAIN,SUB`, `1,2` |
| string | double-quoted, `\"` for a quote, `\\` for a backslash, may contain spaces and `;` | `"SIDE MILL D10"` |
| expression | `{` ... `}`, evaluated by the virtual machine, allowed wherever a number is allowed | `{$Q1 + 20}` |
| state key | `KEY[:ADDR]`; the value of the pseudo-words `@SAVE` and `@RESTORE` of generated blocks only, never of a user word (D95) | `SPINDLE:MAIN`, `COOLANT` |

Grammar (EBNF):

```
file        = { trivia } file_begin { program | sub | trivia } file_end { trivia } ;
file_begin  = "FILE=BEGIN" "NCX=1" [ comment ] EOL ;      (* the first block *)
file_end    = "FILE=END" [ comment ] EOL ;                (* the last block *)
program     = "PROGRAM=BEGIN" { header-word } [ comment ] EOL { block | trivia } "PROGRAM=END" [ comment ] EOL ;
sub         = "SUB=BEGIN" "NAME=" ( ident | integer | string ) [ comment ] EOL { block | trivia } "SUB=END" [ comment ] EOL ;
block       = word { word } [ comment ] EOL ;
trivia      = [ comment ] EOL ;                      (* not a block; may stand on any line, also before file_begin and after file_end, D92 *)
word        = key [ ":" addr ] [ "=" value ] ;
key         = upper { upper | digit | "_" } ;
addr        = upper { upper | digit | "_" } ;
value       = integer | decimal | list | string | expression ;   (* a state key KEY[:ADDR] only on @SAVE and @RESTORE, see below, D95 *)
list        = item { "," item } ;
item        = ident | integer ;
ident       = upper { upper | digit | "_" } ;
integer     = [ "-" ] digit { digit } ;
decimal     = [ "-" ] digit { digit } "." digit { digit } ;
string      = '"' { char | '\"' | '\\' } '"' ;
expression  = "{" expr "}" ;                      (* expr: section 4.12 *)
comment     = ";" { any-char-except-EOL } ;
```

Keys starting with `@` are the pseudo-words `@SAVE` and `@RESTORE` of generated blocks (4.15, virtual machine 3.10); their value is a state key `KEY[:ADDR]` that names a state variable of the channel. They are accepted only under the parser option the expander uses for generated text; in a user file a word starting with `@` is the ERROR "pseudo-word in a user file" (D95).

## 4. Word catalog

Words are grouped by area. "Modal" means the value stays until changed. "Block" means the word applies to its block only. Words marked **verb** make the block a motion or frame block; a block has at most one verb.

### 4.1 File and program

| Word | Value | Meaning | Scope |
|---|---|---|---|
| `FILE=BEGIN` | | First block of every file, together with `NCX=1`; comment-only and blank lines may stand before it (D92). A file holds one or more programs and any number of subprograms (4.13). | file |
| `NCX=1` | integer | Format version, in the `FILE=BEGIN` block. | file |
| `FILE=END` | | Last block of every file (Fanuc closing `%`; Heidenhain and Siemens: end of file). No block follows it; comment-only and blank lines may (D92). | file |
| `PROGRAM=BEGIN` | | Start of a program (Heidenhain `BEGIN PGM`, Fanuc `O0001`, Siemens `%_N_name_MPF`). `NAME`, `NUMBER` and `CHANNEL` may follow in the same block. The first program of a file is the one that runs unless the job manifest or the command line names another. | program |
| `NAME="..."` | string with `PROGRAM=BEGIN`; identifier, integer or string with `SUB=BEGIN` (D90) | Program name (Heidenhain `BEGIN PGM name`, Fanuc `O0001 (name)`, Siemens `%_N_name_MPF`) or the name of a subprogram section (4.9, 4.13); optional program selector on `START_CHANNEL` (4.8). | with `PROGRAM=BEGIN`, `SUB=BEGIN` or `START_CHANNEL` |
| `NUMBER=1` | integer | Program number (Fanuc `O0001`). | with `PROGRAM=BEGIN` |
| `PROGRAM=END` | | End of the program, executed: the control stops and rewinds here (Heidenhain `M30` then `END PGM`, Fanuc `M30` or `M2`, Siemens `M30`, `M2` or `M17`; the compiler writes what `program_end` in the machine configuration says, NCX does not distinguish them, D49). Exactly once per program, its last block. An early end is `JUMP=END` (4.13). | program |
| `CHANNEL=1` | integer | Channel this program runs on (4.14). Header word, follows `PROGRAM=BEGIN`. Default 1. | header |
| `UNITS=MM` | `MM`, `INCH` | Measurement units. Required before the first motion. | modal |
| `COMMENT="..."` | string | Program comment, kept in the output (`(...)`, `; ...`). | block |
| `SECTION="..."` | string | Structuring comment (Heidenhain `* -`); plain comment on other controllers. | block |
| `STOP=PROGRAM` | `PROGRAM`, `OPTIONAL` | Program stop M0, optional stop M1. | block |
| `DWELL=1.5` | number, seconds | Dwell (G4). | block |
| `RAW:FANUC="..."` | string; addr = controller or builder dialect id from the machine configuration (`RAW:NAKAMURA=`) | Source text NCX could not express, kept verbatim: builder macros such as Nakamura `G411` jump programming or `G300` cut-off check. Compiles only to the same controller or builder; any other target is an ERROR. | block |
| `SKIP` | none, or integer 1..9 | Optional block skip: the block is skipped when the control's block skip switch (number n) is on (`/` or `/n` in front of a Fanuc or Siemens block, `/` in Heidenhain). Whether the virtual machine executes or skips such blocks is a run option (D53). | block |
| `TOLERANCE=0.02` | number in the active units, or `OFF` | Path tolerance of the control for 3D and 5-axis programs: how far the control may deviate from the programmed path when it smooths block transitions (Heidenhain `CYCL DEF 32 TOLERANCE`, Siemens `CYCLE832` and `CTOL`, Fanuc `G5.1 Q1` with the tolerance parameter). `OFF` returns to the control's default. The machine configuration maps the word to the control's form (`[tolerance]`); options it cannot express stay `RAW`. D85. | modal |
| `TOLERANCE:ROTARY=0.05` | number, degrees | Orientation tolerance of the rotary axes under the same smoothing (cycle 32 `TA`, `CYCLE832` orientation tolerance, Siemens `OTOL`). Only with `TOLERANCE`. | modal |
| `TOLERANCE_MODE=FINISH` | `FINISH`, `ROUGH` | What the control optimizes for under the tolerance: accuracy or speed (cycle 32 `HSC-MODE`, `CYCLE832` technology 1 or 3). Default `FINISH`. | modal |

### 4.2 Frame

| Word | Value | Meaning | Scope |
|---|---|---|---|
| `WORKPLANE=XY` | `XY`, `ZX`, `YZ` | Working plane with the tool axis perpendicular to it (G17, G18, G19). `ZX` is the G18 orientation. | modal |
| `ORIGIN=1` | integer | Workpiece datum number (G54..G59 = 1..6, G54.1 P1 = 7 and up, Heidenhain cycle 247 Q339). | modal |
| `FRAME=MACHINE` | | The coordinates in this block refer to the machine datum (G53, M91). | block |
| `DIAMETER=ON` | `ON`, `OFF` | Lathe diameter programming: `X` values are diameters (the program says `X=20`, the axis stands at radius 10 from the part centre). Default `OFF`. The virtual machine stores radii. Affects exactly the absolute and incremental X words of the workpiece (`X`, `IX`, the absolute `CENTER:X`) and the X plane words of a cycle with `AXIS=X`; `CENTER:IX`, `R`, `PECK` and every other radial distance stay radius values, as on every controller. What a machine can write (diameters only on most Fanuc lathes, switchable on Siemens with `DIAMON`/`DIAMOF`, radius mode by M code on Mori Seiki) is a machine setting; the compiler converts from the radii of the VM, and the reader takes the source convention from the same setting or from the source's own switch words. D28, D60. | modal |
| `SHIFT X=60 Y=40 Z=-5` | **verb**, axis words | Datum shift in the frame that is active where the word stands (G52, cycle 7, TRANS/ATRANS). Appended to the transform chain (below); a shift after a `TILT` is a shift in the tilted system. Omitted axes are 0. | modal, part of the chain |
| `SHIFT=RESET` | | Remove the shifts from the chain, and everything appended after them. | |
| `ROTATE=30` | number, degrees | Rotation of the working plane about the tool axis around the current origin (G68, cycle 10, ROT/AROT), appended to the chain. `ROTATE=RESET` removes it and what follows. | modal, part of the chain |
| `MIRROR=X` | axis list or `OFF` | Mirror the named axes (G51.1, cycle 8, MIRROR), appended to the chain. | modal, part of the chain |
| `TILT A=0 B=45 C=0` | **verb**, spatial angles | Tilted working plane (PLANE SPATIAL, CYCLE800 with spatial angles, G68.2), appended to the chain. Angles are spatial angles A, B, C about the axes of the frame active where the word stands, in that order. `TILT=RESET` removes it and what follows. Whether the rotary axes move to reach the plane is said by `MOVE` in the same block. | modal, part of the chain |
| `TILT_AXIS A=0 B=45 C=0` | **verb**, rotary axis angles | Tilted working plane given as the rotary axis positions of this machine: the plane that results when the named rotary axes stand at these angles (Heidenhain `PLANE AXIAL` and cycle 19, Siemens `CYCLE800` in the rotary-axes mode, Fanuc `G68.1`/`G53.1` sequences). Machine specific by intent, like machine axis names: the same block means a different plane on a machine with another kinematics. Appended to the chain like `TILT`; `TILT_AXIS=RESET` removes it and what follows. Readers keep the form the source used; the compiler converts between `TILT` and `TILT_AXIS` only when the kinematics module is present, otherwise a form the target cannot write is an ERROR. D82. | modal, part of the chain |
| `MOVE=TURN` | `TURN`, `MOVE`, `STAY`; with `TILT` or `TILT_AXIS` | How the machine reaches the plane: `TURN` positions the rotary axes (tool retracted first, Heidenhain `TURN`, Siemens `CYCLE800` direction -1 or +1, Fanuc `G53.1` after `G68.2`), `MOVE` positions them while the tool tip stays on the workpiece (Heidenhain `MOVE`, Siemens tool-tip tracking), `STAY` only rotates the coordinate system and leaves the axes where they are (Heidenhain `STAY`, Siemens direction 0, Fanuc `G68.2` alone). Default `STAY`. D82. | block |
| `ROT=TABLE` | `TABLE`, `COORD`; with `TILT` or `TILT_AXIS` | On table kinematics: whether the table turns so that the workpiece faces the tool (`TABLE`, Heidenhain `TABLE ROT`) or only the coordinate system rotates (`COORD`, `COORD ROT`). Default `TABLE`. Kept as written; a target that has no such option ignores it with a WARNING. D82. | block |
| `SETPOS C=0` | **verb**, axis words | Declare that the current position has these coordinates in the active workpiece frame; nothing moves (Fanuc `G50 X Z` in system A, `G92 X Z` in B and C, `G50 C0` after a C-axis reference return before polar interpolation; Siemens `PRESETON`). The virtual machine shifts the frame of the named axes so that the position reads as declared. Older programs set their datum this way instead of with `ORIGIN`. The axis must be known in some frame: after `HOME` or a machine-frame move it is known in the MACHINE frame, which is enough; the virtual machine records the shift against the machine position and the axis becomes known in the workpiece frame with the declared value (D101). Directly after a `HOME` of that axis that found no reference point in the configuration, `SETPOS` is accepted as well: the axis becomes known in the workpiece frame with the declared value and its machine position stays unknown (D100, D101). D55. | block |
| `CYLINDER=30` | number, the reference radius, or `OFF` | Cylinder surface transformation for milling on the circumference with the C axis (Fanuc `G7.1`/`G107`, Siemens `TRACYL`, Heidenhain cycle 27 or `FUNCTION`). The value switches the transformation on with that reference radius (`CYLINDER=30` for a cylinder of radius 30), `CYLINDER=OFF` switches it off; there is no `ON` form. While on, `ARC` and `COMP` work in the cylinder plane of the cylinder axis (Z on a lathe) and C as a length on the circumference; from the first motion under it the position is known in that cylinder frame and unknown in the workpiece frame, and after `CYLINDER=OFF` it stays unknown until the next motion with known coordinates (virtual machine 3.1, 3.4). D54, D96, D102. | modal |
| `POLAR=ON` | `ON`, `OFF` | Face transformation, Cartesian programming of the face with X (a diameter under `DIAMETER=ON`) and C as a length in the active units (Fanuc `G12.1`/`G112`, Siemens `TRANSMIT`). While ON, `ARC` and `COMP` work in that face plane; from the first motion under it the position is known in that polar frame and unknown in the workpiece frame, and after `POLAR=OFF` X and C stay unknown in the workpiece frame until the next motion with known coordinates (virtual machine 3.1, 3.4). D54, D102. | modal |
| `TCPM=ON` | `ON`, `OFF` | Tool center point control for 5-axis simultaneous motion (Fanuc `G43.4`, Nakamura `G435`, Siemens `TRAORI`, Heidenhain `M128` / `FUNCTION TCPM`). Under `TCPM=ON` a `LINE` may carry a tool vector instead of rotary axis words (4.3, D81). D54. | modal |
| `ROTARY_PATH=SHORTEST` | `SHORTEST`, `FULL` | Rotary axes take the shortest way to their target (`SHORTEST`, Heidenhain `M126`) or turn exactly as programmed, also the long way round (`FULL`, `M127`). Default `FULL`. D86. | modal |
| `ROTARY_FEED=MM_MIN` | `MM_MIN`, `DEG_MIN` | Feed of rotary axes counted at the tool tip in length per minute (`MM_MIN`, Heidenhain `M116`, Siemens `FGREF`) or in degrees per minute (`DEG_MIN`, `M117`). Default `DEG_MIN`. D86. | modal |

Frame chain (D31, replaces D46): the workpiece frame is `ORIGIN` followed by the transform words in the order the program writes them. Every `SHIFT`, `ROTATE`, `MIRROR` and `TILT` is applied to the frame that is active where it stands and appended to the chain, so `ORIGIN=1`, `SHIFT Z=-5`, `TILT B=45` and `ORIGIN=1`, `TILT B=45`, `SHIFT Z=-5` are different programs, exactly as they are on every controller: a shift before a tilt moves the tilt's origin, a shift after a tilt moves along the tilted axes. The chain is unwound from the end only: a `RESET` of a kind removes that entry and everything after it; `ORIGIN` starts an empty chain. Readers translate the controllers' own habits into this one model: a Fanuc `G52` or a Heidenhain cycle 7 replaces the earlier shift of the same kind, so the reader emits the `RESET` before the new word; Siemens `ATRANS`, `AROT` and `AMIRROR` are appended as written, while `TRANS`, `ROT`, `MIRROR` and `SCALE` (with or without axis words) delete the whole programmable frame before they act (Sinumerik programming manual, frame instructions), so the reader cuts the chain at its first entry and then appends the new word. `TILT_AXIS` entries sit in the chain like `TILT` entries. Compilers write the chain in the same order on the target, which is what keeps the mathematics equal on both machines. `CYLINDER`, `POLAR` and `TCPM` are kinematic transformations, not frame transforms and not part of the chain: the virtual machine tracks that they are on (for `CYLINDER` the reference radius, D96) and, under `POLAR=ON` or `CYLINDER=n`, keeps the position in the face plane of the X word and the C word as a length (`POLAR`) or in the cylinder plane of the cylinder axis (Z on a lathe) and the C word as a length on the circumference (`CYLINDER`) and resolves `ARC` and `COMP` in that plane (`ncx-virtual-machine.md`, sections 2 and 3.1, D102); `TCPM` is state only, and the Cartesian workpiece geometry belongs to the kinematics module (D54).

### 4.3 Motion

| Word | Value | Meaning | Scope |
|---|---|---|---|
| `RAPID` | **verb** | Rapid positioning (G0, FMAX) to the target given by the axis words. | block |
| `LINE` | **verb** | Linear interpolation at the active feed (G1, L with F). | block |
| `ARC=CW` | **verb**, `CW`, `CCW` | Circular interpolation in the working plane to the target, with `CENTER` or `R`, or over a sweep `ANGLE` around `CENTER`. A tool-axis word in the same block makes a helix. | block |
| `ANGLE=737.956` | number, degrees, greater than 0 | Sweep angle of an `ARC` in the direction of its verb, instead of the plane end point: more than 360 degrees means more than one turn (TopSolid writes helices as `CP IPA+737.956 IZ-5.4 DR+`). Needs `CENTER`; plane end-point words and `R` are not allowed with it, a tool-axis word still gives the helix end. The compiler splits the arc into full turns and a rest for controllers that take one turn per block. D84. | block |
| `RETRACT` | **verb**, no value or a number | Retract along the tool axis, in the tilted system when a `TILT` is active: bare, to the axis limit (Heidenhain `M140 MB MAX`); with a value, by that distance (`RETRACT=50`, `M140 MB50`). Only the tool axis moves. The compiler writes the control's form, a builder subprogram (DMG `L_FREI`) or a computed machine-frame move where the kinematics is known; without any of these the block is an ERROR at compile time. D83. | block |
| `HOME X Z` | **verb**, bare axis names, optional `POINT=2` | Reference point return for the named axes (Fanuc `G28 U0 W0`, `G30 P2`, Siemens `G74`, Nakamura `G330`). The axes move at rapid to the reference point of the machine configuration; on controllers without such a command the compiler writes a machine-frame move to those coordinates (Heidenhain `L ... M91`). A machine axis name of the D93 form may stand bare here like a standard axis name (`HOME Z2`); it is resolved against `[[axis]]` like every other machine axis word. On an axis without a reference point in the configuration `HOME` is a WARNING at check time, once per run and axis, and the axis is unknown in every frame afterwards; only a compiler that must write the coordinates reports an ERROR (D100). | block |
| `X=10.5` | number; key = axis name | Absolute target coordinate. Standard axes `X Y Z A B C`; machine axes from the machine configuration (`Z2`, `W`, `C2`) are accepted by the parser by their form (`[XYZABCUVW]` followed by up to two digits, or `I` plus that) and resolved by the virtual machine against the `[[axis]]` list (D93). `A`, `B`, `C` name the rotary axis of the current workpiece holder when that holder has one (4.10). | block |
| `IX=5` | number; key = `I` + axis name | Incremental target: current position plus the value. `IX`, `IY`, `IZ`, `IA`, `IB`, `IC`, `IZ2` ... Absolute and incremental words may be mixed in one block, one form per axis. | block |
| `CENTER:X=50` | number; addr = plane axis | Arc center, absolute. | block |
| `CENTER:IX=-0.534` | number; addr = `I` + plane axis | Arc center, incremental from the start point of the arc (the Fanuc and Siemens `I J K` meaning). | block |
| `TX=0 TY=0.5 TZ=0.866` | three numbers, unit vector | Tool axis direction at the end of a `LINE`, in the active workpiece frame, instead of rotary axis words (Heidenhain `LN` with `TX TY TZ`, Siemens `A3= B3= C3=` under `TRAORI`, Fanuc `G43.5` with `I J K`). Only under `TCPM=ON`, all three together, never mixed with `A`, `B`, `C` in the same block. The VM stores the vector as written and does not resolve it to axes (as it does not resolve the polar or cylinder plane of `POLAR` and `CYLINDER` to the Cartesian workpiece position, D102); turning it into axis positions is the job of the kinematics module, so vector programs survive `convert` and compile to controllers that accept vectors. Further vector forms are expected to follow. D81. | block |
| `NX=0 NY=0 NZ=1` | three numbers, unit vector | Surface normal at the end of the `LINE`, for 3D tool radius compensation (Heidenhain `LN` with `NX NY NZ`, Siemens `A4= B4= C4=`). Optional, only together with `TX TY TZ`. D81. | block |
| `R=5` | number, not 0 | Arc radius. Positive: arc of 180 degrees or less. Negative: more than 180 degrees. Full circles need `CENTER`. | block |
| `F=200` | number | Feed in the active feed mode. | modal |
| `FEED_MODE=PER_MIN` | `PER_MIN`, `PER_REV` | Feed per minute (G94, M137) or per spindle revolution (G95, M136). Default `PER_MIN`. | modal |

Direction: `CW` and `CCW` are seen looking against the tool axis onto the working plane (the G2/G3 definition). Heidenhain `DR-` is `CW`, `DR+` is `CCW`. Because the verb carries the direction, `ANGLE` is unsigned; the reader turns a signed source angle into the verb and the magnitude.

Axis words require a verb. A block with `X=10` and no verb is an ERROR; this catches the classic "forgot the G1" mistake at parse time and keeps state-only blocks free of coordinates.

Chamfers and corner roundings that controllers attach to a motion block (Fanuc `,C2` and `,R4`, Siemens `CHF=`, `CHR=`, `RND=`, Heidenhain `CHF` and `RND` blocks) have no NCX word: readers expand them into the explicit `LINE` and `ARC` blocks they stand for, which every controller can execute (D58).

### 4.4 Tool

A tool change is two things on most machines and one thing in a careless program. NCX names both:

| Word | Value | Meaning | Scope |
|---|---|---|---|
| `PRELOAD=5` | integer or string | Prepare tool 5 in the magazine (Fanuc `T5` alone, Heidenhain `TOOL DEF 5`, Siemens `T5` on machines that change with M6). Nothing happens to the spindle. `PRELOAD=0` clears a pending preload. | modal until consumed |
| `TOOL=4` | integer or string; optional addr = holder role | The spindle (or the addressed holder) now carries tool 4. This is the change. The compiler writes what the machine needs: `T4 M6`, `M6` alone when 4 is already preloaded and the machine allows it, `TOOL CALL 4 Z S...`, `T4` on a turret. `TOOL=0` empties the spindle. | modal |
| `TOOL` | no value | Change to the preloaded tool. ERROR when nothing is preloaded. The compilers write the number; `ncx format` keeps the word as written (D91). | |
| `OFFSET:LEN=4` | integer | Tool length offset register (G43 H). 0 cancels. | modal |
| `OFFSET:RAD=4` | integer | Tool radius offset register (D). | modal |
| `OFFSET=56` | integer | Combined offset register when the controller has one register for geometry and wear of the tool (Fanuc lathe `T0656` = station 6, offset 56; Siemens `D`). Not combinable with `OFFSET:LEN`/`OFFSET:RAD` in the same program. | modal |
| `COMP=LEFT` | `LEFT`, `RIGHT`, `OFF` | Cutter radius compensation (G41/G42/G40, RL/RR/R0). Applies from the motion of the same block on. | modal |

Reader rules for the sources (details in `controller-mapping.md`): Fanuc `T4 M6` is `TOOL=4`; `T5` alone is `PRELOAD=5`; `M6` alone is `TOOL=n` with n the preloaded tool (ERROR when none); a lathe `T0656` is `TOOL=6 OFFSET=56`; Heidenhain `TOOL DEF 5` is `PRELOAD=5` and `TOOL CALL 4 Z S1500` is `TOOL=4 RPM=1500` (plus `WORKPLANE` when the axis letter changes it). Machine builders wrap the change in macros, and the manuals show that all of them fit the two words: Nakamura `G340 T0101. A02.` changes to tool 1 with offset 1 and prepares tool 2 (`TOOL=1 OFFSET=1` and `PRELOAD=2` in one block; `G341 T02.` is the preload alone); Mori Seiki NTX `T9001` brings tool 9001 to the change position (`PRELOAD=9001`) and `G361 B0 D1.` performs the change (`TOOL=9001`, the number of the preload, D91) with the B index angle and the tool kind as parameters; DMG structure programming selects with `T="NAME"` and changes with `TC(1,,,2,90,0)` (offset number, turret direction, place, tool kind, B1 angle, C1 angle). The machine configuration holds the template, and a `PRELOAD` that follows a `TOOL` block (before the next motion) is folded into the change command when the template has a `{next}` placeholder. What the builders carry beyond the tool number is always the same three things: the index angle of the tool carrier (`B`), the orientation of the tool spindle for turning tools (`C1`, `G419 A`) and the tool kind (rotary or turning). D52 decides where NCX takes them from. Whether a compiler emits an automatic `PRELOAD` of the next tool after every change is a machine setting (`auto_preload`), not a program decision.

### 4.5 Spindle

| Word | Value | Meaning | Scope |
|---|---|---|---|
| `SPINDLE=CW` | `CW`, `CCW`, `OFF`; optional addr = spindle role | Spindle on clockwise, counterclockwise, off (M3, M4, M5). Without address: the machine's default spindle. | modal |
| `RPM=1592` | number; optional addr = spindle role | Spindle speed. Per resource in NCX. Fanuc has one `S` word that belongs to the spindle of the M code in the same block (otherwise to the spindle selected last), so the compiler writes `RPM:role` into the block of that role's `SPINDLE` word and the reader assigns a bare `S` to the last selected spindle. | modal |
| `SPINDLE_MODE:MAIN=AXIS` | `SPINDLE`, `AXIS`; addr = spindle role | Work spindle as rotating spindle or as positioning C axis (M70/SPOS, machine M functions). In `AXIS` mode its axis is driven with `C=`; in `SPINDLE` mode `C=` on it is an ERROR. | modal |
| `ORIENT:MAIN=90` | number, degrees; addr optional | Oriented spindle stop (M19, SPOS=). The spindle is stopped afterwards. | block |
| `SPINDLE_SYNC=MAIN,SUB` | list of two spindle roles, or `OFF` | Synchronous spindles, same speed and direction, the second follows the first, for workpiece transfer (Siemens `COUPON` or builder cycle, Nakamura `M96`/`M97`, Doosan `M203`..`M206`, Mori Seiki `M35`/`M36`, Biglia `M61`/`M63`). Some machines accept the codes only from the channel that owns the second spindle, or need them in every channel program: the machine configuration says so (D56). | modal |
| `PHASE=113.5` | number, degrees; only with `SPINDLE_SYNC` | Phase-synchronous run with an angular offset, needed to transfer polygonal or keyed parts (Nakamura `M92`, Doosan `M213`/`M214`, Mori Seiki `M34`, Biglia `M62`, DMG `L726(angle)`). Without `PHASE` the sync is speed only. | with `SPINDLE_SYNC` |

### 4.6 Coolant and machine functions

| Word | Value | Meaning | Scope |
|---|---|---|---|
| `COOLANT=ON` | `ON`, `OFF`; optional addr = channel name | Default coolant channel (M8/M9) or a named channel from the machine configuration (`COOLANT:AIR=ON`, `COOLANT:THROUGH=ON`). | modal per channel |
| `FUNC:CHIP_CONVEYOR=ON` | identifier; addr = function name | Named machine function defined in the machine configuration with its M code(s) per state. This is the normal way to write machine functions. | block |
| `MFUNC=136` | integer | Raw M function by number. Only for functions the machine configuration does not name; the compiler warns. | block |

### 4.7 Cycles

A cycle is defined once (modal) and executed by `CYCLE_CALL` at each position. `SURFACE`, `CLEARANCE`, `DEPTH` and `SAFE` of the built-in drilling family are absolute coordinates along the drilling axis in the active frame (Z on a mill, Z or X on a lathe, see `AXIS`); readers convert Heidenhain depths relative to Q203 and Fanuc `R` planes.

| Word | Value | Meaning | Scope |
|---|---|---|---|
| `CYCLE=DRILL` | cycle name; with a controller address the native cycle number of that family (`CYCLE:HEIDENHAIN=251`, 4.7.1, D94) | Define the active cycle; its parameters follow in the same block. Built-in names: `DRILL`, `DRILL_DWELL`, `PECK` (deep hole, full retract), `CHIP_BREAK`, `TAP`, `REAM`, `BORE`. Further names come from the cycle catalog of the machine configuration (4.7.1). `CYCLE=OFF` cancels (G80). A new `CYCLE` replaces all parameters. | modal |
| `SURFACE=0` | number | Absolute coordinate of the workpiece surface along the drilling axis (Z on a mill; Q203). | with `CYCLE` |
| `CLEARANCE=5` | number | Absolute coordinate of the clearance plane along the drilling axis (Fanuc `R`, Q203 + Q200). | with `CYCLE` |
| `DEPTH=-21.732` | number | Absolute coordinate of the hole bottom along the drilling axis (Fanuc `Z`, or `X` under `AXIS=X`; Q203 + Q201). | with `CYCLE` |
| `SAFE=10` | number | Absolute coordinate of the safe plane along the drilling axis (G98 initial level, Q203 + Q204). Optional. | with `CYCLE` |
| `CYCLE_RETRACT=CLEARANCE` | `CLEARANCE`, `SAFE` | Where the tool ends after each hole (G99 / G98). Default `CLEARANCE`. The word carries the `CYCLE_` prefix because `RETRACT` is a motion verb (D83, D87). | with `CYCLE` |
| `PECK=1.2` | number | Peck depth (Q, Q202). | with `CYCLE` |
| `CYCLE_F=565` | number | Plunge feed of the cycle, in the active feed mode. Its own word, so that the motion feed `F` of the program is never touched by a cycle definition (D29). | with `CYCLE` |
| `CYCLE_DWELL=0.5` | number, seconds | Dwell at the bottom (P, Q211); its own word for the same reason. | with `CYCLE` |
| `PITCH=1.5` | number | Thread pitch for `TAP` (Q239; Fanuc F/S in rigid tapping). | with `CYCLE` |
| `CONTOUR=FACE_A` | sub name: the `NAME` of a `SUB` section of the file | Contour reference of a multiple repetitive turning cycle (Fanuc `G70`..`G76`, 4.7.1): the subprogram section of the file that holds the contour blocks, named on the cycle block. D65, D90. | with `CYCLE` |
| `AXIS=X` | axis name | Drilling axis of the cycle. Default: the tool axis of the active `WORKPLANE`. Lathes drill along Z on the face (`G83`..`G85`, `AXIS=Z`) and along X on the circumference (`G87`..`G89`, `AXIS=X`) with the C axis as the positioning axis; on a lathe under `DIAMETER=ON` the X values of `SURFACE`, `CLEARANCE` and `DEPTH` are diameters like every other X. D59. | with `CYCLE` |
| `CYCLE_CALL` | **verb** | Execute the active cycle at the position given by the axis words, or at the current position when there are none (CYCL CALL, M99, a Fanuc position block under an active G8x, a Fanuc `X`/`Z`/`R` block under an active `G90`/`G92`/`G94` turning cycle). | block |

#### 4.7.1 Cycle catalog

Everything beyond the drilling family (turning cycles `ROUGH_TURN`, `ROUGH_FACE`, `FINISH`, `GROOVE`, `THREAD` for Fanuc `G70`..`G76`, Siemens `CYCLE95`/`CYCLE93`/`CYCLE97` and Heidenhain 81x; pockets, contours, thread milling, measuring) is defined in a cycle catalog per controller family (`machine-config.md`, section 6): a catalog entry gives the NCX cycle name, its parameter names and their mapping to the native cycle. A program may use any catalog name (`CYCLE=RECT_POCKET LENGTH=60 WIDTH=40 DEPTH=-10 ...`). A cycle that only exists on one controller may be written natively, `CYCLE:HEIDENHAIN=251 Q215=0 Q218=60 Q219=40 ...`, which compiles only to that controller family. In such a block every key the catalog does not know, one of the machine-axis form of D93 included, is a native parameter with a number or expression value, kept in source order after the cycle words; a compiler for another controller family reports the block as an ERROR, exactly as it does for `RAW` (D94). Catalog entries are templates, so a builder that selects the peck behaviour with a mode M code before the cycle (Doosan `M291`/`M292` before `G83`) is covered without a new NCX word.

Two kinds of turning cycle exist on Fanuc lathes and the catalog must carry both. The simple cycles `G90` (OD/ID), `G92` (thread) and `G94` (face) (`G77`, `G78`, `G79` in G-code systems B and C) are modal exactly like the drilling cycles: `G90 X60. Z-70. F0.3` cuts one pass and the following blocks `X50.` and `X40.` repeat the cycle with the new value, so they read as `CYCLE=TURN_OD ... ` plus one `CYCLE_CALL` per block and compile back the same way. The multiple repetitive cycles `G70`..`G76` are one-shot blocks whose `P` and `Q` words name the block range of the contour that follows; a catalog entry for them references the contour as a subprogram section of the file, `CONTOUR=name` on the cycle block (D65).

### 4.8 Channels and synchronization

| Word | Value | Meaning | Scope |
|---|---|---|---|
| `SYNC=100` | integer | Rendezvous mark: the channel waits until every participating channel has reached `SYNC=100`, then all continue (Fanuc wait M codes `M100`..`M199`, Siemens `WAITM`). Marks may be reused: the same number may appear several times in a program, and the channels pair them in execution order (the DMG templates wait at 0, 80 and 90 repeatedly). A `SYNC` as the first block after the header is the program start synchronization (Nakamura `M199`). | block |
| `WITH=1,2` | list of integers | Participating channels of the `SYNC` in the same block. Default: all channels of the job. The compiler writes the machine's form (Fanuc `P12`/`P123` path lists or the `P3`/`P7` bitmask, Siemens channel arguments). | block |
| `START_CHANNEL=2` | integer | Start the program of channel 2 (Siemens `START`). Optional `NAME` selects the program. | block |
| `WAIT_CHANNEL=2` | integer | Wait until channel 2 has finished (Siemens `WAITE`). | block |

A job (which programs run on which channel) is described in a job manifest next to the files (`machine-config.md`, section 8).

### 4.9 Variables and control flow

| Word | Value | Meaning | Scope |
|---|---|---|---|
| `VAR:Q1=10` | number, string or expression; addr = variable name | Assign a variable. Creates it if needed. | |
| `$Q1` | inside `{...}` | Read a variable. | |
| `LABEL=1` | integer or identifier | Jump target and repeat start. Unique per program or subprogram; `END` is reserved. | |
| `JUMP=1` | label, or `END` | Continue at the label. Conditional with `IF` in the same block. `JUMP=END` continues at the `PROGRAM=END` of the current program, which is how a conditional early end is written (Fanuc `IF [#503 EQ 0] GOTO 9090` with `N9090 M30`). | block |
| `IF={$Q3 < $Q2}` | expression | Condition for the `JUMP` or `CALL` in the same block; the block executes when the expression is not 0. | block |
| `SUB=BEGIN` | with `NAME` | Start of a subprogram section, `SUB=BEGIN NAME=100` or `SUB=BEGIN NAME=DRILL_ROW` (4.13). Subprograms stand in the file next to the programs, never inside one. | file |
| `SUB=END` | | End of the subprogram section: return to the caller (Fanuc `M99`, Heidenhain `LBL 0`, Siemens `RET` or `M17`). | file |
| `RETURN` | | Return to the caller before `SUB=END` is reached, for example under a condition. In a program (not in a subprogram) it is a WARNING and treated as `JUMP=END`. | block |
| `CALL=100` | sub name, or string for an external program | Call a subprogram of the file by its `NAME` (`M98 P100`, `CALL LBL 100`, `L100`) or an external program by file name (`CALL="O9010"`, `CALL PGM`, `EXTCALL`). | block |
| `ARG:A=1` | number or expression; addr = argument name | Argument of the `CALL` in the same block (`G65 P9010 A1`). The callee sees it as a local variable. | block |
| `TIMES=3` | integer or expression | Repeat count for `CALL` (`M98 P100 L3`) or `REPEAT`. | block |
| `REPEAT=1` | label | Repeat the blocks from the label to this block `TIMES` more times (`CALL LBL 1 REP 3`, `REPEAT LABEL P=3`). | block |

Variable names follow `ADDR`. Readers keep native names where possible: Heidenhain `Q5`, `QL5`, `QR5`, `QS5`; Siemens `R5` and declared names; Fanuc `#105` becomes `V105` because `#` is not a legal character, and G65 letter arguments map to `V1`..`V33` by the standard table. The compiler maps names back through the machine configuration.

### 4.10 Resources and roles

A program addresses spindles and holders by **role**, never by the machine's own id. Roles are defined in the machine configuration; NCX recommends `MAIN` (main work spindle), `SUB` (sub spindle), `TOOL` (milling spindle on a mill-turn, the only spindle on a mill), `TURRET1`, `TURRET2`, `TABLE`. A word without a role address targets the default resource of its kind. An unknown role is an ERROR; without a machine file it is the WARNING "not checked: no machine file" and the word runs against a resource created on the spot (D103, `ncx-virtual-machine.md` 3.8).

| Word | Value | Meaning | Scope |
|---|---|---|---|
| `WORKPIECE=SUB` | holder role | From here on the program machines the part held by this resource. `ORIGIN`, `SHIFT` and `A B C` refer to it. Many lathes have a native command for it (Nakamura `M427`/`M428` with the spindle's datum, Mori Seiki `M303`/`M304`, Doosan `M34`/`M134`, Biglia `M64`/`M65`, DMG `M813`/`M814`), which the machine configuration maps; where there is none, the chuck function rule of D40 applies, and where even that does not fit (an M code that runs a hidden subprogram on that particular machine), a reader rule from a plugin decides (D40: the detection is modular per machine, `ncx-virtual-machine.md` 7). The transfer itself is `FUNC` words and motions around it. Coordinates under a `WORKPIECE` are always in that holder's own right-handed frame with +Z pointing out of its chuck toward the tool, exactly as for the main spindle; whether the machine achieves this with a Z mirror (Nakamura `G360`/`G361`, Mori Seiki `M396`/`M395`) or with a datum whose Z runs the other way is a machine setting and never visible in the program. D57. | modal |

### 4.11 Lathe words

| Word | Value | Meaning | Scope |
|---|---|---|---|
| `DIAMETER=ON` | see 4.2 | Diameter programming for `X`; the word is defined in 4.2, this row only lists it among the lathe words (D90). | modal |
| `CSS=ON` | `ON`, `OFF`; optional addr = spindle role | Constant surface speed (G96/G97). Under `CSS` the spindle follows `VC`; `RPM` keeps its meaning and is ignored while `CSS` is on. | modal |
| `VC=140` | number, m/min or ft/min; optional addr = spindle role | Cutting speed for `CSS` (the `S` of `G96 S140`). | modal |
| `RPM_MAX=3000` | number; optional addr = spindle role | Speed limit under `CSS` (G50 S, LIMS). | modal |

### 4.12 Expressions

```
expr        = orexpr ;
orexpr      = andexpr { "OR" andexpr } ;
andexpr     = notexpr { "AND" notexpr } ;
notexpr     = [ "NOT" ] cmpexpr ;
cmpexpr     = sum [ ( "==" | "!=" | "<" | "<=" | ">" | ">=" ) sum ] ;
sum         = product { ( "+" | "-" ) product } ;
product     = unary { ( "*" | "/" | "MOD" ) unary } ;
unary       = [ "-" ] power ;
power       = primary [ "^" unary ] ;
primary     = number | variable | function "(" expr { "," expr } ")" | "(" expr ")" ;
variable    = "$" addr [ "[" expr "]" ] ;
function    = "SIN" | "COS" | "TAN" | "ASIN" | "ACOS" | "ATAN" | "ATAN2" | "SQRT" | "ABS"
            | "INT" | "FRAC" | "ROUND" | "SGN" | "LN" | "EXP" | "MIN" | "MAX" ;
```

Angles in degrees. `INT` truncates toward zero, `ROUND` rounds half away from zero, `MOD` keeps the sign of the dividend. Comparisons yield 1 or 0. Division by zero is an ERROR. Whitespace inside `{}` is free. Expressions may stand wherever a number is allowed.

System variables: names starting with `SYS_` are reserved for values the control provides (current position `$SYS_POS_X`, machine position `$SYS_MPOS_B`, tool offsets `$SYS_TOOL_LEN`, wear registers with an index `$SYS_WEAR_Z[99]`, active tool `$SYS_TOOL`, part status `$SYS_PART_MAIN`). The index selects a register or table row and may itself be an expression. Readers map the native names (Fanuc `#5021`..`#5025` machine positions, `#11099` = Z wear register 99 in the Nakamura transfer program, Siemens `$AA_IW[X]`, `RG` parameters, Heidenhain `FN 18` ids) through the machine configuration; unmapped ones become `RAW`. D51.

### 4.13 Files, programs and subprograms

A file is framed by `FILE=BEGIN NCX=1` and `FILE=END`. Between them stand, in any order, one or more programs and any number of subprograms (D48):

```
FILE=BEGIN NCX=1
PROGRAM=BEGIN NAME="SHAFT" NUMBER=1
  ...                                ; executed top to bottom
PROGRAM=END                          ; M30, M2 or M17 per machine configuration
SUB=BEGIN NAME=100
  ...                                ; entered by CALL=100
SUB=END                              ; M99, LBL 0, RET
PROGRAM=BEGIN NAME="SHAFT_OP2" NUMBER=2
  ...
PROGRAM=END
FILE=END
```

A program runs from `PROGRAM=BEGIN` to `PROGRAM=END`; `PROGRAM=END` is the executed end and the control rewinds there. Several programs in one file are what Siemens job archives (`%_N_1_0_MPF`, `%_N_TH1_HS_01_SPF` and the tool list in one file on an INDEX), Fanuc files with several `O` programs and multi-channel jobs already are; the first program is the one that runs unless the job manifest (`machine-config.md`, section 8) or the command line names another, and how the compiler writes the others (one after another in one file, or one file each, as Heidenhain needs) is a machine setting. A subprogram belongs to the file and may be called from every program in it; the compiler places it where the target wants it (Fanuc: an `O` program after the caller's `M30`; Heidenhain: `LBL n` ... `LBL 0` after the `M30` of every program that calls it, since labels are program-local there; Siemens: `PROC name` ... `RET` as its own `SPF` unit). A subprogram may use incremental words so that the same section works at every call position; that is the reason incremental words exist in NCX at all.

Rules: `PROGRAM=END` and `SUB=END` are the last blocks of their sections and appear exactly once. Straight-line flow that reaches `SUB=END` returns to the caller; `RETURN` does the same earlier. An early end of a program is `JUMP=END`. A `CALL` from a subprogram to another subprogram is allowed up to the configured depth; a `CALL` of a program (not a subprogram) is an ERROR, programs are entered from the job only.

Code that a source keeps after its `M30` and enters by a jump (Fanuc `GOTO 300` to a block below `M30`, then `GOTO 22` back; Heidenhain `FN 9`..`FN 12` to an `LBL` after `M30`; the Nakamura sample with `N2 M2` as a jump target after `N30 M30`) has no place after `PROGRAM=END`, because nothing follows the end of a program. The reader moves such a section in front of `PROGRAM=END` and guards it with a jump over it, which every controller can execute and which says what happens:

```
  ...
JUMP=END                             ; the main flow ends here
LABEL=300                            ; reached by JUMP=300 from above
  ...
JUMP=22                              ; back into the main flow
PROGRAM=END
```

A section that ends the program (the `N2 M2` case) ends with `JUMP=END`. A block of a program that no `LABEL` makes reachable after an unconditional `JUMP` gets a WARNING.

A program may also loop instead of ending: Fanuc `M99` in a main program jumps back to the first block (bar work runs this way until the bar feeder reports the end), and `/M99` under the block skip switch is the usual way to repeat a section during setup. NCX writes this as a `LABEL` after the header and a `JUMP` to it (with `SKIP` when the source had the slash); a `RETURN` outside a subprogram is not the way to say it (it is a WARNING, treated as `JUMP=END`).

### 4.14 Multi-channel programs

`CHANNEL=n` in the header of a program says which channel it runs on; the programs of a job may stand in one file or in one file each. The job manifest lists the channel programs that run together; `SYNC` marks synchronize them (4.8).

### 4.15 Generated blocks

A machine may need more than the program says: a spindle that has to stop before the through-spindle coolant can engage its clutch and has to run again afterwards, a retract before a tool change, a mode M code before a cycle, a speed clamped to what the spindle can do. NCX does not add words for this. The machine configuration carries expansion rules (`machine-config.md`, section 5a) and plugins carry arbitrary logic (`ncx-virtual-machine.md`, section 7), and both express their result as ordinary NCX blocks inserted before or after the block that triggered them. The virtual machine executes generated blocks like any other, so the state stays right and `trace` shows what was inserted and why; `ncx format` never writes them, `ncx compile` writes them like the rest, and a diagnostic on a generated block names the block it was generated for. Restoring a state the rule had to change (the spindle after the coolant) is done by the VM from its own snapshot, not by the rule guessing the previous value. The expander marks the state a rule has to put back with the pseudo-words `@SAVE=KEY[:ADDR]` and `@RESTORE=KEY[:ADDR]` (section 3, virtual machine 3.10); only generated blocks may carry them (D95). D61, D63.

## 5. Block rules and canonical order

1. A block has at most one verb (`RAPID`, `LINE`, `ARC`, `RETRACT`, `HOME`, `CYCLE_CALL`, `SHIFT`, `TILT`, `TILT_AXIS`, `SETPOS`).
2. Axis words (`X`, `IX`, `CENTER:*`, `R`, `ANGLE`, `TX TY TZ`, `NX NY NZ`) require a motion verb; `SHIFT`, `TILT`, `TILT_AXIS` and `SETPOS` carry their own axis words; `HOME` carries bare axis names; `RETRACT` carries none.
3. State words in a motion block take effect before the motion of that block (`LINE Y=2 COMP=LEFT` compensates the move to Y=2, as `G41 Y2. D1` does).
4. A key may appear once per block; keys with different addresses are different words.
5. `IF`, `ARG`, `TIMES`, `WITH`, `FRAME`, `MOVE`, `ROT`, `POINT`, `PHASE` are block words that need their verb or partner word in the same block.
6. Canonical order, mandatory for `ncx format` and for every writer, free for hand-written files. The word catalog carries the rank of every word, and the table `generated/word-catalog.md`, written from the catalog by a test (P0-03), is the reference (D90). The buckets, with their inner order:
   1. `SKIP`, then the structural words `FILE`, `NCX`, `PROGRAM`, `SUB`, `NAME`, `NUMBER`, `CHANNEL`.
   2. The verb.
   3. Axis words: `X Y Z A B C`, then machine axes in alphabetical order (by letter, then by number: `C2`, `W`, `Z2`); all absolute words in that order, then all incremental words in the same order.
   4. `TX TY TZ`, `NX NY NZ`.
   5. `CENTER:X`, `CENTER:Y`, `CENTER:Z`, then the incremental forms.
   6. `R` or `ANGLE`.
   7. `F`.
   8. `FEED_MODE`.
   9. Tool words: `PRELOAD`, `TOOL`, `OFFSET`, `OFFSET:LEN`, `OFFSET:RAD`, `COMP`.
   10. Spindle words: `SPINDLE`, `RPM`, `CSS`, `VC`, `RPM_MAX`, `SPINDLE_MODE`, `ORIENT`, `SPINDLE_SYNC`, `PHASE`.
   11. `COOLANT`, `FUNC`, `MFUNC`.
   12. Frame and state words: `UNITS`, `WORKPLANE`, `ORIGIN`, `DIAMETER`, `WORKPIECE`, `FRAME`, `SHIFT=RESET`, `ROTATE`, `MIRROR`, `TILT=RESET`, `TILT_AXIS=RESET`, `MOVE`, `ROT`, `POINT`, `CYLINDER`, `POLAR`, `TCPM`, `ROTARY_PATH`, `ROTARY_FEED`, `TOLERANCE`, `TOLERANCE:ROTARY`, `TOLERANCE_MODE`.
   13. Cycle words: `CYCLE`, `AXIS`, `SURFACE`, `CLEARANCE`, `DEPTH`, `SAFE`, `CYCLE_RETRACT`, `PECK`, `CYCLE_F`, `CYCLE_DWELL`, `PITCH`, `CONTOUR`, then the native parameters of a `CYCLE:<controller>=n` block in source order (D94).
   14. Channel words: `SYNC`, `WITH`, `START_CHANNEL`, `WAIT_CHANNEL`.
   15. Variable and flow words: `VAR`, `LABEL`, `JUMP`, `CALL`, `ARG`, `TIMES`, `REPEAT`, `RETURN`, `IF`.
   16. `STOP`, `DWELL`, `RAW`.
   17. `COMMENT` or `SECTION`.

   Words of one key with several addresses sort by the address text (`COOLANT:AIR` before `COOLANT:THROUGH`, `VAR:Q1` before `VAR:Q3`).
7. Canonical layout of a block: the words, then, when the block has a comment, spaces up to column 57 so that the semicolon stands in column 57, or three spaces when the words reach column 54 or later, then the comment. Blank lines and comment-only lines are kept as read (D92).

## 6. Examples

Linear move at feed, Fanuc `N110 G1 Z-10. F2387`, Heidenhain `13 L Z-10 F2387`:

```
LINE Z=-10 F=2387
```

Corner arc, Fanuc `G2 X2. Y7. R5.`, Heidenhain `CR X2 Y7 R5 DR-`:

```
ARC=CW X=2 Y=7 R=5
```

Center arc, Fanuc `G3 X70. Y50. I-.534 J-19.993` from start point 50.534/69.993, Heidenhain `CC X50 Y50` and `C X70 Y50 DR+`:

```
ARC=CCW X=70 Y=50 CENTER:X=50 CENTER:Y=50
```

Reference point return, Fanuc `G28 U0 W0`, Nakamura `G28 U0 V0` (X and Y):

```
HOME X Z
HOME X Y
```

Tool change with preload of the next tool, Fanuc `T4 M6` ... `T5`:

```
TOOL=4 OFFSET:LEN=4 OFFSET:RAD=4 RPM=1592
PRELOAD=5
```

Drilling, Fanuc `G81 G99 Z-21.732 R5. F565` then `X30.`, Heidenhain `CYCL DEF 200 ... ` then `CYCL CALL` and `L X30 FMAX M99`:

```
CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-21.732 CYCLE_RETRACT=CLEARANCE CYCLE_F=565
CYCLE_CALL X=10 Y=10
CYCLE_CALL X=30
CYCLE=OFF
```

Tilted plane by rotary axis angles with the table turned, then a retract along the tool axis, Heidenhain `PLANE AXIAL A-90 C180 TURN FMAX` ... `M140 MB MAX`:

```
TILT_AXIS A=-90 C=180 MOVE=TURN
...
RETRACT
```

5-axis line with a tool vector under TCPM, Heidenhain `LN X+41.786 Y-57.382 Z+95.488 NX0 NY0 NZ1 TX0 TY0.5 TZ0.866 F2841`, Siemens `G1 X41.786 Y-57.382 Z95.488 A3=0 B3=0.5 C3=0.866 F2841` under `TRAORI`:

```
TCPM=ON
LINE X=41.786 Y=-57.382 Z=95.488 TX=0 TY=0.5 TZ=0.866 NX=0 NY=0 NZ=1 F=2841
```

Helix of two turns and a rest from TopSolid, `CP IPA+737.956 IZ-5.4 DR+` around `CC X50 Y50`:

```
ARC=CCW IZ=-5.4 CENTER:X=50 CENTER:Y=50 ANGLE=737.956
```

Path tolerance for a 3D program, Heidenhain `CYCL DEF 32.0 TOLERANZ`, `32.1 T0.02`, `32.2 HSC-MODE:0 TA0.05`; Siemens `CYCLE832(0.02,1,0.05)`:

```
TOLERANCE=0.02 TOLERANCE:ROTARY=0.05 TOLERANCE_MODE=FINISH
```

Complete programs: `examples/2.5D_FRAESEN.ncx` (milling, both source controllers), `examples/PATTERN_LOOP.ncx` (variables and a loop), `examples/INCREMENTAL_SUB.ncx` (subprogram with incremental words), `examples/MILLTURN_TRANSFER.ncx` (roles, spindle sync, workpiece transfer), `examples/POLAR_FACE.ncx` (polar interpolation with `HOME`, `SETPOS`, `POLAR`, side drilling with `AXIS=X`).

## 7. Open decisions

Settled items are in `../decisions/decisions.md`; the questions and answers are kept in `../decisions/rationale.md`. Nothing in this document is open (D87 to D89 confirmed 2026-09-12: `CYCLE_RETRACT`, `JUMP=END`, subprograms as file sections). D90 to D96 were settled 2026-09-11: canonical order as a rank table, `ncx format` as parser and writer only, comments and blank lines as trivia, machine axis words by their form, native cycle parameters, pseudo-words, `CYLINDER` with its radius as the value. D99 to D106 were settled the same day; of these the language is touched by D100 (`HOME` without a reference point, 4.3), D101 (`SETPOS` after `HOME`, 4.2), D102 (under `POLAR` and `CYLINDER` the arc and compensation plane is the face or cylinder plane, 4.2) and D103 (an unknown role without a machine file, 4.10); D101 and D102 were clarified on 2026-09-12 and 2026-09-13.
