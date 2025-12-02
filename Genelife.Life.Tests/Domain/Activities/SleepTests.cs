using FluentAssertions;
using Genelife.Life.Domain;
using Genelife.Life.Domain.Activities;
using Genelife.Life.Interfaces;
using Genelife.Life.Tests.TestData;

namespace Genelife.Life.Tests.Domain.Activities;

public class SleepTests
{
    [Fact]
    public void Sleep_ShouldHaveCorrectTickDuration()
    {
        // Arrange & Act
        var sleep = new Sleep();

        // Assert
        sleep.TickDuration.Should().Be(ILivingActivity.TickPerHour * 8);
    }

    [Fact]
    public void Sleep_ShouldRestoreEnergyToFull()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman(energy: 20.0f);
        var sleep = new Sleep();

        // Act
        var result = sleep.Apply(human);

        // Assert
        result.Energy.Should().Be(100.0f);
        result.FirstName.Should().Be(human.FirstName); // Other properties unchanged
        result.Hunger.Should().Be(human.Hunger);
        result.Hygiene.Should().Be(human.Hygiene);
        result.Money.Should().Be(human.Money);
    }

    [Fact]
    public void Sleep_ShouldWorkWithZeroEnergy()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman(energy: 0.0f);
        var sleep = new Sleep();

        // Act
        var result = sleep.Apply(human);

        // Assert
        result.Energy.Should().Be(100.0f);
    }

    [Fact]
    public void Sleep_ShouldWorkWithFullEnergy()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman(energy: 100.0f);
        var sleep = new Sleep();

        // Act
        var result = sleep.Apply(human);

        // Assert
        result.Energy.Should().Be(100.0f);
    }

    [Fact]
    public void Sleep_ShouldConvertToCorrectEnum()
    {
        // Arrange
        var sleep = new Sleep();

        // Act
        var activityEnum = sleep.ToEnum();

        // Assert
        activityEnum.Should().Be(ActivityEnum.Sleep);
    }
}
