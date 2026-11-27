using Genelife.Web.Pages;
using Genelife.Web.Tests.Support;

namespace Genelife.Web.Tests.Pages;

/// <summary>
/// NotFound is the only admin route with no data dependency. Cheap, but it is a
/// route: nothing rendered any page at all before this tier, so even a static
/// route was unverified.
/// </summary>
public class NotFoundTests : AdminPageTest
{
    [Fact]
    public void RendersWithoutRequestingData()
    {
        var cut = Render<NotFound>();

        Assert.Contains("Not Found", cut.Markup);
        Assert.Empty(Stub.Requests);
    }
}
