using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Application.Systems;

namespace Genelife.UnitTests.Family;

public class ConceptionTests
{
    [Fact]
    public void EligiblePairsConceiveAtConfiguredRate()
    {
        var context = new Context(seed: 123, pairCount: 200);
        var options = new ReproductionOptions { MonthlyConceptionChance = 0.1m };
        context.Run(options, tick: 100);

        Assert.InRange(context.PregnancyCount(), 8, 35);
    }

    [Fact]
    public void ReplayingTheSameSeedProducesTheSamePregnancySet()
    {
        var first = new Context(seed: 77, pairCount: 60);
        var second = new Context(seed: 77, pairCount: 60);
        var options = new ReproductionOptions { MonthlyConceptionChance = 0.2m };
        first.Run(options, tick: 100);
        second.Run(options, tick: 100);

        Assert.Equal(first.PregnancyPairs(), second.PregnancyPairs());
    }

    [Fact]
    public void IneligibleCarryingParentsNeverConceive()
    {
        var context = new Context(seed: 4, pairCount: 2);
        var options = new ReproductionOptions { MonthlyConceptionChance = 1m };
        var minor = context.AddPair(16, 30);
        var dependent = context.AddPair(30, 31);
        var household = context.World.Create(new Household { Head = dependent.Second });
        context.World.Add(dependent.First, new HouseholdMember(household, HouseholdRole.Child));
        var unhealthy = context.AddPair(30, 31, health: 20f);

        context.Run(options, tick: 100);

        Assert.False(context.Pregnancies.TryGetActive(minor.First, out _));
        Assert.False(context.Pregnancies.TryGetActive(dependent.First, out _));
        Assert.False(context.Pregnancies.TryGetActive(unhealthy.First, out _));
    }

    [Fact]
    public void LossCooldownBlocksConception()
    {
        var context = new Context(seed: 5, pairCount: 1);
        var carrying = context.EntityOf(1);
        var other = context.EntityOf(2);
        Assert.True(context.Service.TryStart(
            carrying, other, 100, 1_000, 95, PregnancySource.Conception,
            new ReproductionOptions(), out var pregnancy, out _));
        context.Service.Lose(pregnancy, 150);

        var options = new ReproductionOptions
        {
            MonthlyConceptionChance = 1m,
            LossCooldownMonths = 3m
        };
        context.Run(options, tick: 250);

        Assert.False(context.Pregnancies.TryGetActive(carrying, out _));
    }

    private sealed class Context
    {
        private readonly Dictionary<ulong, Entity> entities = new();
        private ulong nextNetworkId = 1;

        public Context(int seed, int pairCount)
        {
            Economy = new EconomyContext(World, randomSeed: seed);
            Service = new PregnancyService(World, Pregnancies);
            Edges = new RelationshipService(World, Relationships);
            for (var i = 0; i < pairCount; i++)
                AddPair(28 + i % 10, 29 + i % 10);
        }

        public World World { get; } = World.Create();
        public EconomyContext Economy { get; }
        public PregnancyIndex Pregnancies { get; } = new();
        public PregnancyService Service { get; }
        public RelationshipIndex Relationships { get; } = new();
        public RelationshipService Edges { get; }

        public Entity EntityOf(ulong networkId) => entities[networkId];

        public (Entity First, Entity Second) AddPair(int ageA, int ageB, float health = 90f)
        {
            var firstId = nextNetworkId++;
            var secondId = nextNetworkId++;
            var first = World.Create(
                new GenName($"F{firstId}"),
                new Alive(ageA),
                new Health(health),
                new Traits { Sex = Sex.Female },
                new NetworkId(firstId));
            var second = World.Create(
                new GenName($"M{secondId}"),
                new Alive(ageB),
                new Health(health),
                new Traits { Sex = Sex.Male },
                new NetworkId(secondId));
            entities[firstId] = first;
            entities[secondId] = second;
            Edges.TryCreatePair(
                first, second, RelationshipKind.Partner, RelationshipSource.Seed, 0,
                out _, out _, out _);
            return (first, second);
        }

        public void Run(ReproductionOptions options, long tick)
        {
            new ConceptionSystem(World, Economy, Relationships, Pregnancies, Service, options)
                .Update(tick);
        }

        public int PregnancyCount()
        {
            var count = 0;
            World.Query(in new QueryDescription().WithAll<Pregnancy>(), (ref Pregnancy _) => count++);
            return count;
        }

        public List<(ulong Carrying, ulong Other)> PregnancyPairs()
        {
            var pairs = new List<(ulong, ulong)>();
            World.Query(in new QueryDescription().WithAll<Pregnancy>(),
                (ref Pregnancy pregnancy) =>
                {
                    pairs.Add((
                        World.Get<NetworkId>(pregnancy.CarryingParent).Value,
                        World.Get<NetworkId>(pregnancy.OtherParent).Value));
                });
            pairs.Sort();
            return pairs;
        }
    }
}
