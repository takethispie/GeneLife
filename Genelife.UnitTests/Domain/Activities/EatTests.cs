using FluentAssertions;
using Genelife.Domain;
using Genelife.Domain.Activities;
using Genelife.Domain.Activities.Interfaces;
using Genelife.UnitTests.TestData;

namespace Genelife.UnitTests.Domain.Activities;

public class EatTests
{
    [Fact]
    public void Eat_ShouldRestoreHungerToFull()
    {
        var now = DateTime.UtcNow;
        var human = TestDataBuilder.CreateHuman(hunger: 30.0f);
        var eat = new Eat(now);

        human.Do(eat);

        human.Hunger.Should().Be(100.0f);
        human.FirstName.Should().Be(human.FirstName);
        human.Energy.Should().Be(human.Energy);
        human.Hygiene.Should().Be(human.Hygiene);
        human.Money.Should().Be(human.Money);
    }

    [Fact]
    public void Eat_ShouldWorkWithZeroHunger()
    {
        var now = DateTime.UtcNow;
        var human = TestDataBuilder.CreateHuman(hunger: 0.0f);
        var eat = new Eat(now);

        human.Do(eat);

        human.Hunger.Should().Be(100.0f);
    }

    [Fact]
    public void Eat_ShouldWorkWithFullHunger()
    {
        var now = DateTime.UtcNow;
        var human = TestDataBuilder.CreateHuman(hunger: 50.0f);
        var eat = new Eat(now);

        human.Do(eat);

        human.Hunger.Should().Be(100.0f);
    }
}
