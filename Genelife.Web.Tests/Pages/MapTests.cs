using Bunit;
using Genelife.Web.Pages;
using Genelife.Web.Tests.Support;

namespace Genelife.Web.Tests.Pages;

/// <summary>
/// Map page. Reads the map and the world snapshot on load; the create, place and
/// relocate commands are only reached from user actions.
/// </summary>
public class MapTests : AdminPageTest
{
    private static object MapStatus() => new
    {
        hasMap = true,
        mapId = "world",
        version = 3,
        layoutVersion = 1,
        seed = 7,
        width = 32,
        height = 32,
        tileSize = 1f,
        minX = -16,
        minY = -16,
        maxX = 15,
        maxY = 15,
        buildings = 3,
        occupiedTiles = 0,
        buildingList = Array.Empty<object>(),
        unplaced = Array.Empty<object>()
    };

    private static object World() => new
    {
        tick = 10L,
        year = 1,
        month = 0,
        isRunning = true,
        capturedAt = DateTimeOffset.UtcNow,
        genoses = Array.Empty<object>(),
        companies = Array.Empty<object>(),
        offices = Array.Empty<object>(),
        employees = Array.Empty<object>(),
        jobPostings = Array.Empty<object>(),
        jobApplications = Array.Empty<object>(),
        openPositions = Array.Empty<object>(),
        economy = new { },
        housing = new { },
        lifecycle = new { },
        map = (object?)null,
        recentLedger = Array.Empty<object>()
    };

    private void StubAll()
    {
        Stub.Ok("api/map", MapStatus());
        Stub.Ok("api/data", World());
    }

    [Fact]
    public void RendersTheMapSummary()
    {
        StubAll();

        var cut = Render<Map>();

        cut.WaitForAssertion(() => Assert.Contains("world v3", cut.Markup));
        Assert.Contains("Map", cut.Markup);
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();
    }

    [Fact]
    public void FailedMapLoadShowsTheErrorAlert()
    {
        Stub.Fail("api/map");
        Stub.Ok("api/data", World());

        var cut = Render<Map>();

        cut.WaitForAssertion(() => Assert.Contains("Could not load the map.", cut.Markup));
    }

    [Fact]
    public void MapWithNoBuildingsRendersWithoutThrowing()
    {
        Stub.Ok("api/map", new { hasMap = false, mapId = "", width = 0, height = 0, buildingList = Array.Empty<object>(), unplaced = Array.Empty<object>() });
        Stub.Ok("api/data", World());

        var cut = Render<Map>();

        cut.WaitForAssertion(() => Assert.Contains("Map", cut.Markup));
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();
    }
}
