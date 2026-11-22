using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Company;
using Genelife.Components.Finance;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Charges recurring office operating costs. Paid costs are routed to the public
/// account; unpaid costs become company arrears instead of silently vanishing.
/// </summary>
public class CompanyCostsSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly int interval;

    public CompanyCostsSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerPayCycle;
    }

    public string Name => "CompanyCosts";
    public int Order => 510;
    public Cadence Cadence => new(interval <= 0 ? 1 : interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var costs = new Dictionary<Entity, Money>();
        var officeQuery = new QueryDescription().WithAll<Office>();
        world.Query(in officeQuery, (ref Office office) =>
        {
            if (office.IsClosed || !office.OperatingCostPerCycle.IsPositive)
                return;
            costs.TryGetValue(office.Company, out var current);
            costs[office.Company] = current + office.OperatingCostPerCycle;
        });

        foreach (var (company, total) in costs)
        {
            if (!world.IsAlive(company))
                continue;

            var account = economy.Accounts.GetOrCreate(company, Money.Zero, tick);
            var paid = account.Balance.Min(total);
            if (paid.IsPositive)
            {
                if (economy.Accounts.TryDebit(
                        company,
                        paid,
                        LedgerKind.OfficeCost,
                        Guid.NewGuid(),
                        EconomyAccountIds.Government,
                        "office.cost",
                        tick))
                {
                    economy.Government.Balance += paid;
                    economy.Government.OfficeCostsCollected += paid;
                }
            }

            var unpaid = total - paid;
            account = economy.Accounts.GetOrCreate(company, Money.Zero, tick);
            account.CostsLastCycle += total;
            if (unpaid.IsPositive)
            {
                account.Arrears += unpaid;
                if (account.State is CompanySolvency.Healthy or CompanySolvency.Tight)
                {
                    account.State = CompanySolvency.Arrears;
                    account.StateChangedAtTick = tick;
                }
            }
            economy.Accounts.Set(company, account);

            economy.Emit(new OfficeCostCharged(
                CompanyAccountService.NetworkIdOf(world, company),
                total.Cents,
                paid.Cents,
                tick));
        }
    }
}
