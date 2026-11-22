using Arch.Core;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Yearly economy snapshot. Reads only; persists through the outbound event
/// channel and keeps the last 50 years in memory for the admin.
/// </summary>
public class EconomyMetricsSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly int interval;

    public EconomyMetricsSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerYear;
    }

    public string Name => "EconomyMetrics";
    public int Order => 610;
    public Cadence Cadence => new(interval <= 0 ? 1 : interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public EconomySnapshot Update(long tick)
    {
        var householdCash = Money.Zero;
        var householdSavings = Money.Zero;
        var companyCash = Money.Zero;
        var arrears = Money.Zero;
        var bankrupt = 0;
        var wealth = new List<long>();

        var walletQuery = new QueryDescription().WithAll<Wallet>();
        world.Query(in walletQuery, (ref Wallet wallet) => householdCash += wallet.Balance);

        var savingsQuery = new QueryDescription().WithAll<SavingsAccount>();
        world.Query(in savingsQuery, (ref SavingsAccount savings) => householdSavings += savings.Balance);

        var companyQuery = new QueryDescription().WithAll<CompanyAccount>();
        world.Query(in companyQuery, (ref CompanyAccount account) =>
        {
            companyCash += account.Balance;
            arrears += account.Arrears + account.TaxArrears;
            if (account.State == CompanySolvency.Bankrupt)
                bankrupt++;
        });

        var simQuery = new QueryDescription().WithAll<Alive>();
        world.Query(in simQuery, (Entity entity, ref Alive _) =>
        {
            var cash = world.Has<Wallet>(entity) ? world.Get<Wallet>(entity).Balance : Money.Zero;
            var savings = world.Has<SavingsAccount>(entity)
                ? world.Get<SavingsAccount>(entity).Balance
                : Money.Zero;
            wealth.Add((cash + savings).Cents);
        });

        var employed = 0;
        var workingAge = 0;
        var jobSeekers = 0;
        var simsQuery = new QueryDescription().WithAll<Alive>();
        world.Query(in simsQuery, (Entity entity, ref Alive alive) =>
        {
            if (alive.Age < 18)
                return;
            workingAge++;
            if (world.Has<Employee>(entity))
                employed++;
            if (world.Has<JobSeeker>(entity) && world.Get<JobSeeker>(entity).IsLookingForWork)
                jobSeekers++;
        });

        var openSeats = 0;
        var seatQuery = new QueryDescription().WithAll<OpenPosition>();
        world.Query(in seatQuery, (ref OpenPosition _) => openSeats++);

        var totalMoneySupply = householdCash + householdSavings + companyCash
                               + economy.Government.Balance + economy.Pension.Balance
                               + economy.Bank.Balance + economy.Bank.InterestPool;

        foreach (var pending in economy.PendingRevenue.Values)
            totalMoneySupply += pending.Total;

        var unemployment = workingAge > 0 ? jobSeekers / (decimal)workingAge : 0m;
        var vacancy = openSeats + employed > 0 ? openSeats / (decimal)(openSeats + employed) : 0m;
        var wageShare = economy.RevenueThisYear.IsPositive
            ? economy.WagesPaidThisYear.Cents / (decimal)economy.RevenueThisYear.Cents
            : 0m;
        var profitShare = economy.RevenueThisYear.IsPositive
            ? (economy.RevenueThisYear.Cents - economy.WagesPaidThisYear.Cents)
              / (decimal)economy.RevenueThisYear.Cents
            : 0m;

        var snapshot = new EconomySnapshot(
            tick,
            economy.Clock.YearOf(tick),
            householdCash,
            householdSavings,
            companyCash,
            economy.Loans.TotalOutstanding(),
            economy.Pension.Balance,
            economy.Government.Balance,
            economy.Bank.Balance + economy.Bank.InterestPool,
            totalMoneySupply,
            economy.WagesPaidThisYear,
            economy.RevenueThisYear,
            economy.TaxThisYear,
            economy.BasicIncomeThisYear,
            arrears,
            unemployment,
            vacancy,
            wageShare,
            profitShare,
            Gini(wealth),
            0,
            bankrupt);

        economy.MetricHistory.Add(snapshot);
        if (economy.MetricHistory.Count > 50)
            economy.MetricHistory.RemoveAt(0);

        economy.Emit(new EconomySnapshotTaken(
            snapshot.Tick,
            snapshot.Year,
            snapshot.HouseholdCash.Cents,
            snapshot.HouseholdSavings.Cents,
            snapshot.CompanyCash.Cents,
            snapshot.CompanyDebt.Cents,
            snapshot.PensionFund.Cents,
            snapshot.GovernmentFund.Cents,
            snapshot.TotalMoneySupply.Cents,
            snapshot.WagesPaidThisYear.Cents,
            snapshot.RevenueThisYear.Cents,
            snapshot.TaxThisYear.Cents,
            snapshot.Arrears.Cents,
            snapshot.UnemploymentRate,
            snapshot.VacancyRate,
            snapshot.WageShare,
            snapshot.Gini,
            snapshot.BankruptCompanies));
        economy.ResetYearCounters();
        return snapshot;
    }

    public static decimal Gini(List<long> wealth)
    {
        if (wealth.Count == 0)
            return 0m;

        wealth.Sort();
        var sum = 0L;
        for (var i = 0; i < wealth.Count; i++)
            sum += wealth[i];

        if (sum == 0)
            return 0m;

        decimal weighted = 0m;
        for (var i = 0; i < wealth.Count; i++)
            weighted += (i + 1L) * (decimal)wealth[i];

        var n = wealth.Count;
        return (2m * weighted) / (n * (decimal)sum) - ((n + 1m) / n);
    }
}
