using Bunit;
using Genelife.Web.Pages;

namespace Genelife.Web.Tests.Pages;

/// <summary>
/// Regression guard for the admin landing page at "/". Home.razor shipped the stale
/// placeholder copy "Barebone browser shell. Game features are not implemented yet."
/// while every sibling page rendered live data. No test rendered a page at all before
/// this file, which is why the defect reached the running UI unnoticed.
/// </summary>
public class HomeTests : BunitContext
{
    [Fact]
    public void DoesNotClaimFeaturesAreUnimplemented()
    {
        var cut = Render<Home>();

        Assert.DoesNotContain("not implemented", cut.Markup);
        Assert.DoesNotContain("Barebone", cut.Markup);
    }

    [Fact]
    public void RendersAccurateLandingCopy()
    {
        var cut = Render<Home>();

        Assert.Contains("GeneLife Admin", cut.Markup);
        Assert.Contains("Live admin console for the running GeneLife simulation", cut.Markup);
    }

    [Theory]
    [InlineData("data")]
    [InlineData("map")]
    [InlineData("economy")]
    [InlineData("housing")]
    [InlineData("lifecycle")]
    [InlineData("family")]
    [InlineData("players")]
    [InlineData("appearance")]
    [InlineData("consumption")]
    [InlineData("social")]
    public void LinksToEveryAdminSection(string href)
    {
        var cut = Render<Home>();

        Assert.NotEmpty(cut.FindAll($"a[href='{href}']"));
    }
}
