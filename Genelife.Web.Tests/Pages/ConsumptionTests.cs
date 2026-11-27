using Bunit;
using Genelife.Web.Pages;
using Genelife.Web.Tests.Support;

namespace Genelife.Web.Tests.Pages;

/// <summary>
/// Consumption page. Composes one refresh from three endpoints (overview,
/// households page, stores page); the repair and restock commands are only hit
/// from user actions.
/// </summary>
public class ConsumptionTests : AdminPageTest
{
    private static object Overview(int households = 0, int stores = 0, int stockUnits = 0, int groceryCatalogVersion = 1) => new
    {
        networks = Array.Empty<object>(),
        households,
        cutOffHouseholds = 0,
        reducedHouseholds = 0,
        stores,
        stockUnits = stockUnits,
        spoiledThisYear = 0,
        nutrition = Array.Empty<object>(),
        malnourishedHouseholds = 0,
        groceryCatalogVersion
    };

    /// <summary>
    /// Declared path-only on purpose: the harness falls back to a path match, so
    /// these cover the households/stores pages whatever query the page sends.
    /// </summary>
    private void StubAll(int households = 0, int stores = 0, int stockUnits = 0, int groceryCatalogVersion = 1)
    {
        Stub.Ok("api/consumption", Overview(households, stores, stockUnits, groceryCatalogVersion));
        Stub.Ok("api/consumption/households", new { items = Array.Empty<object>(), total = 0 });
        Stub.Ok("api/consumption/stores", new { items = Array.Empty<object>(), total = 0 });
    }

    [Fact]
    public void RendersTheOverviewCounts()
    {
        StubAll(households: 42, stores: 7);

        var cut = Render<Consumption>();

        cut.WaitForAssertion(() => Assert.Contains("42", cut.Markup));
        Assert.Contains("Consumption", cut.Markup);
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();
    }

    [Fact]
    public void GroceryCardInterpolatesTheCatalogVersion()
    {
        // Regression: "catalog v@overview.GroceryCatalogVersion" was parsed as an
        // email-style literal and rendered verbatim. The expression must be
        // wrapped so the numeric version shows through.
        StubAll(stockUnits: 40, groceryCatalogVersion: 3);

        var cut = Render<Consumption>();

        cut.WaitForAssertion(() => Assert.Contains("catalog v3", cut.Markup));
        Assert.Contains("40 stock units", cut.Markup);
        Assert.DoesNotContain("@overview.GroceryCatalogVersion", cut.Markup);
        AssertNoUndeclaredRequests();
    }

    [Fact]
    public void FailedOverviewShowsTheErrorAlert()
    {
        Stub.Fail("api/consumption");
        Stub.Ok("api/consumption/households", new { items = Array.Empty<object>(), total = 0 });
        Stub.Ok("api/consumption/stores", new { items = Array.Empty<object>(), total = 0 });

        var cut = Render<Consumption>();

        cut.WaitForAssertion(() => Assert.Contains("Could not load consumption.", cut.Markup));
    }

    [Fact]
    public void EmptySnapshotRendersWithoutThrowing()
    {
        StubAll();

        var cut = Render<Consumption>();

        cut.WaitForAssertion(() => Assert.Contains("Consumption", cut.Markup));
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();
    }
}
