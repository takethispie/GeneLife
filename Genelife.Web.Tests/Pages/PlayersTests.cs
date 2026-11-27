using Bunit;
using Genelife.Web.Pages;
using Genelife.Web.Tests.Support;

namespace Genelife.Web.Tests.Pages;

/// <summary>
/// Players page. Reads an overview plus a paged lives list on load; the
/// continuation-policy and detach commands are only reached from user actions.
/// </summary>
public class PlayersTests : AdminPageTest
{
    private static object Overview(int players = 0) => new
    {
        players,
        active = 0,
        awaitingContinuation = 0,
        employed = 0,
        housed = 0,
        pendingInvitations = 0,
        deathsThisYear = 0L,
        continuationsThisYear = 0L,
        commandsAppliedThisYear = 0L
    };

    /// <summary>Path-only so it holds whatever query the page sends.</summary>
    private void StubAll(int players = 0)
    {
        Stub.Ok("api/player", Overview(players));
        Stub.Ok("api/player/lives", new { items = Array.Empty<object>(), total = 0 });
    }

    [Fact]
    public void RendersTheOverview()
    {
        StubAll(players: 5);

        var cut = Render<Players>();

        cut.WaitForAssertion(() => Assert.Contains("Players", cut.Markup));
        Assert.Contains("5", cut.Markup);
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();
    }

    [Fact]
    public void FailedLoadShowsTheErrorAlert()
    {
        Stub.Fail("api/player");
        Stub.Ok("api/player/lives", new { items = Array.Empty<object>(), total = 0 });

        var cut = Render<Players>();

        cut.WaitForAssertion(() => Assert.Contains("Could not load player data.", cut.Markup));
    }

    [Fact]
    public void EmptySnapshotRendersWithoutThrowing()
    {
        StubAll();

        var cut = Render<Players>();

        cut.WaitForAssertion(() => Assert.Contains("Players", cut.Markup));
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();
    }
}
