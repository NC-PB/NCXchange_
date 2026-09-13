# P1-04 Validation rules and diagnostics

Phase: 1 | Milestone: M2 | Depends on: `P1-03` | Size: M (S: a day or two, M: up to a week, L: more)

## Goal

Every ERROR and WARNING of section 5 of the virtual machine, each with a `VM` diagnostic code (D98) and a test.

## Scope

- The ERROR list (run stops) and the WARNING list (run continues) implemented where the state is known, each rule a `VM` code in `DiagnosticCodes` (D98) listed in the generated `docs/spec/generated/diagnostics.md` that the documentation can cite; the "spindle OFF before a `LINE`" rule names the holder's spindle.
- Inside a subprogram that no program of the file calls the caller-dependent rules are suppressed: `LINE` without feed, motion before `UNITS`, tool and offset rules, cycle rules, incremental word from an unknown position, spindle OFF before a `LINE` (D99).
- A role, function or machine axis the built-in default machine lacks, when no machine file is given, is the WARNING "not checked: no machine file", once per name, and the word runs against a resource created on the spot (a work spindle with a rotary axis of its own); with a machine file it stays the ERROR of VM 5 (D103).
- Machine limits (`rpm_min`/`rpm_max`, `max_feed`, axis `limits`) as WARNING, or clamped by the expander under `limits = "clamp"` with the WARNING saying so (D64).
- Unreachable block after an unconditional `JUMP` (D89), `JUMP=END` from inside a subprogram, `RETURN` in a program, `SYNC` in a single-channel job.

## References

- ncx-virtual-machine.md section 5
- ncx-language.md section 4.13 (rules)

## Done when

- One test per rule, named after the rule, with the smallest input that triggers it.
- The five examples check with no ERROR without a machine file (D103); the WARNINGs are those the notes and D100/D103 name.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
