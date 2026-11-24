using Genelife.Social;

namespace Genelife.UnitTests.Social;

public class SocialGraphTests
{
    [Fact]
    public void AcceptingAFriendRequestCreatesMirroredEdgesAndRejectsDuplicates()
    {
        var graph = new SocialGraphService();

        var request = graph.RequestFriend("alice", "bob", tick: 1);
        Assert.True(request.Accepted);
        Assert.Equal(SocialRequestStatus.Pending, request.Request?.Status);

        var accepted = graph.RespondToFriendRequest(request.Request!.Id, "bob", accept: true, tick: 2);
        Assert.True(accepted.Accepted);
        Assert.Equal(SocialRequestStatus.Accepted, accepted.Request?.Status);

        Assert.True(graph.AreFriends("alice", "bob"));
        Assert.True(graph.AreFriends("bob", "alice"));
        Assert.Contains(graph.Friends("alice"), edge => edge.ToPlayerId == "bob");
        Assert.Contains(graph.Friends("bob"), edge => edge.ToPlayerId == "alice");

        Assert.Equal("duplicate", graph.AddFriend("alice", "bob", tick: 3).Code);
        Assert.Equal("self_edge", graph.AddFriend("carol", "carol", tick: 3).Code);
    }

    [Fact]
    public void OnlyTheTargetCanRespondAndDeclineStartsACooldown()
    {
        var graph = new SocialGraphService(new SocialGraphOptions
        {
            FriendRequestLifetimeTicks = 100,
            FriendDeclineCooldownTicks = 50
        });

        var request = graph.RequestFriend("alice", "bob", tick: 1);
        Assert.Equal("not_target", graph.RespondToFriendRequest(
            request.Request!.Id, "alice", accept: true, tick: 2).Code);

        var declined = graph.RespondToFriendRequest(request.Request.Id, "bob", accept: false, tick: 3);
        Assert.True(declined.Accepted);
        Assert.Equal(SocialRequestStatus.Declined, declined.Request?.Status);

        Assert.Equal("cooldown", graph.RequestFriend("alice", "bob", tick: 4).Code);
        Assert.True(graph.RequestFriend("alice", "bob", tick: 54).Accepted);
    }

    [Fact]
    public void FriendRequestsExpireLazily()
    {
        var graph = new SocialGraphService(new SocialGraphOptions
        {
            FriendRequestLifetimeTicks = 10
        });

        var request = graph.RequestFriend("alice", "bob", tick: 1);
        var expired = graph.RespondToFriendRequest(request.Request!.Id, "bob", accept: true, tick: 20);

        Assert.False(expired.Accepted);
        Assert.Equal("not_pending", expired.Code);
        Assert.Empty(graph.Overview("bob", tick: 20).IncomingRequests);
    }

    [Fact]
    public void BlocksAreSymmetricCancelRequestsAndRemoveFriendship()
    {
        var graph = new SocialGraphService();
        graph.AddFriend("alice", "bob", tick: 1);
        graph.RequestFriend("alice", "carol", tick: 2);

        var blocked = graph.Block("bob", "alice", tick: 3);
        Assert.True(blocked.Accepted);
        Assert.True(graph.IsBlocked("alice", "bob"));
        Assert.True(graph.IsBlocked("bob", "alice"));
        Assert.False(graph.AreFriends("alice", "bob"));

        var cancelled = graph.RequestFriend("alice", "bob", tick: 4);
        Assert.Equal("blocked", cancelled.Code);

        Assert.True(graph.Unblock("alice", "bob").Accepted);
        Assert.False(graph.IsBlocked("alice", "bob"));
        Assert.False(graph.IsBlocked("bob", "alice"));
    }

    [Fact]
    public void BlockCancelsPendingRequestsInBothDirections()
    {
        var graph = new SocialGraphService();
        var outbound = graph.RequestFriend("alice", "bob", tick: 1);
        var inbound = graph.RequestFriend("carol", "alice", tick: 1);

        graph.Block("alice", "bob", tick: 2);

        Assert.Equal(
            SocialRequestStatus.Cancelled,
            graph.AdminRequests().Single(request => request.Id == outbound.Request!.Id).Status);
        Assert.Equal(
            SocialRequestStatus.Pending,
            graph.AdminRequests().Single(request => request.Id == inbound.Request!.Id).Status);

        graph.Block("alice", "carol", tick: 3);
        Assert.Equal(
            SocialRequestStatus.Cancelled,
            graph.AdminRequests().Single(request => request.Id == inbound.Request!.Id).Status);
    }

