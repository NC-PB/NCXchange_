# Jobs

The Nakamura WY-250L pair as one job, the acceptance of P6-01 (implementation 16; milestone M9): `NakamuraJobTests.cs` reads both paths with the Fanuc reader and `nakamura-ntjx.toml`, writes and parses them again, and runs them with the manifest of `../Fixtures/nakamura-wy250l.ncxjob.toml` in both modes, without deadlock and with every mark paired; `JobFixture.cs` reads the fixtures of `../Fixtures/`. `ncx check --job` and `ncx analyze --job` on the same pair are in `../Cli/JobCommandTests.cs`.

Never here: a rule of the scheduler on its own (`../../Ncx.Core.Tests/Jobs/`), output written anywhere but a temporary folder.
