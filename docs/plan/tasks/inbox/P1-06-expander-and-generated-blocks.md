# P1-06 Expander and generated blocks

Phase: 1 | Milestone: M2 | Depends on: `P1-05` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

The stage between parser and VM that turns expansion rules into ordinary NCX blocks (D63).

## Scope

- `Expander` over the parsed program: for every block, the rules of the machine configuration (`pre`, `post`, `requires`, `restore` on functions, tool change and catalog cycles; phase 2 loads them, a hand-built rule set serves the tests now) and the `IProgramRewriter` hook (the interface in `Ncx.Core` with its caller, together with its signature types `RewriteResult` (`Unchanged`, `Replace`, `Surround(before, after, reason)`) and the `RewriteContext` abstraction, D106; the loader and the concrete context come in phase 7) produce generated blocks with their origin.
- The placeholder `{position:NAME}` in a `pre` or `post` block expands to the axis words of the named entry of the machine's `[positions]` table before the block is parsed (machine-config 5a, D100); an unknown name is an ERROR on the rule.
- The pseudo-words `@SAVE=key` and `@RESTORE=key` (VM section 3.10), the value a state key `KEY[:ADDR]` naming a state variable of the channel, parsed only under the option the expander uses for generated text (D95): a restore stack per state variable, re-applied as if the program had written the words; pseudo-words in a user file are an ERROR.
- Generated blocks execute in both modes, count for analytics, show in trace and annotate with their origin, are never written by `format`.

## References

- ncx-virtual-machine.md sections 1, 3.10
- ncx-language.md section 4.15
- machine-config.md section 5a
- architecture.md section 5.5

## Done when

- The coolant clutch example: `COOLANT:THROUGH=ON` with `requires = { SPINDLE = "OFF" }` and `restore = ["SPINDLE"]` produces stop, coolant, restart with the previous speed, and the VM state after it equals the state without the rule plus the coolant.
- `HOME Z` before a tool change from `[tool_change] pre`.
- `pre = ["RAPID {position:tool_change} FRAME=MACHINE"]` with `tool_change = { X = 0, Z = -120 }` inserts `RAPID X=0 Z=-120 FRAME=MACHINE` (D100).

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
