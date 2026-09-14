# Jobs

The tests of the job scheduler of `src/Ncx.Core/Jobs/` (virtual machine 3.7, architecture 5.4): `JobHarness` runs the files of a test as the channels 1, 2, ... of one job on the built-in machine of D103, with a listener on every channel that records the events of all channels in the order they were raised; `JobSetup` says what the manifest and the run give beyond the files (`[shared]`, the `[[channel]]` tables, STATIC or INTERPRETED).

Open first: `JobRoundTests.cs`, the rounds and the release of three marks in every order; then `DeadlockTests.cs`, `ChannelWordTests.cs` (`WITH`, `WAIT_CHANNEL`, `START_CHANNEL`, the channels a job names), `SharedResourceTests.cs`, `SyncEventTests.cs` and `StaticJobTests.cs`.

Never here: a machine file or a job manifest from TOML (`../../Ncx.Config.Tests/` loads them), the Nakamura pair (`../../Ncx.Acceptance/Jobs/`).
