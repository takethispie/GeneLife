# R01 — Relationship domain model

**Phase:** 1 — Foundation
**Depends:** T01 traits
**Estimate:** 1–2 sessions
**Status:** Done

## Goal

Define relationships as directed edge entities so a sim can have two parents, several children, a partner and a guardian without fixed component slots.

## Context

`HouseholdMember.Role` already has `Partner` and `Child`, but it is a household role, not a relationship. It disappears when a member leaves the household and cannot express a parent who lives elsewhere. Inheritance currently uses household membership because no parent edge exists.

## Design

### Kinds

```csharp
public enum RelationshipKind : byte
{
    Parent,
    Child,
    Partner,
    ExPartner,
    Guardian,
    Ward
}
```

### Source

```csharp
public enum RelationshipSource : byte
{
    Seed,
    Attraction,
    Admin,
    Migration,
    Death
}
```

### Edge component

```csharp
namespace Genelife.Components.Family;

public record struct Relationship
{
    public Entity From;
    public Entity To;
    public RelationshipKind Kind;
    public long SinceTick;
    public RelationshipSource Source;
}
```

Each edge is its own entity with a `NetworkId`. The edge entity is what persistence and the admin read model reference.

### Mirror rules

| Kind | Mirror |
|---|---|
| `Parent` | `Child` on the other sim |
| `Child` | `Parent` on the other sim |
| `Partner` | `Partner` on the other sim |
| `Guardian` | `Ward` on the other sim |
| `Ward` | `Guardian` on the other sim |
| `ExPartner` | `ExPartner` on the other sim (historical, not active) |

`RelationshipService.CreatePair` creates both edges atomically and updates the index. `EndPartnership` marks the partner edges as `ExPartner` and removes the active partner index entry.

### Constraints

- No self-edge.
- No duplicate `(From, To, Kind)` edge.
- At most one active `Partner` per sim in V4a.
- No ancestry cycle: a sim cannot become a parent, grandparent or guardian of an ancestor.
- Parent and child edges survive household changes, moves and death.
- A dead sim's edges remain for history and inheritance until cleanup policy says otherwise.

## Tasks

- [ ] Add `RelationshipKind`, `RelationshipSource` and `Relationship`.
- [ ] Add `RelationshipService.CreatePair`, `EndPartnership` and `RemoveEdge`.
- [ ] Allocate a `NetworkId` for every edge entity.
- [ ] Add validation for self, duplicate, cycle and partner exclusivity.
- [ ] Add test fixtures for a simple family.
- [ ] Document that household roles are not relationships.

## Files

- new `Genelife/Components/Family/Relationship.cs`
- new `Genelife/Family/RelationshipKind.cs`
- new `Genelife/Family/RelationshipService.cs`
- `Genelife.UnitTests/Family/RelationshipDomainTests.cs`

## Acceptance

- Creating a parent creates the child mirror.
- Creating a partner creates both partner edges.
- Duplicate and self edges are rejected.
- An ancestry cycle is rejected.
- A second active partner is rejected.
- Ending a partnership creates or marks `ExPartner` edges and removes the active partner relation.

## Testing

- Create each edge kind and assert the mirror.
- Attempt a duplicate and a self-edge.
- Build a three-generation chain and attempt a cycle.
- End a partnership and assert the index and edges.
- Assert edge `NetworkId`s are unique.