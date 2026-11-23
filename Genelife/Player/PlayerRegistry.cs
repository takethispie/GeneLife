using Arch.Core;

namespace Genelife.Player;

public enum PlayerContinuationPolicy : byte
{
    Heir = 1,
    NewSim = 2,
    Observer = 3
}

/// <summary>
/// Player identity and continuation state. A profile is keyed by player id and
/// survives heir continuation; the controlled sim pointer changes, the player
/// identity does not.
/// </summary>
public sealed class PlayerProfile
{
    public required string PlayerId { get; init; }
    public Entity ControlledSim { get; set; }
    public PlayerContinuationPolicy ContinuationPolicy { get; set; } = PlayerContinuationPolicy.Heir;
    public bool AwaitingContinuation { get; set; }
    public long CreatedTick { get; set; }
    public long LastActionTick { get; set; }
    public long LastSequence { get; set; }
    public ulong LastSimNetworkId { get; set; }
    public bool Observer { get; set; }
}

/// <summary>
/// In-memory player identity index. It is rebuilt from <c>PlayerControlled</c>
/// components on restore; only the profile preferences and life bindings are
/// persisted.
/// </summary>
public sealed class PlayerRegistry
{
    private readonly Dictionary<string, PlayerProfile> byPlayerId = new(StringComparer.Ordinal);
    private readonly Dictionary<Entity, PlayerProfile> bySim = new();

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

    public PlayerProfile GetOrCreate(string playerId, Entity sim, long tick)
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

        if (sim != Entity.Null)
            AttachSim(profile, sim);

        return profile;
    }

    public bool TryGet(string playerId, out PlayerProfile profile) =>
        byPlayerId.TryGetValue(playerId, out profile!);

    public bool TryGetBySim(Entity sim, out PlayerProfile profile) =>
        bySim.TryGetValue(sim, out profile!);

    public void AttachSim(PlayerProfile profile, Entity sim)
    {
        if (profile.ControlledSim != Entity.Null && profile.ControlledSim != sim)
            bySim.Remove(profile.ControlledSim);

        profile.ControlledSim = sim;
        profile.AwaitingContinuation = false;
        if (sim != Entity.Null)
            bySim[sim] = profile;
    }

    public void DetachSim(PlayerProfile profile)
    {
        if (profile.ControlledSim != Entity.Null)
            bySim.Remove(profile.ControlledSim);
        profile.ControlledSim = Entity.Null;
        profile.AwaitingContinuation = true;
    }

    public bool IsControlledByAnotherPlayer(Entity sim, string playerId) =>
        bySim.TryGetValue(sim, out var profile) &&
        !string.Equals(profile.PlayerId, playerId, StringComparison.Ordinal);

    public IEnumerable<PlayerProfile> All() => byPlayerId.Values;

    public void Clear()
    {
        byPlayerId.Clear();
        bySim.Clear();
    }
}
