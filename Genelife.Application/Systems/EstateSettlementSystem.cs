using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Company;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Family;
using Genelife.Life;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Application.Systems;

/// <summary>
/// Settles one estate per death: funeral, arrears, housing transfer and equal
/// distribution to surviving household members, then a means-tested escheat to
/// the government. The estate never creates money.
/// </summary>
public class EstateSettlementSystem : ISimulationSystem
{
    private static readonly Money FuneralCost = Money.FromUnits(100m);

    private readonly World world;
    private readonly EconomyContext economy;
    private readonly RelationshipIndex? relationshipIndex;
    private readonly RelationshipService? relationshipService;
    private readonly InheritanceOptions inheritanceOptions;
    private readonly TrustService? trusts;
    private readonly int interval;

    public EstateSettlementSystem(
        World world,
        EconomyContext economy,
        int? interval = null,
        RelationshipIndex? relationshipIndex = null,
        RelationshipService? relationshipService = null,
        InheritanceOptions? inheritanceOptions = null,
        TrustService? trusts = null)
    {
        this.world = world;
        this.economy = economy;
        this.relationshipIndex = relationshipIndex;
        this.relationshipService = relationshipService;
        this.inheritanceOptions = inheritanceOptions ?? InheritanceOptions.Default;
        this.trusts = trusts;
        this.interval = interval ?? economy.Clock.TicksPerMonth;
    }

    public string Name => "EstateSettlement";
    public int Order => 580;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var requests = economy.Life.EstateQueue
            .OrderBy(request => request.GenosNetworkId)
            .ToList();
        economy.Life.EstateQueue.Clear();

