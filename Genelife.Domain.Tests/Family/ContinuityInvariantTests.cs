using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Values;

namespace Genelife.UnitTests.Family;

public class ContinuityInvariantTests
{
    [Fact]
    public void MinorWithoutGuardianOrStateCareFails()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        world.Create(new GenName("Minor"), new Alive(8), new Wallet(Money.Zero));

        var issues = ContinuityInvariants.Verify(world, new RelationshipIndex(), economy);

        Assert.Contains(issues, issue => issue.Name == "minor.no-guardian" && issue.Level == "Fail");
    }

    [Fact]
    public void TrustWithMissingBeneficiaryFails()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        world.Create(new TrustAccount
        {
            Balance = Money.FromUnits(50m),
            Beneficiary = Entity.Null,
            Guardian = Entity.Null
        });

        var issues = ContinuityInvariants.Verify(world, new RelationshipIndex(), economy);

        Assert.Contains(issues, issue => issue.Name == "trust.beneficiary" && issue.Level == "Fail");
    }

    [Fact]
    public void ActivePartnerEdgeToDeadGenosFails()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var index = new RelationshipIndex();
        var service = new RelationshipService(world, index);
        var deceased = world.Create(new Alive(60), new NetworkId(1));
        var partner = world.Create(new Alive(58), new NetworkId(2));
        service.CreatePair(deceased, partner, RelationshipKind.Partner, RelationshipSource.Seed, 1);
        world.Add(deceased, new Dead(5, DeathCause.Other));

        var issues = ContinuityInvariants.Verify(world, index, economy);

        Assert.Contains(issues, issue => issue.Name == "death.active-partner" && issue.Level == "Fail");
    }

    [Fact]
    public void TrustBalanceIsPartOfMoneySupply()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var before = economy.TotalMoneySupply();
        var beneficiary = world.Create(new Alive(8));
        world.Create(new TrustAccount
        {
            Balance = Money.FromUnits(75m),
            Beneficiary = beneficiary
        });

        Assert.Equal(Money.FromUnits(75m), economy.GetMoneySupplyBreakdown().MinorTrusts);
        Assert.Equal(before + Money.FromUnits(75m), economy.TotalMoneySupply());
    }
}
