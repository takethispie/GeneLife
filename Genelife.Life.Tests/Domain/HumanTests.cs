using FluentAssertions;
using Genelife.Domain;
using Genelife.Life.Tests.TestData;

namespace Genelife.Life.Tests.Domain;

public class HumanTests
{
    [Theory]
    [InlineData(0.0f)]
    [InlineData(50.0f)]
    [InlineData(100.0f)]
    public void Human_ShouldAcceptValidNeedValues(float needValue)
    {
        var human = TestDataBuilder.CreateHuman(hunger: needValue, energy: needValue, hygiene: needValue);
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
        var human = TestDataBuilder.CreateHuman(money: money);
        human.Money.Should().Be(money);
    }
}