using Arch.Core;
using Genelife.Api.Services;
using Genelife.Components.Family;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Housing;
using Genelife.Values;

namespace Genelife.UnitTests.Family;

public class BirthTests
{
    [Fact]
    public void BirthCreatesACompleteNewbornAndClosesThePregnancy()
    {
        var context = new Context(capacity: 3);
        var pregnancy = context.StartPregnancy(dueTick: 100);
        var beforeMoney = context.Economy.TotalMoneySupply();

        context.Birth(tick: 100);

        var childEntity = context.OnlyChild();
        Assert.True(context.World.Has<Traits>(childEntity));
        Assert.True(context.World.Has<GenName>(childEntity));
        Assert.True(context.World.Has<Lifecycle>(childEntity));
        Assert.True(context.World.Has<Wallet>(childEntity));
        Assert.True(context.World.Has<DependentChild>(childEntity));
        Assert.True(context.World.Has<BirthRecord>(childEntity));
        Assert.Equal(LifeStage.Education, context.World.Get<Lifecycle>(childEntity).Stage);
        Assert.Equal(Money.Zero, context.World.Get<Wallet>(childEntity).Balance);
        Assert.Contains(childEntity, context.Relationships.ChildrenOf(context.First));
        Assert.Contains(childEntity, context.Relationships.ChildrenOf(context.Second));
        Assert.Equal(PregnancyStatus.Completed, context.World.Get<Pregnancy>(pregnancy).Status);
        Assert.Equal(beforeMoney, context.Economy.TotalMoneySupply());

        Assert.True(context.Economy.Housing.HouseholdOf(childEntity) != Entity.Null);
        Assert.Equal(HouseholdRole.Child,
            context.World.Get<HouseholdMember>(childEntity).Role);
    }

    [Fact]
    public void BirthIntoAFullUnitOvercrowdsButDoesNotFail()
    {
        var context = new Context(capacity: 1);
        context.StartPregnancy(dueTick: 100);

        context.Birth(tick: 100);

        var child = context.OnlyChild();
        Assert.True(context.World.Has<HouseholdMember>(child));
        var unit = context.Economy.Housing.UnitOf(context.Economy.Housing.HouseholdOf(child));
        Assert.NotEqual(Entity.Null, unit);
        Assert.True(context.World.Get<HousingUnit>(unit).IsOvercrowded);
    }

    [Fact]
    public void BirthIsDeterministicForTheSameSeedAndNetworkSequence()
    {
        var first = new Context(capacity: 3);
        var second = new Context(capacity: 3);
        first.StartPregnancy(dueTick: 100);
        second.StartPregnancy(dueTick: 100);
        first.Birth(tick: 100);
        second.Birth(tick: 100);

        var firstChild = first.OnlyChild();
        var secondChild = second.OnlyChild();
        Assert.Equal(first.World.Get<GenName>(firstChild).Name, first.World.Get<GenName>(secondChild).Name);
        Assert.Equal(first.World.Get<Traits>(firstChild), first.World.Get<Traits>(secondChild));
    }

    private sealed class Context
    {
        public Context(int capacity)
        {
            Economy = new EconomyContext(World, randomSeed: 42);
            NetworkIds = new NetworkIdRegistry();
            Relationships = new RelationshipIndex();
            RelationshipService = new RelationshipService(World, Relationships);
            Pregnancies = new PregnancyIndex();
            PregnancyService = new PregnancyService(World, Pregnancies);
            First = CreateSim(1, 30, Sex.Female);
            Second = CreateSim(2, 31, Sex.Male);
            Household = World.Create(new Household { Head = First, Tenure = Tenure.Renter, MemberCount = 2 });
            World.Add(Household, NetworkIds.Allocate(Household));
            World.Add(First, new HouseholdMember(Household, HouseholdRole.Head));
            World.Add(Second, new HouseholdMember(Household, HouseholdRole.Partner));
            Economy.Housing.AddMember(First, Household);
            Economy.Housing.AddMember(Second, Household);

            var property = World.Create(new Property("Central", "Central", Entity.Null, 1, 0.8f, 0.5f));
            var unit = World.Create(new HousingUnit(
                property, "1", HousingTier.Basic, capacity, Money.FromUnits(100m)));
            Economy.Housing.MoveIn(unit, Household);

            Factory = new NewbornFactory(
                World, Economy, RelationshipService, entity => NetworkIds.Allocate(entity));
        }

        public World World { get; } = World.Create();
        public EconomyContext Economy { get; }
        public NetworkIdRegistry NetworkIds { get; }
        public RelationshipIndex Relationships { get; }
        public RelationshipService RelationshipService { get; }
        public PregnancyIndex Pregnancies { get; }
        public PregnancyService PregnancyService { get; }
        public Entity First { get; }
        public Entity Second { get; }
        public Entity Household { get; }
        public NewbornFactory Factory { get; }

        public Entity StartPregnancy(long dueTick)
        {
            Assert.True(PregnancyService.TryStart(
                First, Second, 0, dueTick, 90, PregnancySource.Conception,
                new ReproductionOptions(), out var pregnancy, out _));
            return pregnancy;
        }

        public void Birth(long tick)
        {
            new BirthSystem(
                World,
                Economy,
                Pregnancies,
                PregnancyService,
                Factory,
                entity => NetworkIds.Allocate(entity),
                new ReproductionOptions()).Update(tick);
        }

        public Entity OnlyChild()
        {
            Entity result = Entity.Null;
            World.Query(in new QueryDescription().WithAll<BirthRecord>(),
                (Entity entity, ref BirthRecord _) =>
                {
                    result = entity;
                });
            Assert.NotEqual(Entity.Null, result);
            return result;
        }

        private Entity CreateSim(ulong networkId, int age, Sex sex) =>
            World.Create(
                new GenName($"Sim {networkId}"),
                new Alive(age),
                new Health(90f),
                new Traits { Sex = sex },
                new NetworkId(networkId),
                new Wallet(Money.Zero));
    }
}
