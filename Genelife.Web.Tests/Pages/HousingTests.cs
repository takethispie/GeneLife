using Bunit;
using Genelife.Web.Pages;
using Genelife.Web.Tests.Support;

namespace Genelife.Web.Tests.Pages;

/// <summary>
/// Housing page. The heaviest read in the admin: one refresh composes six
/// endpoints. Every command (rent index, spawn, create, evict, repair) is only
/// reached from user actions.
/// </summary>
public class HousingTests : AdminPageTest
{
    private static object Overview(int properties = 0) => new
    {
        properties,
        units = 0,
        occupiedUnits = 0,
        vacantUnits = 0,
        shelterBeds = 0,
        shelteredHouseholds = 0,
        homelessHouseholds = 0,
        overcrowdedUnits = 0,
        unitsBelowCondition = 0,
        activeProjects = 0,
        constructionUnits = 0,
        rentIndex = 1.0m
    };

    /// <summary>
    /// Path-only declarations: the harness falls back to a path match, so these
    /// hold whatever query each endpoint is asked for.
    /// </summary>
    private void StubAll(int properties = 0)
    {
        Stub.Ok("api/housing/overview", Overview(properties));
        Stub.Ok("api/housing/properties", Array.Empty<object>());
        Stub.Ok("api/housing/households", Array.Empty<object>());
        Stub.Ok("api/housing/rent-roll", Array.Empty<object>());
        Stub.Ok("api/housing/construction", Array.Empty<object>());
        Stub.Ok("api/housing/metrics/history", Array.Empty<object>());
    }

    [Fact]
    public void RendersTheOverview()
    {
        StubAll(properties: 12);

        var cut = Render<Housing>();

        cut.WaitForAssertion(() => Assert.Contains("Housing", cut.Markup));
        Assert.Contains("12", cut.Markup);
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();
    }

    [Fact]
    public void FailedOverviewShowsTheErrorAlert()
    {
        Stub.Fail("api/housing/overview");
        Stub.Ok("api/housing/properties", Array.Empty<object>());
        Stub.Ok("api/housing/households", Array.Empty<object>());
        Stub.Ok("api/housing/rent-roll", Array.Empty<object>());
        Stub.Ok("api/housing/construction", Array.Empty<object>());
        Stub.Ok("api/housing/metrics/history", Array.Empty<object>());

        var cut = Render<Housing>();

        // One refresh spans six calls; any failure must surface as the page-level
        // error rather than a half-rendered dashboard.
        cut.WaitForAssertion(() => Assert.Contains("Could not load housing data.", cut.Markup));
    }

    [Fact]
    public void EmptySnapshotRendersWithoutThrowing()
    {
        StubAll();

        var cut = Render<Housing>();

        cut.WaitForAssertion(() => Assert.Contains("Housing", cut.Markup));
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();
    }
}
