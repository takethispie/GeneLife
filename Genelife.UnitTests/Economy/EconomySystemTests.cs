using Arch.Core;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Systems;
using Genelife.Values;

namespace Genelife.UnitTests.Economy;

public class EconomySystemTests
{
    [Fact]
    public void Ledger_IsIdempotentAndTracksSources()
    {
        var ledger = new FinanceLedger();
        var id = Guid.NewGuid();

        ledger.Record(id, 1, LedgerKind.BasicIncome, null, 7, Money.FromUnits(50m), "ubi");
        ledger.Record(id, 1, LedgerKind.BasicIncome, null, 7, Money.FromUnits(50m), "ubi");

        Assert.Equal(1, ledger.RecordedCount);
        Assert.Equal(Money.FromUnits(50m), ledger.SourceTotal);
        Assert.Single(ledger.Recent());
    }

    [Fact]
    public void CompanyAccount_NeverGoesNegative()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var company = world.Create(new CompanyName("Acme"), CompanyAccount.Create(Money.FromUnits(100m), 0));

        var ok = economy.Accounts.TryDebit(
            company,
            Money.FromUnits(150m),
            LedgerKind.OfficeCost,
            Guid.NewGuid(),
            EconomyAccountIds.Government,
            "test",
            1);

        Assert.False(ok);
        Assert.Equal(Money.FromUnits(100m), world.Get<CompanyAccount>(company).Balance);
        Assert.DoesNotContain(economy.Ledger.Recent(), entry => entry.Kind == LedgerKind.OfficeCost);
    }

    [Fact]
    public void CompanyAccount_DebitIsIdempotentByTransactionId()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var company = world.Create(new CompanyName("Acme"), CompanyAccount.Create(Money.FromUnits(100m), 0));
        var tx = Guid.NewGuid();

        Assert.True(economy.Accounts.TryDebit(
            company, Money.FromUnits(30m), LedgerKind.OfficeCost, tx,
            EconomyAccountIds.Government, "test", 1));
        Assert.True(economy.Accounts.TryDebit(
            company, Money.FromUnits(30m), LedgerKind.OfficeCost, tx,
            EconomyAccountIds.Government, "test", 1));

        Assert.Equal(Money.FromUnits(70m), world.Get<CompanyAccount>(company).Balance);
        Assert.Single(economy.Ledger.Recent().Where(entry => entry.TransactionId == tx));
    }

    [Fact]
    public void BasicIncome_PaysEverySimRegardlessOfEmployment()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var employed = world.Create(new GenName("Employed"), new Wallet(Money.Zero));
        var unemployed = world.Create(new GenName("Unemployed"), new Wallet(Money.Zero));
        var player = world.Create(new GenName("Player"), new Wallet(Money.Zero), new PlayerControlled("p1"));

        var system = new BasicIncomeSystem(world, economy, interval: 1);
        system.Update(100);

        Assert.Equal(Money.FromUnits(50m), world.Get<Wallet>(employed).Balance);
        Assert.Equal(Money.FromUnits(50m), world.Get<Wallet>(unemployed).Balance);
        Assert.Equal(Money.FromUnits(50m), world.Get<Wallet>(player).Balance);
        Assert.Equal(Money.FromUnits(150m), economy.Ledger.SourceTotal - Money.FromUnits(100_000m));
    }

    [Fact]
    public void ConsumerSpending_DebitsOnceAndRevenueSettlesToCompany()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var company = world.Create(new CompanyName("Shop"), CompanyAccount.Create(Money.Zero, 0));
        world.Create(new Office("HQ", company, 10));
        var sim = world.Create(new GenName("Buyer"), new Wallet(Money.FromUnits(100m)));

        var before = economy.TotalMoneySupply();
        var spending = new ConsumerSpendingSystem(world, economy, interval: 1);
        spending.Update(100);

        Assert.Equal(Money.Zero, world.Get<Wallet>(sim).Balance);
        Assert.True(economy.PendingRevenue.ContainsKey(company));
        Assert.Equal(before, economy.TotalMoneySupply());

        var revenue = new CompanyRevenueSystem(world, economy, interval: 1);
        revenue.Update(300);

        Assert.Equal(Money.FromUnits(100m), world.Get<CompanyAccount>(company).Balance);
        Assert.Equal(before, economy.TotalMoneySupply());
        Assert.Empty(economy.PendingRevenue);
    }

    [Fact]
    public void Diagnostic_FindsFirstMoneyLeak()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var companyA = world.Create(new CompanyName("Factory"), CompanyAccount.Create(Money.FromUnits(10_000m), 0));
        var officeA = world.Create(new Office("Factory HQ", companyA, 10, Money.FromUnits(200m)));
        var companyB = world.Create(new CompanyName("Market"), CompanyAccount.Create(Money.FromUnits(5_000m), 0));
        world.Create(new Office("Market HQ", companyB, 10, Money.FromUnits(100m)));
        world.Create(new GenName("Worker"), new Wallet(Money.Zero), new Alive(30),
            new Employee(companyA, officeA, "Dev", Money.FromUnits(1000m)));
        for (var i = 0; i < 9; i++)
            world.Create(new GenName($"Citizen{i}"), new Wallet(Money.Zero), new Alive(25 + i),
                new JobSeeker(true, Genelife.Enums.Skill.None, Money.FromUnits(1000m)));

        var systems = new ISimulationSystem[]
        {
            new CompanyRevenueSystem(world, economy),
            new ConsumerSpendingSystem(world, economy),
            new PayrollSystem(world, economy),
            new CompanyCostsSystem(world, economy),
            new BasicIncomeSystem(world, economy),
            new BankingSystem(world, economy),
            new CorporateTaxSystem(world, economy),
            new CreditSystem(world, economy),
            new CompanySolvencySystem(world, economy),
            new EconomyMetricsSystem(world, economy)
        };

        for (var tick = 1; tick <= economy.Clock.TicksPerYear; tick++)
        {
            foreach (var system in systems)
            {
                if (!system.Cadence.ShouldRun(tick))
                    continue;
                var before = economy.TotalMoneySupply();
                system.Update(new SimulationContext(tick, 1f / 20f, economy.Clock));
                var after = economy.TotalMoneySupply();
                var expected = system.Name == "BasicIncome"
                    ? economy.Options.UniversalBasicIncome * 10
                    : Money.Zero;
                if (after - before != expected)
                    Assert.Fail($"Money delta {after - before} at tick {tick} in {system.Name}, expected {expected}");
            }
        }
    }

    [Fact]
    public void ClosedLoop_TenYearRunOnlyAddsBasicIncome()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);

        var companyA = world.Create(
            new CompanyName("Factory"),
            CompanyAccount.Create(Money.FromUnits(10_000m), 0));
        var officeA = world.Create(new Office("Factory HQ", companyA, 10, Money.FromUnits(200m)));
        var companyB = world.Create(
            new CompanyName("Market"),
            CompanyAccount.Create(Money.FromUnits(5_000m), 0));
        world.Create(new Office("Market HQ", companyB, 10, Money.FromUnits(100m)));

        world.Create(
            new GenName("Worker"),
            new Wallet(Money.Zero),
            new Alive(30),
            new Employee(companyA, officeA, "Dev", Money.FromUnits(1000m)));

        for (var i = 0; i < 9; i++)
        {
            world.Create(
                new GenName($"Citizen{i}"),
                new Wallet(Money.Zero),
                new Alive(25 + i),
                new JobSeeker(true, Genelife.Enums.Skill.None, Money.FromUnits(1000m)));
        }

        var registry = new SystemRegistry();
        registry.Register(new CompanyRevenueSystem(world, economy));
        registry.Register(new ConsumerSpendingSystem(world, economy));
        registry.Register(new PayrollSystem(world, economy));
        registry.Register(new CompanyCostsSystem(world, economy));
        registry.Register(new BasicIncomeSystem(world, economy));
        registry.Register(new BankingSystem(world, economy));
        registry.Register(new CorporateTaxSystem(world, economy));
        registry.Register(new CreditSystem(world, economy));
        registry.Register(new CompanySolvencySystem(world, economy));
        registry.Register(new EconomyMetricsSystem(world, economy));

        var before = economy.TotalMoneySupply();
        var ticks = economy.Clock.TicksPerYear * 10;
        for (var tick = 1; tick <= ticks; tick++)
            registry.Update(new SimulationContext(tick, 1f / 20f, economy.Clock));

        var expectedUbi = economy.Options.UniversalBasicIncome
            * (10 * (int)(ticks / economy.Options.BasicIncomeIntervalTicks));
        Assert.Equal(before + expectedUbi, economy.TotalMoneySupply());
    }
}
