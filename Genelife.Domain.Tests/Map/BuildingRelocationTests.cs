using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Consumption;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Components.Map;
using Genelife.Components.Social;
using Genelife.Enums;
using Genelife.Map;
using Genelife.Values;

namespace Genelife.UnitTests.Map;

public class BuildingRelocationTests
{
    private static MapContext SmallMap(World world) =>
        MapContext.Generate(
            world,
            new MapOptions { Seed = 7, Width = 64, Height = 64 },
            new MapGenerationOptions { RoadSpacing = 10, WaterFraction = 0.01f });

    private static Entity PlaceResidential(MapContext map, World world, ulong id, string district = "Central")
    {
        var property = world.Create(
            new Property("Home", district, Entity.Null, 10, 0.8f, 0.5f, Money.FromUnits(10m)),
            new NetworkId(id));
        var placed = map.Placement.TryPlace(
            property, new NetworkId(id), BuildingKind.Residential, new Footprint(4, 3), district);
        Assert.True(placed.Success, placed.Message);
        return property;
    }

    private static (int X, int Y, int EntryX, int EntryY) FindDestination(
        WorldMap map, NetworkId id, TileCoord current)
    {
        for (var y = map.Options.Bounds.MinY; y <= map.Options.Bounds.MaxY - 3; y++)
        {
            for (var x = map.Options.Bounds.MinX; x <= map.Options.Bounds.MaxX - 4; x++)
            {
                if (x == current.X && y == current.Y)
                    continue;

                foreach (var entry in new[]
                         {
                             new TileCoord(x + 4, y + 1),
                             new TileCoord(x - 1, y + 1),
                             new TileCoord(x + 1, y - 1),
                             new TileCoord(x + 1, y + 3)
                         })
                {
                    if (map.CanRelocate(id, new TileCoord(x, y), entry, out _, out _) == PlacementError.None)
                        return (x, y, entry.X, entry.Y);
                }
            }
        }

        throw new InvalidOperationException("No valid relocation destination was found.");
    }

    private static (int X, int Y, int EntryX, int EntryY) FindDestinationInLot(
        WorldMap map, NetworkId id, MapLot lot, Placement current)
    {
        var footprint = current.Footprint;
        for (var y = lot.Bounds.MinY; y <= lot.Bounds.MaxY - footprint.Height + 1; y++)
        {
            for (var x = lot.Bounds.MinX; x <= lot.Bounds.MaxX - footprint.Width + 1; x++)
            {
                if (x == current.Anchor.X && y == current.Anchor.Y)
                    continue;

                foreach (var entry in new[]
                         {
                             new TileCoord(x, y - 1),
                             new TileCoord(x - 1, y),
                             new TileCoord(x + footprint.Width, y),
                             new TileCoord(x, y + footprint.Height)
                         })
                {
                    if (map.CanRelocate(id, new TileCoord(x, y), entry, out _, out _) == PlacementError.None)
                        return (x, y, entry.X, entry.Y);
                }
            }
        }

        throw new InvalidOperationException("No valid relocation destination was found within the lot.");
    }

    [Fact]
    public void Relocating_within_a_reserved_lot_releases_the_reservation()
    {
        using var world = World.Create();
        var map = SmallMap(world);
        PlaceResidential(map, world, 70);
        var current = map.Map.Placements[new NetworkId(70)];
        var lot = map.Map.Lots.Single(item => item.Contains(current.Footprint, current.Anchor));
        Assert.True(map.Map.TryReserveLot(lot.Id, new NetworkId(70), out var reserved));
        Assert.True(reserved!.IsReserved);
        Assert.True(map.Map.ReservedTileCount > 0);

        var destination = FindDestinationInLot(map.Map, new NetworkId(70), lot, current);
        var result = new BuildingRelocationService(world, map)
            .TryRelocate(70, destination.X, destination.Y, destination.EntryX, destination.EntryY);

        Assert.True(result.Success, result.Message);
        Assert.True(map.Map.TryGetPlacement(new NetworkId(70), out var moved));
        Assert.Equal(new TileCoord(destination.X, destination.Y), moved.Anchor);
        Assert.Equal(lot.Id, map.Map.Lots.Single(item => item.Contains(moved.Footprint, moved.Anchor)).Id);
        Assert.False(lot.IsReserved);
        Assert.Null(lot.ReservedBy);
        Assert.Equal(0, map.Map.ReservedTileCount);
    }

    [Fact]
    public void Relocating_an_occupied_home_keeps_residents_and_advances_the_map()
    {
        using var world = World.Create();
        var map = SmallMap(world);
        var property = PlaceResidential(map, world, 10);
        var placed = map.Map.Placements[new NetworkId(10)];

        var unit = world.Create(
            new HousingUnit(property, "1A", HousingTier.Basic, 3, Money.FromUnits(100m)),
            new NetworkId(11));
        var household = world.Create(new NetworkId(12));
        var resident = world.Create(
            new NetworkId(1),
            new Residence(household, unit),
            new TilePosition(placed.Entry),
            new MoveTarget(placed.Entry));

        var destination = FindDestination(map.Map, new NetworkId(10), placed.Anchor);
        var service = new BuildingRelocationService(world, map);
        var result = service.TryRelocate(10, destination.X, destination.Y, destination.EntryX, destination.EntryY);

        Assert.True(result.Success, result.Message);
        Assert.Single(result.Plan!.Residents);
        Assert.True(map.Map.TryGetPlacement(new NetworkId(10), out var moved));
        Assert.Equal(new TileCoord(destination.X, destination.Y), moved.Anchor);
        Assert.Equal(new TileCoord(destination.EntryX, destination.EntryY), moved.Entry);
        Assert.Equal(moved, world.Get<Placement>(property));
        Assert.Equal(new TileCoord(destination.EntryX, destination.EntryY),
            world.Get<MoveTarget>(resident).Tile);
        Assert.True(map.Map.Version > 1);
        Assert.Equal(new NetworkId(10), map.Map.BuildingAt(moved.Anchor));
        Assert.Equal(default, map.Map.BuildingAt(placed.Anchor));
    }