        foreach (var request in requests)
            Settle(request, tick);
    }

    private void Settle(EstateRequest request, long tick)
    {
        var genos = request.Genos;
        if (!world.IsAlive(genos))
        {
            // The entity was destroyed before settlement. Remove any family link
            // that still points at it so a snapshot cannot persist a dangling edge.
            relationshipService?.RemoveEdgesForGenos(genos);
            return;
        }

        var cash = world.Has<Wallet>(genos) ? world.Get<Wallet>(genos).Balance : Money.Zero;
        var savings = world.Has<SavingsAccount>(genos) ? world.Get<SavingsAccount>(genos).Balance : Money.Zero;
        var assets = cash + savings;

        if (world.Has<Wallet>(genos))
            world.Get<Wallet>(genos).Balance = Money.Zero;
        if (world.Has<SavingsAccount>(genos))
            world.Get<SavingsAccount>(genos).Balance = Money.Zero;

        var funeral = assets.Min(FuneralCost);
        var remaining = assets - funeral;

        if (funeral.IsPositive)
        {
            economy.Government.Balance += funeral;
            economy.Ledger.Record(
                Guid.NewGuid(),
                tick,
                LedgerKind.FuneralCost,
                request.GenosNetworkId,
                EconomyAccountIds.Government,
                funeral,
                "estate.funeral");
        }

        var arrears = Money.FromUnits((decimal)request.HouseholdRentArrears + (decimal)request.HouseholdUtilityArrears);
        var debtsPaid = remaining.Min(arrears);
        remaining -= debtsPaid;

        if (debtsPaid.IsPositive && world.IsAlive(request.Household) && world.Has<Household>(request.Household))
        {
            var household = world.Get<Household>(request.Household);
            var rentPaid = debtsPaid.Min(household.RentArrears);
            household.RentArrears -= rentPaid;
            var utilityPaid = (debtsPaid - rentPaid).Min(household.UtilityArrears);
            household.UtilityArrears -= utilityPaid;
            world.Get<Household>(request.Household) = household;
            CreditLandlord(request, debtsPaid, tick);
        }

        TransferHousing(request);

        var allocations = relationshipIndex is not null
            ? HeirResolver.Resolve(
                    world,
                    relationshipIndex,
                    genos,
                    inheritanceOptions,
                    economy.Life.Policy)
                .Allocations
                .Where(allocation =>
                    allocation.Heir != genos &&
                    world.IsAlive(allocation.Heir) &&
                    !world.Has<Dead>(allocation.Heir))
                .DistinctBy(allocation => allocation.Heir)
                .OrderBy(allocation => LifeSystemHelpers.NetworkIdOf(world, allocation.Heir))
                .ToList()
            : request.Heirs
                .Where(heir => heir != genos && world.IsAlive(heir) && !world.Has<Dead>(heir))
                .Distinct()
                .OrderBy(heir => LifeSystemHelpers.NetworkIdOf(world, heir))
                .Select(heir => new HeirAllocation(
                    heir,
                    1m / Math.Max(1, request.Heirs.Count),
                    "household"))
                .ToList();

        var distributed = Money.Zero;
        var escheat = Money.Zero;
        var heirCount = allocations.Count;
        if (remaining.IsPositive)
        {
            if (allocations.Count == 0)
            {
                escheat = remaining;
                PayEscheat(request, remaining, tick);
            }
            else
            {
                var amounts = AllocateCents(remaining.Cents, allocations);
                var allocatedShare = allocations.Sum(allocation => allocation.Share);
                for (var i = 0; i < allocations.Count; i++)
                {
                    var amount = new Money(amounts[i]);
                    if (!amount.IsPositive)
                        continue;
                    PayHeir(request, allocations[i].Heir, amount, tick, ref distributed);
                }

                var unallocated = remaining - distributed;
                if (unallocated.IsPositive)
                {
                    escheat = unallocated;
                    PayEscheat(request, unallocated, tick);
                }
            }
        }

        relationshipService?.RemoveEdgesForGenos(genos);

        if (request.GenosNetworkId != 0)
        {
            economy.Emit(new EstateSettled(
                request.GenosNetworkId,
                assets.Cents,
                debtsPaid.Cents,
                funeral.Cents,
                0,
                distributed.Cents,
                escheat.Cents,
                heirCount,
                tick));
        }

        if (request.Household != Entity.Null && world.IsAlive(request.Household) &&
            economy.Housing.MemberCount(request.Household) == 0 &&
            economy.Housing.UnitOf(request.Household) == Entity.Null)
        {
            economy.Housing.DetachNetworkId(request.Household);
            world.Destroy(request.Household);
        }
    }

    private void CreditLandlord(EstateRequest request, Money amount, long tick)
    {
        if (!amount.IsPositive)
            return;

        var unit = request.Unit != Entity.Null ? request.Unit : request.OwnedUnit;
        var landlord = unit != Entity.Null && world.IsAlive(unit)
            ? economy.Housing.EffectiveOwner(unit)
            : Entity.Null;

        if (landlord == request.Genos)
            return;

        if (landlord != Entity.Null && world.IsAlive(landlord) && world.Has<CompanyName>(landlord))
        {
            economy.Accounts.ApplySettlement(landlord, amount, tick, revenue: true);
        }
        else if (landlord != Entity.Null && world.IsAlive(landlord) && world.Has<Household>(landlord))
        {
            var head = world.Get<Household>(landlord).Head;
            if (head != Entity.Null && world.IsAlive(head) && !world.Has<Dead>(head))
            {
                if (!world.Has<Wallet>(head))
                    world.Add(head, new Wallet(Money.Zero));
                world.Get<Wallet>(head).Balance += amount;
            }
            else
            {
                economy.Government.Balance += amount;
            }
        }
        else
        {
            economy.Government.Balance += amount;
        }
    }

    private void TransferHousing(EstateRequest request)
    {
        if (request.OwnedUnit == Entity.Null || !world.IsAlive(request.OwnedUnit) ||
            !world.Has<HousingUnit>(request.OwnedUnit))
            return;

        Entity newOwner = Entity.Null;
        if (relationshipIndex is not null)
        {
            newOwner = relationshipIndex.ExPartnersOf(request.Genos)
                .Concat(relationshipIndex.PartnersOf(request.Genos))
                .FirstOrNull(candidate =>
                    candidate != request.Genos && world.IsAlive(candidate) && !world.Has<Dead>(candidate));

            if (newOwner == Entity.Null)
            {
                var minor = relationshipIndex.ChildrenOf(request.Genos)
                    .FirstOrNull(child =>
                        world.IsAlive(child) &&
                        world.Has<Alive>(child) &&
                        world.Get<Alive>(child).Age < economy.Life.Policy.WorkStartAge &&
                        relationshipIndex.GuardiansOf(child).Count > 0);
                if (minor != Entity.Null)
                    newOwner = relationshipIndex.GuardiansOf(minor)[0];
            }
        }

        if (newOwner == Entity.Null && request.Household != Entity.Null &&
            world.IsAlive(request.Household) && economy.Housing.MemberCount(request.Household) > 0 &&
            world.Has<Household>(request.Household))
        {
            var head = world.Get<Household>(request.Household).Head;
            if (head != Entity.Null && world.IsAlive(head) && !world.Has<Dead>(head))
                newOwner = head;
        }
        if (newOwner == Entity.Null)
        {
            newOwner = request.Heirs.FirstOrNull(heir =>
                heir != request.Genos && world.IsAlive(heir) && !world.Has<Dead>(heir));
        }

        economy.Housing.TransferUnit(request.OwnedUnit, newOwner);
    }

    private void PayHeir(
        EstateRequest request,
        Entity heir,
        Money amount,
        long tick,
        ref Money distributed)
    {
        var isMinor = world.Has<Alive>(heir) &&
                      world.Get<Alive>(heir).Age < economy.Life.Policy.WorkStartAge;
        var guardian = relationshipIndex?.GuardiansOf(heir).FirstOrNull() ?? Entity.Null;
        var releaseAgeTicks = (long)economy.Life.Policy.WorkStartAge * economy.Clock.TicksPerYear;

        if (isMinor && trusts is not null)
        {
            var trust = trusts.Create(
                heir, guardian, amount, request.GenosNetworkId, tick, releaseAgeTicks);
            if (trust == Entity.Null)
                return;
        }
        else
        {
            if (!world.Has<Wallet>(heir))
                world.Add(heir, new Wallet(Money.Zero));
            world.Get<Wallet>(heir).Balance += amount;
            economy.Ledger.Record(
                Guid.NewGuid(),
                tick,
                LedgerKind.Inheritance,
                request.GenosNetworkId,
                LifeSystemHelpers.NetworkIdOf(world, heir),
                amount,
                "estate.inheritance");
        }

        distributed += amount;
        economy.Life.InheritancePaidThisYear += amount;
        economy.Emit(new InheritancePaid(
            request.GenosNetworkId,
            LifeSystemHelpers.NetworkIdOf(world, heir),
            amount.Cents,
            tick));
    }

    private void PayEscheat(EstateRequest request, Money amount, long tick)
    {
        economy.Government.Balance += amount;
        economy.Ledger.Record(
            Guid.NewGuid(),
            tick,
            LedgerKind.Inheritance,
            request.GenosNetworkId,
            EconomyAccountIds.Government,
            amount,
            "estate.escheat");
        economy.Emit(new EscheatRecorded(request.GenosNetworkId, amount.Cents, tick));
    }

    private static long[] AllocateCents(long pool, IReadOnlyList<HeirAllocation> allocations)
    {
        var result = new long[allocations.Count];
        if (pool <= 0 || allocations.Count == 0)
            return result;

        var allocatedShare = Math.Clamp(allocations.Sum(allocation => allocation.Share), 0m, 1m);
        var target = (long)Math.Floor(pool * allocatedShare);
        var assigned = 0L;
        for (var i = 0; i < allocations.Count; i++)
        {
            result[i] = (long)Math.Floor(pool * allocations[i].Share);
            if (result[i] < 0)
                result[i] = 0;
            assigned += result[i];
        }

        var pennies = Math.Min(target - assigned, allocations.Count);
        for (var i = 0; i < pennies; i++)
            result[i]++;
        return result;
    }
}
