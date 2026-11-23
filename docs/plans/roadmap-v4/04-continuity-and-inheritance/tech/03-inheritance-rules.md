# C03 — Inheritance rules

**Phase:** 3 — Inheritance
**Depends:** C02, `EstateSettlementSystem`, `RelationshipIndex`
**Estimate:** 2–3 sessions
**Status:** Done — heir order, minor trusts, release and conservation; beneficiary-death trust escheats rather than replaying the full estate order

## Goal

Replace household-membership heirs with a relationship-based order, add minor trusts, and preserve exact money conservation.

## Context

`EstateSettlementSystem` already pays debts, funeral costs and tax, distributes the remainder to heirs and escheats the rest. `DeathProcessingSystem` builds heirs from household members. Owned housing transfers to a surviving member.

## Design

### Heir resolution

`HeirResolver.Resolve(deceased, index, options)`:

1. surviving partner;
2. living children;
3. living parents;
4. living siblings;
5. grandparents/aunts/uncles at the configured depth;
6. none → escheat.

The resolver returns ordered heirs with shares. It never returns the deceased, a dead sim or a duplicate.

### Shares

```csharp
public sealed class InheritanceOptions
{
    public decimal PartnerShare { get; set; } = 0.50m;
    public decimal ChildSharePool { get; set; } = 0.50m;
    public bool SplitChildPoolEqually { get; set; } = true;
    public int ExtendedFamilyDepth { get; set; } = 2;
    public Money MinorTrustMinimum { get; set; } = Money.FromUnits(100m);
}
```

- Partner share applies only if a living partner exists.
- Children share the residual pool equally.
- If no partner, children take 100%.
- If a partner exists but there are no children, the remaining share follows the parent/sibling/escheat order.
- If no partner or children, parents/siblings take the remainder.
- Any configured share that is not allocated to a partner or child flows to the next tier, never disappears.
- Rounding pennies go to the first heir in stable order.
- Shares sum exactly to the distributable remainder.

### Minor trust

Add a trust account entity with a `NetworkId` and a `TrustAccount` component:

```csharp
public record struct TrustAccount
{
    public Money Balance;
    public Entity Beneficiary;
    public Entity Guardian;
    public long CreatedTick;
    public long ReleaseAgeTicks;
    public ulong SourceSimNetworkId;
}
```

One trust entity per depositing estate, so a child can receive separate trusts from two parents. Each trust has its own `NetworkId` and history.

Rules:

- A minor's inheritance is deposited into the trust, not the guardian's wallet.
- The trust is included in `TotalMoneySupply`.
- The guardian cannot spend it through ordinary consumption.
- At `WorkStartAge` (or the configured release age), the trust is released to the beneficiary's wallet/savings and the account is closed.
- Ledger kinds: `TrustDeposit`, `TrustRelease`.
- If the beneficiary dies before release, the trust follows the normal estate order.

### Housing

- Owned home transfers to the partner first.
- If no partner, the guardian of a minor child holds it until the child is an adult.
- If no partner, child or guardian, the home is sold or escheated through the existing path.
- Do not transfer a rented unit as an asset.

### Conservation

- Debts, funeral, tax, heir payments, trusts and escheat must sum to the estate assets.
- No negative wallet or trust balance.
- The conservation scenario must show a zero gap after births, deaths and inheritance.

## Tasks

- [ ] Add `HeirResolver` and `InheritanceOptions`.
- [ ] Replace household-member heirs in `DeathProcessingSystem`.
- [ ] Add `TrustAccount`, ledger kinds and total-money-supply inclusion.
- [ ] Add trust deposit during estate settlement.
- [ ] Add trust release at adulthood and death.
- [ ] Update owned-home transfer to partner/guardian.
- [ ] Add share, rounding, trust and conservation tests.

## Files

- new `Genelife/Family/HeirResolver.cs`
- new `Genelife/Family/InheritanceOptions.cs`
- new `Genelife/Components/Finance/TrustAccount.cs`
- `Genelife/Systems/EstateSettlementSystem.cs`
- `Genelife/Systems/DeathProcessingSystem.cs`
- `Genelife/Economy/EconomyContext.cs`
- `Genelife.UnitTests/Lifecycle/EstateSettlementTests.cs`

## Acceptance

- Heirs follow partner, children, parents, siblings, escheat.
- Shares sum exactly to the distributable remainder.
- A minor's share goes to a trust, not the guardian's wallet.
- The trust is included in total money supply and released at adulthood.
- Owned housing follows the partner/guardian rule.
- No estate path mints or destroys money.

## Testing

- Partner and two children; partner only; children only; no relatives.
- Minor trust deposit and release.
- Trust beneficiary dies before release.
- Rounding with three children.
- Owned-home transfer.
- Conservation gap equals zero.