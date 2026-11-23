# R02 — Relationship index

**Phase:** 2 — Foundation
**Depends:** R01
**Estimate:** 1–2 sessions
**Status:** Planned (V4)

## Goal

Provide O(1) lookups for partners, parents, children, guardians and wards, and rebuild the index exactly on restore.

## Context

`EmploymentIndex` and `HousingRegistry` are derived indices rebuilt from components. Relationships should follow the same pattern: components are authoritative, the index is a read cache, and no system mutates the index outside an apply phase.

## Design

### Index

```csharp
public sealed class RelationshipIndex
{
    public Dictionary<Entity, List<Entity>> PartnersBySim { get; } = new();
    public Dictionary<Entity, List<Entity>> ParentsBySim { get; } = new();
    public Dictionary<Entity, List<Entity>> ChildrenBySim { get; } = new();
    public Dictionary<Entity, List<Entity>> GuardiansBySim { get; } = new();
    public Dictionary<Entity, List<Entity>> WardsBySim { get; } = new();
    public Dictionary<Entity, List<Entity>> ExPartnersBySim { get; } = new();
}
```

All lists are maintained in stable order (network id, then entity id) so pairing and display do not depend on query order.

### API

- `PartnersOf(sim)`, `ParentsOf(sim)`, `ChildrenOf(sim)`, `GuardiansOf(sim)`, `WardsOf(sim)`.
- `HasPartner(sim)`, `IsParentOf(a, b)`, `AreSiblings(a, b)`.
- `AncestorsOf(sim, maxDepth)` for consanguinity.
- `RelationshipCount`, `ComponentCount` for invariants.

### Rebuild

- `Rebuild(world)` clears and scans all `Relationship` components.
- `OnCreated(edge)` and `OnRemoved(edge)` update the lists in apply phases.
- Restore always rebuilds; the index is never persisted as authoritative state.

### Derived relationships

Siblings and grandparents are computed, not stored:

- siblings share at least one parent;
- grandparents are parents of parents;
- in-laws are derived from partner plus parent edges.

Derived queries are used by eligibility and the admin page; they must not create edges.

## Tasks

- [ ] Add `RelationshipIndex` with the dictionaries and lookup methods.
- [ ] Add `Rebuild`, `OnCreated` and `OnRemoved`.
- [ ] Add stable ordering helpers.
- [ ] Add ancestry and sibling queries.
- [ ] Wire the index into `SimulationEngine` and restore.
- [ ] Add rebuild and mirror-consistency tests.

## Files

- new `Genelife/Family/RelationshipIndex.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Family/RelationshipIndexTests.cs`

## Acceptance

- Index lookups match the edge components after every apply phase.
- Rebuild after restore produces identical lists.
- Sibling and ancestor queries return only living or historical sims as configured.
- No entity is added or removed inside a query callback.
- Removing an edge removes both sides and the index entries.

## Testing

- Build a family, rebuild the index and compare every lookup.
- Remove a parent edge and assert both directions.
- Query siblings for half-siblings and full siblings.
- Query ancestors at depth 1 and 3.
- Assert index counts equal component counts.