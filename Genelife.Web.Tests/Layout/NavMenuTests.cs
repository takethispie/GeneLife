using Bunit;
using Genelife.Web.Layout;

namespace Genelife.Web.Tests.Layout;

/// <summary>
/// Guards the mobile navigation toggle's exposed state. The hamburger used to
/// carry only a <c>title</c> and swapped a CSS class, so assistive technology
/// was never told whether the menu was open or closed.
/// </summary>
public class NavMenuTests : BunitContext
{
    [Fact]
    public void TogglerStartsCollapsedAndSaysSo()
    {
        var cut = Render<NavMenu>();

        var toggler = cut.Find(".navbar-toggler");
        Assert.Equal("false", toggler.GetAttribute("aria-expanded"));
    }

    [Fact]
    public void TogglerExpandedStateIsLowercase()
    {
        var cut = Render<NavMenu>();

        cut.Find(".navbar-toggler").Click();

        // ARIA state values are case-sensitive: "True" is not a valid value.
        Assert.Equal("true", cut.Find(".navbar-toggler").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void AriaControlsPointsAtTheCollapsibleRegion()
    {
        var cut = Render<NavMenu>();

        var controls = cut.Find(".navbar-toggler").GetAttribute("aria-controls");
        Assert.False(string.IsNullOrWhiteSpace(controls), "toggler must name the region it controls");

        // The referenced id must actually exist, or the relationship is empty.
        Assert.NotNull(cut.Find($"#{controls}"));
    }

    [Fact]
    public void TogglerHasAnExplicitAccessibleName()
    {
        var cut = Render<NavMenu>();

        Assert.Equal("Navigation menu", cut.Find(".navbar-toggler").GetAttribute("aria-label"));
    }

    [Fact]
    public void CollapsedClassTracksTheExpandedState()
    {
        var cut = Render<NavMenu>();

        Assert.Contains("collapse", cut.Find("#nav-menu").ClassList);

        cut.Find(".navbar-toggler").Click();

        Assert.DoesNotContain("collapse", cut.Find("#nav-menu").ClassList);
        Assert.Equal("true", cut.Find(".navbar-toggler").GetAttribute("aria-expanded"));
    }
}
