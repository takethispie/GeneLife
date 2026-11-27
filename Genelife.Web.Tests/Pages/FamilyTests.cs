using Bunit;
using Genelife.Web.Pages;
using Genelife.Web.Tests.Support;

namespace Genelife.Web.Tests.Pages;

/// <summary>
/// Family page — the largest route in the admin, and the last one to get
/// page-level coverage. It is tab-driven: the initial load only reads the
/// overview, and each other tab fetches its own paged endpoint on demand, so
/// the default render is what this covers.
/// </summary>
public class FamilyTests : AdminPageTest
{
    private static object Overview(int population = 0) => new
    {
        population,
        adults = 0,
        minors = 0,
        pairedGenoses = 0,
        singleAdults = 0,
        partnerships = 0,
        parentEdges = 0,
        guardianEdges = 0,
        exPartnerships = 0,
        seedPartnershipShare = 0m,
        partnershipRate = 0m,
        sameSexPairingAllowed = true,
        cohabitOnPairing = false
    };

    private void StubAll(int population = 0) => Stub.Ok("api/family", Overview(population));

    [Fact]
    public void RendersTheOverview()
    {
        StubAll(population: 25);

        var cut = Render<Family>();

        cut.WaitForAssertion(() => Assert.Contains("Family", cut.Markup));
        Assert.Contains("25", cut.Markup);
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();
    }

    [Fact]
    public void FailedLoadShowsTheErrorAlert()
    {
        Stub.Fail("api/family");

        var cut = Render<Family>();

        cut.WaitForAssertion(() => Assert.Contains("Could not load family data.", cut.Markup));
    }

    [Fact]
    public void EmptyOverviewRendersWithoutThrowing()
    {
        StubAll();

        var cut = Render<Family>();

        cut.WaitForAssertion(() => Assert.Contains("Family", cut.Markup));
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();
    }

    /// <summary>
    /// Switching tabs must fetch that tab's own endpoint rather than reusing the
    /// overview, and the page must not fall over when the tab's data is absent.
    /// </summary>
    [Fact]
    public void SwitchingToAnotherTabLoadsItsOwnEndpoint()
    {
        StubAll(population: 25);
        Stub.Ok("api/family/genoses", new { items = Array.Empty<object>(), total = 0 });

        var cut = Render<Family>();
        cut.WaitForAssertion(() => Assert.Contains("25", cut.Markup));

        var genosesTab = cut.FindAll("[role=tab], .nav-link").First(t => t.TextContent.Contains("Genoses"));
        genosesTab.Click();

        cut.WaitForAssertion(() => Assert.Contains("api/family/genoses", string.Join(" ", Stub.Requests)));
        AssertNoUndeclaredRequests();
    }
}
