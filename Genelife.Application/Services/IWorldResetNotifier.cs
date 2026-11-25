using Genelife.Api.DTOs;

namespace Genelife.Api.Services;

/// <summary>
/// Lets the simulation lifecycle notify connected clients that the world was
/// replaced without taking a hard dependency on SignalR from the application
/// layer.
/// </summary>
public interface IWorldResetNotifier
{
    void NotifyWorldReset(WorldResetMessage message);
}
