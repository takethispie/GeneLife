using Genelife.Api.Persistence;

namespace Genelife.UnitTests.Persistence;

public class SnapshotMigrationTests
{
    [Fact]
    public void Upgrade_FromV15_SeedsThisCycleAccumulatorsFromLastCycle()
    {
        var state = new PersistedWorldState
        {
            SchemaVersion = 15,
            Companies =
            [
                new PersistedCompany
                {
                    RevenueLastCycleCents = 900,
                    WagesLastCycleCents = 700,
                    CostsLastCycleCents = 50,
                    InterestPaidLastCycleCents = 10
                }
            ]
        };

        SnapshotMigrations.Upgrade(state, 15);

        var company = Assert.Single(state.Companies);
        Assert.Equal(900, company.RevenueThisCycleCents);
        Assert.Equal(700, company.WagesThisCycleCents);
        Assert.Equal(50, company.CostsThisCycleCents);
        Assert.Equal(10, company.InterestPaidThisCycleCents);
        Assert.Equal(17, PersistedWorldState.CurrentSchemaVersion);
        Assert.Equal(17, state.SchemaVersion);
    }
}