    [Fact]
    public void Relocating_an_office_keeps_employees_and_retargets_commutes()
    {
        using var world = World.Create();
        var map = SmallMap(world);
        var office = world.Create(new NetworkId(20));
        var placed = map.Placement.TryPlace(
            office, new NetworkId(20), BuildingKind.Office, new Footprint(3, 3), "Central");
        Assert.True(placed.Success, placed.Message);

        var employee = world.Create(
            new NetworkId(2),
            new Employee(Entity.Null, office, "Analyst", Money.FromUnits(100m)),
            new TilePosition(placed.Placement.Entry),
            new MoveTarget(placed.Placement.Entry));

        var destination = FindDestination(map.Map, new NetworkId(20), placed.Placement.Anchor);
        var result = new BuildingRelocationService(world, map)
            .TryRelocate(20, destination.X, destination.Y, destination.EntryX, destination.EntryY);

        Assert.True(result.Success, result.Message);
        Assert.Single(result.Plan!.Employees);
        Assert.Equal(office, world.Get<Employee>(employee).Office);
        Assert.Equal(new TileCoord(destination.EntryX, destination.EntryY), world.Get<MoveTarget>(employee).Tile);
    }

    [Fact]
    public void Relocating_a_school_keeps_students_enrolled()
    {
        using var world = World.Create();
        var map = SmallMap(world);
        var school = world.Create(
            new School("Test School", "Central", 100, 0.6f),
            new NetworkId(30));
        var placed = map.Placement.TryPlace(
            school, new NetworkId(30), BuildingKind.School, new Footprint(4, 3), "Central");
        Assert.True(placed.Success, placed.Message);

        var student = world.Create(
            new NetworkId(3),
            new Enrollment(school, 0));

        var destination = FindDestination(map.Map, new NetworkId(30), placed.Placement.Anchor);
        var result = new BuildingRelocationService(world, map)
            .TryRelocate(30, destination.X, destination.Y, destination.EntryX, destination.EntryY);

        Assert.True(result.Success, result.Message);
        Assert.Single(result.Plan!.Students);
        Assert.Equal(school, world.Get<Enrollment>(student).School);
        Assert.False(world.Get<Enrollment>(student).IsUnenrolled);
    }

    [Fact]
    public void Invalid_destination_is_rejected_without_mutating_the_placement()
    {
        using var world = World.Create();
        var map = SmallMap(world);
        PlaceResidential(map, world, 40);
        var before = map.Map.Placements[new NetworkId(40)];
        var version = map.Map.Version;

        var result = new BuildingRelocationService(world, map).TryRelocate(40, -100, -100, -99, -99);

        Assert.False(result.Success);
        Assert.True(map.Map.TryGetPlacement(new NetworkId(40), out var after));
        Assert.Equal(before, after);
        Assert.Equal(version, map.Map.Version);
    }

    [Fact]
    public void Broken_utility_link_rejects_the_move()
    {
        using var world = World.Create();
        var map = SmallMap(world);
        var property = PlaceResidential(map, world, 50);
        var link = world.Create(
            new BuildingServiceLink
            {
                Network = Entity.Null,
                Service = UtilityService.Water,
                Demand = 1,
                Connected = true,
                Priority = 1
            },
            new ServiceLinkOwner { Building = property });

        var placed = map.Map.Placements[new NetworkId(50)];
        var destination = FindDestination(map.Map, new NetworkId(50), placed.Anchor);
        var result = new BuildingRelocationService(world, map)
            .TryRelocate(50, destination.X, destination.Y, destination.EntryX, destination.EntryY);

        Assert.False(result.Success);
        Assert.Contains("utility", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(world.IsAlive(link));
    }

    [Fact]
    public void Relocation_preserves_venue_presence_and_rejects_active_player_moves()
    {
        using var world = World.Create();
        var map = SmallMap(world);
        var property = PlaceResidential(map, world, 60);
        world.Add(property, new SocialVenue
        {
            Kind = SocialVenueKind.Civic,
            Capacity = 10,
            Activities = VenueActivityMask.Talk
        });
        var placed = map.Map.Placements[new NetworkId(60)];
        var unit = world.Create(new HousingUnit(property, "1A", HousingTier.Basic, 2, Money.FromUnits(100m)));
        var player = world.Create(
            new NetworkId(4),
            new PlayerControlled("player-1"),
            new Residence(Entity.Null, unit),
            new MoveTarget(placed.Entry));

        var destination = FindDestination(map.Map, new NetworkId(60), placed.Anchor);
        var service = new BuildingRelocationService(world, map);

        var rejected = service.TryRelocate(60, destination.X, destination.Y, destination.EntryX, destination.EntryY);
        Assert.False(rejected.Success);
        Assert.Contains("player", rejected.Message, StringComparison.OrdinalIgnoreCase);

        world.Remove<MoveTarget>(player);
        var allowed = service.TryRelocate(60, destination.X, destination.Y, destination.EntryX, destination.EntryY);
        Assert.True(allowed.Success, allowed.Message);
        Assert.True(allowed.Plan!.HasVenue);
    }
}
