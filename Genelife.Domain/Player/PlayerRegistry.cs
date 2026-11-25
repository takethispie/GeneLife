using Arch.Core;
using Genelife.Components.Gen;

namespace Genelife.Player;

public enum PlayerContinuationPolicy : byte
{
    Heir = 1,
    NewGenos = 2,
    Observer = 3,
    Respawn = 4
}

/// <summary>
/// Player identity and continuation state. A profile is keyed by player id and
/// survives heir continuation; the controlled genos pointer changes, the player
/// identity does not.
/// </summary>
public sealed class PlayerProfile
{
    public required string PlayerId { get; init; }
    public Entity ControlledGenos { get; set; }
    public PlayerContinuationPolicy ContinuationPolicy { get; set; } = PlayerContinuationPolicy.Respawn;
    public bool AwaitingContinuation { get; set; }
    public long CreatedTick { get; set; }
    public long LastActionTick { get; set; }
    public long LastSequence { get; set; }
    public ulong LastGenosNetworkId { get; set; }
    public bool Observer { get; set; }

    /// <summary>
    /// Snapshot of the last controlled genos captured at death so a child can be
    /// created even after the dead entity has been destroyed.
    /// </summary>
    public string? LastGenosName { get; set; }
    public int LastGenosAge { get; set; }
    public Traits? LastGenosTraits { get; set; }
    public ulong LastGenosHouseholdNetworkId { get; set; }
    public long LastGenosDeathTick { get; set; }

    /// <summary>
    /// Child created when a Respawn-policy player dies, before the player has
    /// chosen to control it. Keeping it alive preserves the parent edge after
    /// the dead entity is destroyed.
    /// </summary>
    public ulong PendingRespawnGenosNetworkId { get; set; }
}

/// <summary>
/// In-memory player identity index. It is rebuilt from <c>PlayerControlled</c>
/// components on restore; only the profile preferences and life bindings are
/// persisted.
/// </summary>
public sealed class PlayerRegistry
{
    private readonly Dictionary<string, PlayerProfile> byPlayerId = new(StringComparer.Ordinal);
    private readonly Dictionary<Entity, PlayerProfile> byGenos = new();

    public IReadOnlyDictionary<string, PlayerProfile> Players => byPlayerId;

    public long DeathsThisYear { get; private set; }
    public long ContinuationsThisYear { get; private set; }
    public long CommandsAppliedThisYear { get; private set; }

    public void RecordDeath() => DeathsThisYear++;
    public void RecordContinuation() => ContinuationsThisYear++;
    public void RecordCommand() => CommandsAppliedThisYear++;

    public (long Deaths, long Continuations, long Commands) ResetYearCounters()
    {
        var snapshot = (DeathsThisYear, ContinuationsThisYear, CommandsAppliedThisYear);
        DeathsThisYear = 0;
        ContinuationsThisYear = 0;
        CommandsAppliedThisYear = 0;
        return snapshot;
    }

    public PlayerProfile GetOrCreate(string playerId, Entity genos, long tick)
    {
        if (!byPlayerId.TryGetValue(playerId, out var profile))
        {
            profile = new PlayerProfile
            {
                PlayerId = playerId,
                CreatedTick = tick
            };
            byPlayerId[playerId] = profile;
        }

        if (genos != Entity.Null)
            AttachGenos(profile, genos);

        return profile;
    }

    public bool TryGet(string playerId, out PlayerProfile profile) =>
        byPlayerId.TryGetValue(playerId, out profile!);

    public bool TryGetByGenos(Entity genos, out PlayerProfile profile) =>
        byGenos.TryGetValue(genos, out profile!);

    public bool TryGetByLastGenosNetworkId(ulong networkId, out PlayerProfile profile)
    {
        if (networkId != 0)
        {
            foreach (var candidate in byPlayerId.Values)
            {
                if (candidate.LastGenosNetworkId != networkId)
                    continue;
                profile = candidate;
                return true;
            }
        }

        profile = null!;
        return false;
    }

    public void AttachGenos(PlayerProfile profile, Entity genos)
    {
        if (profile.ControlledGenos != Entity.Null && profile.ControlledGenos != genos)
            byGenos.Remove(profile.ControlledGenos);

        profile.ControlledGenos = genos;
        profile.AwaitingContinuation = false;
        if (genos != Entity.Null)
            byGenos[genos] = profile;
    }

    public void DetachGenos(PlayerProfile profile)
    {
        if (profile.ControlledGenos != Entity.Null)
            byGenos.Remove(profile.ControlledGenos);
        profile.ControlledGenos = Entity.Null;
        profile.AwaitingContinuation = true;
    }

    public bool IsControlledByAnotherPlayer(Entity genos, string playerId) =>
        byGenos.TryGetValue(genos, out var profile) &&
        !string.Equals(profile.PlayerId, playerId, StringComparison.Ordinal);

    public IEnumerable<PlayerProfile> All() => byPlayerId.Values;

    public void Clear()
    {
        byPlayerId.Clear();
        byGenos.Clear();
    }
}
