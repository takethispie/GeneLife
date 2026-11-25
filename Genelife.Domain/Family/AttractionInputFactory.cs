using System.Numerics;
using Arch.Core;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;

namespace Genelife.Family;

/// <summary>
/// Builds the read-only input for the attraction scorer from world state.
/// Shared by the quarterly system and the admin explanation endpoint.
/// </summary>
public static class AttractionInputFactory
{
    public static AttractionInput Create(
        World world,
        EconomyContext economy,
        Entity first,
        Entity second,
        bool firstAvailable,
        bool secondAvailable)
    {
        var sameHousehold = economy.Housing.HouseholdOf(first) != Entity.Null &&
                            economy.Housing.HouseholdOf(first) == economy.Housing.HouseholdOf(second);

        var sameWorkplace = world.Has<Employee>(first) && world.Has<Employee>(second) &&
                            world.Get<Employee>(first).Company == world.Get<Employee>(second).Company;

        var sameSchool = world.Has<Enrollment>(first) && world.Has<Enrollment>(second) &&
                         world.Get<Enrollment>(first).School == world.Get<Enrollment>(second).School;

        return new AttractionInput(
            NetworkIdOf(world, first),
            NetworkIdOf(world, second),
            world.Get<Traits>(first),
            world.Get<Traits>(second),
            world.Get<Alive>(first).Age,
            world.Get<Alive>(second).Age,
            sameHousehold,
            sameWorkplace,
            sameSchool,
            TileDistance(world, first, second),
            Income(world, first),
            Income(world, second),
            world.Has<Employee>(first),
            world.Has<Employee>(second),
            IsHoused(world, economy, first),
            IsHoused(world, economy, second),
            Health(world, first),
            Health(world, second),
            firstAvailable,
            secondAvailable);
    }

    public static ulong NetworkIdOf(World world, Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;

    private static int TileDistance(World world, Entity first, Entity second)
    {
        if (!world.Has<Location>(first) || !world.Has<Location>(second))
            return int.MaxValue;

        var distance = Vector2.Distance(
            world.Get<Location>(first).Position,
            world.Get<Location>(second).Position);
        return (int)MathF.Round(distance);
    }

    private static decimal Income(World world, Entity entity)
    {
        var income = 0m;
        if (world.Has<Wallet>(entity))
            income += Math.Max(0m, world.Get<Wallet>(entity).Balance.Units);
        if (world.Has<SavingsAccount>(entity))
            income += Math.Max(0m, world.Get<SavingsAccount>(entity).Balance.Units);
        return income;
    }

    private static bool IsHoused(World world, EconomyContext economy, Entity entity)
    {
        var household = economy.Housing.HouseholdOf(entity);
        return household != Entity.Null && economy.Housing.UnitOf(household) != Entity.Null;
    }

    private static int Health(World world, Entity entity) =>
        world.Has<Components.Life.Health>(entity)
            ? (int)Math.Round(world.Get<Components.Life.Health>(entity).Value)
            : 0;
}
