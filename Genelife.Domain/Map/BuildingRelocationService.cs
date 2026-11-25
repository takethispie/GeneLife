using Arch.Core;
using Genelife.Components.Consumption;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Components.Map;
using Genelife.Components.Social;
using Genelife.Map;

namespace Genelife.Map;

public sealed record BuildingRelocationPlan(
    NetworkId BuildingId,
    Placement Previous,
    Placement Proposed,
    IReadOnlyList<Entity> Residents,
    IReadOnlyList<Entity> Employees,
    IReadOnlyList<Entity> Students,
    IReadOnlyList<Entity> ServiceLinks,
    bool HasVenue);

public sealed record BuildingRelocationResult(
    bool Success,
    PlacementError Error,
    string? Message,
    BuildingRelocationPlan? Plan)
{
    public static BuildingRelocationResult Fail(PlacementError error, string message) =>
        new(false, error, message, null);

    public static BuildingRelocationResult Ok(BuildingRelocationPlan plan) =>
        new(true, PlacementError.None, null, plan);
}

/// <summary>
/// Validates and applies an occupied-building move. The move plan is computed
/// before any mutation, then the map occupancy, placement component and pending
/// paths are updated together. Occupants keep their entity references: homes,
/// employers and schools move with the building rather than being recreated.
/// </summary>
public sealed class BuildingRelocationService
{
    private readonly World world;
    private readonly MapContext mapContext;
    private readonly MovementIndex? movementIndex;

    public BuildingRelocationService(World world, MapContext mapContext, MovementIndex? movementIndex = null)
    {
        this.world = world;
        this.mapContext = mapContext;
        this.movementIndex = movementIndex;
    }

    public BuildingRelocationResult TryRelocate(ulong buildingIdValue, int x, int y, int entryX, int entryY)
    {
        var buildingId = new NetworkId(buildingIdValue);
        if (!mapContext.Map.TryGetPlacement(buildingId, out var previous))
            return BuildingRelocationResult.Fail(
                PlacementError.DuplicateNetworkId,
                $"Building {buildingIdValue} has no placement.");

        var anchor = new TileCoord(x, y);
        var entry = new TileCoord(entryX, entryY);
        var validation = mapContext.Map.CanRelocate(
            buildingId, anchor, entry, out _, out var validationMessage);
        if (validation != PlacementError.None)
            return BuildingRelocationResult.Fail(validation, validationMessage ?? validation.ToString());

        var buildingEntity = FindEntity(buildingId);
        if (buildingEntity == Entity.Null || !world.Has<Placement>(buildingEntity))
            return BuildingRelocationResult.Fail(
                PlacementError.DuplicateNetworkId,
                $"Building {buildingIdValue} has no placement component.");

        var residents = CollectResidents(buildingEntity);
        var employees = CollectEmployees(buildingEntity);
        var students = CollectStudents(buildingEntity);
        var serviceLinks = CollectServiceLinks(buildingEntity);
        var hasVenue = world.Has<SocialVenue>(buildingEntity);

        foreach (var linkEntity in serviceLinks)
        {
            if (!world.IsAlive(linkEntity))
                return BuildingRelocationResult.Fail(
                    PlacementError.EntryBlocked,
                    "A service link entity is missing.");

            var link = world.Get<BuildingServiceLink>(linkEntity);
            if (link.Connected && !world.IsAlive(link.Network))
                return BuildingRelocationResult.Fail(
                    PlacementError.EntryBlocked,
                    "Relocation would disconnect a utility service link.");
        }

        var dependents = residents.Concat(employees).Concat(students).Distinct().ToArray();
        foreach (var sim in dependents)
        {
            if (world.IsAlive(sim) && world.Has<PlayerControlled>(sim) &&
                world.Has<MoveTarget>(sim) && world.Get<MoveTarget>(sim).HasTarget)
                return BuildingRelocationResult.Fail(
                    PlacementError.EntryBlocked,
                    "A player move depends on this building; retry after it completes.");
        }

        var proposed = new Placement(anchor, previous.Footprint, entry, previous.Kind, previous.Rotation);
        var plan = new BuildingRelocationPlan(
            buildingId,
            previous,
            proposed,
            residents,
            employees,
            students,
            serviceLinks,
            hasVenue);

        var applied = mapContext.Map.Relocate(buildingId, anchor, entry);
        world.Get<Placement>(buildingEntity) = applied;

        RetargetReaders(dependents, previous.Entry, applied.Entry);
        movementIndex?.RebuildRequests(world, mapContext.Version);

        return BuildingRelocationResult.Ok(plan);
    }

    private Entity FindEntity(NetworkId id)
    {
        var found = Entity.Null;
        world.Query(in new QueryDescription().WithAll<NetworkId>(), (Entity entity, ref NetworkId candidate) =>
        {
            if (candidate == id)
                found = entity;
        });
        return found;
    }

    private List<Entity> CollectResidents(Entity building)
    {
        var residents = new List<Entity>();
        if (!world.IsAlive(building))
            return residents;

        world.Query(in new QueryDescription().WithAll<Residence>(), (Entity sim, ref Residence residence) =>
        {
            if (!world.IsAlive(residence.Unit) || !world.Has<HousingUnit>(residence.Unit))
                return;
            if (world.Get<HousingUnit>(residence.Unit).Property == building)
                residents.Add(sim);
        });
        return residents;
    }

    private List<Entity> CollectEmployees(Entity building)
    {
        var employees = new List<Entity>();
        if (!world.IsAlive(building))
            return employees;

        world.Query(in new QueryDescription().WithAll<Employee>(), (Entity sim, ref Employee employee) =>
        {
            if (employee.Office == building)
                employees.Add(sim);
        });
        return employees;
    }

    private List<Entity> CollectStudents(Entity building)
    {
        var students = new List<Entity>();
        if (!world.IsAlive(building))
            return students;

        world.Query(in new QueryDescription().WithAll<Enrollment>(), (Entity sim, ref Enrollment enrollment) =>
        {
            if (!enrollment.IsUnenrolled && enrollment.School == building)
                students.Add(sim);
        });
        return students;
    }

    private List<Entity> CollectServiceLinks(Entity building)
    {
        var links = new List<Entity>();
        if (!world.IsAlive(building))
            return links;

        world.Query(in new QueryDescription().WithAll<BuildingServiceLink, ServiceLinkOwner>(),
            (Entity entity, ref BuildingServiceLink _, ref ServiceLinkOwner owner) =>
            {
                if (owner.Building == building)
                    links.Add(entity);
            });
        return links;
    }

    private void RetargetReaders(IReadOnlyList<Entity> dependents, TileCoord oldEntry, TileCoord newEntry)
    {
        foreach (var sim in dependents)
        {
            if (!world.IsAlive(sim) || !world.Has<MoveTarget>(sim))
                continue;

            var target = world.Get<MoveTarget>(sim);
            if (!target.HasTarget || target.Tile != oldEntry)
                continue;

            target.Tile = newEntry;
            target.PathVersion = movementIndex?.PathVersion ?? target.PathVersion;
            world.Get<MoveTarget>(sim) = target;
        }
    }
}
