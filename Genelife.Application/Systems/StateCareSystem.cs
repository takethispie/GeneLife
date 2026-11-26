using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Application.Systems;
using Genelife.Values;

using Genelife.Family;
namespace Genelife.Application.Systems;

/// <summary>
/// Monthly government transfer to state-care children. It is a government-to-child
/// transfer, never money creation; the child then spends through normal channels.
/// </summary>
public sealed class StateCareSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;

    private readonly int cadenceOffset;

    public StateCareSystem(World world, EconomyContext economy, int cadenceOffset = 0)
    {
        this.world = world;
        this.economy = economy;
        this.cadenceOffset = cadenceOffset;
    }

    public string Name => "StateCare";
    public int Order => 387;
    public Cadence Cadence => new(Math.Max(1, economy.Clock.TicksPerMonth), cadenceOffset);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var children = new List<(Entity Entity, StateCare Care)>();
        world.Query(in new QueryDescription().WithAll<StateCare>(),
            (Entity entity, ref StateCare care) =>
            {
                if (world.IsAlive(entity) && !world.Has<Genelife.Components.Gen.Dead>(entity))
                    children.Add((entity, care));
            });
        children.Sort((a, b) => NetworkIdOf(a.Entity).CompareTo(NetworkIdOf(b.Entity)));

        foreach (var (entity, care) in children)
        {
            var amount = care.MonthlyCost.Min(economy.Government.Balance);
            if (!amount.IsPositive)
                continue;

            economy.Government.Balance -= amount;
            if (!world.Has<Wallet>(entity))
                world.Add(entity, new Wallet(Money.Zero));
            world.Get<Wallet>(entity).Balance += amount;
            economy.Ledger.Record(
                Guid.NewGuid(), tick, LedgerKind.ChildBenefit,
                EconomyAccountIds.Government, NetworkIdOf(entity), amount,
                "state-care");
            economy.Emit(new ChildBenefitPaid(NetworkIdOf(entity), 1, amount.Cents, tick));
        }
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
}
