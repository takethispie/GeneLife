using FluentAssertions;
using Genelife.Domain;
using Genelife.Main.Domain;
using Genelife.Main.Domain.Activities;
using Genelife.Tests.TestData;

namespace Genelife.Tests.Domain.Activities;

public class EatTests
{
    [Fact]
    public void Eat_ShouldHaveCorrectTickDuration()
    {
        // Arrange & Act
        var eat = new Eat();

        // Assert
        eat.TickDuration.Should().Be(Constants.TickPerHour);
    }

    [Fact]
    public void Eat_ShouldRestoreHungerToFull()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman(hunger: 30.0f);
        var eat = new Eat();

        // Act
        var result = eat.Apply(human);

        // Assert
        result.Hunger.Should().Be(100.0f);
        result.FirstName.Should().Be(human.FirstName); // Other properties unchanged
        result.Energy.Should().Be(human.Energy);
        result.Hygiene.Should().Be(human.Hygiene);
        result.Money.Should().Be(human.Money);
    }

    [Fact]
    public void Eat_ShouldWorkWithZeroHunger()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman(hunger: 0.0f);
        var eat = new Eat();

        // Act
        var result = eat.Apply(human);

        // Assert
        result.Hunger.Should().Be(100.0f);
    }

    [Fact]
    public void Eat_ShouldWorkWithFullHunger()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman(hunger: 50.0f);
        var eat = new Eat();

        // Act
        var result = eat.Apply(human);

        // Assert
        result.Hunger.Should().Be(100.0f);
    }

    [Fact]
    public void Eat_ShouldConvertToCorrectEnum()
    {
        // Arrange
        var eat = new Eat();

        // Act
        var activityEnum = eat.ToEnum();

        // Assert
        activityEnum.Should().Be(ActivityEnum.Eat);
    }
}
