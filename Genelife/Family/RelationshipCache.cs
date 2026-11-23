using Arch.Core;

namespace Genelife.Family;

public readonly record struct ScoredCandidate(Entity Sim, int Score);

public readonly record struct RejectedCandidate(Entity Sim, string Reason);

/// <summary>
/// Monthly/quarterly relationship caches. Never persisted; rebuilt after
/// restore. Cooldowns are a pacing mechanism and reset on restore by design.
/// </summary>
public sealed class RelationshipCache
{
    public Dictionary<Entity, List<Entity>> Candidates { get; } = new();
    public Dictionary<Entity, List<RejectedCandidate>> Rejections { get; } = new();
    public Dictionary<Entity, List<ScoredCandidate>> Scored { get; } = new();
    public Dictionary<(Entity First, Entity Second), long> CooldownUntil { get; } = new();

    public void Clear()
    {
        Candidates.Clear();
        Rejections.Clear();
        Scored.Clear();
        CooldownUntil.Clear();
    }

    public void Invalidate(Entity sim)
    {
        Candidates.Remove(sim);
        Rejections.Remove(sim);
        Scored.Remove(sim);
    }

    public bool InCooldown(Entity first, Entity second, long tick) =>
        CooldownUntil.TryGetValue(Canonical(first, second), out var until) && until > tick;

    public void SetCooldown(Entity first, Entity second, long tick, long untilTick)
    {
        if (untilTick <= tick)
            return;
        CooldownUntil[Canonical(first, second)] = untilTick;
    }

    public static (Entity First, Entity Second) Canonical(Entity first, Entity second) =>
        first.Id <= second.Id ? (first, second) : (second, first);
}
