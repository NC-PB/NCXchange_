# Jobs

The job scheduler (virtual machine 3.7, architecture 5.4; D15, D20, D39): the channel programs of a job manifest (`<name>.ncxjob.toml`, machine-config 8) run together, one virtual machine per channel, advanced in rounds in which every channel that is neither finished nor waiting executes one block.

Start with `JobRunner.cs`: `Add` makes the virtual machine of a channel, `Run` starts the channels and runs the rounds; `JobRunner.Words.cs` applies `SYNC` with `WITH`, `WAIT_CHANNEL` and `START_CHANNEL` after the block that carries them (language 4.8), `JobRunner.Waits.cs` releases the channels whose marks have come, all participants together with `SYNC_RELEASE`, and reports the deadlock naming the marks per channel. `SharedResources.cs` is the WARNING for two channels commanding one spindle or axis of `[shared]` between two marks. `ChannelRun.cs` is one channel while it runs (its virtual machine, `WaitingAt`, `Finished`), `JobResult.cs` what the job gives its caller: the channels with their diagnostics, and the timeline of `SYNC_WAIT` and `SYNC_RELEASE` events.

The virtual machine of a channel runs step by step (`../VirtualMachine/VirtualMachine.Steps.cs`), so that a channel can wait at a mark inside a called subprogram; what the scheduler asks of it is in `../VirtualMachine/VirtualMachine.Jobs.cs`. The diagnostic codes are `VM571`, `VM572` and `VM850` to `VM854` (`../Model/DiagnosticCodes.Jobs.cs`), with their rows in the channel family of `../VirtualMachine/Validation/ChannelValidation.cs`.

Never here: reading the manifest or a file (`Ncx.Config` loads the manifest, `Ncx.Cli` reads the files, `ncx check --job` and `ncx analyze --job`), and the output of a controller (the job compiler, P6-02).
