# Glossary

Terms of the project and of the shop floor, as the documents use them.

| Term | Meaning |
|---|---|
| NCX | The controller-independent program format of this project; a text file of blocks of bare words. |
| Block | One line of an NCX file that carries at least one word (a blank or comment-only line is trivia, not a block, D92), or one line of a controller program (Fanuc `N10 G1 X10.`, Heidenhain `12 L X+10 F500`, Siemens `N10 G1 X10`). |
| Word | `KEY`, `KEY=VALUE` or `KEY:ADDR=VALUE` in NCX; an address letter with a value in G-code. |
| Verb | The word that makes a block a motion or frame block: `RAPID`, `LINE`, `ARC`, `RETRACT`, `HOME`, `CYCLE_CALL`, `SHIFT`, `TILT`, `TILT_AXIS`, `SETPOS`. At most one per block. |
| Modal | A state that stays until changed (feed, tool, plane, active cycle), as opposed to a block word that applies to its block only. |
| Reader | The component that turns a controller program into NCX (Fanuc, Heidenhain, Siemens readers). Also called a front end. |
| Compiler | The component that turns NCX into a controller program for one machine described by a machine configuration. Also called a back end or, in CAM, a postprocessor. |
| Virtual machine (VM) | The component that knows the full state of a program at every block; used by readers (source-side state), compilers (target state) and analytics. STATIC mode walks each program once, following every `CALL` into its subprogram with the caller's state and leaving jumps, repeats and expressions unresolved (D99); INTERPRETED mode executes it. |
| Expander | The stage between parser and VM that applies expansion rules and plugin rewriters and inserts generated blocks. |
| Generated block | An NCX block inserted by the expander, carrying its origin; executed like any block, never written by `ncx format`. |
| Diagnostic | A message from any stage with a severity (ERROR stops the run, WARNING is reported and the run continues, INFO is a note that is neither, such as a plugin's inserted blocks), a code of an area prefix and three digits (`VM042`), the file and the line, and for a generated block the line it was generated for; rendered `file(line): ERROR VM042: message` (D98). |
| `@SAVE`, `@RESTORE` | Pseudo-words only the expander writes: push and pop a state variable so that a rule can restore what it had to change (the spindle after a coolant clutch). |
| Machine configuration, machine file | The TOML file that describes one machine: controller, dialect, output format, templates, roles, resources, axes, functions, cycle catalog, variables. |
| Template | A string with `{placeholders}` in the machine file, rendered by compilers and matched by readers (`"G340 T{tool:02}{offset:02}. A{next:02}."`). |
| Role | The name a program uses for a resource: `MAIN`, `SUB`, `TOOL`, `TURRET1`, `TABLE`; the machine file maps roles to the machine's own ids. |
| Resource | A spindle, a tool holder, a table or an axis of the machine with its own state in the VM. |
| Channel, path | One program stream of a multi-channel machine (Siemens channel, Fanuc path); a job runs one program per channel. |
| Job, job manifest | Several channel programs that run together, described by a `<name>.ncxjob.toml`. |
| Wait mark, `SYNC` | A rendezvous of channels: each waits at the mark until all participants have reached it (Fanuc `M100`..`M199`, Siemens `WAITM`). |
| Transform chain | The ordered list of `SHIFT`, `ROTATE`, `MIRROR`, `TILT`, `TILT_AXIS` entries after `ORIGIN` that make up the workpiece frame, in program order (D31). |
| Datum, origin, preset | The workpiece zero point selected by `ORIGIN` (`G54`, cycle 247, `G54`..`G599`). |
| Tilted working plane, swivel | A working plane rotated in space (Heidenhain `PLANE`, Siemens `CYCLE800`, Fanuc `G68.2`); by spatial angles (`TILT`) or by rotary axis positions (`TILT_AXIS`). |
| TCPM, TCP, RTCP | Tool center point management: the control keeps the tool tip on the programmed path while the rotary axes move (`M128`, `TRAORI`, `G43.4`). |
| Tool vector | The direction of the tool axis in the workpiece frame (`TX TY TZ`), used in 5-axis simultaneous programs instead of rotary axis positions. |
| Surface normal | The normal of the machined surface at the tool contact point (`NX NY NZ`), used for 3D radius compensation. |
| Preload | Bringing the next tool to the change position without changing (`PRELOAD`, Fanuc `T5` alone, Heidenhain `TOOL DEF`). |
| Offset | The tool length and radius registers (`OFFSET:LEN`, `OFFSET:RAD`) or the combined register of a lathe (`OFFSET`, the last two digits of `T0656`, Siemens `D`). |
| Turret, ATC | A lathe's tool carrier with fixed stations (turret, `T0101`), or an automatic tool changer with a magazine (ATC, mills and tool spindles). |
| Main spindle, sub spindle, counter spindle | The work spindles of a lathe: the main spindle holds the bar or blank, the sub (counter) spindle takes the part over for the second side. |
| Tool spindle, driven tool | The milling spindle of a turn-mill center, or a rotary tool in a turret station. |
| Workpiece transfer | Handing the part from the main to the sub spindle: spindle synchronization, the slide moves, chucks open and close, cut-off. |
| Spindle synchronization, phase | Two spindles running at the same speed (`SPINDLE_SYNC`), optionally with a fixed angular offset (`PHASE`), for the transfer of polygonal parts. |
| C axis, spindle mode | A work spindle switched from rotating (`SPINDLE`) to positioning as a rotary axis (`AXIS`, `SPINDLE_MODE`). |
| Polar interpolation, TRANSMIT | Milling a face with X and C driven as a Cartesian pair (`POLAR`, Fanuc `G12.1`, Siemens `TRANSMIT`). |
| Cylinder interpolation, TRACYL | Milling on a cylinder surface with the C axis unrolled to a length (`CYLINDER`, Fanuc `G7.1`, Siemens `TRACYL`). |
| Diameter programming | Lathe convention that X values are diameters (`DIAMETER=ON`, Siemens `DIAMON`); the VM stores radii. |
| Constant surface speed, CSS | The spindle speed follows the diameter so that the cutting speed stays constant (`CSS`, `VC`, `G96`), limited by `RPM_MAX` (`G50 S`, `LIMS`). |
| Canned cycle, fixed cycle | A drilling, tapping, boring or turning sequence the control performs from a few parameters (`G81`, `CYCL DEF 200`, `CYCLE81`); NCX defines with `CYCLE=` and calls with `CYCLE_CALL`. |
| Cycle catalog | The per-controller data file that names the cycles beyond the built-in drilling family and maps their parameters. |
| Native cycle, native parameter | A cycle written for one controller family as `CYCLE:<controller>=n`; every key of that block the catalog does not know is a native parameter, kept in source order after the cycle words and left unresolved by the virtual machine; compiles only to that family, an ERROR for another like `RAW` (D94). |
| `RAW` | Source text NCX cannot express, kept verbatim with the controller or builder it belongs to (`RAW:FANUC`, `RAW:NAKAMURA`); a WARNING on read, an ERROR when compiled for another controller. |
| Reader rule | A plugin hook that decides what a source sequence means on one particular machine (an M code that runs a hidden subprogram). |
| Expansion rule | `pre`, `post`, `requires`, `restore` keys in the machine file that make the expander generate blocks around a function, a tool change or a cycle. |
| Machine builder, dialect | The manufacturer of the machine (Nakamura, DMG MORI, Hermle) and the M codes, macros and cycles it adds to the control's language. |
| GILDEMEISTER structure programming | DMG's house programming structure on SINUMERIK lathes (`L7xx` cycles, `TC(...)`, `RG` parameters); documented, not implemented in 1.0. |
| Klartext | Heidenhain's conversational programming format (`L X+10 Y+5 F500`). |
| DIN 66025, ISO, EIA | The G-code standard and the names the trade uses for G-code programming on any control. |
| Custom macro B | Fanuc's variable and flow language (`#` variables, `IF`, `WHILE`, `G65`). |
| Q parameters | Heidenhain's variables (`Q1`, `QL1`, `QR1`, `QS1`) and the `FN` functions on them. |
| R parameters | Siemens' arithmetic variables (`R1`), next to the declared `DEF` variables. |
| Block skip | The optional block switch: a block with `/` in front runs only when the switch is off (`SKIP`). |
| Program end, file end | `PROGRAM=END` (M30, M2, M17: executed) against `FILE=END` (the closing `%`, the end of the file). |
| Segment length, tool vector change | The two 5-axis analytics: the distance between consecutive end points, and the angle between consecutive tool vectors. |
| Trace, annotate | Derived outputs of the VM: one row per changed state variable per block, or the program with the previous values as comments. No history is stored in the program (D16). |
| Definition of done | The checklist of `architecture/code-guidelines.md` section 12 that every change has to pass. |
