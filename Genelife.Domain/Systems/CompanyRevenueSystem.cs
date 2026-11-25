using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Company;
using Genelife.Components.Finance;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Settles consumer demand accumulated since the last pay cycle into company
/// accounts before payroll runs. Revenue is recorded exactly once, at
/// settlement, so wallet debits and company credits reconcile.
/// </summary>
public class CompanyRevenueSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly int interval;

    public CompanyRevenueSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerPayCycle;
    }

    public string Name => "CompanyRevenue";
    public int Order => 450;
    public Cadence Cadence => new(interval <= 0 ? 1 : interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        if (economy.PendingRevenue.Count == 0)
            return;

        var pending = economy.PendingRevenue.ToList();
        economy.PendingRevenue.Clear();

        foreach (var (company, revenue) in pending)
        {
            if (!revenue.Total.IsPositive)
                continue;

            var payee = world.IsAlive(company);
            if (payee)
            {
                foreach (var spend in revenue.Spends)
                {
                    economy.Accounts.Credit(
                        company,
                        spend.Amount,
                        LedgerKind.CompanyRevenue,
                        Guid.NewGuid(),
                        spend.GenosNetworkId,
                        "company.revenue",
                        tick,
                        spend.Category);
                }

                var account = economy.Accounts.GetOrCreate(company, Money.Zero, tick);
                account.RevenueLastCycle += revenue.Total;
                economy.Accounts.Set(company, account);
            }
            else
            {
                // A deleted company's demand is routed to the public account so
                // the money never disappears from the closed loop.
                foreach (var spend in revenue.Spends)
                {
                    economy.Ledger.Record(
                        Guid.NewGuid(),
                        tick,
                        LedgerKind.CompanyRevenue,
                        spend.GenosNetworkId,
                        EconomyAccountIds.Government,
                        spend.Amount,
                        "company.revenue.estate",
                        spend.Category);
                }
                economy.Government.Balance += revenue.Total;
            }

            economy.RevenueThisYear += revenue.Total;
            economy.Emit(new CompanyRevenueBooked(
                CompanyAccountService.NetworkIdOf(world, company),
                revenue.Total.Cents,
                tick,
                string.Join(",", revenue.ByCategory.Select(kvp => $"{kvp.Key}:{kvp.Value.Cents}"))));
        }
    }
}
