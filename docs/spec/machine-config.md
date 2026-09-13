# Machine Configuration (TOML)

Status: Version 1.0 of the specification set, 2026-09-12 (draft 8 in the drafting history of `../decisions/decisions.md`). Sketch level: the schema is finalized when the compiler exists. Companion to `ncx-language.md` and `ncx-virtual-machine.md`.

Templates are strings with `{placeholders}`: `{tool}` (the tool in the spindle after the block, from the VM, so a bare `TOOL` writes the preloaded number, D91), `{name}` (tool name string), `{offset}`, `{next}` (the preloaded next tool), `{kind}` (tool kind from the tool table: rotary or turning, mapped per machine), `{rpm}`, `{axis}` (tool axis letter; in the `[setpos]` and `ROTARY_FEED` templates the axis the call is written for, one call per axis), `{axes}` (the axis list of a `HOME` or `SETPOS` block in the machine's address form), `{angle}`, `{b}` (index angle of the tool carrier from the next positioning block), `{c}` (tool spindle orientation from the next `ORIENT:TOOL`), `{mark}`, `{channels}`, `{paths}`, `{r}` (the reference radius of `CYLINDER=n`, the only source of `{r}`; not the arc key `R`, D96), `{d}` (twice `{r}`: the working diameter that Siemens `TRACYL` takes), `{value}`, `{index}`; for the tilt, retract and tolerance templates `{a}`, `{b}`, `{c}` (the angles of the `TILT` or `TILT_AXIS` block), `{x}`, `{y}`, `{z}`, `{move}` and `{dir}` (from the `MOVE` option), `{distance}`, `{tol}`, `{rotary}`, `{mode}`; in expansion rules `{position:NAME}` (the axis words of a `[positions]` entry, D100). A format suffix pads numbers: `{tool:02}` writes `04`, `{tool:02}.` writes `04.` (the decimal point of the NT NURSE macros is literal text). A missing placeholder value leaves the template unusable and the compiler reports it.

Everything that differs between two machines lives here and nowhere else. A user copies the file of a similar machine and edits it. One machine, one file. The file is TOML; a large kinematic tree may be split into a second TOML file referenced from the first, so the user still edits one configuration language.

## 1. Identity and dialect

```toml
[machine]
name = "DMU 50"
controller = "heidenhain"          # fanuc | heidenhain | siemens
dialect = "iTNC530"                # 30i | 31i-T | 0i-TF | iTNC530 | TNC640 | 840D ...
builder = "dmg"                    # machine builder id, used by RAW:DMG and by the builder function tables
gcode_system = "A"                 # Fanuc lathes only: A (G98/G99 feed, U/W incremental, G90 = turning cycle), B or C (G94/G95, G90/G91)
s_binds_to_spindle_word = true     # Fanuc: one S word, owned by the spindle of the M code in the same block; RPM:role is written into that role's SPINDLE block
limits = "warn"                    # warn | clamp: what the expander does with RPM, F and targets beyond the machine limits (D64)
channels = [1]
units_default = "MM"
default_spindle = "S1"
default_holder = "H1"
default_workpiece = "TABLE1"
tool_table = "dmu50.tools.toml"   # optional (D10): tool kind, length, radius per tool number; when missing, {kind} takes the default and the compiler writes a warning block at the head of the program naming the expected file and the tools it lacks, so nobody runs the program by accident
```

`controller` selects the reader and compiler; `dialect` selects their variant tables. `default_*` resolve NCX words without a role address. `check`, `trace`, `annotate` and `analyze` run without a machine file against a built-in default machine (`ncx-virtual-machine.md`, section 3.8, D103); `convert` and `compile` need one (D77).

## 2. Output format

```toml
[format]
decimal_separator = "."            # "," for Heidenhain
decimals = { X = 3, Y = 3, Z = 3, A = 3, C = 3, F = 1, S = 0 }
trailing_zeros = false
block_numbers = { enabled = true, start = 10, step = 10 }   # Heidenhain: consecutive from 0
line_ending = "CRLF"
program_end = "M30"                # M30 | M2 (Siemens also M17), written for PROGRAM=END (D48, D49)
sub_end = "M99"                    # written for SUB=END and RETURN; Heidenhain "LBL 0", Siemens "RET"
program_layout = "one_file"        # one_file: all programs and subprograms of an NCX file into one output file (Fanuc O programs, Siemens %_N_ units)
                                   # file_per_program: one output file per program, subprograms copied after the M30 of every caller (Heidenhain)
max_line_length = 0                # 0 = unlimited
comment_charset = "ASCII"          # umlauts are transliterated before writing
```

## 3. Tool change

```toml
[tool_change]
change = "T{tool} M6"              # written for TOOL=n and for a bare TOOL ({tool} from the VM, D91)
change_preloaded = "M6"            # optional: written instead of change when n is already preloaded
preload = "T{tool}"                # written for PRELOAD=n; omit on machines without a magazine (PRELOAD is dropped with a WARNING)
unload = "T0 M6"                   # written for TOOL=0
auto_preload = true                # insert PRELOAD of the next tool after each TOOL when the program has none
preload_position = "after_change"  # after_change | before_first_motion
offsets_with_change = false        # true on Heidenhain: OFFSET words are implicit in TOOL CALL and not written
tool_name_allowed = false          # TOOL="NAME" accepted (Heidenhain, Siemens) or mapped to numbers
kind_map = { ROTARY = "0.", TURNING = "1." }   # the value of {kind} per tool kind of the tool table (D52); Mori Seiki as here, DMG { ROTARY = 1, TURNING = 2 }
```

Templates found in the manuals:

| Machine | `change` | `preload` |
|---|---|---|
| Fanuc machining center | `"T{tool} M6"` | `"T{tool}"` |
| Heidenhain | `"TOOL CALL {tool} {axis} S{rpm}"` | `"TOOL DEF {tool}"` |
| Siemens, change on T | `"T{tool}"` or `"T=\"{name}\" M6"` | `"T{tool}"` |
| Fanuc lathe turret | `"T{tool:02}{offset:02}"` | none |
| Nakamura tool spindle (ATC up to 80 tools) | `"G340 T{tool:02}{offset:02}. A{next:02}."` (decimal point literal, per the NT NURSE macro rule) | `"G341 T{next:02}."` |
| Nakamura tool spindle (120-tool ATC, six-digit T) | `"G340 T{tool:03}{offset:03} A{next:03}"` | `"G341 T{next:03}"` |
| Mori Seiki NT/NTX tool spindle (MAPPS) | `"G361 B{b} D{kind}"` with `kind` mapped 0 (rotary), 1 (turning) | `"T{tool:04}"` (the `T` word brings the tool to the change position, the change is `G361`); `unload = "T0\nG361"` |
| DMG structure programming (840D) | `"T=\"{name}\"\nTC({offset},,,{kind},{b},{c})"` with `kind` mapped 1 (rotary), 2 (turning), 3 and 4 for turret tools at spindle 3 or 4 | `"T=\"{name}\""` on machines that prepare on the `T` word |

When `change` contains `{next}`, a `PRELOAD` word that follows the `TOOL` block (before the next motion) is folded into the change command; with `auto_preload` the compiler fills `{next}` from the STATIC look-ahead. `{b}` and `{c}` are filled the same way from the next positioning block and the next `ORIENT:TOOL`; `{kind}` comes from the tool table (D52). A template may span lines with `\n`.

```toml
[home]
template = "G28 {axes}"           # Fanuc: {axes} expands to "U0 W0" (incremental letters) or "G91 X0 Y0 Z0" per gcode_system
point = "G30 P{point} {axes}"     # second and further reference points; Nakamura NT NURSE: "G330 A{point}."
# DMG structure programming: template = "L711({order})" (X, Z, Y tool change point), "L713(0)" on the counter spindle side
# Heidenhain: no template; the compiler writes "L {axes} M91" with the reference coordinates of the [[axis]] table

[setpos]
template = "G50 {axes}"           # Fanuc system A; "G92 {axes}" in B and C; Siemens: "PRESETON({axis},{value})"; Heidenhain: none, the compiler folds it into SHIFT
```

## 4. Roles, resources, axes

```toml
[roles]
MAIN = "S1"
SUB = "S2"
TOOL = "S3"
TURRET1 = "H1"

[[resource]]
id = "S1"
type = "work_spindle"              # work_spindle | tool_spindle | tool_holder | table
axis = "C1"                        # the C axis this spindle becomes in AXIS mode

[[resource]]
id = "S3"
type = "tool_spindle"

[[resource]]
id = "H1"
type = "tool_holder"
spindle = "S3"
magazine = true
channel = 1                        # optional: the channel that commands this resource on a machine with several (Nakamura: turret 1 on path 1, turret 2 on path 2), F23

[[axis]]
id = "X1"
ncx = "X"                          # name used in NCX programs
letter = "X"                       # address written in the output; extended axis names are written with the equals sign the control wants: "C2=" on Fanuc 30i/31i and Siemens, "C" per path on Mori Seiki (D30)
incremental_letter = "U"           # Fanuc lathes: U/W/H/V are the incremental forms of X/Z/C/Y; empty when the control uses G91
kind = "linear"                    # linear | rotary
limits = [-10, 650]                # min and max in machine coordinates (the G53 / M91 frame), for the travel-limit analytics (D100)
rapid = 30000                      # mm/min, for the runtime estimate
max_feed = 10000                   # mm/min, the F this axis can follow; the expander warns or clamps (D64)
acceleration = 3000                # mm/s^2, for the trapezoidal profile of the runtime estimate
programming = "diameter"           # X axis of a lathe: diameter | radius | switchable (D60); the reader interprets the source with it, the compiler writes it
home = 0                           # reference point 1 in machine coordinates (the G53 / M91 frame), for HOME (D100)
home2 = -50                        # reference point 2 (G30 P2), optional
clamp = { ON = "M10", OFF = "M11" }   # rotary axis clamping, written by the compiler around 3+2 machining when set

[[axis]]
id = "C1"
ncx = "C"
letter = "C"
kind = "rotary"
owner = "S1"                       # rotary axis of a work spindle
limits = [0, 360]                 # degrees, modulo axis: the display range, not a travel limit (D100)
home = 0                           # the spindle zero mark, machine frame (D100)
```

`home` and `limits` are machine coordinates (the G53 / M91 frame). `HOME` on an axis without `home` (or without `home2` under `POINT=2`) is a WARNING at check time, once per run and axis, and an ERROR only in a compiler that must write the coordinates (D100). `[positions]` names further machine-frame positions, each a set of axis values; expansion rules reach them through `{position:NAME}` (section 5a).

```toml
[positions]                        # named positions in machine coordinates (D100)
tool_change = { X = 0, Z = -120 }  # reached by expansion rules through {position:tool_change}
program_end = { X = 0, Z = -120 }  # optional
```

```toml
[dynamics]                         # control behaviour for the runtime estimate (D64)
block_time = 0.001                 # seconds the control needs per block; a short segment cannot be faster than this
path_mode = "continuous"           # continuous | exact_stop
corner_speed = 2000                # mm/min carried through a corner in continuous mode

[diameter]                         # how the machine switches, when `programming = "switchable"` on the X axis
ON = "DIAMON"                      # Siemens; Mori Seiki radius mode: ON = "M583", OFF = "M582"; Fanuc: none, the parameter decides
OFF = "DIAMOF"
```

## 5. Functions and coolant

```toml
# functions per spindle role; the VM only knows SPINDLE:role=CW, the template is the machine's business
[spindle.MAIN]
CW = "M3"
CCW = "M4"
OFF = "M5"
ORIENT = "M19"                     # or "M19 S{angle}" where the angle is programmable
RPM = "S{rpm}"
VC = "G96 S{value}"
CSS_OFF = "G97"
RPM_MAX = "G50 S{value}"
rpm_min = 45                       # machine limits, not the program's RPM_MAX clamp: the expander warns or clamps (D64)
rpm_max = 6000
accel_time = 2.5                   # seconds from stop to rpm_max, for the runtime estimate of starts and stops

[spindle.SUB]                      # Nakamura: +50, Mori Seiki: +200, Doosan: P suffix, Siemens: spindle number
CW = "M53"
CCW = "M54"
OFF = "M55"
ORIENT = "M419"

[spindle.TOOL]
CW = "M88"
CCW = "M89"
OFF = "M90"

[spindle_mode.MAIN]
AXIS = "M91"                       # DMG on 840D: "L707({angle})"
SPINDLE = "M41"                    # DMG: "L708"

[spindle_sync]
ON = "M96"                         # Doosan: "M203", Mori Seiki: "M35", Biglia: "M61", Siemens: "COUPON(S3,S4)", DMG: "L726({angle})" with PHASE
OFF = "M97"                        # Mori Seiki: "M36", Biglia: "M63"
PHASE = "M92"                      # phase-synchronous variant (Mori Seiki "M34", Biglia "M62"); omit when the machine has none
channel = 2                        # D56: the machine accepts these codes only from this channel (Nakamura: the path that owns the second spindle)
# channels = "all"                 # D56: the code must stand in every channel program behind a SYNC (Mori Seiki M34/M35)

[workpiece]                        # native selection of the spindle the turret works on, and how that side is programmed (D57)
MAIN = "G54 M428"                  # Mori Seiki: "M303", Doosan: "M34", Biglia: "M64", DMG: "M814"; the datum may be part of the template
SUB = "G59 M427"                   # Mori Seiki: "M304", Doosan: "M134", Biglia: "M65", DMG: "M813"
SUB_frame = "datum"                # datum: the sub spindle datum runs Z the other way and the compiler negates Z; mirror: the compiler writes the mirror cycle
# SUB_mirror = { ON = "G360", OFF = "G361" }   # Nakamura Z mirror cycles (M700/M701); Mori Seiki: { ON = "M396", OFF = "M395" }

[sync]
wait = "M{mark}"                   # Fanuc wait M code; with paths: "M{mark} P{paths}"
paths = "list"                     # list: P12, P13, P123 (parameter 8103#1 = 1); bitmask: P3, P5, P6, P7 (8103#1 = 0)
mark_range = [100, 199]            # Mori Seiki: [101, 197]; Siemens: [0, 99] with 95 taken by BARLOAD_SYNC
start_mark = 199                   # optional: written as the first block of every channel program (Nakamura M199 program start sync)
# groups = [{ channels = [1, 2], range = [100, 199] }, { channels = [3, 4], range = [800, 849] }]   # Nakamura WTW: a second wait group between other units
# Siemens: wait = "WAITM({mark},{channels})", channels written as "1,2" or as the template's variables "WAIT_K1,WAIT_K2"
start_channel = ""                 # Siemens: "START({channel})"
wait_channel = ""                  # Siemens: "WAITE({channel})"

[coolant]
STANDARD = { ON = "M8", OFF = "M9" }
AIR = { ON = "M14", OFF = "M15" }
THROUGH = { ON = "M51", OFF = "M9" }

[func]
CHIP_CONVEYOR = { ON = "M60", OFF = "M61" }
SUB_CHUCK = { OPEN = "M68", CLOSE = "M69" }
MAIN_CHUCK = { OPEN = "M66", CLOSE = "M67" }
TAILSTOCK = { FORWARD = "M21", BACK = "M22" }
DOOR = { OPEN = "M68", CLOSE = "M69", channel = 1 }   # D56: only channel 1 may command it (Biglia); the job compiler moves the word

[func_meta]
SUB_CHUCK.CLOSE = "workpiece_transfer_to = SUB"   # reader fallback rule for machines without a [workpiece] table (D40)
# When neither table fits (a builder M code runs a hidden subprogram on this machine), a reader rule from a plugin decides; the tables are tried first (D40, D66)

[transform]
CYLINDER_ON = "G7.1 C{r}"          # written for CYLINDER=30, {r} is that value, the reference radius (D96); Siemens: "TRACYL({d})" (the working diameter), DMG per spindle: "TRACYL_S4({d})"
CYLINDER_OFF = "G7.1 C0"
POLAR_ON = "G12.1"                 # Siemens: "TRANSMIT", DMG: "TRANSMIT_S4"
POLAR_OFF = "G13.1"
TCPM_ON = "G43.4 H{offset}"        # Nakamura: "G435", Siemens: "TRAORI", Heidenhain: "FUNCTION TCPM F TCP AXIS POS"
TCPM_OFF = "G49"                   # Nakamura: "G436", Siemens: "TRAFOOF", Heidenhain: "FUNCTION RESET TCPM"
TILT_ON = "G68.2 X{x} Y{y} Z{z} I{a} J{b} K{c}"   # Siemens: "CYCLE800(1,\"TC1\",0,57,0,0,0,{a},{b},{c},0,0,0,{dir},100,1)", Heidenhain: "PLANE SPATIAL SPA{a} SPB{b} SPC{c} {move}"
TILT_OFF = "G69"                   # Siemens: "CYCLE800()", Heidenhain: "PLANE RESET STAY"
TILT_AXIS_ON = ""                  # empty = the target cannot write TILT_AXIS without the kinematics module (ERROR at compile time, D82)
                                   # Heidenhain: "PLANE AXIAL A{a} B{b} C{c} {move}"; Siemens: CYCLE800 with the rotary-axes mode (bits 7 and 6 set) and {dir}
TILT_TURN = "G53.1"                # written after TILT_ON for MOVE=TURN where the control positions the axes in a second block (Fanuc); Heidenhain and Siemens use {move} and {dir}
move = { TURN = "TURN FMAX", MOVE = "MOVE ABST50 FMAX", STAY = "STAY" }   # values of {move} per MOVE option (Heidenhain); Siemens {dir}: TURN = -1, STAY = 0
ROTARY_PATH_SHORTEST = "M126"      # D86; Siemens: rotary axes with DC() by the compiler, Fanuc: roll-over axis setting (empty = WARNING, kept as written)
ROTARY_PATH_FULL = "M127"
ROTARY_FEED_MM_MIN = "M116"        # Siemens: "FGREF[{axis}]={radius}" per rotary axis
ROTARY_FEED_DEG_MIN = "M117"
# retract before a tool change is an expansion rule now: [tool_change] pre = ["FUNC:RETRACT=RUN"] with RETRACT = { RUN = "L_FREI" } (section 5a)

[retract]                          # the RETRACT verb (D83)
MAX = "M140 MB MAX"                # bare RETRACT: to the axis limit; DMG: "L_FREI"; empty = computed machine-frame move when the kinematics is known, else ERROR
BY = "M140 MB{distance}"           # RETRACT=50; Siemens: none (computed move under TRAORI with the kinematics module)

[tolerance]                        # the TOLERANCE words (D85)
ON = "G5.1 Q1"                     # Heidenhain: "CYCL DEF 32.0 TOLERANZ\nCYCL DEF 32.1 T{tol}\nCYCL DEF 32.2 HSC-MODE:{mode} TA{rotary}"; Siemens: "CYCLE832({tol},{mode},{rotary})"
OFF = "G5.1 Q0"                    # Heidenhain: "CYCL DEF 32.0 TOLERANZ\nCYCL DEF 32.1 T0"; Siemens: "CYCLE832(0,0,1)"
mode = { FINISH = 0, ROUGH = 1 }   # value of {mode}; Siemens: FINISH = 1, ROUGH = 3; Fanuc: the tolerance value goes to a parameter and stays RAW

[raw]                              # builder codes the reader keeps as RAW with the builder's name, RAW:NAKAMURA (F23)
known = ["G411", "G300"]
```

Function values may carry a parameter: `HIGH_PRESSURE = { ON = "H7={value} M7", OFF = "M9" }` for a coolant pressure that is set with the function. Function values are strings; a bare integer is accepted and means `M` followed by the number. The loader normalizes every M and G code to its spelling without leading zeros (`8`, `08`, `M08` become `"M8"`; `G01` becomes `"G1"`), the compiler writes that form, and readers compare codes by number, so a source `M08` matches the table's `M8` (D105). The normalization applies to every M or G code token of a value, also inside a multi-word template with parameters or placeholders: `"M03 P11"` loads as `"M3 P11"`, `"G01 X{x}"` as `"G1 X{x}"`; text that is not an M or G code (`H7=`, `P11`, `L707(...)`) is kept as written. The same normalization applies to every template of the file that contains an M or G code, in particular `[format] program_end` and `sub_end` (section 2), the `[tool_change]` templates (section 3), `[sync]` and `[transform]`, so that the compiler never writes a leading zero it did not compute from a placeholder; NCX text in `pre` and `post` lists (5a) is not affected, it carries no native codes.

### 5a. Expansion rules

Some machines need a sequence where the program has one word. The example from the shop floor: the through-spindle coolant has a clutch that can only engage while the spindle stands still, so `COOLANT:THROUGH=ON` on that machine means stop the spindle, switch the coolant, start the spindle again with the speed it had. Every function state, the tool change and every catalog cycle may therefore carry four optional keys, and the expander (`ncx-virtual-machine.md`, section 1 and 3.10) turns them into generated NCX blocks around the triggering block:

```toml
[coolant]
THROUGH = { ON = "M51", OFF = "M9", requires = { SPINDLE = "OFF" }, restore = ["SPINDLE"] }

[func]
PALLET_CHANGE = { RUN = "M60", pre = ["HOME Z", "HOME X Y"], post = ["ORIGIN=1"] }

[tool_change]
pre = ["HOME Z"]                   # retract before the change; DMG: ["FUNC:RETRACT=RUN"] with RETRACT = { RUN = "L_FREI" }

# catalog entry
[[cycle]]
name = "PECK"
native = "G83"
pre = ["FUNC:PECK_MODE=RETRACT"]   # Doosan M291 before G83, as an NCX word so that the VM sees it
```

- `pre` and `post`: lists of NCX blocks inserted before and after the block. They are NCX text, not native lines, so the VM executes them and the reader can recognize them; native text goes through `RAW:BUILDER`. A block may contain `{position:NAME}`, which expands to the axis words of that entry of `[positions]`, e.g. `pre = ["RAPID {position:tool_change} FRAME=MACHINE"]` (D100).
- `requires`: state conditions that must hold while the function is written, as state variable = value (`SPINDLE = "OFF"`, `COOLANT = "OFF"`, `SPINDLE_MODE = "AXIS"`). The expander inserts the words that establish them, preceded by `@SAVE` of the variables named in `restore`.
- `restore`: the state variables to put back after the block, through `@RESTORE`; the VM re-applies the saved value from its own snapshot, so the rule never has to know the speed the spindle had. Restoring costs cycle time (a spindle stop and start), which the runtime estimate counts and a WARNING reports at compile time.

Readers use the same rules backwards: a source sequence `M5`, `M51`, `M3 S1500` on a machine whose rule says the spindle stop belongs to `COOLANT:THROUGH=ON` is read as the one NCX word, so the round trip does not accumulate stops. Plugins that need more than these four keys get the same insertion points in code (`ncx-virtual-machine.md`, section 7).

A named function is written as `FUNC:SUB_CHUCK=OPEN` in NCX and compiles to the M code of that state. Readers map M codes back to names; an M code that matches no entry becomes `MFUNC=n` with a WARNING. The states of a `[func]` entry are free identifiers, the values `FUNC:name=` takes (`OPEN_RUNNING`, `NORMAL`, `LOW` and the like); the states of the other function tables are the values of their word: `CW`, `CCW`, `OFF`, `ORIENT`, `RPM`, `VC`, `CSS_OFF`, `RPM_MAX` in `[spindle.ROLE]`, `AXIS` and `SPINDLE` in `[spindle_mode.ROLE]`, `ON`, `OFF` and `PHASE` in `[spindle_sync]`, `ON` and `OFF` in a `[coolant]` channel (F23).

## 6. Cycle catalog

```toml
[cycles]
catalog = "heidenhain-cycles.toml"  # controller family catalog, shipped with NCXchange, user-extendable

# excerpt of a catalog file
[[cycle]]
name = "RECT_POCKET"
native = 251
params = { LENGTH = "Q218", WIDTH = "Q219", DEPTH = "Q201", PECK = "Q202", CYCLE_F = "Q206", SURFACE = "Q203", CLEARANCE = "Q200" }
absolute_from_surface = ["DEPTH"]   # native Q201 is relative to Q203, NCX DEPTH is absolute

# excerpt of the Fanuc catalog
[[cycle]]
name = "ROUGH_TURN"
native = "G71"
contour = ["P", "Q"]                # first and last block of the contour, the SUB section of CONTOUR=name (D65)
```

The built-in drilling family (`DRILL`, `DRILL_DWELL`, `PECK`, `CHIP_BREAK`, `TAP`, `REAM`, `BORE`) is defined in the same way inside NCXchange and can be overridden per machine.

Besides `name`, `native`, `params`, `absolute_from_surface` and the expansion rule keys of 5a, an entry may carry:

- `modal = true`: the native cycle stays active after its block and every following block with a position calls it again, as Fanuc `G81`..`G89` and the simple turning cycles `G90`, `G92`, `G94` do; the reader emits `CYCLE=` and one `CYCLE_CALL` per such block and the compiler writes the repeat blocks back the same way (language 4.7.1). The default `false` is a native block that runs once where it stands (Fanuc `G70`..`G76`). Heidenhain and Siemens entries leave it out: their calls are words of their own (`CYCL CALL`, `M99`, `MCALL`).
- `contour`: the native words that carry the contour of `CONTOUR=name` (language 4.7, D65). Two words are the first and the last block of the contour range (Fanuc `P` and `Q`): the reader turns the range into a `SUB` section of the file, the compiler writes the block numbers of that section. One word names the contour subprogram itself (Siemens `CYCLE95` `NPP`).
- `signature`: every native parameter in the order the control writes it, also those without an NCX word: the positional parameters of a Siemens cycle, the `Q` parameters of a Heidenhain `CYCL DEF` (`../controllers/siemens.md` 12, `heidenhain.md` 8). A Siemens position without a value stays empty and the cycle takes its default (`siemens.md` 7). A native name in `params` that the signature does not list is an address word of its own: Siemens `CYCLE_F = "F"` is the modal `F` before the call that `CYCLE81`..`CYCLE83` use (controller-mapping 5). Fanuc entries, whose parameters are address words of the cycle block, leave it out.
- `fixed`: native parameters the entry always writes with this value, which tell two entries of one native cycle apart: Siemens `CYCLE83` with `VARI = 1` is `PECK`, with `VARI = 0` `CHIP_BREAK` (controller-mapping 5); the reader takes the entry whose fixed values the source block carries.

## 7. Variables

```toml
[variables]
unassigned = "error"               # error | 0
map = { Q = "#1", QL = "#", QR = "#5" }   # compile Heidenhain-named variables to Fanuc ranges: Q1 -> #101 (prefix + number), QR1 -> #501
block_cap = 1000000
call_depth = 8

[system_variables]                 # NCX SYS_ names to native system variables (D51); {index} is the NCX index
SYS_POS_X = "#5041"                # workpiece coordinates
SYS_MPOS_X = "#5021"               # machine coordinates; the Nakamura transfer program reads #5024 and #5025 for the sub spindle slide
SYS_MPOS_B = "#5024"
SYS_WEAR_Z = "#11{index:03}"       # #11099 = Z wear register 99 in the Nakamura transfer program; Siemens: "$AA_IW[X]", "RG{index}"; Heidenhain: FN 18 ids
SYS_PART_MAIN = ""                 # part status of the Nakamura G411 machines has no readable variable; G411 stays RAW
```

## 8. Job manifest and variable start values

```toml
# shaft.ncxjob.toml
[job]
name = "SHAFT"
machine = "nakamura-ntjx.toml"

[[channel]]
id = 1
file = "shaft.ncx"                 # the first program of the file runs unless `program` names another (D48)

[[channel]]
id = 2
file = "shaft.ncx"
program = "SHAFT_CH2"              # NAME of a PROGRAM section in that file; the two channel programs may also be two files

[shared]
spindles = ["S1", "S2"]             # commanded from both channels; the scheduler checks conflicts
axes = ["B1"]                       # machine axes commanded from both channels, checked like the spindles (D20, F24)
```

```toml
# pattern.vars.toml, start values for analyze
Q1 = 10
Q2 = 5
QS1 = "TEXT"
```

## 9. Kinematics (optional)

Read only by the kinematics module. A flat node list with `parent`; resources are leaves.

```toml
[[node]]
id = "X1"
parent = "BASE"
type = "linear"
direction = [1, 0, 0]

[[node]]
id = "Y1"
parent = "X1"
type = "linear"
direction = [0, 1, 0]

[[node]]
id = "TABLE1"
parent = "Y1"

[[node]]
id = "Z1"
parent = "BASE"
type = "linear"
direction = [0, 0, 1]

[[node]]
id = "S3"
parent = "Z1"
type = "rotary"
direction = [0, 0, 1]

[[node]]
id = "H1"
parent = "S3"
```

Or, for a long tree: `[machine] kinematics = "dmu50.kin.toml"`.

## 10. Project layout

```
my-shop/
  ncx.toml                 # working directory settings, plugin DLLs, the machine file --machine defaults to (the built-in default machine of D103 applies only when neither names one)
  machines/
    dmu50.toml
    fanuc-vmc.toml
    millturn1.toml
  cycles/
    heidenhain-cycles.toml
  programs/
    part-123.ncx
    part-123.vars.toml
  out/
    dmu50/part-123.h
    fanuc-vmc/part-123.nc
```

`ncx.toml` names the plugin assemblies (`plugins = ["MyShop.NcxPlugins.dll"]`) that subscribe to VM events (`ncx-virtual-machine.md`, section 7).

## 11. Example machine files

`examples/machines/` holds five configurations written from the manuals listed in `controller-mapping.md`, section 10: four from the builder manuals and `millturn1.toml` from the SINUMERIK 840D sl programming manuals alone (D104). They are sketches of the schema, not verified against a machine: `nakamura-ntjx.toml` (Fanuc 18i-TB, two turrets, two spindles, tool spindle with ATC; M and G codes from the Walter Meier list 2016 and the Nakamura programming manual 4810004EDG), `doosan-puma-2600sy.toml` (Fanuc, single channel, sub spindle, milling spindle selected by `P` on the M code), `mori-ntx1000-mapps.toml` (Fanuc 31i under MAPPS, tool spindle with `T` + `G361` change, turret 2, sub spindle on the A axis, `M34`/`M35`/`M36` synchronization in both channel programs; from the NTX1000SZM programming manual), `dmg-ctx-840d.toml` (Siemens 840D, GILDEMEISTER structure programming: `L7xx` cycles for C axes, synchronous run and tool change point, `TC` tool change, `WAITM` for two channels, the M list of the Technik Programmierung V2.1), `millturn1.toml` (a generic SINUMERIK 840D sl mill-turn without builder cycles: roles `MAIN` = `S1` with axis `C`, `SUB` = `S2` with axis `C2`, `TOOL` = `S3`, `TURRET1` = `H1`; axes `X Y Z C Z2 C2`; functions `SUB_CHUCK`, `MAIN_CHUCK`; numeric tools with `T{tool} D{offset}`; `COUPON`/`COUPOF` for `SPINDLE_SYNC`; `[workpiece]` with `SUB_frame = "datum"`; the machine of `examples/MILLTURN_TRANSFER.ncx`, which checks and compiles for it, D104).
