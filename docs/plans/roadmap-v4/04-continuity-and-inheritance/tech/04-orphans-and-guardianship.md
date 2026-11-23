# C04 — Orphans and guardianship

**Phase:** 4 — Family
**Depends:** C02, C03, `HousingRegistry`, shelter system
**Estimate:** 2 sessions
**Status:** Done — relative/state-care hierarchy and guardian moves implemented

## Goal

Ensure every minor has a responsible adult or an explicit state-care record, without inventing an orphanage entity or a parallel household system.

## Context

`HouseholdRole.Child` and `Tenure.Dependent` exist. The shelter system can house homeless households. Guardianship edges are introduced in feature 02. Minor trusts are introduced in C03.

## Design

### Guardian hierarchy

When a guardian is needed:

1. surviving parent;
2. surviving grandparent;
3. adult sibling;
4. aunt/uncle;
5. another adult relative within the configured depth;
6. state care.

The candidate must be alive, adult, not dependent and not in a care institution. Prefer the same household, then the nearest by `NetworkId` for determinism.

### State care

- Use the existing shelter path, not a new orphanage entity.
- Add a `StateCare` component or flag for a child in state custody:

```csharp
public record struct StateCare
{
    public long SinceTick;
    public Entity CaseWorker;
    public Money MonthlyCost;
}
```

- The government pays the child's cost through the existing finance path.
- The child remains a normal sim with needs, health, school and traits.
- The admin can see the case and the cost.
- A relative may adopt the child later (out of scope for V4a; the record can be reassigned by admin).

### Guardianship edges

- `Guardian`/`Ward` edges are created when the guardian is not already a parent.
- The guardian household is the child's residence.
- A guardian may be a grandparent, sibling or relative; the parent edges do not change.
- The guardian manages the minor's trust but does not own it.

### Capacity

- A guardian must have a household. If the household has no capacity, the child is overcrowded but not separated.
- If the guardian has no household, the child enters state care.
- Moving a child to a guardian uses the family household system, not a direct teleport of components.

### Death of a guardian

- Re-run the hierarchy.
- If no candidate, state care.
- Preserve the trust and school enrollment.

## Tasks

- [ ] Add `GuardianshipService` with hierarchy selection.
- [ ] Add `StateCare` and government cost handling.
- [ ] Create guardian/ward edges when the guardian is not a parent.
- [ ] Move the child with the guardian.
- [ ] Reassign on guardian death.
- [ ] Add orphan, relative, state-care and capacity tests.
- [ ] Add the admin case fields.

## Files

- new `Genelife/Family/GuardianshipService.cs`
- new `Genelife/Components/Family/StateCare.cs`
- `Genelife/Systems/DeathProcessingSystem.cs`
- `Genelife/Systems/FamilyHouseholdSystem.cs`
- `Genelife.UnitTests/Family/GuardianshipTests.cs`

## Acceptance

- Every living minor has a guardian edge or a state-care record.
- The hierarchy chooses a living adult relative before state care.
- A guardian without a household triggers state care.
- A child moves with the guardian and keeps parent edges.
- State-care costs are ledgered and visible.
- Guardian death reassigns the child.

## Testing

- Both parents die; grandparent guardian.
- No relatives; state care.
- Guardian dies; next relative or state care.
- Guardian household full; overcrowding, not separation.
- Trust remains with the child.
- Assert no orphan without a record.