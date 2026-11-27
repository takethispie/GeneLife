# Read-Model Staleness Guards

## Context

`CompaniesEqual` compared three of ten `CompanyStatus` fields, so office count, balance, reserved payroll, arrears, revenue, wages and solvency never produced a delta and clients silently held stale values (fixed at `ba4bb80`). It was found by hand. `DtoGuard` now reflects over the snapshot DTOs so an omitted comparison fails naming the field, and it covers both hand-written comparisons in the repo — but it stops at the DTOs.

Marten projections and read models carry the same silent-staleness risk: a field that stops being projected produces a page or an API response that is wrong without erroring. Nothing currently reflects over them.

## Design

1. **Reflect over the read models the way `DtoGuard` reflects over the DTOs.** For each projection, assert that every field of the projected shape is actually populated from the source — a field that is never written should fail, naming itself.
2. **Reuse the mechanic, not a copy of it.** `DtoGuard`'s value is that it probes properties generically and fails loudly on a value type it does not know. Extend that shape to projections rather than writing a second, weaker checker.
3. **Guard the drift, not the values.** The check is "is this field derived from a live source", not "does this field equal a constant" — a projection that hard-codes a value is exactly the failure being hunted.
4. **Assert the producer side too, where cheap.** The DTO defect was an omission in a comparison function; the projection equivalent is an omission in a projection definition. A test that fails when a projection stops mapping a field belongs next to the projection.
5. **Name what is not covered.** If a read model cannot be covered without heavy scaffolding, say so, rather than implying whole coverage.

## Tasks

- [ ] Enumerate the Marten projections and read models and identify which have fields derived from live sources.
- [ ] Generalise `DtoGuard` so it can probe a projection's shape as well as a DTO's properties.
- [ ] Add a guard per projection that fails naming any field which stops being populated.
- [ ] Add a producer-side test where a projection definition omits a field.
- [ ] Record any read model left uncovered, with the reason.

## Files

- `Genelife.Domain.Tests/Snapshots/DtoGuard.cs`
- `Genelife.Domain.Tests/Snapshots/` (guards)
- Marten projection definitions under `Genelife.Application/` and `Genelife.Infrastructure/`

## Contracts

- `DtoGuard`'s existing DTO coverage keeps passing unchanged.
- A projection field that stops being populated fails a test naming that field.
- Uncovered read models are named, not implied.

## Acceptance

- Dropping a field from a projection makes a test fail with the field named.
- The snapshot DTO guards still pass unchanged.
- The set of covered read models is recorded in `../summary.md`.
