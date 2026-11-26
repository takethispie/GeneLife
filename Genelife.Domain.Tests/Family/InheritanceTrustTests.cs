using Arch.Core;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Life;
using Genelife.Application.Systems;
using Genelife.Values;

namespace Genelife.UnitTests.Family;

public class InheritanceTrustTests
{
    [Fact]
    public void MinorShareGoesToTrustAndIsReleasedAtAdulthood()
    {
        var context = new Context();
        var before = context.Economy.TotalMoneySupply();

        context.Settle();

        Assert.Equal(Money.FromUnits(450m), context.WalletOf(context.Partner));
        Assert.Equal(Money.FromUnits(225m), context.WalletOf(context.AdultChild));
        Assert.Equal(Money.Zero, context.WalletOf(context.MinorChild));

        var trust = context.OnlyTrust();
        Assert.Equal(Money.FromUnits(225m), context.World.Get<TrustAccount>(trust).Balance);
        Assert.Equal(context.MinorChild, context.World.Get<TrustAccount>(trust).Beneficiary);
        Assert.Equal(before, context.Economy.TotalMoneySupply());

        context.World.Get<Alive>(context.MinorChild) = new Alive(18);
        new TrustSystem(context.Economy, context.Trusts).Update(100);

        Assert.Equal(Money.FromUnits(225m), context.WalletOf(context.MinorChild));
        Assert.Equal(0, context.TrustCount());
        Assert.Equal(Money.FromUnits(225m), context.Economy.Life.TrustReleasedThisYear);
        Assert.Equal(before, context.Economy.TotalMoneySupply());
    }

    [Fact]
    public void TrustEscheatsToGovernmentWhenBeneficiaryDies()
    {
        var context = new Context();
        context.Settle();
        var trust = context.OnlyTrust();
        context.World.Add(context.MinorChild, new Dead(50, DeathCause.Other));
        context.World.Get<Alive>(context.MinorChild) = new Alive(10);
        var governmentBefore = context.Economy.Government.Balance;

        new TrustSystem(context.Economy, context.Trusts).Update(100);

        Assert.Equal(0, context.TrustCount());
        Assert.Equal(governmentBefore + Money.FromUnits(225m), context.Economy.Government.Balance);
    }

    private sealed class Context
    {
        public Context()
        {
            Economy = new EconomyContext(World);
            Service = new RelationshipService(World, Index);
            Trusts = new TrustService(World, Economy);
            Deceased = World.Create(
                new GenName("Deceased"),
                new Alive(60),
                new Wallet(Money.FromUnits(1_000m)),
                new NetworkId(1));
            Partner = World.Create(new GenName("Partner"), new Alive(58), new Wallet(Money.Zero), new NetworkId(2));
            AdultChild = World.Create(new GenName("Adult"), new Alive(35), new Wallet(Money.Zero), new NetworkId(3));
            MinorChild = World.Create(new GenName("Minor"), new Alive(10), new Wallet(Money.Zero), new NetworkId(4));
            Service.CreatePair(Deceased, Partner, RelationshipKind.Partner, RelationshipSource.Seed, 1);
            Service.CreatePair(Deceased, AdultChild, RelationshipKind.Parent, RelationshipSource.Seed, 1);
            Service.CreatePair(Deceased, MinorChild, RelationshipKind.Parent, RelationshipSource.Seed, 1);
        }

        public World World { get; } = World.Create();
        public EconomyContext Economy { get; }
        public RelationshipIndex Index { get; } = new();
        public RelationshipService Service { get; }
        public TrustService Trusts { get; }
        public Entity Deceased { get; }
        public Entity Partner { get; }
        public Entity AdultChild { get; }
        public Entity MinorChild { get; }

        public void Settle()
        {
            Economy.Life.EstateQueue.Add(new EstateRequest
            {
                Genos = Deceased,
                GenosNetworkId = 1,
                Name = "Deceased",
                Age = 60,
                Tick = 1
            });
            new EstateSettlementSystem(
                World,
                Economy,
                relationshipIndex: Index,
                relationshipService: Service,
                trusts: Trusts).Update(1);
        }

        public Money WalletOf(Entity entity) =>
            World.Has<Wallet>(entity) ? World.Get<Wallet>(entity).Balance : Money.Zero;

        public Entity OnlyTrust()
        {
            Entity result = Entity.Null;
            World.Query(in new QueryDescription().WithAll<TrustAccount>(),
                (Entity entity, ref TrustAccount _) => result = entity);
            Assert.NotEqual(Entity.Null, result);
            return result;
        }

        public int TrustCount()
        {
            var count = 0;
            World.Query(in new QueryDescription().WithAll<TrustAccount>(), (ref TrustAccount _) => count++);
            return count;
        }
    }
}
