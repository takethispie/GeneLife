using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Housing;
using Genelife.Values;

namespace Genelife.UnitTests.Housing;

public class HousingDomainTests
{
    [Fact]
    public void Registry_move_in_and_out_keeps_caches_and_verification_clean()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var registry = economy.Housing;
        var (_, unit) = TestHousing.CreateUnit(world, economy, 10, 11);
        var sim = TestHousing.Sim(world, 1, 500m);
        var household = world.Create(new Household
        {
            Head = sim,
            Tenure = Tenure.Renter,
            MonthlyRent = Money.FromUnits(120m)
        });
        world.Add(sim, new HouseholdMember(household, HouseholdRole.Head));
        registry.AddMember(sim, household);

        Assert.False(registry.TryGetUnitForSim(sim, out _));
        registry.MoveIn(unit, household);

        Assert.True(registry.TryGetUnitForSim(sim, out var found));
        Assert.Equal(unit, found);
        Assert.Equal(household, registry.HouseholdIn(unit));
        Assert.Equal(1, registry.MemberCount(household));
        Assert.Empty(registry.Verify());

        var partner = TestHousing.Sim(world, 2, 500m);
        world.Add(partner, new HouseholdMember(household, HouseholdRole.Partner));
        registry.AddMember(partner, household);
        Assert.Equal(2, world.Get<HousingUnit>(unit).OccupantCount);
        registry.RemoveMember(partner);
        Assert.Equal(1, world.Get<HousingUnit>(unit).OccupantCount);
        Assert.Empty(registry.Verify());

        registry.MoveOut(unit, household);

        Assert.False(registry.TryGetUnitForSim(sim, out _));
        Assert.Equal(Entity.Null, world.Get<Household>(household).Unit);
        Assert.True(world.Get<HousingUnit>(unit).IsVacant);
        Assert.Empty(registry.Verify());
    }

    [Fact]
    public void Household_finance_pays_head_then_adults_then_savings_and_never_children()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var registry = economy.Housing;
        var head = TestHousing.Sim(world, 1, 50m);
        var partner = TestHousing.Sim(world, 2, 200m);
        var child = TestHousing.Sim(world, 3, 500m, age: 10);
        world.Add(head, new SavingsAccount(new Money(100_00)));
        world.Add(partner, new SavingsAccount(new Money(1000_00)));

        var household = world.Create(new Household { Head = head, Tenure = Tenure.Renter });
        world.Add(household, new NetworkId(900));
        world.Add(head, new HouseholdMember(household, HouseholdRole.Head));
        world.Add(partner, new HouseholdMember(household, HouseholdRole.Partner));
        world.Add(child, new HouseholdMember(household, HouseholdRole.Child));
        registry.AddMember(head, household);
        registry.AddMember(partner, household);
        registry.AddMember(child, household);

        var finance = new HouseholdFinanceService(world, economy);
        var ledgerBefore = economy.Ledger.RecordedCount;
        var totalBefore = economy.Ledger.TotalRecorded;
        var paid = finance.TryDebit(
            household,
            new Money(300_00),
            LedgerKind.RentPaid,
            100,
            55,
            "test.rent",
            out var shortfall);

        Assert.True(paid);
        Assert.Equal(Money.Zero, shortfall);
        Assert.Equal(Money.Zero, world.Get<Wallet>(head).Balance);
        Assert.Equal(Money.Zero, world.Get<Wallet>(partner).Balance);
        Assert.Equal(new Money(500_00), world.Get<Wallet>(child).Balance);
        Assert.Equal(new Money(50_00), world.Get<SavingsAccount>(head).Balance);
        Assert.Equal(new Money(1000_00), world.Get<SavingsAccount>(partner).Balance);
        Assert.Equal(3, economy.Ledger.RecordedCount - ledgerBefore);
        Assert.Equal(new Money(300_00), economy.Ledger.TotalRecorded - totalBefore);
    }

    [Fact]
    public void Tier_ladder_is_ordered_and_purchase_price_uses_rent_multiple()
    {
        Assert.True(HousingTierPolicy.BaseRent(HousingTier.Basic) <
                    HousingTierPolicy.BaseRent(HousingTier.Comfortable));
        Assert.True(HousingTierPolicy.BaseRent(HousingTier.Comfortable) <
                    HousingTierPolicy.BaseRent(HousingTier.Luxury));
        Assert.True(HousingTierPolicy.Capacity(HousingTier.Basic) <
                    HousingTierPolicy.Capacity(HousingTier.Luxury));
        Assert.Equal(
            HousingTierPolicy.BaseRent(HousingTier.Basic).Multiply(240m),
            HousingTierPolicy.PurchasePrice(HousingTier.Basic));
    }
}

internal static class TestHousing
{
    public static Entity Sim(World world, ulong networkId, decimal wallet, int age = 30)
    {
        var entity = world.Create(
            new GenName($"Sim {networkId}"),
            new Needs(),
            new CurrentAction(ActionType.Idle, 0f),
            new Alive(age),
            new Wallet(Money.FromUnits(wallet)));
        world.Add(entity, new NetworkId(networkId));
        return entity;
    }

    public static (Entity Property, Entity Unit) CreateUnit(
        World world,
        EconomyContext economy,
        ulong propertyId,
        ulong unitId,
        HousingTier tier = HousingTier.Basic)
    {
        var property = world.Create(new Property("Test Property", "Central", Entity.Null, 1));
        world.Add(property, new NetworkId(propertyId));
        economy.Housing.AddProperty(property, Entity.Null);

        var unit = world.Create(new HousingUnit(
            property,
            "1",
            tier,
            HousingTierPolicy.Capacity(tier),
            HousingTierPolicy.BaseRent(tier),
            1f));
        world.Add(unit, new NetworkId(unitId));
        economy.Housing.AddUnit(unit, property);
        return (property, unit);
    }

    public static Entity Household(
        World world,
        HousingRegistry registry,
        Entity head,
        Entity unit,
        decimal rent)
    {
        var household = world.Create(new Household
        {
            Head = head,
            Tenure = Tenure.Renter,
            Unit = Entity.Null,
            MonthlyRent = Money.FromUnits(rent),
            LastRentTick = 0
        });
        world.Add(head, new HouseholdMember(household, HouseholdRole.Head));
        registry.AddMember(head, household);
        registry.MoveIn(unit, household);
        return household;
    }
}
