using System.Reflection;
using Arch.Core;
using Genelife.Api.Auth;
using Genelife.Api.Services;
using Genelife.Api.Social;
using Genelife.Components.Gen;
using Genelife.Components.Map;
using Genelife.Components.Social;
using Genelife.Map;
using Genelife.Social;

namespace Genelife.UnitTests.Performance;

[Trait("Category", "Performance")]
public sealed class SocialSoakTests
{
    [Fact]
    public async Task ThirtyTwoPlayerSocialSoakKeepsMessagesEdgesAndTickBudget()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();

        var connections = new ConnectionRegistry();
        var privacy = new SocialPrivacyRegistry();
        var directory = new ChatDirectory(engine, connections, privacy);
        var store = new InMemoryChatStore();
        var chat = new ChatService(
            store,
            engine.SocialGraph,
            directory,
            new ChatOptions
            {
                SayPerWindow = 100,
                GlobalSayPerWindow = 2_000,
                VenuePerWindow = 100,
                GlobalVenuePerWindow = 2_000,
                WhisperPerWindow = 100,
                GlobalWhisperPerWindow = 2_000,
                RateWindowSeconds = 1
            },
            TimeProvider.System);

        var simIds = new List<ulong>();
        for (var i = 0; i < 32; i++)
        {
            var playerId = $"soak-{i}";
            simIds.Add(engine.EnsurePlayerSim(playerId));
            connections.Register($"soak-conn-{i}", playerId);
        }

        Entity venue = default;
        engine.ExecuteWithWorld(world =>
        {
            venue = world.Create(
                new SocialVenue
                {
                    Kind = SocialVenueKind.Cafe,
                    Capacity = 12,
                    OpenFromTick = 0,
                    OpenToTick = 23,
                    Activities = VenueActivityMask.Sit | VenueActivityMask.Talk
                },
                new Placement(
                    new TileCoord(10, 10),
                    new Footprint(2, 2),
                    new TileCoord(9, 10),
                    BuildingKind.Civic),
                new NetworkId(900));
            engine.NetworkIds.AllocateWithValue(venue, new NetworkId(900));

            for (var i = 0; i < 8; i++)
            {
                if (engine.NetworkIds.TryGetEntity(new NetworkId(simIds[i]), out var sim))
                    world.Get<TilePosition>(sim) = new TilePosition(new TileCoord(9, 10));
            }
            return 0;
        });

        engine.Venues!.RefreshPresence(tick: 8);
        for (var i = 0; i < 8; i++)
        {
            var activity = engine.VenueActivities!.TryPerform(
                $"soak-{i}", venue, VenueActivity.Sit, tick: 8 + i);
            Assert.True(activity.Applied, activity.Code);
        }

        for (var i = 0; i < 16; i++)
        {
            var first = $"soak-{i * 2}";
            var second = $"soak-{i * 2 + 1}";
            var request = engine.SocialGraph.RequestFriend(first, second, tick: i);
            Assert.True(request.Accepted, request.Code);
            Assert.True(engine.SocialGraph.RespondToFriendRequest(
                request.Request!.Id, second, accept: true, tick: i).Accepted);
        }

        for (var round = 0; round < 5; round++)
        {
            for (var i = 0; i < 32; i++)
            {
                var sent = await chat.SendAsync($"soak-{i}", new ChatSendRequest
                {
                    Text = $"soak message {round}-{i}",
                    Channel = ChatChannel.Say
                }, tick: round);
                Assert.True(sent.Accepted, sent.Code);
            }
        }

        var edges = engine.SocialGraph.AdminEdges();
        Assert.Equal(32, edges.Count);
        Assert.Equal(16, edges
            .Where(edge => edge.Kind == SocialEdgeKind.Friend)
            .Select(edge => string.CompareOrdinal(edge.FromPlayerId, edge.ToPlayerId) < 0
                ? $"{edge.FromPlayerId}|{edge.ToPlayerId}"
                : $"{edge.ToPlayerId}|{edge.FromPlayerId}")
            .Distinct()
            .Count());

        var retention = await chat.StatusAsync(DateTimeOffset.UtcNow);
        Assert.Equal(160, retention.RetainedMessages);

        for (var i = 0; i < 200; i++)
            engine.Tick(1f / 20f);

        var yearTicks = engine.Economy!.Clock.TicksPerYear;
        for (var i = 0; i < yearTicks; i++)
            engine.Tick(1f / 20f);

        var metrics = engine.GetMetricsSnapshot();
        Assert.True(metrics.P95TickMilliseconds <= 100,
            $"social soak p95 {metrics.P95TickMilliseconds:F2}ms exceeds 100ms");
        Assert.Equal(160, (await chat.StatusAsync(DateTimeOffset.UtcNow)).RetainedMessages);

        var chatFields = typeof(SimulationEngine)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(field => field.FieldType == typeof(ChatService)
                || field.FieldType == typeof(EmoteService))
            .ToArray();
        Assert.Empty(chatFields);
    }
}
