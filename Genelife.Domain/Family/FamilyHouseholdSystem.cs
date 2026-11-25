using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Messages.Events;
using Genelife.Systems;

namespace Genelife.Family;

/// <summary>
/// Monthly household reconciliation: retry eligible partner merges, release
/// adult children under policy, and repair the housing registry if needed.
/// </summary>
public sealed class FamilyHouseholdSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly RelationshipIndex relationships;
    private readonly RelationshipOptions relationshipOptions;
    private readonly FamilyHousingPolicy policy;
    private readonly HouseholdMergeService merger;

    private readonly int cadenceOffset;

    public FamilyHouseholdSystem(
        World world,
        EconomyContext economy,
        RelationshipIndex relationships,
        RelationshipOptions relationshipOptions,
        FamilyHousingPolicy? policy = null,
        int cadenceOffset = 0)
    {
        this.world = world;
        this.economy = economy;
        this.relationships = relationships;
        this.relationshipOptions = relationshipOptions;
        this.policy = policy ?? FamilyHousingPolicy.Default;
        merger = new HouseholdMergeService(world, economy);
        this.cadenceOffset = cadenceOffset;
    }

    public string Name => "FamilyHousehold";
    public int Order => 385;
    public Cadence Cadence => new(Math.Max(1, economy.Clock.TicksPerMonth), cadenceOffset);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        policy.Validate();

        if (relationshipOptions.CohabitOnPairing)
            RetryMerges(tick);

        ReleaseAdultChildren(tick);

        if (economy.Housing.Verify().Count > 0)
            economy.Housing.Rebuild(world);
    }

    private void RetryMerges(long tick)
    {
        var partners = new List<(Entity First, Entity Second)>();
        var seen = new HashSet<(ulong First, ulong Second)>();
        world.Query(in new QueryDescription().WithAll<Alive>().WithNone<Dead>(),
            (Entity entity, ref Alive _) =>
            {
                foreach (var partner in relationships.PartnersOf(entity))
                {
                    if (!world.IsAlive(partner))
                        continue;
                    if (!relationships.TryGetEdge(entity, partner, RelationshipKind.Partner, out var edge) ||
                        world.Get<Relationship>(edge).Source != RelationshipSource.Attraction)
                        continue;
                    var first = NetworkIdOf(entity);
                    var second = NetworkIdOf(partner);
                    var key = first <= second ? (first, second) : (second, first);
                    if (seen.Add(key))
                        partners.Add((entity, partner));
                }
            });

        partners.Sort((a, b) =>
        {
            var byFirst = NetworkIdOf(a.First).CompareTo(NetworkIdOf(b.First));
            return byFirst != 0 ? byFirst : NetworkIdOf(a.Second).CompareTo(NetworkIdOf(b.Second));
        });

        foreach (var (first, second) in partners)
        {
            if (!world.IsAlive(first) || !world.IsAlive(second))
                continue;
            var firstHousehold = economy.Housing.HouseholdOf(first);
            var secondHousehold = economy.Housing.HouseholdOf(second);
            if (firstHousehold == Entity.Null || secondHousehold == Entity.Null ||
                firstHousehold == secondHousehold)
                continue;

            if (!merger.TryMergePartners(
                    first, second,
                    out _, out _,
                    out var fromId,
                    out var toId) ||
                fromId == 0 || toId == 0 || fromId == toId)
                continue;

            economy.Emit(new HouseholdMerged(fromId, toId, tick));
        }
    }

    private void ReleaseAdultChildren(long tick)
    {
        var adults = new List<Entity>();
        var workAge = economy.Life.Policy.WorkStartAge;
        world.Query(in new QueryDescription().WithAll<Alive, HouseholdMember>().WithNone<Dead>(),
            (Entity entity, ref Alive alive, ref HouseholdMember member) =>
            {
                if (alive.Age >= workAge && member.Role == HouseholdRole.Child)
                    adults.Add(entity);
            });
        adults.Sort((a, b) => NetworkIdOf(a).CompareTo(NetworkIdOf(b)));

        foreach (var entity in adults)
        {
            if (!world.IsAlive(entity) || !world.Has<HouseholdMember>(entity))
                continue;

            ChildhoodPolicy.ClearDependentMarker(world, entity);
            var member = world.Get<HouseholdMember>(entity);
            var household = member.Household;
            if (household == Entity.Null || !world.IsAlive(household) ||
                economy.Housing.MemberCount(household) <= 1)
            {
                SetRole(entity, household, HouseholdRole.Relative);
                continue;
            }

            var stream = $"family.moveout.{NetworkIdOf(entity)}.{economy.Clock.YearOf(tick)}";
            var roll = economy.Random.For(stream).NextDouble();
            if (roll >= (double)policy.AdultChildMoveOutShare)
            {
                SetRole(entity, household, HouseholdRole.Relative);
                continue;
            }

            if (merger.TrySplit(entity, tick, out var newHousehold) &&
                newHousehold != Entity.Null)
            {
                economy.Emit(new HouseholdSplit(NetworkIdOf(newHousehold), NetworkIdOf(household), tick));
            }
        }
    }

    private void SetRole(Entity sim, Entity household, HouseholdRole role)
    {
        if (household == Entity.Null || !world.IsAlive(household) || !world.Has<HouseholdMember>(sim))
            return;
        var member = world.Get<HouseholdMember>(sim);
        member.Role = role;
        world.Get<HouseholdMember>(sim) = member;
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
}
