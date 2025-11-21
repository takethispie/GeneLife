using FluentAssertions;
using Genelife.Life.Interfaces;
using Genelife.Life.Tests.TestData;

namespace Genelife.Life.Tests.Domain.Activities;

public class WorkTests
{
    [Fact]
    public void Work_ShouldHaveCorrectTickDuration()
    {
        var work = new Life.Domain.Activities.Work();
        work.TickDuration.Should().Be(ILivingActivity.TickPerHour * 6);
    }

    [Fact]
    public void Work_ShouldReduceEnergyAndIncreaseMoney()
    {
        var human = TestDataBuilder.CreateHuman(energy: 80.0f, money: 1000.0f);
        var work = new Life.Domain.Activities.Work();
        var result = work.Apply(human);
        result.Energy.Should().Be(40.0f);
        result.Money.Should().Be(1200.0f);
        result.FirstName.Should().Be(human.FirstName);
        result.Hunger.Should().Be(human.Hunger);
        result.Hygiene.Should().Be(human.Hygiene);
    }

    [Fact]
    public void Work_ShouldClampEnergyToZero()
    {
        var human = TestDataBuilder.CreateHuman(energy: 30.0f, money: 500.0f);
        var work = new Life.Domain.Activities.Work();
        var result = work.Apply(human);
        result.Energy.Should().Be(0.0f);
        result.Money.Should().Be(650.0f);
    }

    [Fact]
    public void Work_ShouldNotExceedMaxEnergy()
    {
        var human = TestDataBuilder.CreateHuman(energy: 100.0f, money: 0.0f);
        var work = new Life.Domain.Activities.Work();
        var result = work.Apply(human);
        result.Energy.Should().Be(60.0f);
        result.Money.Should().Be(300.0f);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(50.0f)]
    [InlineData(100.0f)]
    public void Work_ShouldHandleVariousEnergyLevels(float initialEnergy)
    {
        var human = TestDataBuilder.CreateHuman(energy: initialEnergy, money: 0.0f);
        var work = new Life.Domain.Activities.Work();
        var result = work.Apply(human);
        var expectedEnergy = Math.Clamp(initialEnergy - 40, 0, 100);
        result.Energy.Should().Be(expectedEnergy);
        result.Money.Should().Be(100.0f);
    }

    [Fact]
    public void Work_ShouldHandleZeroSalary()
    {
        var human = TestDataBuilder.CreateHuman(energy: 60.0f, money: 100.0f);
        var work = new Life.Domain.Activities.Work();
        var result = work.Apply(human);
        result.Energy.Should().Be(20.0f);
        result.Money.Should().Be(100.0f);
    }

    [Fact]
    public void Work_ShouldHandleNegativeMoney()
    {
        var human = TestDataBuilder.CreateHuman(energy: 70.0f, money: -100.0f);
        var work = new Life.Domain.Activities.Work();
        var result = work.Apply(human);
        result.Energy.Should().Be(30.0f);
        result.Money.Should().Be(-50.0f);
    }
}
