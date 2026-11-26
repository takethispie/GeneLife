using Arch.Core;
using Genelife.Components.Company;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Components.Map;
using Genelife.Map;

namespace Genelife.Application.Systems;

public sealed record MapMetricsSnapshot(
    long Tick,
    int Year,
    int Buildings,
    int Residential,
    int Offices,
    int Schools,
    int OccupiedTiles,
    int ReservedTiles,
    int UnplacedBuildings,
    decimal BuiltDensity);

/// <summary>
/// Records a compact yearly map snapshot and keeps an in-memory history for the
/// admin surface. Expensive reconciliation is not performed here; it belongs to
/// restore, admin command application and the map health endpoint.
/// </summary>
public sealed class MapMetricsSystem : ISimulationSystem
{
    private readonly World world;
    private readonly WorldMap map;
    private readonly int interval;
    private readonly List<MapMetricsSnapshot> history = new();

    public MapMetricsSystem(World world, WorldMap map, int? interval = null)
    {
        this.world = world;
        this.map = map;
        this.interval = interval ?? 1200;
    }

    public string Name => "MapMetrics";
    public int Order => 612;
    public Cadence Cadence => new(Math.Max(1, interval));
    public IReadOnlyList<MapMetricsSnapshot> History => history.ToArray();

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var placements = map.Placements.Values;
        var buildings = map.Placements.Count;
        var occupied = map.OccupiedTileCount;
        var density = map.Options.Bounds.Area == 0
            ? 0m
            : Math.Round((decimal)occupied / map.Options.Bounds.Area, 4);

        var snapshot = new MapMetricsSnapshot(
            tick,
            (int)(tick / Math.Max(1, interval)),
            buildings,
            placements.Count(placement => placement.Kind == BuildingKind.Residential),
            placements.Count(placement => placement.Kind == BuildingKind.Office),
            placements.Count(placement => placement.Kind == BuildingKind.School),
            occupied,
            map.ReservedTileCount,
            CountUnplaced(),
            density);

        history.Add(snapshot);
        if (history.Count > 64)
            history.RemoveAt(0);
    }

    private int CountUnplaced()
    {
        var count = 0;
        var properties = new QueryDescription().WithAll<Property>().WithNone<Placement>();
        world.Query(in properties, (ref Property _) => count++);
        var offices = new QueryDescription().WithAll<Office>().WithNone<Placement>();
        world.Query(in offices, (ref Office _) => count++);
        var schools = new QueryDescription().WithAll<School>().WithNone<Placement>();
        world.Query(in schools, (ref School _) => count++);
        return count;
    }
}
