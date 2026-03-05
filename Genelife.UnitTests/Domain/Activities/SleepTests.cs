using FluentAssertions;
using Genelife.Domain;
using Genelife.Domain.Activities;
using Genelife.Domain.Activities.Interfaces;
using Genelife.UnitTests.TestData;

namespace Genelife.UnitTests.Domain.Activities;

public class SleepTests
{
    [Fact]
    public void Sleep_ShouldHaveCorrectTickDuration()
    {
        var sleep = new Sleep();
        sleep.TickDuration.Should().Be(IBeingActivity.TickPerHour * 8);
    }

    [Fact]
    public void Sleep_ShouldRestoreEnergyToFull()
    {
        var human = TestDataBuilder.CreateHuman(energy: 20.0f);
        var sleep = new Sleep();

        human.Do(sleep);

        human.Energy.Should().Be(100.0f);
        human.FirstName.Should().Be(human.FirstName);
        human.Hunger.Should().Be(human.Hunger);
        human.Hygiene.Should().Be(human.Hygiene);
        human.Money.Should().Be(human.Money);
    }

    [Fact]
    public void Sleep_ShouldWorkWithZeroEnergy()
    {
        var human = TestDataBuilder.CreateHuman(energy: 0.0f);
        var sleep = new Sleep();

        human.Do(sleep);

        human.Energy.Should().Be(100.0f);
    }

    [Fact]
    public void Sleep_ShouldWorkWithFullEnergy()
    {
        var human = TestDataBuilder.CreateHuman(energy: 100.0f);
        var sleep = new Sleep();

        human.Do(sleep);

        human.Energy.Should().Be(100.0f);
    }
}
