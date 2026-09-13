# PL-02 Further controllers and dialects

Phase: later | Milestone: later | Depends on: `P7-03` | Size: L (S: a day or two, M: up to a week, L: more)

## Goal

What the sample corpus contains beyond the three families of 1.0.

## Scope

- Mazak EIA specifics (`G41.2`, `G5 P2`), Matsuura precision modes, Heidenhain turning (TNC 640, `FUNCTION MODE TURN`, cycles 8xx), GILDEMEISTER structure programming as a Siemens dialect (D68), Okuma OSP (a new family), the TNC7 Klartext.
- Each as a dialect table or a reader variant, never as a change of the language without a decision.

## References

- controllers/*.md
- decisions D68, D69

## Done when

- One corpus file of the family converts without `RAW` for the constructs the task names.

## Log

(who, when, what was decided while doing it; a decision that changes the specification gets a row in `../../../decisions/decisions.md`)
