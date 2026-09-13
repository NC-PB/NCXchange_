# Model tests

The records of `src/Ncx.Core/Model/`: a block read back through `Find` and `Has` (`BlockTests`, D106), words and values with their canonical text (`WordTests`, `ValueTests`, `ToolRefTests`), a program with its sections and trivia (`NcxProgramTests`), diagnostics rendered as `file(line): ERROR PAR003: message` and `file(line, from 12): ...` (`DiagnosticsTests`, D98). `DiagnosticCodesTests` checks that every code of every part of `DiagnosticCodes` has its prefix and three digits and that no two share a code.
