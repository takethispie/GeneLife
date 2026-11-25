using Arch.Core;
using Genelife.Components.Family;

namespace Genelife.Family;

/// <summary>
/// Derived pregnancy lookup. Components are authoritative; rebuild on restore.
/// </summary>
public sealed class PregnancyIndex
{
    public Dictionary<Entity, Entity> ActiveByCarryingParent { get; } = new();
    public List<Entity> Active { get; } = new();
    public Dictionary<Entity, List<Entity>> HistoryByParent { get; } = new();

    public void Clear()
    {
        ActiveByCarryingParent.Clear();
        Active.Clear();
        HistoryByParent.Clear();
    }

    public void Rebuild(World world)
    {
        AttachWorld(world);
        Clear();
        var pregnancies = new List<(Entity Entity, Pregnancy Pregnancy)>();
        world.Query(in new QueryDescription().WithAll<Pregnancy>(),
            (Entity entity, ref Pregnancy pregnancy) => pregnancies.Add((entity, pregnancy)));

        pregnancies.Sort((a, b) =>
        {
            var aId = world.Has<Components.Gen.NetworkId>(a.Entity)
                ? world.Get<Components.Gen.NetworkId>(a.Entity).Value
                : (ulong)a.Entity.Id;
            var bId = world.Has<Components.Gen.NetworkId>(b.Entity)
                ? world.Get<Components.Gen.NetworkId>(b.Entity).Value
                : (ulong)b.Entity.Id;
            return aId.CompareTo(bId);
        });

        foreach (var (entity, pregnancy) in pregnancies)
        {
            AddHistory(pregnancy.CarryingParent, entity);
            if (IsActive(pregnancy.Status))
            {
                Active.Add(entity);
                ActiveByCarryingParent[pregnancy.CarryingParent] = entity;
            }
        }
    }

    public void OnStarted(Entity pregnancy, in Pregnancy data)
    {
        AddHistory(data.CarryingParent, pregnancy);
        if (!IsActive(data.Status))
            return;

        Active.Add(pregnancy);
        ActiveByCarryingParent[data.CarryingParent] = pregnancy;
    }

    public void OnResolved(Entity pregnancy, in Pregnancy data)
    {
        Active.Remove(pregnancy);
        if (ActiveByCarryingParent.TryGetValue(data.CarryingParent, out var active) && active == pregnancy)
            ActiveByCarryingParent.Remove(data.CarryingParent);
        AddHistory(data.CarryingParent, pregnancy);
    }

    public void OnParentChanged(Entity pregnancy, in Pregnancy data, Entity oldParent)
    {
        if (ActiveByCarryingParent.TryGetValue(oldParent, out var active) && active == pregnancy)
            ActiveByCarryingParent.Remove(oldParent);
        AddHistory(data.CarryingParent, pregnancy);
        if (IsActive(data.Status))
            ActiveByCarryingParent[data.CarryingParent] = pregnancy;
    }

    public bool TryGetActive(Entity carryingParent, out Entity pregnancy) =>
        ActiveByCarryingParent.TryGetValue(carryingParent, out pregnancy);

    public IReadOnlyList<Entity> HistoryOf(Entity parent) =>
        HistoryByParent.TryGetValue(parent, out var history) ? history : Array.Empty<Entity>();

    public bool IsInLossCooldown(
        Entity carryingParent,
        long tick,
        ReproductionOptions options,
        SimulationClock clock)
    {
        if (options.LossCooldownMonths <= 0m)
            return false;

        var months = (long)Math.Ceiling(options.LossCooldownMonths);
        var cooldown = months * clock.TicksPerMonth;
        foreach (var pregnancy in HistoryOf(carryingParent))
        {
            if (!TryRead(pregnancy, out var data))
                continue;
            if (data.Status == PregnancyStatus.Lost && data.ResolvedTick > 0 &&
                tick < data.ResolvedTick + cooldown)
                return true;
        }

        return false;
    }

    private bool TryRead(Entity entity, out Pregnancy pregnancy)
    {
        pregnancy = default;
        if (world is null || !world.IsAlive(entity) || !world.Has<Pregnancy>(entity))
            return false;
        pregnancy = world.Get<Pregnancy>(entity);
        return true;
    }

    private World? world;

    public void AttachWorld(World world) => this.world = world;

    private void AddHistory(Entity parent, Entity pregnancy)
    {
        if (!HistoryByParent.TryGetValue(parent, out var history))
        {
            history = new List<Entity>();
            HistoryByParent[parent] = history;
        }

        if (!history.Contains(pregnancy))
            history.Add(pregnancy);
    }

    private static bool IsActive(PregnancyStatus status) =>
        status is PregnancyStatus.Active or PregnancyStatus.ReadyToBirth;
}
