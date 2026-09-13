# P7-02 Plugin template and `ncx plugin` commands

Phase: 7 | Milestone: M10 | Depends on: `P7-01` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The lowest hurdle for an NC programmer: a working plugin to change one method in.

## Scope

- `templates/ncx-plugin/` per code-guidelines section 11: the project, one `.cs` with a rewriter that does something visible, one test, a README with the six steps, `ncx.toml` snippet; `dotnet new` template packaging optional.
- `ncx plugin new <name>` copies and renames; `ncx plugin build` builds into `plugins/` and registers the line in `ncx.toml`; `ncx plugin check <dll>` loads and lists the interfaces; `ncx plugin test` runs the plugin's tests.
- `samples/plugins/`: the coolant clutch rule, a `BLOCK_WRITE` writer that puts Z on its own line, a source rule that folds `M5`, `M51`, `M3 S` into `COOLANT:THROUGH=ON`.
- `docs/plugins.md`: the six steps, the four interfaces with one example each, what a plugin may not do (D61).

## References

- code-guidelines.md section 11
- architecture.md sections 9 and 10

## Done when

- The template builds and runs unchanged after `ncx plugin new`; the three samples build and their tests pass; `ncx compile` prints the INFO diagnostic (D98) `plugin <name>: inserted 2 blocks at line 12` for the clutch sample (M10).

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
