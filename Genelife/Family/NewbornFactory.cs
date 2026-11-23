using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Life;
using Genelife.Values;

namespace Genelife.Family;

public sealed record NewbornResult(Entity Entity, Traits Traits, string Name);

/// <summary>
/// Shared newborn creation path. Births and any future seed code use the same
/// complete component set so no system has to special-case children.
/// </summary>
public sealed class NewbornFactory
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly RelationshipService relationships;
    private readonly Func<Entity, NetworkId> allocateNetworkId;

    public NewbornFactory(
        World world,
        EconomyContext economy,
        RelationshipService relationships,
        Func<Entity, NetworkId> allocateNetworkId)
    {
        this.world = world;
        this.economy = economy;
        this.relationships = relationships;
        this.allocateNetworkId = allocateNetworkId;
    }

    public NewbornResult Create(
        Entity parentA,
        Entity parentB,
        Entity guardianHousehold,
        Entity pregnancy,
        long tick)
    {
        var entity = world.Create(
            new GenName(string.Empty),
            new Needs(),
            new CurrentAction(ActionType.Idle, 0f),
            new Alive(0),
            new Location(0f, 0f),
            LifecycleFactory.Create(0, tick, economy.Clock, economy.Life.Policy),
            new Health(90f),
            new Wallet(Money.Zero),
            new DependentChild());

        var networkId = allocateNetworkId(entity);
        world.Add(entity, networkId);

        var firstTraits = parentA != Entity.Null && world.IsAlive(parentA) && world.Has<Traits>(parentA)
            ? world.Get<Traits>(parentA)
            : default;
        var secondTraits = parentB != Entity.Null && world.IsAlive(parentB) && world.Has<Traits>(parentB)
            ? world.Get<Traits>(parentB)
            : firstTraits;
        var inherited = parentA == Entity.Null
            ? TraitsGenerator.Create(economy.Random.Seed, networkId.Value, TraitSource.Seed, economy.Genetics)
            : parentB == Entity.Null
                ? TraitsInheritance.CreateWithOptionalParents(
                    firstTraits,
                    NetworkIdOf(parentA),
                    null,
                    null,
                    economy.Random.Seed,
                    networkId.Value,
                    economy.Genetics).Traits
                : TraitsInheritance.Create(
                    firstTraits,
                    NetworkIdOf(parentA),
                    secondTraits,
                    NetworkIdOf(parentB),
                    economy.Random.Seed,
                    networkId.Value,
                    economy.Genetics);

        var profile = new SkillProfile();
        AptitudeSkills.ApplyStartingBonus(ref profile, inherited.Aptitude);

        world.Add(entity, inherited);
        world.Add(entity, profile);
        world.Get<GenName>(entity) = new GenName(ChildNameGenerator.For(networkId.Value));
        world.Add(entity, new BirthRecord
        {
            PregnancyNetworkId = NetworkIdOf(pregnancy),
            BornTick = tick
        });

        if (guardianHousehold != Entity.Null && world.IsAlive(guardianHousehold))
        {
            world.Add(entity, new HouseholdMember(guardianHousehold, HouseholdRole.Child));
            economy.Housing.AddMember(entity, guardianHousehold);
        }

        if (parentA != Entity.Null && world.IsAlive(parentA))
        {
            relationships.TryCreatePair(
                parentA, entity, RelationshipKind.Parent, RelationshipSource.Birth, tick,
                out _, out _, out _);
        }
        if (parentB != Entity.Null && world.IsAlive(parentB) && parentB != parentA)
        {
            relationships.TryCreatePair(
                parentB, entity, RelationshipKind.Parent, RelationshipSource.Birth, tick,
                out _, out _, out _);
        }

        var guardian = GuardianEntity(guardianHousehold);
        if (guardian != Entity.Null && guardian != parentA && guardian != parentB)
        {
            relationships.TryCreatePair(
                guardian, entity, RelationshipKind.Guardian, RelationshipSource.Birth, tick,
                out _, out _, out _);
        }

        return new NewbornResult(entity, inherited, world.Get<GenName>(entity).Name);
    }

    private Entity GuardianEntity(Entity household)
    {
        if (household == Entity.Null || !world.IsAlive(household) || !world.Has<Household>(household))
            return Entity.Null;
        var head = world.Get<Household>(household).Head;
        return world.IsAlive(head) ? head : Entity.Null;
    }

    private ulong NetworkIdOf(Entity entity) =>
        entity != Entity.Null && world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0;
}
