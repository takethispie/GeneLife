using Arch.Core;
using Genelife.Api.Auth;
using Genelife.Api.Hubs;
using Genelife.Api.Services;
using Genelife.Components.Gen;
using Genelife.Messages.Events;
using Microsoft.AspNetCore.SignalR;

namespace Genelife.Api.Handlers.Push;

/// <summary>
/// Keeps a connected player's company room in step with their employment. Joining the
/// employer's group once on connect is not enough: a player hired or fired mid-session
/// would otherwise keep listening to the wrong company, or to none at all.
/// </summary>
/// <remarks>
/// A hire only adds the new company's group. Switching employers therefore leaves the
/// player subscribed to the previous company as well, because the previous employer is no
/// longer readable from the world at that point. That is a stale subscription, not a lost
/// one: the player still receives their current employer's events.
/// </remarks>
public class EmploymentGroupPushHandler
{
    private readonly IHubContext<WorldHub> hubContext;
    private readonly IConnectionRegistry registry;
    private readonly SimulationEngine engine;

    public EmploymentGroupPushHandler(
        IHubContext<WorldHub> hubContext,
        IConnectionRegistry registry,
        SimulationEngine engine)
    {
        this.hubContext = hubContext;
        this.registry = registry;
        this.engine = engine;
    }

    public async Task Handle(EmployeeHired @event)
    {
        foreach (var connection in ConnectionsOf(@event.GenosId))
            await hubContext.Groups.AddToGroupAsync(connection, $"company:{@event.CompanyId}");
    }

    public async Task Handle(EmployeeFired @event)
    {
        foreach (var connection in ConnectionsOf(@event.GenosId))
            await hubContext.Groups.RemoveFromGroupAsync(connection, $"company:{@event.CompanyId}");
    }

    private IReadOnlyCollection<string> ConnectionsOf(ulong genosId)
    {
        var playerId = PlayerOf(genosId);
        return playerId is null ? Array.Empty<string>() : registry.GetConnections(playerId);
    }

    /// <summary>
    /// The player driving this genos, or null when it is an ordinary NPC. Resolved with a
    /// single world pass rather than by nesting engine lookups, which take the same lock.
    /// </summary>
    private string? PlayerOf(ulong genosId) =>
        engine.ExecuteWithWorld(world =>
        {
            var query = new QueryDescription().WithAll<NetworkId, PlayerControlled>();
            string? playerId = null;
            world.Query(in query, (ref NetworkId id, ref PlayerControlled controlled) =>
            {
                if (id.Value == genosId)
                    playerId = controlled.PlayerId;
            });
            return playerId;
        });
}