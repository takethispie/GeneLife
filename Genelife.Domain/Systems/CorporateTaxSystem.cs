using Arch.Core;
using Genelife.Components.Company;
using Genelife.Components.Finance;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Corporate profit tax with loss carry-forward. Tax is paid only when cash is
/// available; otherwise it becomes a visible liability.
/// </summary>
public class CorporateTaxSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly int interval;

    public CorporateTaxSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerPayCycle;
    }

    public string Name => "CorporateTax";
    public int Order => 535;
    public Cadence Cadence => new(interval <= 0 ? 1 : interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var companies = new List<(Entity Entity, CompanyAccount Account)>();
        var query = new QueryDescription().WithAll<CompanyName, CompanyAccount>();
        world.Query(in query, (Entity entity, ref CompanyName _, ref CompanyAccount account) =>
            companies.Add((entity, account)));

        foreach (var (company, accountSnapshot) in companies)
        {
            if (!world.IsAlive(company))
                continue;

            if (accountSnapshot.State == CompanySolvency.Bankrupt)
            {
                var bankrupt = economy.Accounts.GetOrCreate(company, Money.Zero, tick);
                RollCycleToDisplay(ref bankrupt);
                economy.Accounts.Set(company, bankrupt);
                continue;
            }

            var work = accountSnapshot;
            work.LastTaxPaid = Money.Zero;

            if (work.LossCarryForwardExpiresAtTick > 0 && tick > work.LossCarryForwardExpiresAtTick)
                work.LossCarryForward = Money.Zero;

            if (work.TaxArrears.IsPositive)
            {
                var arrearsPaid = work.TaxArrears.Min(work.Balance);
                if (arrearsPaid.IsPositive && economy.Accounts.TryDebit(
                        company,
                        arrearsPaid,
                        LedgerKind.CorporateTaxPaid,
                        Guid.NewGuid(),
                        EconomyAccountIds.Government,
                        "corporate.tax.arrears",
                        tick))
                {
                    economy.Government.Balance += arrearsPaid;
                    work.TaxArrears -= arrearsPaid;
                }
            }

            var profit = work.RevenueThisCycle
                         - work.WagesThisCycle
                         - work.CostsThisCycle
                         - work.InterestPaidThisCycle;
            work.LastProfit = profit;

            if (profit.IsNegative)
            {
                if (work.LossCarryForward.IsZero)
                {
                    work.LossCarryForwardExpiresAtTick = tick
                        + economy.Clock.TicksPerYear * economy.Options.LossCarryForwardYears;
                }
                work.LossCarryForward += -profit;
            }
            else
            {
                var absorbed = profit.Min(work.LossCarryForward);
                work.LossCarryForward -= absorbed;
                var taxable = profit - absorbed;
                var tax = taxable.Multiply(economy.Options.CorporateTaxRate);
                if (tax.IsPositive)
                {
                    var blocked = work.State is CompanySolvency.Arrears
                        or CompanySolvency.Insolvent
                        or CompanySolvency.Bankrupt;

                    if (!blocked && economy.Accounts.TryDebit(
                            company,
                            tax,
                            LedgerKind.CorporateTaxPaid,
                            Guid.NewGuid(),
                            EconomyAccountIds.Government,
                            "corporate.tax",
                            tick))
                    {
                        economy.Government.Balance += tax;
                        economy.Government.CorporateTaxCollected += tax;
                        economy.TaxThisYear += tax;
                        work.LastTaxPaid = tax;
                    }
                    else
                    {
                        work.TaxArrears += tax;
                        economy.Emit(new CorporateTaxDeferred(
                            CompanyAccountService.NetworkIdOf(world, company),
                            tax.Cents,
                            tick));
                    }
                }
            }

            var live = economy.Accounts.GetOrCreate(company, Money.Zero, tick);
            RollCycleToDisplay(ref live);
            live.LastProfit = work.LastProfit;
            live.LastTaxPaid = work.LastTaxPaid;
            live.LossCarryForward = work.LossCarryForward;
            live.LossCarryForwardExpiresAtTick = work.LossCarryForwardExpiresAtTick;
            live.TaxArrears = work.TaxArrears;
            economy.Accounts.Set(company, live);

            economy.Emit(new CorporateTaxAssessed(
                CompanyAccountService.NetworkIdOf(world, company),
                profit.Cents,
                work.LastTaxPaid.Cents,
                tick));
        }
    }

    private static void RollCycleToDisplay(ref CompanyAccount account)
    {
        account.RevenueLastCycle = account.RevenueThisCycle;
        account.WagesLastCycle = account.WagesThisCycle;
        account.CostsLastCycle = account.CostsThisCycle;
        account.InterestPaidLastCycle = account.InterestPaidThisCycle;
        account.RevenueThisCycle = Money.Zero;
        account.WagesThisCycle = Money.Zero;
        account.CostsThisCycle = Money.Zero;
        account.InterestPaidThisCycle = Money.Zero;
    }
}
