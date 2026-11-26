using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Housing;
using Genelife.Messages.Events;
using Genelife.Application.Systems;
using Genelife.Values;

namespace Genelife.Api.Repositories;

/// <summary>
/// Admin-facing housing operations: property and unit CRUD, assignment,
/// eviction, ownership transfer and construction.
/// </summary>
public sealed class HousingRepository
{
    private readonly World world;
    private readonly NetworkIdRegistry networkIds;
    private readonly EconomyContext economy;
    private readonly HousingRegistry registry;
    private readonly HousingAllocator allocator;
    private readonly HouseholdFinanceService finance;

    public HousingRepository(World world, NetworkIdRegistry networkIds, EconomyContext economy)
    {
        this.world = world;
        this.networkIds = networkIds;
        this.economy = economy;
        registry = economy.Housing;
        allocator = new HousingAllocator(world, economy);
        finance = new HouseholdFinanceService(world, economy);
    }

    public NetworkId CreateProperty(
        ulong ownerId,
        string name,
        string district,
        int unitCount,
        HousingTier tier,
        decimal? monthlyRent,
        float condition,
        float neighbourhood,
        long tick)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Property name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(district))
            throw new ArgumentException("District is required.", nameof(district));
        if (unitCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(unitCount), "Unit count must be positive.");

        var owner = ResolveOwner(ownerId);
        var propertyEntity = world.Create(new Property(
            name,
            district,
            owner,
            unitCount,
            Math.Clamp(condition, 0.1f, 1f),
            Math.Clamp(neighbourhood, 0f, 1f),
            economy.HousingOptions.DefaultPropertyUpkeep,
            tier == HousingTier.Shelter));
        var propertyId = registry.AllocateNetworkId(propertyEntity);
        registry.AddProperty(propertyEntity, owner);

        var rent = monthlyRent is null
            ? HousingTierPolicy.BaseRent(tier)
            : Money.FromUnits(monthlyRent.Value);
        var purchasePrice = HousingTierPolicy.PurchasePrice(tier);

        for (var i = 0; i < unitCount; i++)
        {
            var unit = world.Create(new HousingUnit(
                propertyEntity,
                $"{propertyId.Value}-{i + 1}",
                tier,
                HousingTierPolicy.Capacity(tier),
                rent,
                Math.Clamp(condition, 0.1f, 1f),
                isVacant: true,
                owner: Entity.Null,
                purchasePrice: purchasePrice));
            registry.AllocateNetworkId(unit);
            registry.AddUnit(unit, propertyEntity);
        }

        economy.Emit(new PropertyCreated(
            propertyId.Value,
            ownerId,
            name,
            district,
            unitCount,
            tier.ToString(),
            tick));

        return propertyId;
    }

    public bool Repair(NetworkId propertyId, Money amount, long tick)
    {
        if (!TryGetProperty(propertyId, out var entity))
            return false;
        if (!amount.IsPositive)
            throw new ArgumentOutOfRangeException(nameof(amount));

        var property = world.Get<Property>(entity);
        if (!TryDebitOwner(property.Owner, amount, tick))
            throw new InvalidOperationException("The owner cannot fund this repair.");

        var units = registry.UnitsByProperty.TryGetValue(entity, out var list)
            ? list
            : new List<Entity>();
        var gain = HousingCostPolicy.RepairGain(amount, units.Count);
        property.Condition = Math.Clamp(property.Condition + gain, HousingCostPolicy.ConditionFloor, 1f);
        world.Get<Property>(entity) = property;

        foreach (var unit in units)
        {
            if (!world.IsAlive(unit) || !world.Has<HousingUnit>(unit))
                continue;
            var data = world.Get<HousingUnit>(unit);
            data.Condition = Math.Clamp(data.Condition + gain, HousingCostPolicy.ConditionFloor, 1f);
            if (data.Condition >= HousingCostPolicy.UninhabitableThreshold)
                data.IsCondemned = false;
            world.Get<HousingUnit>(unit) = data;
        }

        economy.Emit(new PropertyUpdated(
            propertyId.Value,
            property.Name,
            property.Condition,
            property.MonthlyUpkeep.Cents));
        return true;
    }

    public bool UpdateProperty(
        NetworkId propertyId,
        string? name,
        float? condition,
        Money? monthlyUpkeep,
        long tick)
    {
        if (!TryGetProperty(propertyId, out var entity))
            return false;

        ref var property = ref world.Get<Property>(entity);
        if (!string.IsNullOrWhiteSpace(name))
            property.Name = name;
        if (condition is not null)
            property.Condition = Math.Clamp(condition.Value, 0.1f, 1f);
        if (monthlyUpkeep is not null)
            property.MonthlyUpkeep = monthlyUpkeep.Value.Max(Money.Zero);

        economy.Emit(new PropertyUpdated(
            propertyId.Value,
            property.Name,
            property.Condition,
            property.MonthlyUpkeep.Cents));
        return true;
    }

    public bool UpdateUnit(
        NetworkId unitId,
        HousingTier? tier,
        int? capacity,
        Money? baseRent,
        float? condition)
    {
        if (!TryGetUnit(unitId, out var entity))
            return false;

        ref var unit = ref world.Get<HousingUnit>(entity);
        if (tier is not null)
        {
            unit.Tier = tier.Value;
            unit.Capacity = capacity ?? HousingTierPolicy.Capacity(tier.Value);
            unit.BaseRent = baseRent ?? HousingTierPolicy.BaseRent(tier.Value);
        }
        else
        {
            if (capacity is not null)
                unit.Capacity = Math.Max(1, capacity.Value);
            if (baseRent is not null)
                unit.BaseRent = baseRent.Value.Max(Money.Zero);
        }

        if (condition is not null)
        {
            unit.Condition = Math.Clamp(condition.Value, 0.1f, 1f);
            if (unit.Condition >= HousingCostPolicy.UninhabitableThreshold)
                unit.IsCondemned = false;
        }

        economy.Emit(new HousingUnitUpdated(
            unitId.Value,
            unit.Tier.ToString(),
            unit.Capacity,
            unit.BaseRent.Cents,
            unit.Condition,
            unit.IsVacant));
        return true;
    }

    public bool Assign(ulong householdId, ulong unitId, long tick)
    {
        if (!TryGetHousehold(new NetworkId(householdId), out var household))
            throw new InvalidOperationException("Household not found.");
        if (!TryGetUnit(new NetworkId(unitId), out var unit))
            throw new InvalidOperationException("Unit not found.");

        return allocator.TryAssign(household, unit, Tenure.Renter, tick);
    }

    public bool Evict(ulong householdId, bool immediate, long tick)
    {
        if (!TryGetHousehold(new NetworkId(householdId), out var household))
            throw new InvalidOperationException("Household not found.");

        if (!immediate)
        {
            ref var data = ref world.Get<Household>(household);
            data.NoticeStage = 3;
            data.NoticeStartedTick = tick;
            data.CureDeadlineTick = tick + economy.Clock.TicksPerMonth * economy.HousingOptions.NoticeMonths;
            economy.Emit(new TenantNoticeIssued(
                householdId,
                NetworkIdOf(data.Unit),
                data.NoticeStage,
                data.CureDeadlineTick,
                tick));
            return true;
        }

        return new EvictionSystem(world, economy).EvictNow(household, tick);
    }

    public bool Purchase(ulong householdId, ulong unitId, long tick)
    {
        if (!TryGetHousehold(new NetworkId(householdId), out var household))
            throw new InvalidOperationException("Household not found.");
        if (!TryGetUnit(new NetworkId(unitId), out var unit))
            throw new InvalidOperationException("Unit not found.");

        var data = world.Get<Household>(household);
        var head = finance.Head(household);
        if (head == Entity.Null || !finance.IsAdult(head))
            throw new InvalidOperationException("The household head must be an adult.");
        if (data.RentArrears.IsPositive || data.UtilityArrears.IsPositive)
            throw new InvalidOperationException("A household in arrears cannot buy a home.");

        var seller = registry.EffectiveOwner(unit);
        if (seller == household)
            return true;

        var occupant = registry.HouseholdIn(unit);
        if (occupant != Entity.Null && occupant != household)
            throw new InvalidOperationException("The unit is occupied by another household.");

        var unitData = world.Get<HousingUnit>(unit);
        var price = PurchasePrice(unitData);
        if (finance.TotalWealth(household) < price)
            throw new InvalidOperationException("The household does not have enough wealth to buy this unit.");

        finance.TryDebit(
            household,
            price,
            LedgerKind.PropertyPurchase,
            tick,
            OwnerLedgerId(seller),
            "housing.purchase",
            out var shortfall);
        if (shortfall.IsPositive)
            throw new InvalidOperationException("The household could not pay the purchase price.");

        CreditOwner(seller, price, tick);

        var currentUnit = registry.UnitOf(household);
        if (currentUnit != Entity.Null && currentUnit != unit)
            registry.MoveOut(currentUnit, household);

        registry.TransferUnit(unit, household);
        allocator.TryAssign(household, unit, Tenure.Owner, tick, Money.Zero, "housing.purchase");

        economy.Emit(new HomePurchased(
            householdId,
            unitId,
            NetworkIdOf(seller),
            price.Cents,
            tick));
        return true;
    }

    public bool Sell(ulong householdId, ulong unitId, ulong buyerHouseholdId, long tick)
    {
        if (!TryGetHousehold(new NetworkId(householdId), out var seller))
            throw new InvalidOperationException("Seller household not found.");
        if (!TryGetUnit(new NetworkId(unitId), out var unit))
            throw new InvalidOperationException("Unit not found.");
        if (registry.EffectiveOwner(unit) != seller)
            throw new InvalidOperationException("The household does not own this unit.");

        var occupant = registry.HouseholdIn(unit);
        if (occupant != Entity.Null && occupant != seller)
            throw new InvalidOperationException("The unit is occupied by a tenant.");

        var price = PurchasePrice(world.Get<HousingUnit>(unit));
        if (buyerHouseholdId == 0)
            throw new InvalidOperationException("A buyer household is required for a sale.");

        if (!TryGetHousehold(new NetworkId(buyerHouseholdId), out var buyer))
            throw new InvalidOperationException("Buyer household not found.");
        if (buyer == seller)
            throw new InvalidOperationException("A household cannot buy its own home.");

        var buyerData = world.Get<Household>(buyer);
        if (buyerData.RentArrears.IsPositive || buyerData.UtilityArrears.IsPositive)
            throw new InvalidOperationException("A household in arrears cannot buy a home.");
        if (finance.TotalWealth(buyer) < price)
            throw new InvalidOperationException("The buyer does not have enough wealth.");

        finance.TryDebit(
            buyer,
            price,
            LedgerKind.PropertyPurchase,
            tick,
            OwnerLedgerId(seller),
            "housing.sale",
            out var shortfall);
        if (shortfall.IsPositive)
            throw new InvalidOperationException("The buyer could not pay the purchase price.");

        CreditOwner(seller, price, tick);

        if (occupant == seller)
            registry.MoveOut(unit, seller);

        var buyerUnit = registry.UnitOf(buyer);
        if (buyerUnit != Entity.Null && buyerUnit != unit)
            registry.MoveOut(buyerUnit, buyer);

        registry.TransferUnit(unit, buyer);
        allocator.TryAssign(buyer, unit, Tenure.Owner, tick, Money.Zero, "housing.sale");

        if (registry.UnitOf(seller) == Entity.Null)
            allocator.TryAssignBest(seller, tick);

        economy.Emit(new HomeSold(
            householdId,
            unitId,
            buyerHouseholdId,
            price.Cents,
            tick));
        return true;
    }

    public Entity StartConstruction(
        ulong developerId,
        string district,
        HousingTier tier,
        int units,
        long tick)
    {
        var developer = developerId == 0 ? Entity.Null : ResolveOwner(developerId);
        return new HousingDevelopmentSystem(world, economy).StartProject(
            developer,
            district,
            tier,
            units,
            tick);
    }

    public bool CancelConstruction(ulong projectId, long tick)
    {
        if (!registry.EntityByNetworkId.TryGetValue(new NetworkId(projectId), out var entity))
            return false;
        if (!world.IsAlive(entity) || !world.Has<ConstructionProject>(entity))
            return false;

        registry.DetachNetworkId(entity);
        world.Destroy(entity);
        economy.Emit(new ConstructionCancelled(projectId, "admin", tick));
        return true;
    }

    public bool SetMarketIndex(decimal index)
    {
        var options = economy.HousingOptions;
        registry.Market.Index = Math.Clamp(index, options.MinRentIndex, options.MaxRentIndex);
        return true;
    }

    public bool TryGetProperty(NetworkId propertyId, out Entity entity)
    {
        if (networkIds.TryGetEntity(propertyId, out entity) &&
            world.IsAlive(entity) &&
            world.Has<Property>(entity))
            return true;
        entity = Entity.Null;
        return false;
    }

    public bool TryGetUnit(NetworkId unitId, out Entity entity)
    {
        if (networkIds.TryGetEntity(unitId, out entity) &&
            world.IsAlive(entity) &&
            world.Has<HousingUnit>(entity))
            return true;
        entity = Entity.Null;
        return false;
    }

    public bool TryGetHousehold(NetworkId householdId, out Entity entity)
    {
        if (networkIds.TryGetEntity(householdId, out entity) &&
            world.IsAlive(entity) &&
            world.Has<Household>(entity))
            return true;
        entity = Entity.Null;
        return false;
    }

    private Entity ResolveOwner(ulong ownerId)
    {
        if (ownerId == 0)
            return Entity.Null;
        if (!networkIds.TryGetEntity(new NetworkId(ownerId), out var owner) || !world.IsAlive(owner))
            throw new InvalidOperationException("Owner not found.");
        if (!world.Has<CompanyName>(owner) && !world.Has<Household>(owner))
            throw new InvalidOperationException("Owner must be a company or household.");
        return owner;
    }

    private static Money PurchasePrice(HousingUnit unit)
    {
        var price = unit.PurchasePrice.IsPositive
            ? unit.PurchasePrice
            : HousingTierPolicy.PurchasePrice(unit.Tier);
        var conditionFactor = 0.6m + 0.4m * (decimal)Math.Clamp(unit.Condition, 0f, 1f);
        return price.Multiply(conditionFactor);
    }

    private bool TryDebitOwner(Entity owner, Money amount, long tick)
    {
        if (!amount.IsPositive)
            return true;

        if (owner == Entity.Null)
        {
            if (economy.Government.Balance < amount)
                return false;
            economy.Government.Balance -= amount;
            economy.Ledger.Record(
                Guid.NewGuid(),
                tick,
                LedgerKind.MaintenancePaid,
                EconomyAccountIds.Government,
                null,
                amount,
                "housing.repair.municipal");
            return true;
        }

        if (world.Has<CompanyName>(owner))
        {
            return economy.Accounts.TryDebit(
                owner,
                amount,
                LedgerKind.MaintenancePaid,
                Guid.NewGuid(),
                null,
                "housing.repair",
                tick);
        }

        if (world.Has<Household>(owner))
        {
            if (finance.TotalWealth(owner) < amount)
                return false;
            finance.TryDebit(
                owner,
                amount,
                LedgerKind.MaintenancePaid,
                tick,
                null,
                "housing.repair",
                out var shortfall);
            return shortfall.IsZero;
        }

        return false;
    }

    private void CreditOwner(Entity owner, Money amount, long tick)
    {
        if (!amount.IsPositive)
            return;

        if (owner == Entity.Null)
        {
            economy.Government.Balance += amount;
            return;
        }

        if (world.Has<CompanyName>(owner))
        {
            economy.Accounts.ApplySettlement(owner, amount, tick, revenue: true);
            return;
        }

        if (world.Has<Household>(owner))
        {
            var head = finance.Head(owner);
            if (head != Entity.Null && world.IsAlive(head))
            {
                if (!world.Has<Wallet>(head))
                    world.Add(head, new Wallet(Money.Zero));
                world.Get<Wallet>(head).Balance += amount;
            }
            return;
        }

        economy.Government.Balance += amount;
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;

    private ulong OwnerLedgerId(Entity owner) =>
        owner == Entity.Null ? EconomyAccountIds.Government : NetworkIdOf(owner);
}
