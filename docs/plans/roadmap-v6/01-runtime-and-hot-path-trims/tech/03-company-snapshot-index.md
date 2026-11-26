# Company Snapshot Index

## Context

`BuildCompanySnapshots` constructs and runs an `Office` query inside the per-company loop (`Genelife.Application/Services/SimulationEngine.cs:5897-5905`). Its cost is O(companies × offices). At a few hundred companies and a few hundred offices this is a large, avoidable per-tick cost.

## Design

Build an office count lookup once per snapshot, then read it per company:

1. Iterate all `Office` entities once and build a `Dictionary<Entity, int>` of office counts per company (or the key the snapshot needs).
2. In the company loop, read the count from the dictionary instead of running a query.

The dictionary is local to the snapshot build and discarded after it. No new persisted state.

## Tasks

- [ ] Add a single office pass before the company loop.
- [ ] Replace the per-company query with a dictionary lookup.
- [ ] Confirm the resulting `WorldSnapshot` is byte-identical to the current output.
- [ ] Run the 5k `LoadHarness` and record the snapshot build time and allocation.

## Files

- `Genelife.Application/Services/SimulationEngine.cs`

## Contracts

- `WorldSnapshot` and company DTO shapes are unchanged.
- Snapshot contents are identical; only the cost changes.

## Acceptance

- Snapshot build time scales with companies plus offices, not their product.
- The 5k run shows a lower snapshot build time or lower allocation.
- Snapshot equality tests pass.
