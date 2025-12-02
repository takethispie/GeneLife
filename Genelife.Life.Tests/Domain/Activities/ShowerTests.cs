using FluentAssertions;
using Genelife.Life.Domain;
using Genelife.Life.Domain.Activities;
using Genelife.Life.Tests.TestData;

namespace Genelife.Life.Tests.Domain.Activities;

public class ShowerTests
{
    [Fact]
    public void Shower_ShouldHaveCorrectTickDuration()
    {
        // Arrange & Act
        var shower = new Shower();

        // Assert
        shower.TickDuration.Should().Be(5);
    }

    [Fact]
    public void Shower_ShouldRestoreHygieneToFull()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman(hygiene: 40.0f);
        var shower = new Shower();

        // Act
        var result = shower.Apply(human);

        // Assert
        result.Hygiene.Should().Be(100.0f);
        result.FirstName.Should().Be(human.FirstName); // Other properties unchanged
        result.Hunger.Should().Be(human.Hunger);
        result.Energy.Should().Be(human.Energy);
        result.Money.Should().Be(human.Money);
    }

    [Fact]
    public void Shower_ShouldWorkWithZeroHygiene()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman(hygiene: 0.0f);
        var shower = new Shower();

        // Act
        var result = shower.Apply(human);

        // Assert
        result.Hygiene.Should().Be(100.0f);
    }

    [Fact]
    public void Shower_ShouldWorkWithFullHygiene()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman(hygiene: 100.0f);
        var shower = new Shower();

        // Act
        var result = shower.Apply(human);

        // Assert
        result.Hygiene.Should().Be(100.0f);
    }

    [Fact]
    public void Shower_ShouldConvertToCorrectEnum()
    {
        // Arrange
        var shower = new Shower();

        // Act
        var activityEnum = shower.ToEnum();

        // Assert
        activityEnum.Should().Be(ActivityEnum.Shower);
    }
}
