using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Application.Systems;

/// <summary>
/// Monthly pension payout from the pooled fund, with a government top-up and a
/// visible pro-rata reduction when both are exhausted. The fund never goes
/// negative and each retiree is paid at most once per month.
/// </summary>
public class PensionSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription query;
    private readonly int interval;

    public PensionSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerMonth;
        query = new QueryDescription()
            .WithAll<RetirementIncome, Lifecycle>()
            .WithNone<Dead>();
    }

    public string Name => "Pension";
    public int Order => 505;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick, context.Time);

    public void Update(long tick) => Update(tick, economy.Clock);

    public void Update(long tick, SimulationClock clock)
    {
        var life = economy.Life;
        economy.Pension.PayoutsThisCycle = Money.Zero;
        economy.Pension.Shortfall = Money.Zero;
        var fundingRatio = life.PensionFundingRatio(economy.Pension.Balance);

        if (clock.IsYearBoundary(tick))
            ApplyYearBoundary(tick, fundingRatio);

        var due = new List<(Entity Entity, RetirementIncome Income)>();
        world.Query(in query, (Entity entity, ref RetirementIncome income, ref Lifecycle lifecycle) =>
        {
            if (lifecycle.Stage is not (Enums.LifeStage.Retirement or Enums.LifeStage.Elderly))
                return;
            if (income.LastPaidTick == tick || !income.MonthlyPension.IsPositive)
                return;
            due.Add((entity, income));
        });

        var totalDue = Money.Zero;
        foreach (var item in due)
            totalDue += item.Income.MonthlyPension;

        if (!totalDue.IsPositive)
            return;

        var fundAvailable = economy.Pension.Balance;
        var governmentAvailable = economy.Government.Balance;
        var scale = 1m;
        if (fundAvailable + governmentAvailable < totalDue)
            scale = (fundAvailable + governmentAvailable).Cents / (decimal)totalDue.Cents;

        var fundRemaining = fundAvailable;
        var governmentRemaining = governmentAvailable;
        var paidTotal = Money.Zero;

        foreach (var (entity, dueIncome) in due)
        {
            if (!world.IsAlive(entity))
                continue;

            var income = dueIncome;
            var paidDue = new Money((long)Math.Floor(income.MonthlyPension.Cents * scale));
            var fromFund = income.MeansTested ? Money.Zero : paidDue.Min(fundRemaining);
            var fromGovernment = paidDue - fromFund;
            if (fromGovernment > governmentRemaining)
                fromGovernment = governmentRemaining;
            var paid = fromFund + fromGovernment;

            fundRemaining -= fromFund;
            governmentRemaining -= fromGovernment;
            economy.Pension.Balance = fundRemaining;
            economy.Government.Balance = governmentRemaining;
            economy.Pension.PayoutsThisCycle += paid;
            paidTotal += paid;

            if (paid.IsPositive)
            {
                LifeSystemHelpers.EnsureWallet(world, entity);
                world.Get<Wallet>(entity).Balance += paid;

                if (fromFund.IsPositive)
                    economy.Ledger.Record(Guid.NewGuid(), tick, LedgerKind.PensionPaid,
                        EconomyAccountIds.PensionFund, LifeSystemHelpers.NetworkIdOf(world, entity), fromFund, "pension.payout");
                if (fromGovernment.IsPositive)
                {
                    economy.Ledger.Record(Guid.NewGuid(), tick, LedgerKind.PensionTopUp,
                        EconomyAccountIds.Government, LifeSystemHelpers.NetworkIdOf(world, entity), fromGovernment, "pension.topup");
                    economy.Emit(new PensionFundTopUp(fromGovernment.Cents, fundRemaining.Cents, tick));
                }

                economy.Emit(new PensionPaid(LifeSystemHelpers.NetworkIdOf(world, entity), paid.Cents,
                    fromFund.IsPositive, tick));
            }

            var reduced = paid < income.MonthlyPension;
            if (reduced)
            {
                economy.Pension.Shortfall += income.MonthlyPension - paid;
                economy.Emit(new PensionReduced(
                    LifeSystemHelpers.NetworkIdOf(world, entity),
                    income.MonthlyPension.Cents,
                    paid.Cents,
                    tick));
            }

            income.Reduced = reduced;
            income.LastPaidTick = tick;
            world.Get<RetirementIncome>(entity) = income;
        }
    }

    private void ApplyYearBoundary(long tick, decimal fundingRatio)
    {
        var life = economy.Life;
        if (fundingRatio >= 1m && economy.Pension.Balance.IsPositive)
        {
            var yield = economy.Pension.Balance.Multiply(life.Pensions.AnnualFundYield).Min(economy.Government.Balance);
            if (yield.IsPositive)
            {
                economy.Government.Balance -= yield;
                economy.Pension.Balance += yield;
                economy.Ledger.Record(Guid.NewGuid(), tick, LedgerKind.PensionFundYield,
                    EconomyAccountIds.Government, EconomyAccountIds.PensionFund, yield, "pension.yield");
                economy.Emit(new PensionFundYieldCredited(yield.Cents, economy.Pension.Balance.Cents, tick));
            }

            var indexed = new List<(Entity Entity, RetirementIncome Income)>();
            world.Query(in query, (Entity entity, ref RetirementIncome income, ref Lifecycle _) =>
            {
                if (income.MonthlyPension.IsPositive)
                    indexed.Add((entity, income));
            });

            foreach (var (entity, indexedIncome) in indexed)
            {
                if (!world.IsAlive(entity))
                    continue;
                var income = indexedIncome;
                income.MonthlyPension = life.Pensions.IndexedAmount(income.MonthlyPension, true);
                world.Get<RetirementIncome>(entity) = income;
            }
        }
    }
}
