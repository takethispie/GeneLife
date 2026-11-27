using Bunit;
using Genelife.Web.Pages;
using Genelife.Web.Tests.Support;

namespace Genelife.Web.Tests.Pages;

/// <summary>
/// Data page — the world snapshot browser. Reads a single endpoint but renders
/// several tables from it, so it is the widest surface in the admin.
/// </summary>
public class DataTests : AdminPageTest
{
    private static object World(string companyName = "Foundry Ltd", string genosName = "Ada") => new
    {
        tick = 500L,
        year = 3,
        month = 4,
        isRunning = true,
        capturedAt = DateTimeOffset.UtcNow,
        genoses = new[]
        {
            new
            {
                networkId = 1UL,
                name = genosName,
                age = 30,
                isPlayerControlled = false,
                hunger = 80f,
                energy = 70f,
                hygiene = 65f,
                bladder = 60f,
                currentAction = "Idle",
                timeRemaining = 0f,
                x = 1f,
                y = 2f,
                tileX = 1,
                tileY = 2,
                hasMoveTarget = false,
                moveTargetX = 0,
                moveTargetY = 0,
                walletBalance = 120.50m,
                savingsBalance = 40m,
                unmetDemand = 0m,
                householdNetworkId = (ulong?)null,
                housingUnitNetworkId = (ulong?)null,
                tenure = "owner",
                housingTier = "standard",
                homeless = false,
                stage = "Adult",
                health = 100m,
                topSkill = "logic",
                seniority = "none",
                isDead = false,
                appearance = (object?)null
            }
        },
        companies = new[]
        {
            new
            {
                networkId = 9UL,
                name = companyName,
                employeeCount = 4,
                officeCount = 1
            }
        },
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

    [Fact]
    public void RendersTheDefaultTabAndSwitchesTabs()
    {
        Stub.Ok("api/data", World(companyName: "Foundry Ltd", genosName: "Ada"));

        var cut = Render<Data>();

        // The page opens on the NPCs tab, so the genos is what must be visible.
        cut.WaitForAssertion(() => Assert.Contains("Ada", cut.Markup));
        Assert.Contains("Data", cut.Markup);
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();

        // Companies live behind a tab; switching must actually swap the content.
        var companiesTab = cut.FindAll("[role=tab]").First(t => t.TextContent.Contains("Companies"));
        companiesTab.Click();

        cut.WaitForAssertion(() => Assert.Contains("Foundry Ltd", cut.Markup));
    }

    [Fact]
    public void FailedLoadShowsTheErrorAlert()
    {
        Stub.Fail("api/data");

        var cut = Render<Data>();

        cut.WaitForAssertion(() => Assert.Contains("Could not load world data.", cut.Markup));
    }

    [Fact]
    public void EmptyWorldRendersWithoutThrowing()
    {
        Stub.Ok("api/data", new
        {
            tick = 0L,
            year = 0,
            month = 0,
            isRunning = false,
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
        });

        var cut = Render<Data>();

        cut.WaitForAssertion(() => Assert.Contains("Data", cut.Markup));
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();
    }
}
