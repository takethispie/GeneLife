# P05 — Player relationships and family actions

**Phase:** 5 — Family
**Depends:** P01, V4 `RelationshipService`, `EligibilityPolicy`, `AttractionSystem`, `ConceptionSystem`, `BirthSystem`
**Estimate:** 3 sessions
**Status:** Planned (V5)

## Goal

Let a player form a partnership and raise a child through the V4 family graph, with explicit consent and without bypassing eligibility, exclusivity or household rules.

## Context

V4 relationships are directed edges with mirrored parent/child, partner and guardian/ward pairs. Pairing is deterministic and explainable; conception requires an eligible partnership and a deterministic chance roll. The family read model and admin page already exist.

The player needs an invitation path because a person cannot be paired by a background decision. NPCs also need a deterministic response policy so a player cannot coerce a family relationship by repeating requests.

## Design

### Invitation

```csharp
public enum FamilyInvitationKind : byte
{
    Partnership,
    Conception,
    Guardianship
}

public enum FamilyInvitationStatus : byte
{
    Pending,
    Accepted,
    Declined,
    Expired,
    Cancelled
}

public sealed record FamilyInvitation
{
    public Guid Id { get; init; }
    public Entity From;
    public Entity To;
    public FamilyInvitationKind Kind;
    public long CreatedTick;
    public long ExpiresTick;
    public FamilyInvitationStatus Status;
}
```

Invitations are persisted and expire after a configured number of ticks. A pending invitation blocks duplicate invitations of the same kind between the same sims.

### Commands

- `InvitePartner` with the target sim network id.
- `RespondToInvitation` with the invitation id and accept/decline.
- `TryForChild` with the partner sim network id.
- `NameChild` with the newborn network id and a name.

### NPC consent policy

An NPC response is deterministic and bounded:

- eligibility and attraction score are evaluated with the existing policy;
- the NPC must not have an active partner, a pending invitation or a recent rejection cooldown;
- the response is drawn from `SimulationRandom.For("social.encounter")` keyed by the pair and tick;
- a rejection includes a stable reason and starts a cooldown;
- repeated requests during the cooldown are rejected without a new roll.

The policy never lets a player bypass eligibility, consanguinity, age or exclusivity.

### Player–player consent

- The target player receives the invitation in the HUD.
- Only the target player can accept or decline.
- A disconnect leaves the invitation pending until it expires.
- Accepting creates the mirrored partner edges through `RelationshipService`.
- Declining or expiring creates no edge.

### Partnership consequences

- A partnership does not merge wallets.
- Household merge follows the V4 capacity-aware rule and `CohabitOnPairing`.
- Ending a partnership uses the V4 `ExPartner` path and does not remove shared children.

### Conception and birth

- `TryForChild` requires an eligible current partnership, both parents alive and not dependent, and no existing pregnancy.
- Both players must consent when both are player-controlled.
- The existing conception chance and pregnancy progression apply.
- Birth creates a complete newborn with inherited traits, parent edges and household membership.
- The player may name the child; an unnamed child gets the deterministic V4 name.

## Tasks

- [ ] Add `FamilyInvitation`, status and expiry.
- [ ] Add the invitation commands and HUD read model.
- [ ] Implement the deterministic NPC consent policy and cooldown.
- [ ] Reuse `RelationshipService` for accepted partnerships.
- [ ] Reuse `ConceptionSystem` and `BirthSystem` for player families.
- [ ] Add child naming without changing the V4 name fallback.
- [ ] Add family events and admin visibility for player invitations.
- [ ] Add tests for consent, cooldown, decline, expiry and birth.

## Files

- new `Genelife/Components/Family/FamilyInvitation.cs`
- new `Genelife/Family/FamilyInvitationService.cs`
- new `Genelife/Family/NpcConsentPolicy.cs`
- `Genelife/Family/RelationshipService.cs`
- `Genelife/Family/ConceptionSystem.cs`
- `Genelife/Family/BirthSystem.cs`
- `Genelife.API/Services/SimulationEngine.cs`
- `Genelife.UnitTests/Player/PlayerFamilyTests.cs`

## Acceptance

- A player can invite an eligible NPC or player and the target receives a pending invitation.
- The target can accept or decline; only acceptance creates partner edges.
- An ineligible target is rejected before an invitation is created.
- A declined or expired invitation creates no edge.
- Repeating a request during the cooldown does not reroll consent.
- A partnership does not merge wallets and merges households only when capacity allows.
- A player child is born with inherited traits, parent edges and household membership.
- The admin can inspect invitations, partnerships and player children.

## Testing

- Invite and accept an NPC; assert mirrored partner edges.
- Invite and decline; assert no edge and a cooldown.
- Invite an existing partner; assert rejection.
- Expire an invitation and assert no edge.
- Conceive and give birth; assert traits, parent edges and household.
- Replay the same invitation/consent sequence and compare results.
