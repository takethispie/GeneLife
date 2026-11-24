using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Life;
using Genelife.Systems;
using Genelife.Values;

namespace Genelife.UnitTests.Family;

public class WidowhoodTests
{
    [Fact]
    public void PartnerDeathKeepsTheFamilyTogetherAndTrustsTheMinorShare()
    {
        var context = new Context();
        var before = context.Economy.TotalMoneySupply();

        context.Death.Update(10);
        context.Estate.Update(10);

        var household = context.World.Get<Household>(context.Household);
        Assert.Equal(context.Surviving, household.Head);
        Assert.False(context.Relationships.HasPartner(context.Deceased));
        Assert.True(context.Relationships.IsParentOf(context.Surviving, context.Child));
        Assert.Equal(Money.FromUnits(450m), context.WalletOf(context.Surviving));
        Assert.Equal(Money.Zero, context.WalletOf(context.Child));
        Assert.Equal(Money.FromUnits(450m), context.Trusts.TotalHeld());
        Assert.Equal(before, context.Economy.TotalMoneySupply());
    }

    [Fact]
    public void OrphanMovesToAnAdultRelative()
    {
        var context = new Context();
        var grandparent = context.World.Create(
            new GenName("Grandparent"),
            new Alive(70),
            new Wallet(Money.Zero),
            new NetworkId(20));
        context.Service.CreatePair(
            grandparent, context.Deceased, RelationshipKind.Parent, RelationshipSource.Seed, 1);
        var grandparentHousehold = context.World.Create(
            new Household { Head = grandparent, Tenure = Tenure.Renter, MemberCount = 1 });
        context.World.Add(grandparent, new HouseholdMember(grandparentHousehold, HouseholdRole.Head));
        context.Economy.Housing.AddMember(grandparent, grandparentHousehold);
        context.World.Add(context.Deceased, new Dead(5, DeathCause.Other));
        context.World.Add(context.Surviving, new Dead(5, DeathCause.Other));

        Assert.True(context.Guardianship.EnsureGuardian(context.Child, 10, out var guardian, out var stateCare));

        Assert.False(stateCare);
        Assert.Equal(grandparent, guardian);
        Assert.Equal(grandparentHousehold, context.Economy.Housing.HouseholdOf(context.Child));
        Assert.True(context.Relationships.IsGuardianOf(grandparent, context.Child));
        Assert.False(context.World.Has<StateCare>(context.Child));
    }

    [Fact]
    public void ChildWithoutARelativeGetsStateCare()
    {
        var context = new Context();
        context.World.Add(context.Deceased, new Dead(5, DeathCause.Other));
        context.World.Add(context.Surviving, new Dead(5, DeathCause.Other));

        Assert.True(context.Guardianship.EnsureGuardian(context.Child, 10, out var guardian, out var stateCare));

        Assert.True(stateCare);
        Assert.Equal(Entity.Null, guardian);
        Assert.True(context.World.Has<StateCare>(context.Child));
    }

    [Fact]
    public void MinorOutsideDeadParentsHouseholdGetsLivingGuardian()
    {
        var context = new Context();
        MoveChildToSeparateHousehold(context);
        var grandparent = context.World.Create(
            new GenName("Grandparent"), new Alive(75), new Wallet(Money.Zero), new NetworkId(20));
        var grandparentHousehold = context.World.Create(
            new Household { Head = grandparent, Tenure = Tenure.Renter, MemberCount = 1 });
        context.World.Add(grandparent, new HouseholdMember(grandparentHousehold, HouseholdRole.Head));
        context.Economy.Housing.AddMember(grandparent, grandparentHousehold);
        context.Service.CreatePair(
            grandparent, context.Surviving, RelationshipKind.Parent, RelationshipSource.Seed, 1);
        context.World.Add(context.Surviving, new Dead(5, DeathCause.Other));

        context.Death.Update(10);

        Assert.False(context.World.Has<StateCare>(context.Child));
        Assert.True(context.Relationships.IsGuardianOf(grandparent, context.Child));
        Assert.Equal(grandparentHousehold, context.Economy.Housing.HouseholdOf(context.Child));
    }

