using System.Reflection;
using Genelife.Api.Services;
using Genelife.Api.Social;
using Genelife.Social;

namespace Genelife.UnitTests.Social;

public class ChatTests
{
    [Fact]
    public async Task SayReachesNearbyAndSameVenueOnly()
    {
        var fixture = new ChatFixture(sayRadius: 5);
        fixture.Directory.Add("alice", 10, 10, venue: 100);
        fixture.Directory.Add("bob", 12, 10, venue: 100);
        fixture.Directory.Add("dave", 50, 50, venue: 100);
        fixture.Directory.Add("erin", 11, 11, venue: null);
        fixture.Directory.Add("carol", 40, 40, venue: null);

        var say = await fixture.Chat.SendAsync("alice", new ChatSendRequest
        {
            Text = "hello",
            Channel = ChatChannel.Say
        }, tick: 5);

        Assert.True(say.Accepted);
        Assert.Contains("bob", say.Recipients);
        Assert.Contains("dave", say.Recipients);
        Assert.Contains("erin", say.Recipients);
        Assert.DoesNotContain("carol", say.Recipients);

        var venue = await fixture.Chat.SendAsync("alice", new ChatSendRequest
        {
            Text = "venue hello",
            Channel = ChatChannel.Venue
        }, tick: 6);

        Assert.True(venue.Accepted);
        Assert.Contains("bob", venue.Recipients);
        Assert.Contains("dave", venue.Recipients);
        Assert.DoesNotContain("erin", venue.Recipients);
        Assert.DoesNotContain("carol", venue.Recipients);
    }

    [Fact]
    public async Task VenueChannelRequiresAVenue()
    {
        var fixture = new ChatFixture();
        fixture.Directory.Add("alice", 10, 10, venue: null);

        var result = await fixture.Chat.SendAsync("alice", new ChatSendRequest
        {
            Text = "anyone here?",
            Channel = ChatChannel.Venue
        }, tick: 1);

        Assert.False(result.Accepted);
        Assert.Equal("not_in_venue", result.Code);
    }

    [Fact]
    public async Task WhisperRequiresFriendshipAndBlockingStopsDelivery()
    {
        var fixture = new ChatFixture();
        fixture.Directory.Add("alice", 10, 10, venue: null);
        fixture.Directory.Add("bob", 30, 30, venue: null);

        var rejected = await fixture.Chat.SendAsync("alice", new ChatSendRequest
        {
            Text = "secret",
            Channel = ChatChannel.Whisper,
            TargetPlayerId = "bob"
        }, tick: 1);

        Assert.False(rejected.Accepted);
        Assert.Equal("not_friends", rejected.Code);

        Assert.True(fixture.Social.AddFriend("alice", "bob", tick: 0).Accepted);
        var accepted = await fixture.Chat.SendAsync("alice", new ChatSendRequest
        {
            Text = "secret",
            Channel = ChatChannel.Whisper,
            TargetPlayerId = "bob"
        }, tick: 2);

        Assert.True(accepted.Accepted);
        Assert.Equal("bob", accepted.Message?.TargetPlayerId);
        Assert.Equal(new[] { "bob" }, accepted.Recipients);

        Assert.True(fixture.Social.Block("bob", "alice", tick: 1).Accepted);
        var blocked = await fixture.Chat.SendAsync("alice", new ChatSendRequest
        {
            Text = "still there?",
            Channel = ChatChannel.Whisper,
            TargetPlayerId = "bob"
        }, tick: 3);

        Assert.False(blocked.Accepted);
        Assert.Equal("blocked", blocked.Code);
    }

    [Fact]
    public async Task MuteHidesMessagesWithoutDeletingThem()
    {
        var fixture = new ChatFixture(sayRadius: 20);
        fixture.Directory.Add("alice", 10, 10, venue: null);
        fixture.Directory.Add("bob", 11, 10, venue: null);
        fixture.Directory.Add("carol", 12, 10, venue: null);

        Assert.True(fixture.Social.Mute("bob", "alice", tick: 1).Accepted);
        var sent = await fixture.Chat.SendAsync("alice", new ChatSendRequest
        {
            Text = "muted hello",
            Channel = ChatChannel.Say
        }, tick: 4);

        Assert.True(sent.Accepted);
        Assert.DoesNotContain("bob", sent.Recipients);
        Assert.Contains("carol", sent.Recipients);

        var bobSees = await fixture.Chat.ReadAsync("bob", ChatChannel.Say, null, take: 10);
        Assert.DoesNotContain(bobSees, message => message.Id == sent.Message?.Id);

        var carolSees = await fixture.Chat.ReadAsync("carol", ChatChannel.Say, null, take: 10);
        Assert.Contains(carolSees, message => message.Id == sent.Message?.Id);
        Assert.NotNull(await fixture.Store.GetAsync(sent.Message!.Id));
    }

