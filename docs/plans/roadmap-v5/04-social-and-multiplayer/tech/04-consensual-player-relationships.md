# S04 — Consensual player relationships

**Phase:** 4 — Relationships
**Depends:** S03, feature 01 `FamilyInvitation`, V4 `RelationshipService`
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Let two players form a partnership through an explicit invitation and acceptance, using the V4 partner edge and without automatic wallet or household changes.

## Context

Feature 01 adds player–NPC and player–player invitations for partnership, conception and guardianship. Feature 04 adds the social graph and block rules. A player partnership must be a social choice that becomes a real V4 relationship, not a UI-only flag.

V4 pairing creates a partner edge and attempts a capacity-aware household merge. For two players, the household merge must be a separate consent, not an automatic side effect of partnership.

## Design

### Invitation

Reuse the `FamilyInvitation` lifecycle and add `Kind = Cohabitation` for the household merge. Player–player invitations are keyed by player id, not sim entity, so they survive heir continuation; the V4 partner edge is created between the current controlled sims at acceptance.

- `InvitePartner` checks eligibility, block state, existing partner and cooldown.
- The target sees the invitation in the social panel.
- Only the target can accept or decline.
- Accepting resolves both players' current controlled sims and creates the mirrored `Partner` edges through `RelationshipService`.
- If either player has continued to a new sim since the invitation, the invitation is revalidated against the current sims and may expire.
- Declining or expiring creates no edge.
- A block cancels a pending invitation in both directions.

### Household merge

- A partnership does not move either player automatically.
- A separate `Cohabit` invitation is required.
- Accepting the cohabitation invitation applies the V4 capacity-aware merge.
- If capacity is unavailable, the partnership remains across households.
- A player cannot force another player to leave a household or surrender a lease.

### Wallet and property

- Partnership does not merge wallets.
- Shared household expenses follow the existing household finance waterfall.
- Property ownership and leases are not transferred by partnership alone.
- Inheritance follows the V4 relationship order, not a player-only override.

### Ending

- A player can end a partnership through the V4 `ExPartner` path.
- Ending a partnership does not end a friendship automatically.
- A cohabitation split follows the V4 household split rules.
- Both players receive an event and a read-model update.

## Tasks

- [ ] Extend `FamilyInvitationKind` with `Cohabitation`.
- [ ] Add player–player partnership invitation flow.
- [ ] Reuse `RelationshipService` for accepted partnerships.
- [ ] Add the separate cohabitation invitation and capacity-aware merge.
- [ ] Add block and cooldown checks.
- [ ] Add partnership end and household split.
- [ ] Add social-panel DTOs and notifications.
- [ ] Add tests for accept, decline, block, cohabitation and end.

## Files

- `Genelife/Components/Family/FamilyInvitation.cs`
- `Genelife/Family/FamilyInvitationService.cs`
- `Genelife/Family/RelationshipService.cs`
- `Genelife/Family/FamilyHouseholdSystem.cs`
- `clients/browser/src/ui/social/relationships.ts`
- `Genelife.UnitTests/Social/PlayerRelationshipTests.cs`

## Acceptance

- A player partnership requires an invitation and acceptance.
- A blocked player cannot invite or be invited.
- Accepting creates mirrored partner edges.
- A partnership does not merge wallets or move households automatically.
- Cohabitation requires a separate acceptance and respects capacity.
- Ending a partnership creates `ExPartner` edges and splits the household only when required.
- Both players see the result and the admin can inspect it.

## Testing

- Invite and accept; assert partner edges.
- Decline and expire; assert no edge and cooldown.
- Block and assert invitation cancellation.
- Accept cohabitation with capacity and assert merge.
- Accept cohabitation without capacity and assert separate households.
- End partnership and assert edges and household.
