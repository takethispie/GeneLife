# R06 — Attraction and pairing system

**Phase:** 6 — Pairing
**Depends:** R03–R05
**Estimate:** 2 sessions
**Status:** Planned (V4)

## Goal

Turn scored candidates into partnerships deterministically, with mutual choice, cooldowns and no duplicate edges.

## Context

Candidate generation and scoring are pure functions. Pairing is an apply-phase system: it creates edge entities, updates the relationship index and may trigger a household merge.

## Design

### Systems

| Order | System | Cadence | Responsibility |
|---|---|---|---|
| 355 | `RelationshipExposureSystem` | 100 | refresh candidate caches |
| 360 | `AttractionSystem` | 300 | score cached candidates, keep top N |
| 365 | `PairingSystem` | 300 | decide, accept, create edges |

`AttractionSystem` does not create entities. It writes a per-sim scored list into the cache or a small component.

### Pairing algorithm

1. Collect eligible singletons, sorted by `NetworkId`.
2. For each, take the top-scored eligible candidate above `PairingThreshold`.
3. Build a proposal map. Resolve **mutual top choices** first: if A's top is B and B's top is A, pair them.
4. For the remaining sims, iterate in `NetworkId` order and pair greedily when both are still single and above threshold.
5. For each proposed pair, make one deterministic acceptance roll per sim from `attraction.pairing`, keyed by the canonical pair and tick. If either rejects, record a cooldown and skip.
6. Create both partner edges atomically, update the index, emit events, invalidate caches.
7. Attempt a household merge (R07), but do not roll it back if capacity is unavailable.

### Cooldowns

- A rejected pair is remembered for `RejectionCooldownMonths`.
- A paired sim is removed from the eligible pool immediately.
- Cooldowns reset on restore; they are a pacing mechanism, not historical state. Document this.

### Ties and determinism

- Sort by score descending, then `NetworkId` ascending.
- Use canonical pair order (lower `NetworkId` first) for the roll and for the event payload.
- Never iterate a `Dictionary` without sorting.
- Do not depend on entity ids across runs when a network id exists.

### Seeded family path

- The generator uses `SeedFamilyBuilder` with the same eligibility and scorer.
- It pairs a deterministic share of adults; it does not run the quarterly system before the first tick.
- Seeded edges use `RelationshipSource.Seed`.

### Failure handling

- If an edge creation fails validation (race impossible in a single-threaded apply phase), skip the pair and record a health issue.
- If one partner dies between proposal and apply, skip.
- If a household merge fails, keep the partnership.

## Tasks

- [ ] Add `AttractionSystem` and `PairingSystem` at orders 360/365.
- [ ] Add scored-candidate storage and top-N selection.
- [ ] Add mutual-top resolution and deterministic greedy fallback.
- [ ] Add acceptance rolls and cooldowns.
- [ ] Add edge creation, index update, events and cache invalidation.
- [ ] Add seeded family builder using the same scorer.
- [ ] Add replay and duplicate-edge tests.

## Files

- new `Genelife/Systems/AttractionSystem.cs`
- new `Genelife/Systems/PairingSystem.cs`
- new `Genelife/Family/SeedFamilyBuilder.cs`
- new `Genelife/Family/PairingDecisions.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Family/PairingSystemTests.cs`

## Acceptance

- A pair above threshold forms a partnership when both accept.
- A rejection creates a cooldown and no edge.
- Mutual top choices pair before greedy fallback.
- No sim has two active partners.
- No duplicate partner edges are created.
- The same seed and options produce the same partnerships.
- A household merge failure does not undo the partnership.

## Testing

- Deterministic small world with known scores and rolls.
- Mutual-top vs greedy scenarios.
- Threshold boundary and cooldown expiry.
- Replay the same seed twice and compare the partnership set.
- Assert event order and index consistency after each pair.