    [Fact]
    public async Task RateLimitDropsExcessMessagesWithNotice()
    {
        var fixture = new ChatFixture(options: new ChatOptions
        {
            SayPerWindow = 2,
            RateWindowSeconds = 10
        });
        fixture.Directory.Add("alice", 10, 10, venue: null);

        for (var i = 0; i < 2; i++)
        {
            var accepted = await fixture.Chat.SendAsync("alice", new ChatSendRequest
            {
                Text = $"message {i}",
                Channel = ChatChannel.Say
            }, tick: i);
            Assert.True(accepted.Accepted);
        }

        var dropped = await fixture.Chat.SendAsync("alice", new ChatSendRequest
        {
            Text = "one too many",
            Channel = ChatChannel.Say
        }, tick: 3);

        Assert.False(dropped.Accepted);
        Assert.Equal("rate_limited", dropped.Code);
        Assert.Contains("too quickly", dropped.Notice);

        fixture.Time.Advance(TimeSpan.FromSeconds(11));
        var recovered = await fixture.Chat.SendAsync("alice", new ChatSendRequest
        {
            Text = "after the window",
            Channel = ChatChannel.Say
        }, tick: 4);
        Assert.True(recovered.Accepted);
    }

    [Fact]
    public async Task RetentionRemovesMessagesFromReadsAndStorage()
    {
        var fixture = new ChatFixture();
        fixture.Directory.Add("alice", 10, 10, venue: null);

        var sent = await fixture.Chat.SendAsync("alice", new ChatSendRequest
        {
            Text = "expiring",
            Channel = ChatChannel.Say
        }, tick: 7);

        Assert.Single(await fixture.Chat.ReadAsync("alice", ChatChannel.Say, null, take: 10));

        fixture.Time.Advance(TimeSpan.FromDays(fixture.Options.RetentionDays + 1));

        Assert.Empty(await fixture.Chat.ReadAsync("alice", ChatChannel.Say, null, take: 10));
        Assert.Equal(1, await fixture.Store.PurgeExpiredAsync(fixture.Time.GetUtcNow()));
        Assert.Null(await fixture.Store.GetAsync(sent.Message!.Id));
    }

    [Fact]
    public async Task ReportCapturesEvidenceAndRemovalHidesMessage()
    {
        var fixture = new ChatFixture();
        fixture.Directory.Add("alice", 10, 10, venue: null);
        fixture.Directory.Add("bob", 11, 10, venue: null);

        var sent = await fixture.Chat.SendAsync("alice", new ChatSendRequest
        {
            Text = "evidence text",
            Channel = ChatChannel.Say
        }, tick: 8);

        var report = await fixture.Chat.ReportAsync(
            "bob", sent.Message!.Id, ReportReason.Harassment, tick: 9);

        Assert.True(report.Accepted);
        Assert.Equal("evidence text", report.Report?.Evidence);
        Assert.Equal("alice", report.Report?.TargetPlayerId);

        var duplicate = await fixture.Chat.ReportAsync(
            "bob", sent.Message!.Id, ReportReason.Harassment, tick: 10);
        Assert.Equal(report.Report?.Id, duplicate.Report?.Id);

        Assert.True(await fixture.Chat.RemoveAsync(sent.Message.Id));
        Assert.Null(await fixture.Store.GetAsync(sent.Message.Id));
        Assert.Empty(await fixture.Chat.ReadAsync("alice", ChatChannel.Say, null, take: 10));

        var reports = fixture.Social.Reports(sent.Message.Id);
        Assert.Single(reports);
        Assert.Equal("evidence text", reports[0].Evidence);
    }

