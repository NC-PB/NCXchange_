# Jobs

The Nakamura WY-250L pair as one job, the acceptance of P6-01 (implementation 16; milestone M9): `NakamuraJobTests.cs` reads both paths with the Fanuc reader and `nakamura-ntjx.toml`, writes and parses them again, and runs them with the manifest of `../Fixtures/nakamura-wy250l.ncxjob.toml` in both modes, without deadlock and with every mark paired; `JobFixture.cs` reads the fixtures of `../Fixtures/`. `ncx check --job` and `ncx analyze --job` on the same pair are in `../Cli/JobCommandTests.cs`.

`NakamuraJobCompileTests.cs` is the acceptance of P6-02: the same pair compiled back by the job compiler with the Fanuc compiler, its wait codes in the order of the sources, `M96` and `M97` in the program of path 2, which `[spindle_sync] channel = 2` binds them to, and the builder `RAW` lines verbatim. It compiles for `nakamura-ntjx.toml` with the two templates the Fanuc compiler still asks for filled in (`[spindle.SUB] CSS_OFF` and the incremental letter of `B`, D243; the `TODO(question)` of the class). `ncx compile --job` is in `../Cli/JobCommandTests.cs` as well.

Never here: a rule of the scheduler on its own (`../../Ncx.Core.Tests/Jobs/`), output written anywhere but a temporary folder.
