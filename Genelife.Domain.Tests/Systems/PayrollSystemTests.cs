using Arch.Core;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Application.Systems;
using Genelife.Values;

namespace Genelife.UnitTests.Systems;

public class PayrollSystemTests
{
    private static readonly SimulationContext Context = new(Tick: 1, DeltaTime: 1f / 20f);

    private static void RunTicks(World world, EconomyContext economy, int payrollInterval, int ticks)
    {
        var registry = new SystemRegistry();
        registry.Register(new PayrollSystem(world, economy, payrollInterval));

        for (var tick = 1; tick <= ticks; tick++)
            registry.Update(new SimulationContext(tick, Context.DeltaTime));
    }

    private static Entity AddFundedCompany(World world, decimal balance)
    {
        return world.Create(CompanyAccount.Create(Money.FromUnits(balance), 0));
    }

    [Fact]
    public void Update_PaysNetSalaryAndWithholdsTaxAndPension()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var company = AddFundedCompany(world, 10_000m);
        var office = world.Create();
        var employee = world.Create(
            new Employee(company, office, "Dev", Money.FromUnits(1000m)),
            new Wallet(Money.Zero));

        RunTicks(world, economy, payrollInterval: 3, ticks: 3);

        Assert.Equal(Money.FromUnits(830m), world.Get<Wallet>(employee).Balance);
        Assert.Equal(Money.FromUnits(8950m), world.Get<CompanyAccount>(company).Balance);
        Assert.Equal(Money.FromUnits(120m), economy.Government.Balance);
        Assert.Equal(Money.FromUnits(100m), economy.Pension.Balance);
        Assert.Equal(0, world.Get<CompanyAccount>(company).Arrears.Cents);
        Assert.False(world.Has<WageClaim>(employee));
    }

    [Fact]
    public void Update_DoesNotPayBeforeInterval()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var company = AddFundedCompany(world, 10_000m);
        var office = world.Create();
        var employee = world.Create(
            new Employee(company, office, "Dev", Money.FromUnits(1000m)),
            new Wallet(Money.Zero));

        RunTicks(world, economy, payrollInterval: 5, ticks: 2);

        Assert.Equal(Money.Zero, world.Get<Wallet>(employee).Balance);
        Assert.Equal(Money.FromUnits(10_000m), world.Get<CompanyAccount>(company).Balance);
    }

    [Fact]
    public void Update_AccumulatesAcrossIntervals()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var company = AddFundedCompany(world, 10_000m);
        var office = world.Create();
        var employee = world.Create(
            new Employee(company, office, "Dev", Money.FromUnits(500m)),
            new Wallet(Money.Zero));

        RunTicks(world, economy, payrollInterval: 2, ticks: 4);

        Assert.Equal(Money.FromUnits(830m), world.Get<Wallet>(employee).Balance);
    }

    [Fact]
    public void Update_ShortfallCreatesClaimWithoutMintingMoney()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var company = AddFundedCompany(world, 100m);
        var office = world.Create();
        var employee = world.Create(
            new Employee(company, office, "Dev", Money.FromUnits(1000m)),
            new Wallet(Money.Zero));

        var before = economy.TotalMoneySupply();
        RunTicks(world, economy, payrollInterval: 3, ticks: 3);
        var after = economy.TotalMoneySupply();

        Assert.Equal(before, after);
        Assert.True(world.Has<WageClaim>(employee));
        Assert.True(world.Get<WageClaim>(employee).UnpaidNet.IsPositive);
        Assert.True(world.Get<CompanyAccount>(company).Arrears.IsPositive);
    }

    [Fact]
    public void Update_ArrearsArePaidAfterCurrentWages()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var company = AddFundedCompany(world, 100m);
        var office = world.Create();
        var employee = world.Create(
            new Employee(company, office, "Dev", Money.FromUnits(1000m)),
            new Wallet(Money.Zero));

        RunTicks(world, economy, payrollInterval: 1, ticks: 1);
        var claim = world.Get<WageClaim>(employee);
        Assert.True(claim.UnpaidNet.IsPositive);

        var account = world.Get<CompanyAccount>(company);
        account.Balance += Money.FromUnits(2000m);
        world.Get<CompanyAccount>(company) = account;

        RunTicks(world, economy, payrollInterval: 1, ticks: 1);

        Assert.Equal(0, world.Get<CompanyAccount>(company).Arrears.Cents);
        Assert.False(world.Has<WageClaim>(employee));
    }
}
