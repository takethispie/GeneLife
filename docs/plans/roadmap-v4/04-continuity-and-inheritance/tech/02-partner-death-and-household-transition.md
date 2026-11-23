# C02 — Partner death and household transition

**Phase:** 2 — Family
**Depends:** feature 02 relationships, feature 03 births, `DeathProcessingSystem`
**Estimate:** 2 sessions
**Status:** Planned (V4)

## Goal

When a partner or guardian dies, end the active partnership, reassign the household head and guardian, and keep children and housing valid.

## Context

`DeathProcessingSystem` currently:

- removes the sim from employment, school and household;
- reassigns the household head to a surviving member;
- computes heirs from household members;
- transfers an owned home to a surviving member;
- queues an estate settlement.

It does not know about partners, guardians or minor trusts.

## Design

### Order of operations

On death:

1. Mark the sim `Dead`.
2. End the active partnership with `RelationshipService.EndPartnership(reason: Death)`; preserve parent edges and history.
3. Remove employment, school and job search.
4. Remove household membership.
5. Reassign guardianship for minors:
   - surviving partner if they are a parent/guardian;
   - otherwise the nearest relative from the relationship graph;
   - otherwise state care (feature 04).
6. Reassign the household head:
   - surviving partner;
   - otherwise the oldest adult member;
   - otherwise the guardian;
   - otherwise the lowest `NetworkId`.
7. Move minors with their new guardian if the guardian is in another household.
8. Release or keep the unit through the existing housing path.
9. Queue estate settlement with relationship-based heirs.

### Partnership history

- The dead sim's partner edge becomes historical (`ExPartner` or a `Partner` edge with a `Source=Death` and an ended tick).
- The surviving partner is eligible to pair again after a configured mourning/cooldown period.
- Do not delete the edge; the family history and children remain.

### Minor trust

- If the deceased leaves money to a minor, the estate creates a `TrustAccount` managed by the guardian (C03/C04).
- The guardian does not receive the minor's share as personal money.

### Housing

- A surviving partner inherits an owned home.
- A guardian of a minor holds an owned home until the minor is an adult.
- A renter household continues if a member remains; otherwise it is released through the existing move-out path.
- A capacity check is a warning, not a reason to evict a grieving household in the same tick.

## Tasks

- [ ] Integrate partnership end into `DeathProcessingSystem`.
- [ ] Add guardian reassignment before head reassignment.
- [ ] Update head selection to prefer the surviving partner.
- [ ] Move minors with the guardian.
- [ ] Preserve partner history and parent edges.
- [ ] Queue relationship-based heirs.
- [ ] Add widowhood, guardian and housing tests.

## Files

- `Genelife/Systems/DeathProcessingSystem.cs`
- `Genelife/Family/RelationshipService.cs`
- `Genelife/Family/GuardianshipService.cs`
- `Genelife/Systems/FamilyHouseholdSystem.cs`
- `Genelife.UnitTests/Family/WidowhoodTests.cs`

## Acceptance

- A partner's death ends the active partnership and preserves history.
- Parent edges remain intact.
- Minors move to the surviving parent, relative or state care.
- The household head becomes the surviving partner when possible.
- The owned home transfers to the partner or guardian.
- No child is left without a guardian or state-care record.
- No money is minted.

## Testing

- Partner dies with children: surviving partner becomes head and guardian.
- Both parents die: relative guardian, then state care.
- Minor with a trust: guardian manages, minor owns.
- Owned home transfer.
- Renter household release.
- Assert no orphan and no minting.