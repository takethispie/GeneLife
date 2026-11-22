using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Housing;
using Genelife.Enums;
using Genelife.Economy;
using Genelife.Map;
using Genelife.Values;

namespace Genelife.Housing;

/// <summary>
/// Creates the initial housing stock for a world. Used by migration, the admin
/// seed command and scenario harnesses.
/// </summary>
public static class HousingBootstrap
{
    public static int CreateStock(
        World world,
        EconomyContext economy,
        long tick,
        string district = "Central",
        MapContext? map = null)
    {
        var created = 0;
        created += CreateProperty(world, economy, district, HousingTier.Basic, 30, 0.6f, 0.5f, tick, map: map);
        created += CreateProperty(world, economy, district, HousingTier.Comfortable, 12, 0.8f, 0.65f, tick, map: map);
        created += CreateProperty(world, economy, district, HousingTier.Luxury, 3, 0.95f, 0.8f, tick, map: map);
        created += CreateShelter(world, economy, district, 6, tick, map);
        return created;
    }

    public static int CreateProperty(
        World world,
        EconomyContext economy,
        string district,
        HousingTier tier,
        int unitCount,
        float condition,
        float neighbourhood,
        long tick,
        Entity owner = default,
        string? name = null,
        MapContext? map = null)
    {
        if (unitCount <= 0)
            return 0;

        var property = world.Create(new Property(
            name ?? $"{district} {tier}",
            district,
            owner,
            unitCount,
            condition,
            neighbourhood,
            economy.HousingOptions.DefaultPropertyUpkeep,
            tier == HousingTier.Shelter));
        var propertyId = economy.Housing.AllocateNetworkId(property);
        economy.Housing.AddProperty(property, owner);

        if (map is not null)
        {
            var footprint = BuildingFootprints.ForProperty(unitCount, tier == HousingTier.Shelter);
            var kind = tier == HousingTier.Shelter ? BuildingKind.Civic : BuildingKind.Residential;
            var placement = map.Placement.TryPlace(property, propertyId, kind, footprint, district);
            if (!placement.Success)
            {
                throw new InvalidOperationException(
                    $"Unable to place {tier} property in {district}: {placement.Error} {placement.Message}");
            }
        }

        for (var i = 0; i < unitCount; i++)
        {
            var unit = world.Create(new HousingUnit(
                property,
                $"{propertyId.Value}-{i + 1}",
                tier,
                HousingTierPolicy.Capacity(tier),
                HousingTierPolicy.BaseRent(tier),
                condition,
                isVacant: true,
                owner: Entity.Null,
                purchasePrice: HousingTierPolicy.PurchasePrice(tier)));
            economy.Housing.AllocateNetworkId(unit);
            economy.Housing.AddUnit(unit, property);
        }

        economy.Emit(new Messages.Events.PropertyCreated(
            propertyId.Value,
            0,
            world.Get<Property>(property).Name,
            district,
            unitCount,
            tier.ToString(),
            tick));
        return unitCount;
    }

    public static int CreateShelter(
        World world,
        EconomyContext economy,
        string district,
        int beds,
        long tick,
        MapContext? map = null) =>
        CreateProperty(world, economy, district, HousingTier.Shelter, beds, 0.2f, 0.3f, tick, map: map);
}
