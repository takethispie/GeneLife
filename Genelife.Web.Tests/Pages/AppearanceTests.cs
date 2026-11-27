using Bunit;
using Genelife.Web.Pages;
using Genelife.Web.Tests.Support;

// "Appearance" is also a namespace in this solution, so alias the page type.
using AppearancePage = Genelife.Web.Pages.Appearance;

namespace Genelife.Web.Tests.Pages;

/// <summary>
/// Appearance page. Reads a paged list on load; the focal-genos and family
/// endpoints are only hit when a genos is selected.
/// </summary>
public class AppearanceTests : AdminPageTest
{
    private static object Genos(ulong id, string name) => new
    {
        networkId = id,
        name,
        age = 30,
        isPlayerControlled = false,
        playerId = (string?)null,
        stage = "Adult",
        descriptor = new
        {
            skinTone = (byte)2,
            faceShape = (byte)0,
            hairColor = (byte)1,
            hairStyle = (byte)0,
            eyeColor = (byte)0,
            bodyFrame = (byte)1,
            heightScale = (byte)80,
            ageBand = (byte)2,
            expression = (byte)0,
            appearanceVersion = (byte)1
        },
        layers = new
        {
            bodyPart = "body-adult-average",
            skinPalette = "skin-olive",
            facePart = "face-oval",
            eyePart = "eyes-brown",
            hairPart = "hair-short-brown",
            outfitPart = "outfit-worker",
            expressionPart = "expression-calm",
            heightScale = 0.8f,
            layerOrderVersion = 1
        }
    };

    private void StubPage(params object[] items) =>
        Stub.Ok("api/appearance?take=200&skip=0", new { items, total = items.Length });

    [Fact]
    public void RendersTheGenosList()
    {
        StubPage(Genos(1, "Ada"), Genos(2, "Bram"));

        var cut = Render<AppearancePage>();

        cut.WaitForAssertion(() => Assert.Contains("Ada", cut.Markup));
        Assert.Contains("Bram", cut.Markup);
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();
    }

    [Fact]
    public void FailedLoadShowsTheErrorAlert()
    {
        Stub.Fail("api/appearance?take=200&skip=0");

        var cut = Render<AppearancePage>();

        cut.WaitForAssertion(() => Assert.Contains("Could not load appearance.", cut.Markup));
    }

    [Fact]
    public void EmptyPageRendersWithoutThrowing()
    {
        StubPage();

        var cut = Render<AppearancePage>();

        cut.WaitForAssertion(() => Assert.Contains("Appearance", cut.Markup));
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();
    }

    /// <summary>
    /// Regression guard for the R2 responsive defect: the search input carried
    /// style="min-width: 18rem" inside a non-wrapping flex row, so below ~406px
    /// the row's min-content exceeded the viewport and the Search button was
    /// pushed off-screen. A fixed min-width on a phone-width search field is the
    /// bug, so assert it cannot come back.
    /// </summary>
    [Fact]
    public void SearchInputCarriesNoFixedMinWidth()
    {
        StubPage(Genos(1, "Ada"));

        var cut = Render<AppearancePage>();

        var input = cut.Find("input[aria-label='Search genos name or network id']");
        var style = input.GetAttribute("style") ?? "";

        Assert.DoesNotContain("18rem", style);
        Assert.Contains("min-width: 0", style);

        // The row must be allowed to wrap, or the button is squeezed off again.
        var row = input.ParentElement!;
        Assert.Contains("flex-wrap", row.ClassList);
    }
}
