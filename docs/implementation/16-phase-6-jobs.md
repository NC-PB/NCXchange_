# Phase 6: jobs and channels

Status: written 2026-09-11; open on 2026-09-18. P6-01 is in `../plan/tasks/done/`; P6-02 is on `main` (496e752) and waits for two answers on `nakamura-ntjx.toml`: the `CSS_OFF` of `[spindle.SUB]` and D243. Milestone M9. Tasks P6-01, P6-02. Closed when the two Nakamura WY250L programs read as a job, analyze without deadlock and compile back with their wait codes (`../plan/phases.md`).

## Entry state

P4-01 (the INTERPRETED loop) and P3-06 (the Fanuc compiler) closed; in the plan's order, phases 3, 4 and 5 are closed. The Nakamura pair converts as single files since P3-02 (with `RAW:NAKAMURA` for the builder macros) and `nakamura-ntjx.toml` carries the builder tables (`[sync]`, `[spindle_sync]` with `channel = 2`, `[workpiece]`, `[raw]`).

## Decisions needed first

None new. D15, D20, D39, D56 govern the phase; the document fix F24 (one manifest name, `[shared] axes`, `--job` on three commands) lands in P6-01 if P2-01 has not done it.

## Tasks

### P6-01 Job scheduler and `SYNC`

Files in `src/Ncx.Core/Jobs/`: `JobRunner` (one `VirtualMachine` per channel program from the `JobManifest`, the shared resources, rounds per VM 3.7 and architecture 5.4: every channel that is neither finished nor waiting executes one block; `SYNC=m` marks the channel waiting at m with its `WITH` set (default all); when every participant waits at m all are released and `SYNC_RELEASE` is raised; marks matched in execution order, a mark reused freely; `WAIT_CHANNEL=c` waits for c to finish; `START_CHANNEL` starts a channel's program; all channels waiting with no releasable mark is the deadlock ERROR naming the marks per channel; `SYNC` in a single-channel run a WARNING; two channels commanding one shared spindle between two marks a WARNING), `ChannelRun` (the VM, its program, `WaitingAt`, `Finished`), `JobResult` (per channel diagnostics and timeline events); `ncx analyze --job <name.ncxjob.toml>` and `ncx check --job` in `Ncx.Cli` (the job's `machine` from the manifest, `--machine` overriding).

Cite: VM 2.8, 3.7; architecture 5.4; machine-config 8; language 4.8, 4.14; D15, D20, D39.

Tests first (`tests/Ncx.Core.Tests/Jobs/`): a two-channel job with three marks in different orders passes and the release rounds are as expected; a job whose channels wait at different marks reports the deadlock naming both marks; a mark used twice pairs in execution order; `WITH=1,2` on a three-channel job releases without channel 3; `WAIT_CHANNEL`; a shared spindle commanded from both channels between marks warns; the acceptance: the Nakamura pair as a job (`tests/Ncx.Acceptance/Fixtures/nakamura-wy250l.ncxjob.toml` naming the two converted files) analyzes without deadlock, with the `M106`..`M195` marks paired and `M199` as the start synchronization.

### P6-02 Job compiler and channel binding

Files: `src/Ncx.Compilers/JobCompiler.cs` (per channel a compile through the family compiler with a shared view of the job: `SYNC` written as the wait code from `[sync]` with the path list or bitmask, `start_mark` as the first block of every channel program, `groups` for a second wait range; channel-bound words (`channel = n` on a table) moved to the owning channel's program at the same mark, words with `channels = "all"` duplicated into every channel program behind a generated `SYNC` (D56); a single-channel compile of a channel-bound word an ERROR), `ChannelOutputNaming` (`O1000`, `O1000.P-2` per the Nakamura convention from `[machine] channels` and a `[format]` key; `_C1`/`_C2` for the STAMA form; `%_N_1000_MPF`/`%_N_2000_MPF` for Siemens), `ncx compile --job` in `Ncx.Cli`.

Cite: VM 3.8 rule 2a; controller-mapping 7; machine-config 4, 5 (`[sync]`, `[spindle_sync]` `channel`, `channels`), 8; D56.

Tests first: a two-channel NCX job with a `SPINDLE_SYNC` word in channel 1 and `[spindle_sync] channel = 2` compiles the word into channel 2's program at the same mark; a `channels = "all"` word appears in both programs behind a generated wait; a single-file compile of such a word is the ERROR of D56; the acceptance: the Nakamura pair compiles back with its wait codes in the same order and the synchronization words in the path that owns them (M9), compared under the comparison rules with the builder `RAW` lines verbatim.

## Risks and open ends

- The Nakamura programs use `G411` jumps on the part status and `#5025`-based moves; in INTERPRETED mode those need the vars file or the `RAW` blocks are skipped with a WARNING. The job acceptance is "no deadlock", not "every block executes"; say so in the report.
- Moving a channel-bound word changes the block count of both programs and therefore the line numbers the diagnostics cite; a diagnostic on a moved word cites the destination block as its line and the block the word came from as `OriginLine`, rendered like a generated block (D98).
- The timeline analytic (architecture 9) needs the runtime estimate per channel and the waits; it is small once P4-02 and P6-01 exist and is added here as part of `analyze --job` if time allows, otherwise recorded as a follow-up task `P6-03` in `tasks/inbox/`.

## Exit checklist

- `phases.md` row 6: the Nakamura pair reads as a job, analyzes without deadlock, compiles back with its wait codes.
- `--job` on `compile`, `check`, `analyze` in the CLI table; the manifest documented once.
- P6-01, P6-02 in `done/`; F24 marked resolved.

## Log

(filled when the phase starts and when it closes)
