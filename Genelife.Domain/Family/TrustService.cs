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
    private readonly RelationshipIndex? relationshipIndex;
    private ulong fallbackId = 4_000_000_000;

    public TrustService(World world, EconomyContext economy, RelationshipIndex? relationshipIndex = null)
    {
        this.world = world;
        this.economy = economy;
        this.relationshipIndex = relationshipIndex;
    }

    public Func<Entity, NetworkId>? NetworkIdAllocator { get; set; }
    public Action<Entity>? NetworkIdRemover { get; set; }

    public Entity Create(
        Entity beneficiary,
        Entity guardian,
        Money amount,
        ulong sourceGenosNetworkId,
        long tick,
        long releaseAgeTicks)
    {
        if (!amount.IsPositive || beneficiary == Entity.Null || !world.IsAlive(beneficiary))
            return Entity.Null;

        if (guardian != Entity.Null && (!world.IsAlive(guardian) || world.Has<Dead>(guardian)))
            guardian = ReplacementGuardianFor(beneficiary, guardian, economy.Life.Policy.WorkStartAge);

        var trust = world.Create(new TrustAccount
        {
            Balance = amount,
            Beneficiary = beneficiary,
            Guardian = guardian,
            CreatedTick = tick,
            ReleaseAgeTicks = releaseAgeTicks,
            SourceGenosNetworkId = sourceGenosNetworkId
        });
        var networkId = NetworkIdAllocator?.Invoke(trust) ?? new NetworkId(fallbackId++);
        world.Add(trust, networkId);
        economy.Ledger.Record(
            Guid.NewGuid(),
            tick,
            LedgerKind.TrustDeposit,
            sourceGenosNetworkId == 0 ? null : sourceGenosNetworkId,
            networkId.Value,
            amount,
            "estate.trust-deposit");
        economy.Emit(new TrustOpened(
            networkId.Value,
            NetworkIdOf(beneficiary),
            NetworkIdOf(guardian),
            amount.Cents,
            sourceGenosNetworkId,
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

    /// <summary>
    /// A trust's guardian is only a pointer for administrative/reporting purposes;
    /// once that guardian dies the trust must not keep referencing them. Replace
    /// the dead guardian with the beneficiary's currently assigned living adult
    /// guardian, or clear it when no such guardian exists.
    /// </summary>
    public void ReassignAfterGuardianDeath(Entity guardian, long tick)
    {
        var workAge = economy.Life.Policy.WorkStartAge;
        var updates = new List<(Entity Entity, Entity Guardian)>();

        world.Query(in new QueryDescription().WithAll<TrustAccount>(),
            (Entity entity, ref TrustAccount account) =>
            {
                var current = account.Guardian;
                if (current == Entity.Null)
                    return;
                if (current != guardian && world.IsAlive(current) && !world.Has<Dead>(current))
                    return;

                updates.Add((entity, ReplacementGuardianFor(account.Beneficiary, guardian, workAge)));
            });

        foreach (var (entity, replacement) in updates)
        {
            if (!world.IsAlive(entity) || !world.Has<TrustAccount>(entity))
                continue;

            var account = world.Get<TrustAccount>(entity);
            if (account.Guardian == replacement)
                continue;

            account.Guardian = replacement;
            world.Get<TrustAccount>(entity) = account;
        }
    }

    private Entity ReplacementGuardianFor(Entity beneficiary, Entity excluded, int workAge)
    {
        if (relationshipIndex is null || beneficiary == Entity.Null ||
            !world.IsAlive(beneficiary) || world.Has<Dead>(beneficiary))
            return Entity.Null;

        foreach (var candidate in relationshipIndex.GuardiansOf(beneficiary))
        {
            if (candidate == Entity.Null || candidate == excluded)
                continue;
            if (!world.IsAlive(candidate) || world.Has<Dead>(candidate))
                continue;
            if (!world.Has<Alive>(candidate) || world.Get<Alive>(candidate).Age < workAge)
                continue;
            return candidate;
        }

        return Entity.Null;
    }

    public Money TotalHeld()
    {
        var total = Money.Zero;
        world.Query(in new QueryDescription().WithAll<TrustAccount>(),
            (ref TrustAccount account) => total += account.Balance);
        return total;
    }

    /// <summary>
    /// Releases a trust immediately for an audited admin override. The transfer is
    /// ledgered exactly like the scheduled release path.
    /// </summary>
    public bool ReleaseNow(Entity trust, string reason, long tick)
    {
        if (!world.IsAlive(trust) || !world.Has<TrustAccount>(trust))
            return false;

        var account = world.Get<TrustAccount>(trust);
        var beneficiary = account.Beneficiary;
        var beneficiaryAlive = beneficiary != Entity.Null && world.IsAlive(beneficiary) &&
                               !world.Has<Dead>(beneficiary);
        var amount = account.Balance;

        if (amount.IsPositive)
        {
            if (beneficiaryAlive)
            {
                if (!world.Has<Wallet>(beneficiary))
                    world.Add(beneficiary, new Wallet(Money.Zero));
                world.Get<Wallet>(beneficiary).Balance += amount;
                economy.Ledger.Record(
                    Guid.NewGuid(), tick, LedgerKind.TrustRelease,
                    NetworkIdOf(trust), NetworkIdOf(beneficiary), amount,
                    "trust.admin-release");
                economy.Emit(new TrustReleased(
                    NetworkIdOf(trust), NetworkIdOf(beneficiary), amount.Cents, reason, tick));
            }
            else
            {
                economy.Government.Balance += amount;
                economy.Ledger.Record(
                    Guid.NewGuid(), tick, LedgerKind.TrustRelease,
                    NetworkIdOf(trust), EconomyAccountIds.Government, amount,
                    "trust.admin-escheat");
                economy.Emit(new TrustReleased(
                    NetworkIdOf(trust), 0, amount.Cents, reason, tick));
            }

            economy.Life.TrustReleasedThisYear += amount;
        }

        Close(trust);
        return true;
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
