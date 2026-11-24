using System.Reflection;
using Arch.Core;
using Genelife.Api.Services;
using Genelife.Api.Social;
using Genelife.Components.Gen;
using Genelife.Components.Map;
using Genelife.Components.Social;
using Genelife.Map;
using Genelife.Messages.Events;
using Genelife.Social;

namespace Genelife.UnitTests.Social;

public sealed class ModerationTests : IDisposable
{
    private readonly SimulationEngine engine;
    private readonly SocialGraphService social = new();
    private readonly InMemoryChatStore store = new();
    private readonly FakeChatDirectory directory = new();
    private readonly ChatService chat;
    private readonly ModerationService moderation;
    private readonly Entity venue;

    public ModerationTests()
    {
        engine = new SimulationEngine();
        engine.Initialize();
        directory.Add("alice", "Alice", 10, 10);
        directory.Add("bob", "Bob", 11, 10);
        chat = new ChatService(store, social, directory, new ChatOptions(), TimeProvider.System);
        moderation = new ModerationService(engine, chat, social);

        Entity created = default;
        engine.ExecuteWithWorld(world =>
        {
            created = world.Create(
                new SocialVenue
                {
                    Kind = SocialVenueKind.Cafe,
                    Capacity = 4,
                    OpenFromTick = 0,
                    OpenToTick = 23,
                    Activities = VenueActivityMask.Talk
                },
                new Placement(
                    new TileCoord(20, 20),
                    new Footprint(2, 2),
                    new TileCoord(19, 20),
                    BuildingKind.Civic),
                new NetworkId(900));
            engine.NetworkIds.AllocateWithValue(created, new NetworkId(900));
            return 0;
        });
        venue = created;
    }

    [Fact]
    public async Task MuteAndSuspendChatAreReverseableAndBlockChat()
    {
        var mute = await moderation.ApplyAsync(
            new ModerationRequest(ModerationActionKind.Mute, "alice", "flooding", DurationTicks: 20),
            "moderator",
            tick: 10);
        Assert.True(mute.Applied, mute.Code);
        Assert.True(social.IsChatBlocked("alice", 10));

        var blocked = await chat.SendAsync("alice", new ChatSendRequest
        {
            Text = "hello",
            Channel = ChatChannel.Say
        }, tick: 10);
        Assert.False(blocked.Accepted);
        Assert.Equal("chat_suspended", blocked.Code);

        var reversed = moderation.Reverse(mute.Action!.Id, "moderator", "appeal upheld", tick: 11);
        Assert.True(reversed.Applied, reversed.Code);
        Assert.False(social.IsChatBlocked("alice", 11));
        Assert.Contains(moderation.Actions(), action => action.Kind == ModerationActionKind.Unmute);

        var suspend = await moderation.ApplyAsync(
            new ModerationRequest(ModerationActionKind.SuspendChat, "bob", "abuse", DurationTicks: 20),
            "moderator",
            tick: 12);
        Assert.True(suspend.Applied, suspend.Code);
        Assert.True(social.IsChatBlocked("bob", 12));
        Assert.True(social.AreInvitationsBlocked("bob", 12));
        Assert.False(social.IsActivityBlocked("bob", 12));
        Assert.Equal("suspended", social.RequestFriend("bob", "alice", tick: 12).Code);

        moderation.Reverse(suspend.Action!.Id, "moderator", "resolved", tick: 13);
        Assert.False(social.AreInvitationsBlocked("bob", 13));
    }

    [Fact]
    public async Task BlockAndRemoveMessageActionsAreAuditedAndReverseable()
    {
        var message = await chat.SendAsync("alice", new ChatSendRequest
        {
            Text = "reported message",
            Channel = ChatChannel.Say
        }, tick: 1);
        Assert.True(message.Accepted, message.Code);

        var report = social.Report(
            "bob",
            "alice",
            ReportReason.Harassment,
            message.Message!.Id,
            message.Message.Text,
            tick: 2);

        var block = await moderation.ApplyAsync(
            new ModerationRequest(
                ModerationActionKind.Block,
                "alice",
                "harassment",
                ReportId: report.Report!.Id),
            "moderator",
            tick: 3);
        Assert.True(block.Applied, block.Code);
        Assert.True(social.IsBlocked("alice", "bob"));
        Assert.Equal(ReportStatus.InReview, social.Reports().Single().Status);

        var unblock = moderation.Reverse(block.Action!.Id, "moderator", "appeal", tick: 4);
        Assert.True(unblock.Applied, unblock.Code);
        Assert.False(social.IsBlocked("alice", "bob"));

        var remove = await moderation.ApplyAsync(
            new ModerationRequest(
                ModerationActionKind.RemoveMessage,
                "alice",
                "remove evidence",
                ReportId: report.Report.Id,
                MessageId: message.Message.Id),
            "moderator",
            tick: 5);
        Assert.True(remove.Applied, remove.Code);
        Assert.Null(await store.GetAsync(message.Message.Id));
        Assert.Equal("reported message", remove.Action!.Evidence);
        Assert.Equal("reported message", social.Reports().Single().Evidence);
        Assert.Equal("reported message", moderation.Actions().Single(action => action.Id == remove.Action.Id).Evidence);
        Assert.False(moderation.Reverse(remove.Action.Id, "moderator", "undo", tick: 6).Applied);
    }

