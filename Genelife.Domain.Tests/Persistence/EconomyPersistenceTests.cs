using Arch.Core;
using Genelife.Api.Repositories;
using Genelife.Application.Systems;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Enums;
using Genelife.Values;

namespace Genelife.UnitTests.Persistence;

public class EconomyPersistenceTests
{
    [Fact]
    public void CaptureAndRestore_PreservesAccountsOfficesPostingsEmploymentAndWallets()
    {
        var engine = new SimulationEngine();
        engine.Initialize();
        var world = engine.World!;
        var economy = engine.Economy!;
        var registry = engine.NetworkIds;

        var companyRepository = new CompanyRepository(world, economy.Employment, registry, economy);
        var officeRepository = new OfficeRepository(world, registry, economy);
        var postingRepository = new JobPostingRepository(world, registry, economy);
        var genosRepository = new GenosRepository(world, registry);

        var companyId = companyRepository.Add("Acme");
        var officeId = officeRepository.Add(companyId, "Main", 5, Money.FromUnits(50m));
        var postingId = postingRepository.Open(companyId, officeId, "Dev", Money.FromUnits(1000m), Skill.Programming, 1);
        var genosId = genosRepository.Add("Alice", 30);

        registry.TryGetEntity(companyId, out var company);
        registry.TryGetEntity(officeId, out var office);
        registry.TryGetEntity(genosId, out var genos);

        world.Add(genos, new Employee(company, office, "Dev", Money.FromUnits(1000m)));
        world.Add(genos, new SavingsAccount(Money.FromUnits(250m)));
        economy.Employment.Add(company, genos);

        var account = world.Get<CompanyAccount>(company);
        account.Balance = Money.FromUnits(7_500m);
        account.Arrears = Money.FromUnits(25m);
        account.State = CompanySolvency.Arrears;
        account.RevenueLastCycle = Money.FromUnits(900m);
        account.WagesLastCycle = Money.FromUnits(700m);
        account.CostsLastCycle = Money.FromUnits(50m);
        account.InterestPaidLastCycle = Money.FromUnits(10m);
        account.RevenueThisCycle = Money.FromUnits(100m);
        account.WagesThisCycle = Money.FromUnits(80m);
        account.CostsThisCycle = Money.FromUnits(20m);
        account.InterestPaidThisCycle = Money.FromUnits(5m);
        world.Get<CompanyAccount>(company) = account;

        var state = engine.CaptureState(registry);
        engine.Shutdown();

        var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);
        var restoredWorld = restored.World!;
        var restoredEconomy = restored.Economy!;

        Assert.True(restored.NetworkIds.TryGetEntity(companyId, out var restoredCompany));
        Assert.True(restored.NetworkIds.TryGetEntity(genosId, out var restoredGenos));
        Assert.True(restoredEconomy.Accounts.TryGet(restoredCompany, out var restoredAccount));
        Assert.Equal(Money.FromUnits(7_500m), restoredAccount.Balance);
        Assert.Equal(Money.FromUnits(25m), restoredAccount.Arrears);
        Assert.Equal(CompanySolvency.Arrears, restoredAccount.State);
        Assert.Equal(Money.FromUnits(900m), restoredAccount.RevenueLastCycle);
        Assert.Equal(Money.FromUnits(700m), restoredAccount.WagesLastCycle);
        Assert.Equal(Money.FromUnits(50m), restoredAccount.CostsLastCycle);
        Assert.Equal(Money.FromUnits(10m), restoredAccount.InterestPaidLastCycle);
        Assert.Equal(Money.FromUnits(100m), restoredAccount.RevenueThisCycle);
        Assert.Equal(Money.FromUnits(80m), restoredAccount.WagesThisCycle);
        Assert.Equal(Money.FromUnits(20m), restoredAccount.CostsThisCycle);
        Assert.Equal(Money.FromUnits(5m), restoredAccount.InterestPaidThisCycle);
        Assert.Equal(Money.FromUnits(250m), restoredWorld.Get<SavingsAccount>(restoredGenos).Balance);
        Assert.True(restoredWorld.Has<Employee>(restoredGenos));
        Assert.Equal(1, restoredEconomy.Employment.CountEmployees(restoredCompany));
        Assert.True(restored.NetworkIds.TryGetEntity(postingId, out var restoredPosting));
        Assert.Equal(1, restoredWorld.Get<JobPosting>(restoredPosting).TotalSeats);
        restored.Shutdown();
    }

    [Fact]
    public void CaptureAndRestore_PreservesPendingConsumerRevenue()
    {
        var engine = new SimulationEngine();
        engine.Initialize();
        var world = engine.World!;
        var economy = engine.Economy!;
        var registry = engine.NetworkIds;

        var companyRepository = new CompanyRepository(world, economy.Employment, registry, economy);
        var genosRepository = new GenosRepository(world, registry);
        var companyId = companyRepository.Add("Shop");
        var genosId = genosRepository.Add("Buyer", 30);
        registry.TryGetEntity(companyId, out var company);

        economy.BookRevenue(company, "Food", genosId.Value, Money.FromUnits(40m), 100);
        var before = economy.TotalMoneySupply();

        var state = engine.CaptureState(registry);
        engine.Shutdown();

        var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        Assert.Equal(before, restored.Economy!.TotalMoneySupply());
        Assert.Single(restored.Economy.PendingRevenue);
        restored.Shutdown();
    }

    [Fact]
    public void CaptureAndRestore_PreservesTheAnnualFailureFlowAndRate()
    {
        var engine = new SimulationEngine();
        engine.Initialize();
        var world = engine.World!;
        var economy = engine.Economy!;
        var registry = engine.NetworkIds;

        var companies = new CompanyRepository(world, economy.Employment, registry, economy);
        companies.Add("Acme");

        economy.CompaniesFailedThisYear = 1;
        new EconomyMetricsSystem(world, economy).Update(economy.Clock.TicksPerYear);

        var rate = economy.MetricHistory[^1].CompanyFailureRate;
        Assert.True(rate > 0m);

        // Failures since the last snapshot are the current year's flow.
        economy.CompaniesFailedThisYear = 2;

        var state = engine.CaptureState(registry);
        engine.Shutdown();

        var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        Assert.Equal(2, restored.Economy!.CompaniesFailedThisYear);
        Assert.Equal(rate, restored.Economy.MetricHistory[^1].CompanyFailureRate);
        restored.Shutdown();
    }
}
