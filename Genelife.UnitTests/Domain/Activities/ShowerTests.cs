using FluentAssertions;
using Genelife.Domain;
using Genelife.Domain.Activities;
using Genelife.Domain.Human.Activities;
using Genelife.UnitTests.TestData;

namespace Genelife.UnitTests.Domain.Activities;

public class ShowerTests
{
    [Fact]
    public void Shower_ShouldHaveCorrectTickDuration()
    {
        var shower = new Shower();
        shower.TickDuration.Should().Be(5);
    }

    [Fact]
    public void Shower_ShouldRestoreHygieneToFull()
    {
        var human = TestDataBuilder.CreateHuman(hygiene: 40.0f);
        var shower = new Shower();

        human.Do(shower);

        human.Hygiene.Should().Be(100.0f);
        human.FirstName.Should().Be(human.FirstName);
        human.Hunger.Should().Be(human.Hunger);
        human.Energy.Should().Be(human.Energy);
        human.Money.Should().Be(human.Money);
    }

    [Fact]
    public void Shower_ShouldWorkWithZeroHygiene()
    {
        var human = TestDataBuilder.CreateHuman(hygiene: 0.0f);
        var shower = new Shower();

        human.Do(shower);

        human.Hygiene.Should().Be(100.0f);
    }

    [Fact]
    public void Shower_ShouldWorkWithFullHygiene()
    {
        var human = TestDataBuilder.CreateHuman(hygiene: 100.0f);
        var shower = new Shower();

        human.Do(shower);

        human.Hygiene.Should().Be(100.0f);
    }
}
