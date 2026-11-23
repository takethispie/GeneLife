using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Company;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Life;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

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
    private readonly int interval;

    public EstateSettlementSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerMonth;
    }

    public string Name => "EstateSettlement";
    public int Order => 580;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var requests = economy.Life.EstateQueue
            .OrderBy(request => request.SimNetworkId)
            .ToList();
        economy.Life.EstateQueue.Clear();

        foreach (var request in requests)
            Settle(request, tick);
    }

    private void Settle(EstateRequest request, long tick)
    {
        var sim = request.Sim;
        if (!world.IsAlive(sim))
            return;

        var cash = world.Has<Wallet>(sim) ? world.Get<Wallet>(sim).Balance : Money.Zero;
        var savings = world.Has<SavingsAccount>(sim) ? world.Get<SavingsAccount>(sim).Balance : Money.Zero;
        var assets = cash + savings;

        if (world.Has<Wallet>(sim))
            world.Get<Wallet>(sim).Balance = Money.Zero;
        if (world.Has<SavingsAccount>(sim))
            world.Get<SavingsAccount>(sim).Balance = Money.Zero;

        var funeral = assets.Min(FuneralCost);
        var remaining = assets - funeral;

        if (funeral.IsPositive)
        {
            economy.Government.Balance += funeral;
            economy.Ledger.Record(
                Guid.NewGuid(),
                tick,
                LedgerKind.FuneralCost,
                request.SimNetworkId,
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

        var heirs = request.Heirs
            .Where(heir => heir != sim && world.IsAlive(heir) && !world.Has<Dead>(heir))
            .Distinct()
            .OrderBy(heir => LifeSystemHelpers.NetworkIdOf(world, heir))
            .ToList();

        var distributed = Money.Zero;
        var escheat = Money.Zero;
        if (remaining.IsPositive)
        {
            if (heirs.Count == 0)
            {
                escheat = remaining;
                economy.Government.Balance += remaining;
                economy.Ledger.Record(
                    Guid.NewGuid(),
                    tick,
                    LedgerKind.Inheritance,
                    request.SimNetworkId,
                    EconomyAccountIds.Government,
                    remaining,
                    "estate.escheat");
                economy.Emit(new EscheatRecorded(request.SimNetworkId, remaining.Cents, tick));
            }
            else
            {
                var share = new Money(remaining.Cents / heirs.Count);
                var remainder = remaining - share * heirs.Count;
                for (var i = 0; i < heirs.Count; i++)
                {
                    var heir = heirs[i];
                    var amount = i == 0 ? share + remainder : share;
                    if (!amount.IsPositive)
                        continue;

                    if (!world.Has<Wallet>(heir))
                        world.Add(heir, new Wallet(Money.Zero));
                    world.Get<Wallet>(heir).Balance += amount;
                    distributed += amount;

                    economy.Ledger.Record(
                        Guid.NewGuid(),
                        tick,
                        LedgerKind.Inheritance,
                        request.SimNetworkId,
                        LifeSystemHelpers.NetworkIdOf(world, heir),
                        amount,
                        "estate.inheritance");
                    economy.Emit(new InheritancePaid(
                        request.SimNetworkId,
                        LifeSystemHelpers.NetworkIdOf(world, heir),
                        amount.Cents,
                        tick));
                }
            }
        }

        if (request.SimNetworkId != 0)
        {
            economy.Emit(new EstateSettled(
                request.SimNetworkId,
                assets.Cents,
                debtsPaid.Cents,
                funeral.Cents,
                0,
                distributed.Cents,
                escheat.Cents,
                heirs.Count,
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

        if (landlord == request.Sim)
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

        var householdContinues = request.Household != Entity.Null &&
                                 world.IsAlive(request.Household) &&
                                 economy.Housing.MemberCount(request.Household) > 0;

        Entity newOwner = Entity.Null;
        if (householdContinues && world.Has<Household>(request.Household))
        {
            var head = world.Get<Household>(request.Household).Head;
            if (head != Entity.Null && world.IsAlive(head) && !world.Has<Dead>(head))
                newOwner = head;
        }
        if (newOwner == Entity.Null)
        {
            newOwner = request.Heirs.FirstOrDefault(heir =>
                world.IsAlive(heir) && !world.Has<Dead>(heir));
        }

        economy.Housing.TransferUnit(request.OwnedUnit, newOwner);
    }
}
