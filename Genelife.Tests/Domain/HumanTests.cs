using FluentAssertions;
using Genelife.Domain;
using Genelife.Tests.TestData;

namespace Genelife.Tests.Domain;

public class HumanTests
{
    [Theory]
    [InlineData(0.0f)]
    [InlineData(50.0f)]
    [InlineData(100.0f)]
    public void Human_ShouldAcceptValidNeedValues(float needValue)
    {
        // Arrange & Act
        var human = TestDataBuilder.CreateHuman(hunger: needValue, energy: needValue, hygiene: needValue);

        // Assert
        human.Hunger.Should().Be(needValue);
        human.Energy.Should().Be(needValue);
        human.Hygiene.Should().Be(needValue);
    }

    [Theory]
    [InlineData(-1000.0f)]
    [InlineData(0.0f)]
    [InlineData(1000.0f)]
    public void Human_ShouldAcceptAnyMoneyValue(float money)
    {
        // Arrange & Act
        var human = TestDataBuilder.CreateHuman(money: money);

        // Assert
        human.Money.Should().Be(money);
    }
}