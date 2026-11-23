using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Family;
using Genelife.Messages.Events;

namespace Genelife.UnitTests.Family;

public class PairingSystemTests
{
    [Fact]
    public void MutualTopCandidatesFormMirroredPartnerships()
    {
        var context = new Context();
        var a = context.Sim(30, Sex.Female, 1);
        var b = context.Sim(31, Sex.Male, 2);
        context.Score(a, b, 9_000);
        context.Score(b, a, 9_000);

        context.Run(tick: 0);

        Assert.Contains(b, context.Index.PartnersOf(a));
        Assert.Contains(a, context.Index.PartnersOf(b));
        Assert.Contains(context.Economy.PendingEvents, @event => @event is PartnershipFormed);
    }

    [Fact]
    public void RejectedPairEntersCooldownAndDoesNotCreateEdges()
    {
        var context = new Context(new RelationshipOptions
        {
            PairingAcceptanceChance = 0m,
            RejectionCooldownMonths = 6
        });
        var a = context.Sim(30, Sex.Female, 1);
        var b = context.Sim(31, Sex.Male, 2);
        context.Score(a, b, 9_000);
        context.Score(b, a, 9_000);

        context.Run(tick: 0);

        Assert.Empty(context.Index.PartnersOf(a));
        Assert.True(context.Cache.InCooldown(a, b, 0));
        Assert.Contains(context.Economy.PendingEvents, @event => @event is PairingRejected);
    }

    [Fact]
    public void APairedSimCannotBePairedAgainInTheSamePass()
    {
        var context = new Context(new RelationshipOptions { PairingAcceptanceChance = 1m });
        var a = context.Sim(30, Sex.Female, 1);
        var b = context.Sim(31, Sex.Male, 2);
        var c = context.Sim(32, Sex.Male, 3);
        context.Score(a, b, 9_000, c, 8_000);
        context.Score(b, a, 9_000);
        context.Score(c, a, 8_000);

        context.Run(tick: 0);

        Assert.Single(context.Index.PartnersOf(a));
        Assert.Single(context.Index.PartnersOf(b));
        Assert.Empty(context.Index.PartnersOf(c));
    }

    [Fact]
    public void SameSeedAndInputsProduceTheSamePartnership()
    {
        static (ulong A, ulong B) RunScenario()
        {
            var context = new Context(new RelationshipOptions { PairingAcceptanceChance = 1m });
            var a = context.Sim(30, Sex.Female, 10);
            var b = context.Sim(31, Sex.Male, 20);
            context.Score(a, b, 7_500);
            context.Score(b, a, 7_500);
            context.Run(tick: 500);
            return (
                context.World.Get<NetworkId>(context.Index.PartnersOf(a)[0]).Value,
                context.World.Get<NetworkId>(a).Value);
        }

        Assert.Equal(RunScenario(), RunScenario());
    }


    [Fact]
    public void CohabitationOptionMergesThePairIntoOneHousehold()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        Genelife.Housing.HousingBootstrap.CreateProperty(
            world, economy, "Central", Genelife.Enums.HousingTier.Basic, 4, 0.8f, 0.5f, 0);
        var allocator = new Genelife.Housing.HousingAllocator(world, economy);
        var index = new RelationshipIndex
        {
            NetworkIdResolver = entity => world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : 0
        };
        var service = new RelationshipService(world, index);
        var cache = new RelationshipCache();
        var first = world.Create(new Alive(30), new Traits { Sex = Sex.Female }, new NetworkId(1),
            new GenName("First"));
        var second = world.Create(new Alive(31), new Traits { Sex = Sex.Male }, new NetworkId(2),
            new GenName("Second"));
        var firstHousehold = world.Create(new Genelife.Components.Housing.Household
            { Head = first, Tenure = Genelife.Enums.Tenure.Renter, MemberCount = 1 });
        world.Add(firstHousehold, new NetworkId(10));
        world.Add(first, new Genelife.Components.Housing.HouseholdMember(
            firstHousehold, Genelife.Enums.HouseholdRole.Head));
        economy.Housing.AddMember(first, firstHousehold);
        Assert.True(allocator.TryAssignBest(firstHousehold, 0));

        var secondHousehold = world.Create(new Genelife.Components.Housing.Household
            { Head = second, Tenure = Genelife.Enums.Tenure.Renter, MemberCount = 1 });
        world.Add(secondHousehold, new NetworkId(11));
        world.Add(second, new Genelife.Components.Housing.HouseholdMember(
            secondHousehold, Genelife.Enums.HouseholdRole.Head));
        economy.Housing.AddMember(second, secondHousehold);
        Assert.True(allocator.TryAssignBest(secondHousehold, 0));

        cache.Scored[first] = [new ScoredCandidate(second, 9_000)];
        cache.Scored[second] = [new ScoredCandidate(first, 9_000)];
        var options = new RelationshipOptions
        {
            PairingAcceptanceChance = 1m,
            CohabitOnPairing = true
        };

        new PairingSystem(world, economy, service, index, options, cache).Update(0);

        Assert.Contains(second, index.PartnersOf(first));
        Assert.True(world.IsAlive(firstHousehold) ^ world.IsAlive(secondHousehold));
        var surviving = world.IsAlive(firstHousehold) ? firstHousehold : secondHousehold;
        Assert.Equal(2, economy.Housing.MemberCount(surviving));
        Assert.Contains(economy.PendingEvents, @event => @event is HouseholdMerged);
    }

    private sealed class Context
    {
        public Context(RelationshipOptions? options = null)
        {
            Options = options ?? new RelationshipOptions { PairingAcceptanceChance = 1m };
            Economy = new EconomyContext(World);
            Index = new RelationshipIndex
            {
                NetworkIdResolver = entity =>
                    World.Has<NetworkId>(entity) ? World.Get<NetworkId>(entity).Value : 0
            };
            Service = new RelationshipService(World, Index);
            Cache = new RelationshipCache();
        }

        public World World { get; } = World.Create();
        public EconomyContext Economy { get; }
        public RelationshipIndex Index { get; }
        public RelationshipService Service { get; }
        public RelationshipCache Cache { get; }
        public RelationshipOptions Options { get; }

        public Entity Sim(int age, Sex sex, ulong networkId) =>
            World.Create(
                new Alive(age),
                new Traits { Sex = sex },
                new NetworkId(networkId),
                new GenName($"Sim {networkId}"));

        public void Score(Entity sim, Entity candidate, int score) =>
            Cache.Scored[sim] = [new ScoredCandidate(candidate, score)];

        public void Score(Entity sim, Entity first, int firstScore, Entity second, int secondScore) =>
            Cache.Scored[sim] =
            [
                new ScoredCandidate(first, firstScore),
                new ScoredCandidate(second, secondScore)
            ];

        public void Run(long tick)
        {
            var system = new PairingSystem(World, Economy, Service, Index, Options, Cache);
            system.Update(tick);
        }
    }
}
