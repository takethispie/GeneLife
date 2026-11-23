using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Components.Map;
using Genelife.Economy;
using Genelife.Family;
using Genelife.Systems;

namespace Genelife.Family;

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

    public RelationshipExposureSystem(
        World world,
        EconomyContext economy,
        RelationshipIndex relationships,
        RelationshipOptions options,
        RelationshipCache cache)
    {
        this.world = world;
        this.economy = economy;
        this.relationships = relationships;
        this.options = options;
        this.cache = cache;
        generator = new CandidateGenerator(world, economy, relationships, options);
    }

    public string Name => "RelationshipExposure";
    public int Order => 355;
    public Cadence Cadence => new(Math.Max(1, economy.Clock.TicksPerMonth));

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

        var sims = new List<Entity>();
        world.Query(in new QueryDescription().WithAll<Alive, Traits, NetworkId>().WithNone<Dead>(),
            (Entity entity, ref Alive _, ref Traits _, ref NetworkId _) => sims.Add(entity));
        sims.Sort((a, b) =>
        {
            var byNetworkId = NetworkIdOf(a).CompareTo(NetworkIdOf(b));
            return byNetworkId != 0 ? byNetworkId : a.Id.CompareTo(b.Id);
        });

        cache.Candidates.Clear();
        cache.Rejections.Clear();
        cache.Scored.Clear();

        foreach (var sim in sims)
        {
            if (!world.IsAlive(sim) || relationships.HasPartner(sim))
                continue;

            var result = generator.Generate(sim, spatial, studentsBySchool, cache, tick);
            cache.Candidates[sim] = result.Candidates.ToList();
            if (result.Rejections.Count > 0)
                cache.Rejections[sim] = result.Rejections.ToList();
        }
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
}
