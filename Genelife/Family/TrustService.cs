using Arch.Core;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Life;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Family;

/// <summary>
/// Minor trust accounts. Deposits and releases are transfers between the
/// estate/trust and a beneficiary or government; the trust balance is part of
/// the money supply and never spendable through ordinary consumption.
/// </summary>
public sealed class TrustService
{
    private readonly World world;
    private readonly EconomyContext economy;
    private ulong fallbackId = 4_000_000_000;

    public TrustService(World world, EconomyContext economy)
    {
        this.world = world;
        this.economy = economy;
    }

    public Func<Entity, NetworkId>? NetworkIdAllocator { get; set; }
    public Action<Entity>? NetworkIdRemover { get; set; }

    public Entity Create(
        Entity beneficiary,
        Entity guardian,
        Money amount,
        ulong sourceSimNetworkId,
        long tick,
        long releaseAgeTicks)
    {
        if (!amount.IsPositive || beneficiary == Entity.Null || !world.IsAlive(beneficiary))
            return Entity.Null;

        var trust = world.Create(new TrustAccount
        {
            Balance = amount,
            Beneficiary = beneficiary,
            Guardian = guardian,
            CreatedTick = tick,
            ReleaseAgeTicks = releaseAgeTicks,
            SourceSimNetworkId = sourceSimNetworkId
        });
        var networkId = NetworkIdAllocator?.Invoke(trust) ?? new NetworkId(fallbackId++);
        world.Add(trust, networkId);
        economy.Ledger.Record(
            Guid.NewGuid(),
            tick,
            LedgerKind.TrustDeposit,
            sourceSimNetworkId == 0 ? null : sourceSimNetworkId,
            networkId.Value,
            amount,
            "estate.trust-deposit");
        economy.Emit(new TrustOpened(
            networkId.Value,
            NetworkIdOf(beneficiary),
            NetworkIdOf(guardian),
            amount.Cents,
            sourceSimNetworkId,
            tick));
        return trust;
    }

    public void ReleaseDue(long tick)
    {
        var policy = economy.Life.Policy;
        var trusts = new List<(Entity Entity, TrustAccount Account)>();
        world.Query(in new QueryDescription().WithAll<TrustAccount>(),
            (Entity entity, ref TrustAccount account) => trusts.Add((entity, account)));
        trusts.Sort((a, b) => NetworkIdOf(a.Entity).CompareTo(NetworkIdOf(b.Entity)));

        foreach (var (entity, account) in trusts)
        {
            if (!world.IsAlive(entity))
                continue;

            var beneficiary = account.Beneficiary;
            var beneficiaryAlive = beneficiary != Entity.Null && world.IsAlive(beneficiary) &&
                                   !world.Has<Dead>(beneficiary);
            var age = beneficiaryAlive && world.Has<Alive>(beneficiary)
                ? world.Get<Alive>(beneficiary).Age
                : 0;

            if (beneficiaryAlive && age < policy.WorkStartAge)
                continue;

            var amount = account.Balance;
            if (!amount.IsPositive)
            {
                Close(entity);
                continue;
            }

            if (beneficiaryAlive)
            {
                if (!world.Has<Wallet>(beneficiary))
                    world.Add(beneficiary, new Wallet(Money.Zero));
                world.Get<Wallet>(beneficiary).Balance += amount;
                economy.Life.TrustReleasedThisYear += amount;
                economy.Ledger.Record(
                    Guid.NewGuid(), tick, LedgerKind.TrustRelease,
                    NetworkIdOf(entity), NetworkIdOf(beneficiary), amount,
                    "trust.release");
                economy.Emit(new TrustReleased(
                    NetworkIdOf(entity), NetworkIdOf(beneficiary), amount.Cents, "adulthood", tick));
            }
            else
            {
                economy.Government.Balance += amount;
                economy.Life.TrustReleasedThisYear += amount;
                economy.Ledger.Record(
                    Guid.NewGuid(), tick, LedgerKind.TrustRelease,
                    NetworkIdOf(entity), EconomyAccountIds.Government, amount,
                    "trust.escheat");
                economy.Emit(new TrustReleased(
                    NetworkIdOf(entity), 0, amount.Cents, "beneficiary-dead", tick));
            }

            Close(entity);
        }
    }

    public Money TotalHeld()
    {
        var total = Money.Zero;
        world.Query(in new QueryDescription().WithAll<TrustAccount>(),
            (ref TrustAccount account) => total += account.Balance);
        return total;
    }

    private void Close(Entity trust)
    {
        if (world.IsAlive(trust))
            world.Destroy(trust);
        NetworkIdRemover?.Invoke(trust);
    }

    private ulong NetworkIdOf(Entity entity) =>
        entity != Entity.Null && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0;
}
