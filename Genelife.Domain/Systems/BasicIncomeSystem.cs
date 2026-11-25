using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Systems;

/// <summary>
/// Universal basic income: every NPC and player receives 50 units every 100
/// ticks regardless of employment status. This is the only intentional source of
/// new household money in the economy and is recorded as such in the ledger.
/// </summary>
public class BasicIncomeSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription simQuery;
    private readonly int interval;

    public BasicIncomeSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Options.BasicIncomeIntervalTicks;
        simQuery = new QueryDescription().WithAll<GenName>().WithNone<Components.Gen.Dead>();
    }

    public string Name => "BasicIncome";
    public int Order => 440;
    public Cadence Cadence => new(interval <= 0 ? 1 : interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var amount = economy.Options.UniversalBasicIncome;
        if (!amount.IsPositive)
            return;

        var recipients = new List<Entity>();
        world.Query(in simQuery, (Entity entity, ref GenName _) => recipients.Add(entity));

        var totalPaid = Money.Zero;
        foreach (var entity in recipients)
        {
            if (!world.IsAlive(entity))
                continue;

            if (!world.Has<Wallet>(entity))
                world.Add(entity, new Wallet(Money.Zero));

            ref var wallet = ref world.Get<Wallet>(entity);
            wallet.Balance += amount;
            economy.BasicIncomeThisYear += amount;
            totalPaid += amount;

            economy.Ledger.Record(
                Guid.NewGuid(),
                tick,
                LedgerKind.BasicIncome,
                null,
                CompanyAccountService.NetworkIdOf(world, entity),
                amount,
                "ubi");
        }

        if (recipients.Count > 0)
        {
            economy.Emit(new BasicIncomeBatchPaid(
                recipients.Count,
                totalPaid.Cents,
                tick));
        }
    }
}
