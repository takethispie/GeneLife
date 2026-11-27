using Bunit;
using Genelife.Web.Pages;
using Genelife.Web.Tests.Support;

namespace Genelife.Web.Tests.Pages;

/// <summary>
/// Economy page. Reads three endpoints (world snapshot, fiscal, history) and
/// renders a closed-money-loop summary.
/// </summary>
public class EconomyTests : AdminPageTest
{
    private const decimal MoneySupply = 98765.43m;

    private static object World(decimal moneySupply, bool running = true) => new
    {
        tick = 90L,
        year = 2,
        month = 1,
        isRunning = running,
        capturedAt = DateTimeOffset.UtcNow,
        genoses = Array.Empty<object>(),
        companies = Array.Empty<object>(),
        offices = Array.Empty<object>(),
        employees = Array.Empty<object>(),
        jobPostings = Array.Empty<object>(),
        jobApplications = Array.Empty<object>(),
        openPositions = Array.Empty<object>(),
        economy = new { totalMoneySupply = moneySupply },
        housing = new { },
        lifecycle = new { },
        map = (object?)null,
        recentLedger = Array.Empty<object>()
    };

    private void StubAll(decimal moneySupply, bool running = true)
    {
        Stub.Ok("api/data", World(moneySupply, running));
        Stub.Ok("api/economy/fiscal", new { governmentBalance = 1000m, pensionBalance = 500m });
        Stub.Ok("api/economy/metrics/history", Array.Empty<object>());
    }

    [Fact]
    public void RendersTheMoneySupply()
    {
        StubAll(MoneySupply);

        var cut = Render<Economy>();

        cut.WaitForAssertion(() => Assert.Contains("98,765.43", cut.Markup));
        Assert.Contains("Economy", cut.Markup);
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();
    }

    [Fact]
    public void FailedFiscalLoadShowsTheErrorAlert()
    {
        Stub.Ok("api/data", World(MoneySupply));
        Stub.Fail("api/economy/fiscal");
        Stub.Ok("api/economy/metrics/history", Array.Empty<object>());

        var cut = Render<Economy>();

        // The page composes one refresh across three calls; any failure must
        // surface as the page-level error rather than a half-rendered summary.
        cut.WaitForAssertion(() => Assert.Contains("Could not load economy data.", cut.Markup));
    }

    [Fact]
    public void EmptySnapshotRendersWithoutThrowing()
    {
        StubAll(0m, running: false);

        var cut = Render<Economy>();

        cut.WaitForAssertion(() => Assert.Contains("Economy", cut.Markup));
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();
    }
}
