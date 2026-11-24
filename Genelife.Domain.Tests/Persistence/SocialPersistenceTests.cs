using Genelife.Api.Persistence;
using Genelife.Api.Services;
using Genelife.Social;

namespace Genelife.UnitTests.Persistence;

public class SocialPersistenceTests
{
    [Fact]
    public void SocialEdgesRequestsAndPrivacyRoundTripAndDoubleRestore()
    {
        var privacy = new SocialPrivacyRegistry();
        var graph = new SocialGraphService();
        using var engine = new SimulationEngine(socialGraph: graph, socialPrivacy: privacy);
        engine.Initialize();

        Assert.True(graph.AddFriend("alice", "bob", tick: 1).Accepted);
        Assert.True(graph.Block("bob", "carol", tick: 2).Accepted);
        var request = graph.RequestFriend("bob", "dave", tick: 3);
        Assert.True(request.Accepted, request.Code);
        privacy.Set("alice", new SocialPrivacy
        {
            ShowDisplayName = false,
            AllowNearbyForNonFriends = false,
            FriendsOnlyMessages = true
        });

        var state = engine.CaptureState(engine.NetworkIds);
        engine.Shutdown();

        Assert.Equal(2, state.SocialEdges.Count(edge => edge.Kind == SocialEdgeKind.Friend));
        Assert.Contains(state.SocialEdges, edge => edge.Kind == SocialEdgeKind.Block);
        Assert.Single(state.SocialRequests);
        Assert.Contains(state.SocialSettings, setting => setting.PlayerId == "alice" && setting.FriendsOnlyMessages);

        var restoredPrivacy = new SocialPrivacyRegistry();
        var restoredGraph = new SocialGraphService();
        using var restored = new SimulationEngine(socialGraph: restoredGraph, socialPrivacy: restoredPrivacy);
        restored.Restore(state, restored.NetworkIds);

        Assert.True(restoredGraph.AreFriends("alice", "bob"));
        Assert.True(restoredGraph.IsBlocked("bob", "carol"));
        Assert.Contains(restoredGraph.AdminRequests(), pending => pending.Id == request.Request!.Id);
        Assert.False(restoredPrivacy.For("alice").ShowDisplayName);
        Assert.False(restoredPrivacy.For("alice").AllowNearbyForNonFriends);
        Assert.True(restoredPrivacy.For("alice").FriendsOnlyMessages);

        var secondState = restored.CaptureState(restored.NetworkIds);
        restored.Shutdown();
        var secondPrivacy = new SocialPrivacyRegistry();
        var secondGraph = new SocialGraphService();
        using var restoredAgain = new SimulationEngine(socialGraph: secondGraph, socialPrivacy: secondPrivacy);
        restoredAgain.Restore(secondState, restoredAgain.NetworkIds);

        Assert.Equal(2, secondGraph.AdminEdges().Count(edge => edge.Kind == SocialEdgeKind.Friend));
        Assert.True(secondGraph.IsBlocked("bob", "carol"));
        Assert.Single(secondGraph.AdminRequests());
        Assert.False(secondPrivacy.For("alice").ShowDisplayName);
    }

    [Fact]
    public void SchemaFourteenMigratesToEmptySocialStateAndDefaultPrivacy()
    {
        var state = new PersistedWorldState
        {
            SchemaVersion = 14,
            PlayerProfiles =
            [
                new PersistedPlayerProfile
                {
                    PlayerId = "alice",
                    ContinuationPolicy = "Heir"
                }
            ]
        };

        SnapshotMigrations.Upgrade(state, 14);

        Assert.Equal(15, PersistedWorldState.CurrentSchemaVersion);
        Assert.Equal(15, state.SchemaVersion);
        Assert.Empty(state.SocialEdges);
        Assert.Empty(state.SocialRequests);
        Assert.Empty(state.SocialRestrictions);
        Assert.Single(state.SocialSettings);
        var setting = Assert.Single(state.SocialSettings);
        Assert.Equal("alice", setting.PlayerId);
        Assert.True(setting.ShowDisplayName);
        Assert.True(setting.AllowNearbyForNonFriends);

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);
        Assert.Empty(restored.SocialGraph.AdminEdges());
        Assert.Empty(restored.SocialGraph.AdminRequests());
        restored.Shutdown();
    }

    [Fact]
    public void AuditStoreRebuildsReportsForANewGraphInstance()
    {
        var audit = new InMemorySocialAuditStore();
        var first = new SocialGraphService(auditStore: audit);
        var report = first.Report("bob", "alice", ReportReason.Spam, null, "evidence", tick: 1);
        Assert.True(report.Accepted);
        Assert.True(first.SetReportStatus(report.Report!.Id, ReportStatus.Resolved));

        var second = new SocialGraphService(auditStore: audit);
        var rebuilt = Assert.Single(second.Reports());
        Assert.Equal(report.Report.Id, rebuilt.Id);
        Assert.Equal(ReportStatus.Resolved, rebuilt.Status);
        Assert.Equal("evidence", rebuilt.Evidence);
    }

    [Fact]
    public void ChatAndReportsAreNotPartOfTheWorldSnapshot()
    {
        var state = new PersistedWorldState();
        var properties = typeof(PersistedWorldState).GetProperties().Select(property => property.Name);
        Assert.DoesNotContain(properties, name => name.Contains("Chat", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("Report", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("ModerationAction", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(state.SocialEdges);
    }
}