    [Fact]
    public void OrphanedMinorOutsideHouseholdGetsStateCareAfterParentEdgesClear()
    {
        var context = new Context();
        MoveChildToSeparateHousehold(context);
        context.World.Add(context.Child, new BirthRecord { PregnancyNetworkId = 99, BornTick = 0 });
        context.World.Add(context.Surviving, new Dead(5, DeathCause.Other));

        context.Death.Update(10);

        context.Service.RemoveEdgesForSim(context.Deceased);
        context.Service.RemoveEdgesForSim(context.Surviving);

        Assert.Empty(context.Relationships.ParentsOf(context.Child));
        Assert.True(context.World.Has<StateCare>(context.Child));
        var issues = ReproductionInvariants.Verify(context.World, context.Relationships, context.Economy);
        Assert.DoesNotContain(issues,
            issue => issue.Name == "newborn.parent-edges" && issue.Level == "Fail");
    }

    private static void MoveChildToSeparateHousehold(Context context)
    {
        var household = context.World.Create(
            new Household { Head = context.Child, Tenure = Tenure.Renter, MemberCount = 1 },
            new NetworkId(30));
        context.Economy.Housing.RemoveMember(context.Child);
        context.World.Get<HouseholdMember>(context.Child) = new HouseholdMember(household, HouseholdRole.Child);
        context.Economy.Housing.AddMember(context.Child, household);
    }

    private sealed class Context
    {
        public Context()
        {
            Economy = new EconomyContext(World);
            Relationships = new RelationshipIndex();
            Service = new RelationshipService(World, Relationships);
            Trusts = new TrustService(World, Economy);
            Guardianship = new GuardianshipService(
                World, Economy, Relationships, Service, new GuardianshipOptions());
            Deceased = World.Create(
                new GenName("Deceased"), new Alive(60), new Wallet(Money.FromUnits(1_000m)),
                new NetworkId(1), new Lifecycle(LifeStage.Work, 60L * Economy.Clock.TicksPerYear, 0));
            Surviving = World.Create(
                new GenName("Surviving"), new Alive(58), new Wallet(Money.Zero), new NetworkId(2));
            Child = World.Create(
                new GenName("Child"), new Alive(10), new Wallet(Money.Zero), new NetworkId(3),
                new DependentChild());
            Household = World.Create(new Household
            {
                Head = Deceased,
                Tenure = Tenure.Renter,
                MemberCount = 3
            });
            World.Add(Household, new NetworkId(10));
            World.Add(Deceased, new HouseholdMember(Household, HouseholdRole.Head));
            World.Add(Surviving, new HouseholdMember(Household, HouseholdRole.Partner));
            World.Add(Child, new HouseholdMember(Household, HouseholdRole.Child));
            Economy.Housing.AddMember(Deceased, Household);
            Economy.Housing.AddMember(Surviving, Household);
            Economy.Housing.AddMember(Child, Household);
            Service.CreatePair(Deceased, Surviving, RelationshipKind.Partner, RelationshipSource.Seed, 1);
            Service.CreatePair(Deceased, Child, RelationshipKind.Parent, RelationshipSource.Seed, 1);
            Service.CreatePair(Surviving, Child, RelationshipKind.Parent, RelationshipSource.Seed, 1);
            Economy.Life.DeathQueue.Add(Deceased);
            Death = new DeathProcessingSystem(
                World, Economy,
                relationships: Service,
                relationshipIndex: Relationships,
                guardianship: Guardianship);
            Estate = new EstateSettlementSystem(
                World, Economy,
                relationshipIndex: Relationships,
                relationshipService: Service,
                trusts: Trusts);
        }

        public World World { get; } = World.Create();
        public EconomyContext Economy { get; }
        public RelationshipIndex Relationships { get; }
        public RelationshipService Service { get; }
        public TrustService Trusts { get; }
        public GuardianshipService Guardianship { get; }
        public Entity Deceased { get; }
        public Entity Surviving { get; }
        public Entity Child { get; }
        public Entity Household { get; }
        public DeathProcessingSystem Death { get; }
        public EstateSettlementSystem Estate { get; }

        public Money WalletOf(Entity entity) =>
            World.Has<Wallet>(entity) ? World.Get<Wallet>(entity).Balance : Money.Zero;
    }
}
