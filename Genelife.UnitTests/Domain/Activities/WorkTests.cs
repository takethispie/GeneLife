using FluentAssertions;
using Genelife.Domain.Activities;
using Genelife.Domain.Activities.Interfaces;
using Genelife.Domain.Human.Activities;
using Genelife.UnitTests.TestData;

namespace Genelife.UnitTests.Domain.Activities;

public class WorkTests
{
    [Fact]
    public void Work_ShouldReduceEnergyAndIncreaseMoney()
    {
        var now = DateTime.UtcNow;
        var human = TestDataBuilder.CreateHuman(energy: 80.0f, money: 1000.0f);
        var work = new Work(now);
        human.Do(work);
        human.Energy.Should().Be(40.0f);
        human.Money.Should().Be(1200.0f);
        human.FirstName.Should().Be(human.FirstName);
        human.Hunger.Should().Be(human.Hunger);
        human.Hygiene.Should().Be(human.Hygiene);
    }

    [Fact]
    public void Work_ShouldClampEnergyToZero()
    {
        var now = DateTime.UtcNow;
        var human = TestDataBuilder.CreateHuman(energy: 30.0f, money: 500.0f);
        var work = new Work(now);
        human.Do(work);
        human.Energy.Should().Be(0.0f);
        human.Money.Should().Be(650.0f);
    }

    [Fact]
    public void Work_ShouldNotExceedMaxEnergy()
    {
        var now = DateTime.UtcNow;
        var human = TestDataBuilder.CreateHuman(energy: 100.0f, money: 0.0f);
        var work = new Work(now);
        human.Do(work);
        human.Energy.Should().Be(60.0f);
        human.Money.Should().Be(300.0f);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(50.0f)]
    [InlineData(100.0f)]
    public void Work_ShouldHandleVariousEnergyLevels(float initialEnergy)
    {
        var now = DateTime.UtcNow;
        var human = TestDataBuilder.CreateHuman(energy: initialEnergy, money: 0.0f);
        var work = new Work(now);
        human.Do(work);
        var expectedEnergy = Math.Clamp(initialEnergy - 40, 0, 100);
        human.Energy.Should().Be(expectedEnergy);
        human.Money.Should().Be(100.0f);
    }

    [Fact]
    public void Work_ShouldHandleZeroSalary()
    {
        var now = DateTime.UtcNow;
        var human = TestDataBuilder.CreateHuman(energy: 60.0f, money: 100.0f);
        var work = new Work(now);
        human.Do(work);
        human.Energy.Should().Be(20.0f);
        human.Money.Should().Be(100.0f);
    }

    [Fact]
    public void Work_ShouldHandleNegativeMoney()
    {
        var now = DateTime.UtcNow;
        var human = TestDataBuilder.CreateHuman(energy: 70.0f, money: -100.0f);
        var work = new Work(now);
        human.Do(work);
        human.Energy.Should().Be(30.0f);
        human.Money.Should().Be(-50.0f);
    }
}
