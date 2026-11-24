namespace Genelife.Api.DTOs;

/// <summary>
/// Real-time player lifecycle notice pushed to a player's connections. Died
/// notices carry the deceased sim so the client can show a respawn prompt;
/// respawned notices carry the new controlled sim.
/// </summary>
public sealed record PlayerLifecycleNotice(
    string PlayerId,
    string Kind,
    ulong SimNetworkId,
    ulong PreviousSimNetworkId,
    string? Name,
    int Age,
    string? Cause,
    long Tick,
    bool CanRespawn);
