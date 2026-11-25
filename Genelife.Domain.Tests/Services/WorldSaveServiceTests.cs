using Arch.Core;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Values;

namespace Genelife.UnitTests.Services;

public class WorldSaveServiceTests
{
    [Fact]
    public void Build_PopulatesCompanyFinancialFieldsAndOfficeCount()
    {
        var world = World.Create();
        var index = new EmploymentIndex();

        var account = CompanyAccount.Create(Money.FromUnits(5_000m), 0);
        account.ReservedForPayroll = Money.FromUnits(1_200m);
        account.Arrears = Money.FromUnits(300m);
        account.RevenueLastCycle = Money.FromUnits(9_000m);
        account.WagesLastCycle = Money.FromUnits(6_000m);
        account.State = CompanySolvency.Tight;

        var company = world.Create(
            new CompanyName("Acme"),
            new NetworkId(42),
            account);
        var openOffice = world.Create(new Office("Main", company, 10, Money.FromUnits(100m)));
        world.Create(new Office("Second", company, 5, Money.FromUnits(50m)));
        world.Create(new Office("Closed", company, 5, Money.FromUnits(50m), isClosed: true));

        var employee = world.Create(new Employee(company, openOffice, "Dev", Money.FromUnits(1_000m)));
        index.Add(company, employee);

        var batch = new WorldSaveService().Build(world, index);
        var document = Assert.Single(batch.Companies);

        Assert.Equal((long)42, document.NetworkId);
        Assert.Equal("Acme", document.Name);
        Assert.Equal(1, document.EmployeeCount);
        Assert.Equal(2, document.OfficeCount);
        Assert.Equal(Money.FromUnits(5_000m).Units, document.Balance);
        Assert.Equal(Money.FromUnits(1_200m).Units, document.ReservedForPayroll);
        Assert.Equal(Money.FromUnits(300m).Units, document.Arrears);
        Assert.Equal(Money.FromUnits(9_000m).Units, document.RevenueLastCycle);
        Assert.Equal(Money.FromUnits(6_000m).Units, document.WagesLastCycle);
        Assert.Equal(nameof(CompanySolvency.Tight), document.Solvency);

        var status = document.ToCompanyStatus();
        Assert.Equal((ulong)42, status.NetworkId);
        Assert.Equal("Acme", status.Name);
        Assert.Equal(1, status.EmployeeCount);
        Assert.Equal(2, status.OfficeCount);
        Assert.Equal(Money.FromUnits(5_000m).Units, status.Balance);
        Assert.Equal(Money.FromUnits(1_200m).Units, status.ReservedForPayroll);
        Assert.Equal(Money.FromUnits(300m).Units, status.Arrears);
        Assert.Equal(Money.FromUnits(9_000m).Units, status.RevenueLastCycle);
        Assert.Equal(Money.FromUnits(6_000m).Units, status.WagesLastCycle);
        Assert.Equal(nameof(CompanySolvency.Tight), status.Solvency);
    }
}
