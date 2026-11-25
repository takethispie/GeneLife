using System.Text.Json;
using Genelife.Api.Commands;
using Genelife.Api.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Genelife.UnitTests.Players;

public class PlayerCommandTests
{
    private static PlayerActionIntent ChooseContinuation(
        Guid commandId,
        string policy,
        long sequence,
        ulong genosNetworkId = 0) =>
        new()
        {
            PlayerId = "p1",
            Sequence = sequence,
            GenosNetworkId = genosNetworkId,
            Kind = PlayerActionKind.ChooseContinuation,
            Payload = JsonSerializer.SerializeToElement(new { policy }),
            CommandId = commandId
        };

    [Fact]
    public void StaleSequence_IsSupersededWithoutApplying()
    {
        var queue = new CommandQueue();
        using var engine = new SimulationEngine(commandQueue: queue);
        engine.Initialize();
        var genosId = engine.EnsurePlayerGenos("p1");
        var newerId = Guid.NewGuid();
        var staleId = Guid.NewGuid();

        Assert.True(queue.TryEnqueue(ChooseContinuation(newerId, "Observer", 2, genosId), out _));
        Assert.True(queue.TryEnqueue(ChooseContinuation(staleId, "NewGenos", 1, genosId), out _));
        engine.Tick(1f / 20f);

        var newer = engine.GetPlayerCommandResult(newerId);
        var stale = engine.GetPlayerCommandResult(staleId);
        Assert.NotNull(newer);
        Assert.NotNull(stale);
        Assert.Equal(PlayerCommandStatus.Applied, newer!.Status);
        Assert.Equal(PlayerCommandStatus.Superseded, stale!.Status);
        Assert.Equal("stale_sequence", stale.Code);
        Assert.True(engine.Players.TryGet("p1", out var profile));
        Assert.Equal(Genelife.Player.PlayerContinuationPolicy.Observer, profile.ContinuationPolicy);
    }

    [Fact]
    public void DuplicateCommandId_AppliesOnlyOnce()
    {
        var queue = new CommandQueue();
        using var engine = new SimulationEngine(commandQueue: queue);
        engine.Initialize();
        var genosId = engine.EnsurePlayerGenos("p1");
        var id = Guid.NewGuid();

        Assert.True(queue.TryEnqueue(ChooseContinuation(id, "Observer", 1, genosId), out _));
        Assert.True(queue.TryEnqueue(ChooseContinuation(id, "NewGenos", 2, genosId), out _));
        engine.Tick(1f / 20f);

        Assert.Single(engine.GetPlayerCommandAudit("p1"));
        Assert.Equal(PlayerCommandStatus.Applied, engine.GetPlayerCommandResult(id)!.Status);
        Assert.True(engine.Players.TryGet("p1", out var profile));
        Assert.Equal(Genelife.Player.PlayerContinuationPolicy.Observer, profile.ContinuationPolicy);
    }

    [Fact]
    public void Disconnect_DropsQueuedCommandWithoutApplying()
    {
        var queue = new CommandQueue();
        using var engine = new SimulationEngine(commandQueue: queue);
        engine.Initialize();
        var genosId = engine.EnsurePlayerGenos("p1");
        var id = Guid.NewGuid();

        Assert.True(queue.TryEnqueue(ChooseContinuation(id, "Observer", 1, genosId), out _));
        engine.MarkPlayerDisconnected("p1");
        engine.Tick(1f / 20f);

        var result = engine.GetPlayerCommandResult(id);
        Assert.NotNull(result);
        Assert.Equal(PlayerCommandStatus.Rejected, result!.Status);
        Assert.Equal("disconnected", result.Code);
        Assert.True(engine.Players.TryGet("p1", out var profile));
        Assert.Equal(Genelife.Player.PlayerContinuationPolicy.Respawn, profile.ContinuationPolicy);
    }

    [Fact]
    public void Admission_RejectsOverPerPlayerBudget()
    {
        var queue = new CommandQueue();
        using var engine = new SimulationEngine(commandQueue: queue);
        engine.Initialize();
        var genosId = engine.EnsurePlayerGenos("p1");
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new PlayerCommandService(engine, new RequestRateLimiter(cache), queue);
        var payload = JsonSerializer.SerializeToElement(new { policy = "Heir" });

        for (var i = 0; i < 8; i++)
        {
            var accepted = service.Admit("p1", PlayerActionKind.ChooseContinuation, genosId, payload, i + 1, Guid.NewGuid());
            Assert.True(accepted.Accepted, accepted.Code);
        }

        var rejected = service.Admit(
            "p1", PlayerActionKind.ChooseContinuation, genosId, payload, 9, Guid.NewGuid());
        Assert.False(rejected.Accepted);
        Assert.Equal("rate_limited", rejected.Code);
    }

    [Fact]
    public void Admission_RejectsMalformedPayloadWithoutThrowing()
    {
        var queue = new CommandQueue();
        using var engine = new SimulationEngine(commandQueue: queue);
        engine.Initialize();
        var genosId = engine.EnsurePlayerGenos("p1");
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new PlayerCommandService(engine, new RequestRateLimiter(cache), queue);

        var result = service.Admit(
            "p1",
            PlayerActionKind.ChooseContinuation,
            genosId,
            JsonSerializer.SerializeToElement(new[] { 1, 2, 3 }),
            1,
            Guid.NewGuid());

        Assert.False(result.Accepted);
        Assert.Equal("malformed_payload", result.Code);
    }

    [Fact]
    public void TryReservePlayerCommand_EnforcesBudgetAndRelease()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        for (var i = 0; i < 8; i++)
            Assert.True(engine.TryReservePlayerCommand("p1"));

        Assert.False(engine.TryReservePlayerCommand("p1"));
        for (var i = 0; i < 8; i++)
            engine.ReleasePlayerCommand("p1");
        Assert.True(engine.TryReservePlayerCommand("p1"));
        engine.ReleasePlayerCommand("p1");
    }
}
