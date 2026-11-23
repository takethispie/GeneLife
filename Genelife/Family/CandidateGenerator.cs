using System.Numerics;
using Arch.Core;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Components.Map;
using Genelife.Economy;
using Genelife.Systems;

namespace Genelife.Family;

public sealed record CandidateGenerationResult(
    IReadOnlyList<Entity> Candidates,
    IReadOnlyList<RejectedCandidate> Rejections,
    int Inspected);

/// <summary>
/// Local candidate generation from household, workplace, school and spatial
/// neighbours. It never scans the full population per sim.
/// </summary>
public sealed class CandidateGenerator
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly RelationshipIndex relationships;
    private readonly RelationshipOptions options;

    public CandidateGenerator(
        World world,
        EconomyContext economy,
        RelationshipIndex relationships,
        RelationshipOptions options)
    {
        this.world = world;
        this.economy = economy;
        this.relationships = relationships;
        this.options = options;
    }

    public CandidateGenerationResult Generate(
        Entity sim,
        SpatialIndex spatial,
        IReadOnlyDictionary<Entity, List<Entity>> studentsBySchool,
        RelationshipCache? cache = null,
        long tick = 0)
    {
        var raw = new HashSet<Entity>();

        if (economy.Housing.HouseholdBySim.TryGetValue(sim, out var household))
        {
            foreach (var member in economy.Housing.GetMembers(household))
                raw.Add(member);
        }

        if (world.Has<Employee>(sim))
        {
            var company = world.Get<Employee>(sim).Company;
            if (economy.Employment.EmployeesByCompany.TryGetValue(company, out var employees))
            {
                foreach (var employee in employees)
                    raw.Add(employee);
            }
        }

        if (world.Has<Enrollment>(sim))
        {
            var school = world.Get<Enrollment>(sim).School;
            if (studentsBySchool.TryGetValue(school, out var students))
            {
                foreach (var student in students)
                    raw.Add(student);
            }
        }

        if (world.Has<Location>(sim))
        {
            var position = world.Get<Location>(sim).Position;
            foreach (var (entity, _) in spatial.GetNeighbors(
                         position,
                         options.NeighbourRadiusTiles,
                         GetPosition))
            {
                raw.Add(entity);
            }
        }

        raw.Remove(sim);

        var candidates = new List<Entity>(raw.Count);
        var rejections = new List<RejectedCandidate>();
        foreach (var candidate in raw)
        {
            var eligibility = EligibilityPolicy.Evaluate(
                world,
                relationships,
                sim,
                candidate,
                options,
                (first, second) => cache?.InCooldown(first, second, tick) == true);

            if (eligibility.Eligible)
                candidates.Add(candidate);
            else
                rejections.Add(new RejectedCandidate(
                    candidate,
                    eligibility.Code ?? "ineligible"));
        }

        candidates.Sort((a, b) =>
        {
            var byNetworkId = NetworkIdOf(a).CompareTo(NetworkIdOf(b));
            return byNetworkId != 0 ? byNetworkId : a.Id.CompareTo(b.Id);
        });

        if (candidates.Count > options.MaxCandidatesPerSim)
            candidates.RemoveRange(options.MaxCandidatesPerSim, candidates.Count - options.MaxCandidatesPerSim);

        return new CandidateGenerationResult(candidates, rejections, raw.Count);
    }

    private Vector2 GetPosition(Entity entity) =>
        world.IsAlive(entity) && world.Has<Location>(entity)
            ? world.Get<Location>(entity).Position
            : Vector2.Zero;

    private ulong NetworkIdOf(Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
}
