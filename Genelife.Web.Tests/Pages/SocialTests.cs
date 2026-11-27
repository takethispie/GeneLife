using Bunit;
using Genelife.Web.Pages;
using Genelife.Web.Tests.Support;

namespace Genelife.Web.Tests.Pages;

/// <summary>
/// Social admin page. It reads a single endpoint, so it is also the harness
/// smoke test: if the stub is wired correctly these pass, and if it is not they
/// fail loudly rather than rendering an unexplained empty page.
/// </summary>
public class SocialTests : AdminPageTest
{
    private static object Overview(
        int online = 0,
        int unresolvedReports = 0,
        int edges = 0,
        int retained = 0) => new
    {
        tick = 1234L,
        online = Enumerable.Range(0, online).Select(i => new
        {
            playerId = $"player-{i}",
            displayName = $"Player {i}",
            genosNetworkId = 100UL + (ulong)i,
            tileX = i,
            tileY = i,
            buildingNetworkId = (ulong?)null,
            state = "Active",
            appearance = (object?)null,
            lastSeenTick = 1234L
        }).ToArray(),
        venues = Array.Empty<object>(),
        reports = Enumerable.Range(0, unresolvedReports).Select(i => new
        {
            id = Guid.NewGuid(),
            reporterPlayerId = $"reporter-{i}",
            targetPlayerId = $"target-{i}",
            reason = "Harassment",
            messageId = (Guid?)null,
            evidence = (string?)null,
            createdTick = 1000L,
            status = "Open"
        }).ToArray(),
        edges = Enumerable.Range(0, edges).Select(i => new
        {
            fromPlayerId = $"a-{i}",
            toPlayerId = $"b-{i}",
            kind = "Friend",
            sinceTick = 900L,
            source = "Manual"
        }).ToArray(),
        requests = Array.Empty<object>(),
        partnershipInvitations = Array.Empty<object>(),
        moderationActions = Array.Empty<object>(),
        privacy = Array.Empty<object>(),
        chatRetention = new
        {
            retainedMessages = retained,
            deletedMessages = 0,
            oldestRetained = (DateTimeOffset?)null,
            checkedAt = DateTimeOffset.UtcNow,
            retentionDays = 30
        }
    };

    [Fact]
    public void RendersTheOverviewCounts()
    {
        Stub.Ok("api/social/admin/overview", Overview(online: 3, unresolvedReports: 2, edges: 4, retained: 42));

        var cut = Render<Social>();

        cut.WaitForAssertion(() => Assert.Contains("3", cut.Markup));
        AssertNoUndeclaredRequests();

        var counts = cut.FindAll(".fs-3.fw-semibold").Select(e => e.TextContent.Trim()).ToList();
        Assert.Contains("3", counts);  // online
        Assert.Contains("2", counts);  // unresolved reports
        Assert.Contains("4", counts);  // edges
        Assert.Contains("42", counts); // retained messages
    }

    [Fact]
    public void FailedLoadShowsTheErrorAlertInsteadOfCrashing()
    {
        Stub.Fail("api/social/admin/overview");

        var cut = Render<Social>();

        cut.WaitForAssertion(() => Assert.Contains("alert-danger", cut.Markup));
        Assert.Contains("Could not load social data.", cut.Markup);
    }

    [Fact]
    public void EmptyOverviewRendersWithoutThrowing()
    {
        Stub.Ok("api/social/admin/overview", Overview());

        var cut = Render<Social>();

        cut.WaitForAssertion(() => Assert.DoesNotContain("alert-danger", cut.Markup));
        AssertNoUndeclaredRequests();
    }
}
