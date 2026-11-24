using System.Text.Json;
using Arch.Core;
using Genelife.Api.Commands;
using Genelife.Api.Services;
using Genelife.Components.Gen;
using Genelife.Enums;
using Genelife.Player;

namespace Genelife.UnitTests.Players;

public class PlayerAcceptanceTests
{
    [Fact]
    public void RecordedPlayerCommandReplaysFromSnapshotWithTheSameOutcome()
    {
        var queueA = new CommandQueue();
        using var first = new SimulationEngine(commandQueue: queueA);
        first.Initialize();
        var simId = first.EnsurePlayerSim("replay-player");
        var start = first.CaptureState(first.NetworkIds);

        var commandId = Guid.NewGuid();
        var intent = ChooseContinuation(commandId, "replay-player", simId, "Observer", sequence: 1);
        Assert.True(queueA.TryEnqueue(intent, out _));
        first.Tick(1f / 20f);

        Assert.True(first.Players.TryGet("replay-player", out var applied));
        Assert.Equal(PlayerContinuationPolicy.Observer, applied.ContinuationPolicy);
        var expected = first.GetPlayerCommandResult(commandId);
        Assert.NotNull(expected);
        Assert.Equal(PlayerCommandStatus.Applied, expected!.Status);
        first.Shutdown();

        var queueB = new CommandQueue();
        using var second = new SimulationEngine(commandQueue: queueB);
        second.Restore(start, second.NetworkIds);
        Assert.True(queueB.TryEnqueue(intent, out _));
        second.Tick(1f / 20f);

        Assert.True(second.Players.TryGet("replay-player", out var replayed));
        Assert.Equal(PlayerContinuationPolicy.Observer, replayed.ContinuationPolicy);
        var actual = second.GetPlayerCommandResult(commandId);
        Assert.NotNull(actual);
        Assert.Equal(expected.Status, actual!.Status);
        Assert.Equal(expected.Code, actual.Code);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(1, second.GetPlayerCommandAudit("replay-player").Count);

        var recaptured = second.CaptureState(second.NetworkIds);
        Assert.Equal(
            applied.ContinuationPolicy.ToString(),
            recaptured.PlayerProfiles.Single(profile => profile.PlayerId == "replay-player").ContinuationPolicy);
        second.Shutdown();
    }

    [Fact]
    public void ThirtyTwoPlayersSubmitCommandsInOneTickWithoutLoss()
    {
        var queue = new CommandQueue();
        using var engine = new SimulationEngine(commandQueue: queue);
        engine.Initialize();

        var players = new List<(string PlayerId, ulong SimId, Guid CommandId)>();
        for (var i = 0; i < 32; i++)
        {
            var playerId = $"soak-{i:00}";
            var simId = engine.EnsurePlayerSim(playerId);
            Assert.NotEqual(0UL, simId);
            var commandId = Guid.NewGuid();
            players.Add((playerId, simId, commandId));
            Assert.True(queue.TryEnqueue(ChooseContinuation(commandId, playerId, simId, "Heir", 1), out _));
        }

        engine.Tick(1f / 20f);
        for (var tick = 0; tick < 100; tick++)
            engine.Tick(1f / 20f);

        foreach (var (playerId, _, commandId) in players)
        {
            var result = engine.GetPlayerCommandResult(commandId);
            Assert.NotNull(result);
            Assert.Equal(PlayerCommandStatus.Applied, result!.Status);
            Assert.True(engine.Players.TryGet(playerId, out var profile));
            Assert.Equal(PlayerContinuationPolicy.Heir, profile.ContinuationPolicy);
        }

        Assert.Equal(32, engine.Players.Players.Count);
        Assert.Equal(32, engine.BuildPlayerOverview().Active);
        Assert.Equal(32, engine.BuildPlayerLives(100, 0, null, "active").Total);
    }

    [Fact]
    public void RespawnPlayerAsChild_CreatesAControlledChildOfTheDeadSim()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var oldSimId = engine.EnsurePlayerSim("respawn-player");
        Assert.NotEqual(0UL, oldSimId);

        var world = engine.World!;
        Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(oldSimId), out var oldSim));
        world.Add(oldSim, new Dead(engine.CurrentTick, DeathCause.Age));
        engine.Economy!.Life.DeathQueue.Add(oldSim);

        for (var i = 0; i < 100; i++)
            engine.Tick(1f / 20f);

        Assert.True(engine.Players.TryGet("respawn-player", out var profile));
        Assert.True(profile.AwaitingContinuation);
        Assert.Equal(oldSimId, profile.LastSimNetworkId);
        Assert.NotNull(profile.LastSimName);
        Assert.True(engine.TryDequeuePlayerLifecycleNotice(out var died));
        Assert.Equal("died", died.Kind);
        Assert.NotEqual(0UL, profile.PendingRespawnSimNetworkId);

        var result = engine.RespawnPlayerAsChild("respawn-player");

        Assert.True(result.Success);
        Assert.NotEqual(0UL, result.SimNetworkId);
        Assert.NotEqual(oldSimId, result.SimNetworkId);
        Assert.Equal(result.SimNetworkId, engine.GetPlayerSimNetworkId("respawn-player"));
        Assert.False(profile.AwaitingContinuation);
        Assert.NotEqual(Entity.Null, profile.ControlledSim);
        Assert.True(world.Has<PlayerControlled>(profile.ControlledSim));
        Assert.Equal(result.SimNetworkId, world.Get<NetworkId>(profile.ControlledSim).Value);

        Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(result.SimNetworkId), out var child));
        Assert.Equal(engine.Economy.Life.Policy.WorkStartAge, world.Get<Alive>(child).Age);
        Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(oldSimId), out var parent));
        Assert.Contains(child, engine.Relationships.ChildrenOf(parent));
    }

    private static PlayerActionIntent ChooseContinuation(
        Guid commandId,
        string playerId,
        ulong simId,
        string policy,
        long sequence) =>
        new()
        {
            PlayerId = playerId,
            Sequence = sequence,
            SimNetworkId = simId,
            Kind = PlayerActionKind.ChooseContinuation,
            Payload = JsonSerializer.SerializeToElement(new { policy }),
            CommandId = commandId
        };
}