    [Fact]
    public async Task CloseVenueAndReopenIsAudited()
    {
        var close = await moderation.ApplyAsync(
            new ModerationRequest(
                ModerationActionKind.CloseVenue,
                TargetPlayerId: string.Empty,
                Reason: "unsafe",
                VenueNetworkId: 900),
            "moderator",
            tick: 10);
        Assert.True(close.Applied, close.Code);
        Assert.False(engine.Venues!.TryEnter("alice", venue, tick: 10, out var code, out _));
        Assert.Equal("venue_closed", code);

        var reopen = moderation.Reverse(close.Action!.Id, "moderator", "safe again", tick: 11);
        Assert.True(reopen.Applied, reopen.Code);
        Assert.True(engine.Venues.TryEnter("alice", venue, tick: 11, out _, out _));
    }

    [Fact]
    public void DuplicateReportsCreateOneQueueItemAndRepeatOffenders()
    {
        var first = social.Report("bob", "alice", ReportReason.Spam, null, null, tick: 1);
        var duplicate = social.Report("bob", "alice", ReportReason.Spam, null, null, tick: 2);
        Assert.Equal(first.Report!.Id, duplicate.Report!.Id);

        var queue = moderation.Queue(tick: 3);
        Assert.Single(queue.Items);
        Assert.Equal(0, queue.RepeatOffenders.Count);

        social.Report("carol", "alice", ReportReason.Harassment, null, null, tick: 4);
        social.Report("dave", "alice", ReportReason.Hate, null, null, tick: 5);
        var offender = Assert.Single(moderation.RepeatOffenders(tick: 6));
        Assert.Equal("alice", offender.PlayerId);
        Assert.Equal(3, offender.ReportCount);
        Assert.Equal(3, offender.DistinctReporters);
    }

    [Fact]
    public void WarningsAreAuditedAndRetentionPurgesReportsAfterTheWindow()
    {
        var report = social.Report("bob", "alice", ReportReason.Other, null, null, tick: 1);
        var warn = moderation.ApplyAsync(
            new ModerationRequest(ModerationActionKind.Warn, "alice", "warning", report.Report!.Id),
            "moderator",
            tick: 2).GetAwaiter().GetResult();
        Assert.True(warn.Applied, warn.Code);
        Assert.Single(moderation.Actions());

        var removed = moderation.PurgeExpired(tick: 2500);
        Assert.True(removed >= 1);
        Assert.Empty(social.Reports());
        Assert.Empty(moderation.Actions());
    }

    [Fact]
    public void BlocksSurviveSnapshotRoundTripDespiteReconnect()
    {
        social.Block("alice", "bob", tick: 1, SocialEdgeSource.Player);
        var restored = new SocialGraphService();
        restored.Restore(social.Export());
        Assert.True(restored.IsBlocked("alice", "bob"));
        Assert.True(restored.IsBlocked("bob", "alice"));
    }

    [Fact]
    public void ModerationDoesNotEnterTheSimulationTickLoop()
    {
        var fields = typeof(SimulationEngine)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(field => field.FieldType == typeof(ModerationService))
            .ToArray();
        Assert.Empty(fields);
    }

    public void Dispose() => engine.Dispose();

    private sealed class FakeChatDirectory : IChatDirectory
    {
        private readonly Dictionary<string, ChatPlayerContext> players = new(StringComparer.Ordinal);

        public void Add(string playerId, string name, int x, int y) =>
            players[playerId] = new ChatPlayerContext(playerId, name, x, y, null, false);

        public ChatPlayerContext? Describe(string playerId) =>
            players.TryGetValue(playerId, out var player) ? player : null;

        public IReadOnlyList<ChatPlayerContext> Online() => players.Values.ToArray();
    }
}