    [Fact]
    public void MutesAreOneDirectionalAndDoNotBlockOrEndFriendship()
    {
        var graph = new SocialGraphService();
        graph.AddFriend("alice", "bob", tick: 1);
        Assert.True(graph.Mute("alice", "bob", tick: 2).Accepted);

        Assert.True(graph.IsMuted("alice", "bob"));
        Assert.False(graph.IsMuted("bob", "alice"));
        Assert.True(graph.AreFriends("alice", "bob"));
        Assert.False(graph.IsBlocked("alice", "bob"));

        Assert.Equal("duplicate", graph.Mute("alice", "bob", tick: 3).Code);
        Assert.True(graph.Unmute("alice", "bob").Accepted);
        Assert.False(graph.IsMuted("alice", "bob"));
    }

    [Fact]
    public void ReportsCaptureEvidenceAndAreIdempotentWithinTheWindow()
    {
        var graph = new SocialGraphService(new SocialGraphOptions
        {
            ReportDedupeWindowTicks = 100
        });
        var messageId = Guid.NewGuid();

        var first = graph.Report(
            "bob", "alice", ReportReason.Harassment, messageId, "evidence text", tick: 10);
        var duplicate = graph.Report(
            "bob", "alice", ReportReason.Harassment, messageId, "evidence text", tick: 50);
        var afterWindow = graph.Report(
            "bob", "alice", ReportReason.Harassment, messageId, "evidence text", tick: 200);
        var otherReason = graph.Report(
            "bob", "alice", ReportReason.Spam, messageId, "evidence text", tick: 11);

        Assert.True(first.Accepted);
        Assert.Equal(first.Report?.Id, duplicate.Report?.Id);
        Assert.NotEqual(first.Report?.Id, afterWindow.Report?.Id);
        Assert.NotEqual(first.Report?.Id, otherReason.Report?.Id);
        Assert.Equal("evidence text", first.Report?.Evidence);
        Assert.Equal(3, graph.Reports(messageId).Count);
        Assert.Equal("self_report", graph.Report(
            "alice", "alice", ReportReason.Other, null, null, tick: 12).Code);
    }

    [Fact]
    public void AdminCanReadAndResolveReports()
    {
        var graph = new SocialGraphService();
        var report = graph.Report(
            "bob", "alice", ReportReason.Spam, null, null, tick: 1);

        Assert.Single(graph.Reports());
        Assert.True(graph.SetReportStatus(report.Report!.Id, ReportStatus.Resolved));
        Assert.Equal(ReportStatus.Resolved, graph.Reports().Single().Status);
        Assert.False(graph.SetReportStatus(Guid.NewGuid(), ReportStatus.Dismissed));
    }

    [Fact]
    public void SnapshotRoundTripIsExactAndDoubleRestoreIsIdempotent()
    {
        var graph = new SocialGraphService();
        graph.AddFriend("alice", "bob", tick: 1, SocialEdgeSource.Migration);
        graph.Block("carol", "dave", tick: 2, SocialEdgeSource.Admin);
        graph.Mute("alice", "carol", tick: 3, SocialEdgeSource.Player);
        graph.RequestFriend("dave", "alice", tick: 4);
        graph.Report("bob", "carol", ReportReason.Hate, Guid.NewGuid(), "evidence", tick: 5);

        var snapshot = graph.Export();
        var restored = new SocialGraphService();
        restored.Restore(snapshot);

        Assert.Equal(Canonical(snapshot), Canonical(restored.Export()));
        Assert.True(restored.AreFriends("alice", "bob"));
        Assert.True(restored.IsBlocked("carol", "dave"));
        Assert.True(restored.IsMuted("alice", "carol"));
        Assert.Single(restored.AdminRequests());
        Assert.Single(restored.Reports());

        restored.Restore(restored.Export());
        Assert.Equal(Canonical(snapshot), Canonical(restored.Export()));
    }

    private static string Canonical(SocialGraphSnapshot snapshot) =>
        string.Join("|",
            snapshot.Edges
                .OrderBy(edge => edge.Kind)
                .ThenBy(edge => edge.FromPlayerId, StringComparer.Ordinal)
                .ThenBy(edge => edge.ToPlayerId, StringComparer.Ordinal)
                .Select(edge => $"{edge.FromPlayerId}>{edge.ToPlayerId}:{(byte)edge.Kind}:{edge.SinceTick}"))
        + "||"
        + string.Join("|", snapshot.Requests
            .OrderBy(request => request.Id)
            .Select(request => $"{request.Id}:{request.Status}"))
        + "||"
        + string.Join("|", snapshot.Reports
            .OrderBy(report => report.Id)
            .Select(report => $"{report.Id}:{report.MessageId}:{report.Reason}:{report.Status}"));
}
