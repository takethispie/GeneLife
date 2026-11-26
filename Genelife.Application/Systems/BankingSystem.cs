using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Application.Systems;

/// <summary>
/// Moves excess household cash into savings and draws savings back when cash is
/// low. Savings interest is paid only from the bank interest pool, never minted.
/// </summary>
public class BankingSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription genosQuery;
    private readonly int interval;

    public BankingSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerPayCycle;
        genosQuery = new QueryDescription().WithAll<GenName, Wallet>().WithNone<Components.Gen.Dead>();
    }

    public string Name => "Banking";
    public int Order => 530;
    public Cadence Cadence => new(interval <= 0 ? 1 : interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var genoses = new List<Entity>();
        world.Query(in genosQuery, (Entity entity, ref GenName _, ref Wallet _) => genoses.Add(entity));

        foreach (var genos in genoses)
        {
            if (!world.IsAlive(genos))
                continue;

            if (!world.Has<SavingsAccount>(genos))
                world.Add(genos, new SavingsAccount());

            ref var wallet = ref world.Get<Wallet>(genos);
            ref var savings = ref world.Get<SavingsAccount>(genos);

            var buffer = economy.Options.SavingsBuffer;
            var minimum = economy.Options.MinimumCash;

            if (wallet.Balance > buffer)
            {
                var deposit = wallet.Balance - buffer;
                wallet.Balance -= deposit;
                savings.Balance += deposit;
                savings.LifetimeDeposited += deposit;
                economy.Ledger.Record(
                    Guid.NewGuid(),
                    tick,
                    LedgerKind.SavingsDeposit,
                    CompanyAccountService.NetworkIdOf(world, genos),
                    CompanyAccountService.NetworkIdOf(world, genos),
                    deposit,
                    "savings.deposit");
                economy.Emit(new SavingsDeposited(
                    CompanyAccountService.NetworkIdOf(world, genos),
                    deposit.Cents,
                    tick));
            }
            else if (wallet.Balance < minimum && savings.Balance.IsPositive)
            {
                var needed = minimum - wallet.Balance;
                var withdraw = needed.Min(savings.Balance);
                savings.Balance -= withdraw;
                savings.LifetimeWithdrawn += withdraw;
                wallet.Balance += withdraw;
                economy.Ledger.Record(
                    Guid.NewGuid(),
                    tick,
                    LedgerKind.SavingsWithdrawal,
                    CompanyAccountService.NetworkIdOf(world, genos),
                    CompanyAccountService.NetworkIdOf(world, genos),
                    withdraw,
                    "savings.withdrawal");
                economy.Emit(new SavingsWithdrawn(
                    CompanyAccountService.NetworkIdOf(world, genos),
                    withdraw.Cents,
                    tick));
            }
        }

        PayInterest(tick);
    }

    private void PayInterest(long tick)
    {
        if (!economy.Bank.InterestPool.IsPositive)
            return;

        var savingsQuery = new QueryDescription().WithAll<SavingsAccount>().WithNone<Components.Gen.Dead>();
        var totalSavings = Money.Zero;
        world.Query(in savingsQuery, (ref SavingsAccount savings) =>
        {
            totalSavings += savings.Balance;
        });

        if (!totalSavings.IsPositive)
            return;

        var pool = economy.Bank.InterestPool;
        var payouts = new List<(Entity Genos, Money Amount)>();
        var allocated = Money.Zero;

        world.Query(in savingsQuery, (Entity entity, ref SavingsAccount savings) =>
        {
            if (!savings.Balance.IsPositive)
                return;
            var share = new Money((long)Math.Floor(pool.Cents * (savings.Balance.Cents / (decimal)totalSavings.Cents)));
            if (share.IsPositive)
            {
                payouts.Add((entity, share));
                allocated += share;
            }
        });

        var remainder = pool - allocated;
        if (remainder.IsPositive && payouts.Count > 0)
        {
            var first = payouts[0];
            payouts[0] = (first.Genos, first.Amount + remainder);
        }

        foreach (var (genos, amount) in payouts)
        {
            ref var savings = ref world.Get<SavingsAccount>(genos);
            savings.Balance += amount;
            savings.InterestEarned += amount;
            savings.LastInterestTick = tick;
            economy.Bank.InterestPool -= amount;
            economy.Ledger.Record(
                Guid.NewGuid(),
                tick,
                LedgerKind.SavingsInterest,
                EconomyAccountIds.Bank,
                CompanyAccountService.NetworkIdOf(world, genos),
                amount,
                "savings.interest");
            economy.Emit(new SavingsInterestPaid(
                CompanyAccountService.NetworkIdOf(world, genos),
                amount.Cents,
                tick));
        }
    }
}
