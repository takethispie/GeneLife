using Bunit;
using Genelife.Api.DTOs;
using Genelife.Web.Components;

namespace Genelife.Web.Tests.Components;

/// <summary>
/// First component tests for Genelife.Web. The Blazor admin shipped 13 pages and
/// half a dozen components with no rendered-output coverage at all -- the suite
/// only ever exercised the server side. These assert what the components actually
/// render, which is what a regression would change.
/// </summary>
public class EmptyStateTests : BunitContext
{
    [Fact]
    public void RendersItsMessage()
    {
        var cut = Render<EmptyState>(p => p.Add(c => c.Message, "No genoses found"));

        Assert.Contains("No genoses found", cut.Markup);
    }
}

public class ConfirmDialogTests : BunitContext
{
    [Fact]
    public void RendersNothingWhenNotVisible()
    {
        var cut = Render<ConfirmDialog>(p => p.Add(c => c.IsVisible, false));

        Assert.Empty(cut.FindAll(".confirm-dialog-card"));
    }

    [Fact]
    public void RendersTitleMessageAndLabelsWhenVisible()
    {
        var cut = Render<ConfirmDialog>(p => p
            .Add(c => c.IsVisible, true)
            .Add(c => c.Title, "Reset world")
            .Add(c => c.Message, "This cannot be undone")
            .Add(c => c.ConfirmLabel, "Yes, reset")
            .Add(c => c.CancelLabel, "Keep it"));

        Assert.Contains("Reset world", cut.Markup);
        Assert.Contains("This cannot be undone", cut.Markup);
        Assert.Contains("Yes, reset", cut.Markup);
        Assert.Contains("Keep it", cut.Markup);
    }

    [Fact]
    public void DestructiveDialogUsesDangerButton()
    {
        var cut = Render<ConfirmDialog>(p => p
            .Add(c => c.IsVisible, true)
            .Add(c => c.IsDestructive, true));

        Assert.Contains("btn-danger", cut.Find(".card-footer").InnerHtml);
    }

    [Fact]
    public void BusyDialogDisablesBothButtons()
    {
        var cut = Render<ConfirmDialog>(p => p
            .Add(c => c.IsVisible, true)
            .Add(c => c.IsBusy, true));

        foreach (var button in cut.FindAll(".card-footer button"))
            Assert.True(button.HasAttribute("disabled"), "every action must be disabled while busy");
    }

    [Fact]
    public void ConfirmButtonInvokesTheCallback()
    {
        var confirmed = false;
        var cut = Render<ConfirmDialog>(p => p
            .Add(c => c.IsVisible, true)
            .Add(c => c.OnConfirm, () => confirmed = true));

        cut.FindAll(".card-footer button")[1].Click();

        Assert.True(confirmed, "clicking the confirm button must raise OnConfirm");
    }

    [Fact]
    public void CancelButtonInvokesTheCallback()
    {
        var cancelled = false;
        var cut = Render<ConfirmDialog>(p => p
            .Add(c => c.IsVisible, true)
            .Add(c => c.OnCancel, () => cancelled = true));

        cut.FindAll(".card-footer button")[0].Click();

        Assert.True(cancelled, "clicking cancel must raise OnCancel");
    }

    [Fact]
    public void EscapeKeyCancels()
    {
        var cancelled = false;
        var cut = Render<ConfirmDialog>(p => p
            .Add(c => c.IsVisible, true)
            .Add(c => c.OnCancel, () => cancelled = true));

        cut.Find(".confirm-dialog-backdrop")
            .TriggerEvent("onkeydown", new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.True(cancelled, "Escape must cancel the dialog");
    }

    [Fact]
    public void BusyDialogIgnoresConfirmClicks()
    {
        var confirmed = false;
        var cut = Render<ConfirmDialog>(p => p
            .Add(c => c.IsVisible, true)
            .Add(c => c.IsBusy, true)
            .Add(c => c.OnConfirm, () => confirmed = true));

        cut.FindAll(".card-footer button")[1].Click();

        Assert.False(confirmed, "a busy dialog must not confirm");
    }
}

public class GenosesTableTests : BunitContext
{
    private static GenosStatus Genos(ulong id, string name, float hunger, bool player = false) => new()
    {
        NetworkId = id,
        Name = name,
        Age = 30,
        Hunger = hunger,
        Energy = 80f,
        Hygiene = 80f,
        Bladder = 80f,
        CurrentAction = "Idle",
        WalletBalance = 123.45m,
        IsPlayerControlled = player,
    };

    [Fact]
    public void RowsAreOrderedByNetworkId()
    {
        var cut = Render<GenosesTable>(p => p
            .Add(c => c.Genoses, new List<GenosStatus>
            {
                Genos(7, "Seven", 80f),
                Genos(2, "Two", 80f),
                Genos(5, "Five", 80f),
            }));

        var names = cut.FindAll("tbody tr td:nth-child(2)").Select(td => td.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "Two", "Five", "Seven" }, names);
    }

    [Theory]
    [InlineData(10f, "text-danger")]
    [InlineData(40f, "text-warning-emphasis")]
    public void LowNeedsGetTheRightEmphasisClass(float hunger, string expectedClass)
    {
        var cut = Render<GenosesTable>(p => p
            .Add(c => c.Genoses, new List<GenosStatus> { Genos(1, "A", hunger) }));

        // Hunger is the 6th column.
        var hungerCell = cut.Find("tbody tr td:nth-child(6)");
        Assert.Contains(expectedClass, hungerCell.ClassList);
    }

    [Fact]
    public void HealthyNeedsHaveNoEmphasisClass()
    {
        var cut = Render<GenosesTable>(p => p
            .Add(c => c.Genoses, new List<GenosStatus> { Genos(1, "A", 90f) }));

        var hungerCell = cut.Find("tbody tr td:nth-child(6)");
        Assert.DoesNotContain("text-danger", hungerCell.ClassList);
        Assert.DoesNotContain("text-warning-emphasis", hungerCell.ClassList);
    }

    [Fact]
    public void PlayerControlledGenosGetsThePlayerBadge()
    {
        var cut = Render<GenosesTable>(p => p
            .Add(c => c.Genoses, new List<GenosStatus> { Genos(1, "Hero", 90f, player: true) }));

        Assert.Contains("Player", cut.Find("tbody tr td:nth-child(4)").TextContent);
    }

    [Fact]
    public void EmptyListRendersNoRows()
    {
        var cut = Render<GenosesTable>();

        Assert.Empty(cut.FindAll("tbody tr"));
    }
}
