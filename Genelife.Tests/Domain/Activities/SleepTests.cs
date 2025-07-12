using FluentAssertions;
using Genelife.Domain;
using Genelife.Main.Domain;
using Genelife.Main.Domain.Activities;
using Genelife.Tests.TestData;

namespace Genelife.Tests.Domain.Activities;

public class SleepTests
{
    [Fact]
    public void Sleep_ShouldHaveCorrectTickDuration()
    {
        // Arrange & Act
        var sleep = new Sleep();

        // Assert
        sleep.TickDuration.Should().Be(Constants.TickPerHour * 8);
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