    [Fact]
    public void EmotesArePredefinedCooldownLimitedAndPublic()
    {
        var fixture = new ChatFixture(sayRadius: 20);
        fixture.Directory.Add("alice", 10, 10, venue: 100);
        fixture.Directory.Add("bob", 11, 10, venue: 100);
        fixture.Directory.Add("carol", 40, 40, venue: null);

        var wave = fixture.Emotes.Send("alice", EmoteKind.Wave, tick: 1);
        Assert.True(wave.Accepted);
        Assert.Equal(new[] { "bob" }, wave.Recipients);

        var cooldown = fixture.Emotes.Send("alice", EmoteKind.Wave, tick: 2);
        Assert.False(cooldown.Accepted);
        Assert.Equal("emote_cooldown", cooldown.Code);

        var nod = fixture.Emotes.Send("alice", EmoteKind.Nod, tick: 3);
        Assert.True(nod.Accepted);
        Assert.DoesNotContain("carol", nod.Recipients);

        var invalid = fixture.Emotes.Send("alice", (EmoteKind)99, tick: 4);
        Assert.False(invalid.Accepted);
        Assert.Equal("invalid_emote", invalid.Code);

        fixture.Time.Advance(TimeSpan.FromSeconds(fixture.Options.EmoteCooldownSeconds + 1));
        Assert.True(fixture.Emotes.Send("alice", EmoteKind.Wave, tick: 5).Accepted);
    }

    [Fact]
    public void EmotesShareAPerVenueBudget()
    {
        var fixture = new ChatFixture(options: new ChatOptions
        {
            EmoteCooldownSeconds = 0,
            EmoteVenueBudget = 1,
            EmoteVenueWindowSeconds = 10
        });
        fixture.Directory.Add("alice", 10, 10, venue: 100);
        fixture.Directory.Add("bob", 11, 10, venue: 100);

        Assert.True(fixture.Emotes.Send("alice", EmoteKind.Wave, tick: 1).Accepted);
        var overBudget = fixture.Emotes.Send("bob", EmoteKind.Cheer, tick: 2);
        Assert.False(overBudget.Accepted);
        Assert.Equal("emote_budget", overBudget.Code);
    }

    [Fact]
    public void ChatDoesNotEnterTheSimulationTickLoop()
    {
        var chatFieldTypes = new[]
        {
            typeof(ChatService),
            typeof(EmoteService),
            typeof(ChatMessage),
            typeof(ChatEmote)
        };
        var simulationFields = typeof(SimulationEngine)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(field => chatFieldTypes.Contains(field.FieldType))
            .ToArray();
        Assert.Empty(simulationFields);

        var chatConstructor = Assert.Single(typeof(ChatService).GetConstructors());
        Assert.DoesNotContain(
            chatConstructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(SimulationEngine));

        Assert.NotEqual(typeof(SimulationEngine).Assembly, typeof(ChatMessage).Assembly);
    }

    private sealed class ChatFixture
    {
        public ChatFixture(int sayRadius = 12, ChatOptions? options = null)
        {
            Options = options ?? new ChatOptions { SayRadius = sayRadius };
            Options.SayRadius = sayRadius;
            Options.Validate();
            Time = new MutableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            Store = new InMemoryChatStore();
            Social = new SocialGraphService();
            Directory = new FakeChatDirectory();
            Chat = new ChatService(Store, Social, Directory, Options, Time);
            Emotes = new EmoteService(Social, Directory, Options, Time);
        }

        public ChatOptions Options { get; }
        public MutableTimeProvider Time { get; }
        public InMemoryChatStore Store { get; }
        public SocialGraphService Social { get; }
        public FakeChatDirectory Directory { get; }
        public ChatService Chat { get; }
        public EmoteService Emotes { get; }
    }

    private sealed class FakeChatDirectory : IChatDirectory
    {
        private readonly Dictionary<string, ChatPlayerContext> players = new(StringComparer.Ordinal);

        public void Add(string playerId, int tileX, int tileY, ulong? venue) =>
            players[playerId] = new ChatPlayerContext(
                playerId,
                playerId,
                tileX,
                tileY,
                venue,
                venue is not null);

        public ChatPlayerContext? Describe(string playerId) =>
            players.TryGetValue(playerId, out var player) ? player : null;

        public IReadOnlyList<ChatPlayerContext> Online() => players.Values.ToArray();
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan by) => current += by;
    }
}
