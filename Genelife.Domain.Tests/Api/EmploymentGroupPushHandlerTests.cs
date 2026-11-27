using Arch.Core;
using Genelife.Api.Auth;
using Genelife.Api.Handlers.Push;
using Genelife.Api.Hubs;
using Genelife.Api.Services;
using Genelife.Components.Gen;
using Genelife.Messages.Events;
using Microsoft.AspNetCore.SignalR;

namespace Genelife.UnitTests.Api;

/// <summary>
/// Coverage for <see cref="EmploymentGroupPushHandler"/>, the handler that keeps a
/// connected player's company room in step with their employment. Group membership is
/// not observable from a SignalR client, so the handler is driven against a stub
/// <see cref="IGroupManager"/> that records the Add/Remove calls.
/// </summary>
public class EmploymentGroupPushHandlerTests : IDisposable
{
    private const ulong CompanyId = 42;
    private const ulong GenosId = 7;
    private const string PlayerId = "player-1";

    private readonly List<SimulationEngine> engines = [];

    public void Dispose()
    {
        foreach (var engine in engines)
            engine.Dispose();
    }

    [Fact]
    public async Task Hire_AddsEveryConnectionOfThePlayerToTheCompanyRoom()
    {
        var groups = new StubGroupManager();
        var handler = Handler(groups, connections: ["conn-1", "conn-2"]);

        await handler.Handle(new EmployeeHired(CompanyId, GenosId, "Worker", 0));

        Assert.Equal(2, groups.Calls.Count);
        Assert.All(groups.Calls, call =>
        {
            Assert.True(call.Added, "a hire must ADD the company room");
            Assert.Equal($"company:{CompanyId}", call.Group);
        });
        Assert.Equal(
            new[] { "conn-1", "conn-2" },
            groups.Calls.Select(call => call.Connection).OrderBy(connection => connection).ToArray());
    }

    [Fact]
    public async Task Fire_RemovesEveryConnectionOfThePlayerFromTheCompanyRoom()
    {
        var groups = new StubGroupManager();
        var handler = Handler(groups, connections: ["conn-1"]);

        await handler.Handle(new EmployeeFired(CompanyId, GenosId));

        var call = Assert.Single(groups.Calls);
        Assert.False(call.Added, "a fire must REMOVE the company room");
        Assert.Equal($"company:{CompanyId}", call.Group);
        Assert.Equal("conn-1", call.Connection);
    }

    [Fact]
    public async Task Hire_ForAnNpc_TouchesNoRooms()
    {
        var groups = new StubGroupManager();
        var handler = Handler(groups, connections: ["conn-1"], playerControlled: false);

        await handler.Handle(new EmployeeHired(CompanyId, GenosId, "Worker", 0));

        Assert.Empty(groups.Calls);
    }

    [Fact]
    public async Task Hire_ForAPlayerWithNoConnections_TouchesNoRooms()
    {
        var groups = new StubGroupManager();
        var handler = Handler(groups, connections: []);

        await handler.Handle(new EmployeeHired(CompanyId, GenosId, "Worker", 0));

        Assert.Empty(groups.Calls);
    }

    [Fact]
    public async Task SwitchingEmployers_AddsTheNewRoomAndLeavesTheOldSubscription()
    {
        // Documented behaviour: a hire only ADDs. Switching employer therefore keeps a
        // stale subscription to the previous company (a stale subscription, not a lost
        // one - the player still receives their current employer's events). Guarded
        // here so the behaviour cannot change silently.
        var groups = new StubGroupManager();
        var handler = Handler(groups, connections: ["conn-1"]);

        await handler.Handle(new EmployeeFired(CompanyId, GenosId));
        await handler.Handle(new EmployeeHired(99, GenosId, "Worker", 0));

        Assert.Equal(2, groups.Calls.Count);
        Assert.False(groups.Calls[0].Added);
        Assert.Equal($"company:{CompanyId}", groups.Calls[0].Group);
        Assert.True(groups.Calls[1].Added);
        Assert.Equal("company:99", groups.Calls[1].Group);
    }

    private EmploymentGroupPushHandler Handler(
        StubGroupManager groups,
        IReadOnlyList<string> connections,
        bool playerControlled = true)
    {
        var registry = new StubRegistry();
        foreach (var connection in connections)
            registry.Register(connection, PlayerId);

        var engine = new SimulationEngine();
        engines.Add(engine);
        engine.Initialize();
        engine.ExecuteWithWorld(world =>
        {
            if (playerControlled)
                world.Create(new NetworkId(GenosId), new PlayerControlled(PlayerId));
            else
                world.Create(new NetworkId(GenosId));
            return 0;
        });

        return new EmploymentGroupPushHandler(new StubHubContext(groups), registry, engine);
    }

    private sealed class StubGroupManager : IGroupManager
    {
        public List<Call> Calls { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Calls.Add(new Call(connectionId, groupName, Added: true));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Calls.Add(new Call(connectionId, groupName, Added: false));
            return Task.CompletedTask;
        }

        public readonly record struct Call(string Connection, string Group, bool Added);
    }

    private sealed class StubHubContext(IGroupManager groups) : IHubContext<WorldHub>
    {
        public IGroupManager Groups { get; } = groups;

        public IHubClients Clients => throw new NotSupportedException("the handler only uses Groups");
    }

    private sealed class StubRegistry : IConnectionRegistry
    {
        private readonly Dictionary<string, string> byConnection = [];

        public void Register(string connectionId, string playerId) => byConnection[connectionId] = playerId;

        public bool TryUnregister(string connectionId, out string? playerId) =>
            byConnection.Remove(connectionId, out playerId);

        public bool TryGetPlayerId(string connectionId, out string? playerId) =>
            byConnection.TryGetValue(connectionId, out playerId);

        public IReadOnlyCollection<string> GetConnections(string playerId) =>
            byConnection.Where(pair => pair.Value == playerId).Select(pair => pair.Key).ToList();

        public IReadOnlyCollection<string> GetPlayerIds() =>
            byConnection.Values.Distinct().ToList();

        public IReadOnlyCollection<string> GetConnectionIds() =>
            byConnection.Keys.ToList();
    }
}
