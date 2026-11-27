using Bunit;
using Genelife.Web.Pages;
using Genelife.Web.Tests.Support;

namespace Genelife.Web.Tests.Pages;

/// <summary>
/// Lifecycle page. Carries the edit-school form, whose four controls shipped
/// with visual-only labels: the label was a sibling with no <c>for</c> and the
/// control had no <c>id</c>, so <c>input.labels</c> was empty and a screen
/// reader announced "edit text" / "combo box" with no field name.
/// </summary>
public class LifecycleTests : AdminPageTest
{
    /// <summary>Every control in the edit-school form, and the label that must name it.</summary>
    private static readonly (string Id, string Label)[] EditSchoolControls =
    {
        ("editSchoolCapacity", "Capacity"),
        ("editSchoolQuality", "Quality"),
        ("editSchoolCost", "Cost"),
        ("editSchoolEmphasis", "Emphasis")
    };

    private void StubLifecycle()
    {
        Stub.Ok("api/lifecycle/overview", new
        {
            status = new { immigration = new { targetPopulation = 120 } },
            schools = new[]
            {
                new
                {
                    networkId = 7001UL,
                    name = "North School",
                    district = "civic",
                    capacity = 60,
                    enrolled = 42,
                    attendance = 0.9m,
                    quality = 0.65m,
                    emphasis = "Programming",
                    monthlyCost = 50m,
                    ownerNetworkId = (ulong?)null,
                    owner = ""
                }
            },
            pensions = new { },
            mortality = Array.Empty<object>(),
            populationHistory = Array.Empty<object>()
        });
        Stub.Ok("api/lifecycle/genoses", Array.Empty<object>());
    }

    [Fact]
    public void RendersTheOverview()
    {
        StubLifecycle();

        var cut = Render<Lifecycle>();

        cut.WaitForAssertion(() => Assert.Contains("North School", cut.Markup));
        Assert.DoesNotContain("alert-danger", cut.Markup);
        AssertNoUndeclaredRequests();
    }

    [Fact]
    public void EditActionRevealsALabelledForm()
    {
        StubLifecycle();

        var cut = Render<Lifecycle>();
        cut.WaitForAssertion(() => Assert.Contains("North School", cut.Markup));

        // Drive the real Edit action rather than rendering the form directly --
        // the defect was in the form the action reveals.
        var edit = cut.FindAll("button").First(b => b.TextContent.Trim() == "Edit");
        edit.Click();

        foreach (var (id, label) in EditSchoolControls)
        {
            // Find throws if the control is missing, so reaching the label check
            // already proves the control rendered.
            cut.Find($"#{id}");

            // The label must point at this control, or the association is empty.
            var labels = cut.FindAll($"label[for='{id}']");
            Assert.True(labels.Count == 1, $"#{id} must have exactly one label[for], found {labels.Count}");
            Assert.Contains(label, labels[0].TextContent);
        }
    }

    [Fact]
    public void FailedLoadShowsTheErrorAlert()
    {
        Stub.Fail("api/lifecycle/overview");
        Stub.Ok("api/lifecycle/genoses", Array.Empty<object>());

        var cut = Render<Lifecycle>();

        cut.WaitForAssertion(() => Assert.Contains("alert-danger", cut.Markup));
    }
}
