namespace Genelife.Api.DTOs;

/// <summary>
/// Real-time player lifecycle notice pushed to a player's connections. Died
/// notices carry the deceased genos so the client can show a respawn prompt;
/// respawned notices carry the new controlled genos.
/// </summary>
public sealed record PlayerLifecycleNotice(
    string PlayerId,
    string Kind,
    ulong GenosNetworkId,
    ulong PreviousGenosNetworkId,
    string? Name,
    int Age,
    string? Cause,
    long Tick,
    bool CanRespawn);
