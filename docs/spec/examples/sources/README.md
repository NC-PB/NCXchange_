# Source programs of the examples

The two programs behind `examples/2.5D_FRAESEN.ncx`, as a CAM system wrote them for a Fanuc control (`2.5D_FRAESEN.fanuc.nc`, program `O0001`) and for a Heidenhain iTNC 530 (`2.5D_FRAESEN.h`, Klartext with the comma as decimal separator). The first Fanuc reader and the first Heidenhain reader must produce the same canonical NCX text from them, and the two compilers must write both back without loss; that is the acceptance test of milestone M4 (`architecture.md`, section 12).

`BOHREN.fanuc.nc` and `BOHREN.h` are the drilling test: the same holes with the drilling, pecking, tapping and reaming cycles of both controllers (`G81`..`G85` with `G98`/`G99` against `CYCL DEF 200`..`207` with `CYCL CALL` and `M99`), the case that shaped the cycle words of the language.

`3D_FRAESEN.fanuc.nc` and `3D_FRAESEN.h` are the same part programmed as a 3-axis 3D finishing job (834 blocks each, short linear moves, `G1` and `L` only after the header). They are the first performance and robustness inputs: the readers must not slow down on long point lists, and the segment length analytic has a known answer on them.

`NAKAMURA_WY250L_O1000.path1.nc` and `.path2.nc` are the two paths of one real job on a Nakamura WY-250L twin-turret lathe (Fanuc 31i-B, G-code system A), from the builder's jump-programming manual: the upper turret program `O1000` and the lower turret program `O1000.P-2` with wait marks `M106`..`M195`, `M199` start synchronization, block skip, `G53` with macro variables, `G10 L2` datum writes, `IF [...] GOTO`, `G4 U`, `G28 U0 B0`, `T0656`, `G54 M428`/`G59 M427` per spindle, the workpiece transfer with `M96` and the `G300` cut-off check, and `G411` jumps on the part status. They are the acceptance input of milestone M9 (jobs and channels) and the first test of `RAW:BUILDER`.

Two larger pairs exist outside the repository (a 5-axis A/C program of 1.2 MB and a 5.7 MB point-list program, both from the same CAM system); ask the maintainer for them when the analytics of milestone M7 are tested.

The files are kept as written by the CAM system, line endings and all; the readers must accept them as they are.
