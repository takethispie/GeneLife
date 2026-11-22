using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Housing;

public readonly record struct UnitCandidate(
    Entity Unit,
    Entity Property,
    HousingTier Tier,
    Money Rent,
    int Capacity,
    int Occupants,
    float Condition,
    float Neighbourhood);

/// <summary>
/// Deterministic allocation and move-in logic shared by assignment, eviction,
/// voluntary moves and admin commands.
/// </summary>
public sealed class HousingAllocator
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly HousingRegistry registry;
    private readonly HouseholdFinanceService finance;

    public HousingAllocator(World world, EconomyContext economy)
    {
        this.world = world;
        this.economy = economy;
        registry = economy.Housing;
        finance = new HouseholdFinanceService(world, economy);
    }

    public Money MarketRent(Entity unit)
    {
        if (!world.IsAlive(unit) || !world.Has<HousingUnit>(unit))
            return Money.Zero;

        var data = world.Get<HousingUnit>(unit);
        var conditionFactor = 0.6m + 0.4m * (decimal)Math.Clamp(data.Condition, 0f, 1f);
        return data.BaseRent
            .Multiply(registry.Market.Index)
            .Multiply(conditionFactor)
            .Max(Money.Zero);
    }

    public List<UnitCandidate> RankVacant(
        Money monthlyIncome,
        int members,
        bool includeShelter = true,
        bool requireAffordable = false,
        HousingTier? maxTier = null)
    {
        var candidates = new List<UnitCandidate>();
        foreach (var unit in registry.VacantUnits())
        {
            if (!world.IsAlive(unit) || !world.Has<HousingUnit>(unit))
                continue;

            var data = world.Get<HousingUnit>(unit);
            if (!world.IsAlive(data.Property) || !world.Has<Property>(data.Property))
                continue;
            if (data.Condition < HousingCostPolicy.UninhabitableThreshold)
                continue;
            if (data.Tier == HousingTier.Shelter && !includeShelter)
                continue;
            if (maxTier is not null && data.Tier > maxTier.Value)
                continue;
            if (data.Tier != HousingTier.Shelter && data.Capacity < members)
                continue;

            var property = world.Get<Property>(data.Property);
            var rent = MarketRent(unit);
            if (requireAffordable && monthlyIncome.IsPositive &&
                rent > monthlyIncome.Multiply(economy.HousingOptions.AffordabilityRatio))
                continue;

            candidates.Add(new UnitCandidate(
                unit,
                data.Property,
                data.Tier,
                rent,
                data.Capacity,
                registry.MemberCount(registry.HouseholdIn(unit)),
                data.Condition,
                property.NeighbourhoodScore));
        }

        candidates.Sort((a, b) =>
        {
            var byRent = a.Rent.Cents.CompareTo(b.Rent.Cents);
            if (byRent != 0)
                return byRent;
            var byShelter = (a.Tier == HousingTier.Shelter ? 1 : 0)
                .CompareTo(b.Tier == HousingTier.Shelter ? 1 : 0);
            if (byShelter != 0)
                return byShelter;
            var byNeighbourhood = b.Neighbourhood.CompareTo(a.Neighbourhood);
            if (byNeighbourhood != 0)
                return byNeighbourhood;
            return a.Condition.CompareTo(b.Condition);
        });

        return candidates;
    }

    public bool TryFindBest(
        Entity household,
        out Entity unit,
        out bool sheltered,
        bool includeShelter = true,
        Money? incomeOverride = null,
        Entity excludeUnit = default)
    {
        unit = Entity.Null;
        sheltered = false;

        var members = Math.Max(1, registry.MemberCount(household));
        var income = incomeOverride ?? finance.MonthlyIncome(household);

        var candidates = RankVacant(income, members, includeShelter: false, requireAffordable: true);
        if (candidates.Count == 0)
            candidates = RankVacant(income, members, includeShelter: false, requireAffordable: false);

        if (excludeUnit != Entity.Null)
            candidates.RemoveAll(candidate => candidate.Unit == excludeUnit);

        if (candidates.Count > 0)
        {
            unit = candidates[0].Unit;
            return true;
        }

        if (!includeShelter)
            return false;

        var shelter = RankVacant(income, members, includeShelter: true, requireAffordable: false);
        foreach (var candidate in shelter)
        {
            if (candidate.Tier != HousingTier.Shelter)
                continue;
            if (candidate.Unit == excludeUnit)
                continue;
            unit = candidate.Unit;
            sheltered = true;
            return true;
        }

        return false;
    }

    public bool TryAssign(
        Entity household,
        Entity unit,
        Tenure tenure,
        long tick,
        Money? rentOverride = null,
        string reference = "housing.assign",
        bool resetArrears = true)
    {
        if (!world.IsAlive(household) || !world.Has<Household>(household))
            return false;
        if (!world.IsAlive(unit) || !world.Has<HousingUnit>(unit))
            return false;

        var currentUnit = registry.UnitOf(household);
        if (currentUnit != Entity.Null && currentUnit != unit)
            registry.MoveOut(currentUnit);

        registry.MoveIn(unit, household);

        var householdData = world.Get<Household>(household);
        householdData.Tenure = tenure;
        householdData.MonthlyRent = tenure == Tenure.Owner
            ? Money.Zero
            : rentOverride ?? MarketRent(unit);
        householdData.MoveInTick = tick;
        if (resetArrears)
        {
            householdData.ArrearsCycles = 0;
            householdData.RentArrears = Money.Zero;
            householdData.UtilityArrears = Money.Zero;
        }
        householdData.NoticeStage = 0;
        householdData.NoticeStartedTick = 0;
        householdData.CureDeadlineTick = 0;
        householdData.OverburdenMonths = 0;
        householdData.LastRentTick = tick;
        householdData.LastUtilityTick = tick;
        householdData.MemberCount = registry.MemberCount(household);
        world.Get<Household>(household) = householdData;

        var propertyOwner = world.Get<HousingUnit>(unit).Property;
        var landlord = world.IsAlive(propertyOwner) && world.Has<Property>(propertyOwner)
            ? registry.EffectiveOwner(unit)
            : Entity.Null;

        economy.Emit(new TenancyStarted(
            NetworkIdOf(household),
            NetworkIdOf(unit),
            NetworkIdOf(landlord),
            tenure.ToString(),
            householdData.MonthlyRent.Cents,
            tick));
        economy.Emit(new HouseholdRelocated(
            NetworkIdOf(household),
            NetworkIdOf(currentUnit),
            NetworkIdOf(unit),
            tick));

        if (reference == "housing.move")
            registry.Market.MovesThisYear++;

        return true;
    }

    public bool TryAssignBest(Entity household, long tick, Money? incomeOverride = null)
    {
        if (!TryFindBest(household, out var unit, out var sheltered, incomeOverride: incomeOverride))
        {
            economy.Emit(new HouseholdHomeless(NetworkIdOf(household), tick));
            return false;
        }

        return TryAssign(
            household,
            unit,
            sheltered ? Tenure.Sheltered : Tenure.Renter,
            tick,
            reference: "housing.assign");
    }

    public ulong NetworkIdOf(Entity entity) =>
        world.IsAlive(entity) && world.Has<Genelife.Components.Gen.NetworkId>(entity)
            ? world.Get<Genelife.Components.Gen.NetworkId>(entity).Value
            : 0UL;
}
