using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Components.Map;
using Genelife.Map;
using Genelife.Economy;
using Genelife.Family;
using Genelife.Application.Systems;

namespace Genelife.Application.Systems;

/// <summary>
/// Monthly local exposure refresh. Builds a spatial index once, then asks the
/// candidate generator for each adult's small local candidate set.
/// </summary>
public sealed class RelationshipExposureSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly RelationshipIndex relationships;
    private readonly RelationshipOptions options;
    private readonly RelationshipCache cache;
    private readonly CandidateGenerator generator;
    private readonly int cadenceOffset;

    public RelationshipExposureSystem(
        World world,
        EconomyContext economy,
        RelationshipIndex relationships,
        RelationshipOptions options,
        RelationshipCache cache,
        int cadenceOffset = 0)
    {
        this.world = world;
        this.economy = economy;
        this.relationships = relationships;
        this.options = options;
        this.cache = cache;
        this.cadenceOffset = cadenceOffset;
        generator = new CandidateGenerator(world, economy, relationships, options);
    }

    public string Name => "RelationshipExposure";
    public int Order => 355;
    public Cadence Cadence => new(Math.Max(1, economy.Clock.TicksPerMonth), cadenceOffset);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        options.Validate();

        var spatial = SpatialIndex.CreateDefault();
        world.Query(in new QueryDescription().WithAll<Alive, Location>().WithNone<Dead>(),
            (Entity entity, ref Alive _, ref Location location) => spatial.Add(entity, location.Position));

        var studentsBySchool = new Dictionary<Entity, List<Entity>>();
        world.Query(in new QueryDescription().WithAll<Enrollment>().WithNone<Dead>(),
            (Entity entity, ref Enrollment enrollment) =>
            {
                if (!studentsBySchool.TryGetValue(enrollment.School, out var students))
                {
                    students = new List<Entity>();
                    studentsBySchool[enrollment.School] = students;
                }
                students.Add(entity);
            });

        var genoses = new List<Entity>();
        world.Query(in new QueryDescription().WithAll<Alive, Traits, NetworkId>().WithNone<Dead>(),
            (Entity entity, ref Alive _, ref Traits _, ref NetworkId _) => genoses.Add(entity));
        genoses.Sort((a, b) =>
        {
            var byNetworkId = NetworkIdOf(a).CompareTo(NetworkIdOf(b));
            return byNetworkId != 0 ? byNetworkId : a.Id.CompareTo(b.Id);
        });

        cache.Candidates.Clear();
        cache.Rejections.Clear();
        cache.Scored.Clear();

        foreach (var genos in genoses)
        {
            if (!world.IsAlive(genos) || relationships.HasPartner(genos))
                continue;

            var result = generator.Generate(genos, spatial, studentsBySchool, cache, tick);
            cache.Candidates[genos] = result.Candidates.ToList();
            if (result.Rejections.Count > 0)
                cache.Rejections[genos] = result.Rejections.ToList();
        }
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
}
