using FluentAssertions;
using Genelife.Domain;
using Genelife.Main.Domain.Activities;
using Genelife.Tests.TestData;

namespace Genelife.Tests.Domain.Activities;

public class WorkTests
{
    [Theory]
    [InlineData(100.0f)]
    [InlineData(250.0f)]
    [InlineData(500.0f)]
    public void Work_ShouldHaveCorrectDailySalary(float dailySalary)
    {
        // Arrange & Act
        var work = new Work(dailySalary);

        // Assert
        work.DailySalary.Should().Be(dailySalary);
    }

    [Fact]
    public void Work_ShouldHaveCorrectTickDuration()
    {
        // Arrange & Act
        var work = new Work(200.0f);

        // Assert
        work.TickDuration.Should().Be(Constants.TickPerHour * 6);
    }

    [Fact]
    public void Work_ShouldReduceEnergyAndIncreaseMoney()
    {
        // Arrange
        var dailySalary = 200.0f;
        var human = TestDataBuilder.CreateHuman(energy: 80.0f, money: 1000.0f);
        var work = new Work(dailySalary);

        // Act
        var result = work.Apply(human);

        // Assert
        // 80 - 40 = 40
        result.Energy.Should().Be(40.0f);
        // 1000 + 200 = 1200
        result.Money.Should().Be(1200.0f);
        result.FirstName.Should().Be(human.FirstName);
        result.Hunger.Should().Be(human.Hunger);
        result.Hygiene.Should().Be(human.Hygiene);
    }

    [Fact]
    public void Work_ShouldClampEnergyToZero()
    {
        // Arrange
        var dailySalary = 150.0f;
        var human = TestDataBuilder.CreateHuman(energy: 30.0f, money: 500.0f);
        var work = new Work(dailySalary);

        // Act
        var result = work.Apply(human);

        // Assert
        // 30 - 40 = -10, clamped to 0
        result.Energy.Should().Be(0.0f);
        // 500 + 150 = 650
        result.Money.Should().Be(650.0f);
    }

    [Fact]
    public void Work_ShouldNotExceedMaxEnergy()
    {
        // Arrange
        var dailySalary = 300.0f;
        var human = TestDataBuilder.CreateHuman(energy: 100.0f, money: 0.0f);
        var work = new Work(dailySalary);

        // Act
        var result = work.Apply(human);

        // Assert
        // 100 - 40 = 60, within bounds
        result.Energy.Should().Be(60.0f);
        // 0 + 300 = 300
        result.Money.Should().Be(300.0f);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(50.0f)]
    [InlineData(100.0f)]
    public void Work_ShouldHandleVariousEnergyLevels(float initialEnergy)
    {
        // Arrange
        var dailySalary = 100.0f;
        var human = TestDataBuilder.CreateHuman(energy: initialEnergy, money: 0.0f);
        var work = new Work(dailySalary);

        // Act
        var result = work.Apply(human);

        // Assert
        var expectedEnergy = Math.Clamp(initialEnergy - 40, 0, 100);
        result.Energy.Should().Be(expectedEnergy);
        result.Money.Should().Be(100.0f);
    }

    [Fact]
    public void Work_ShouldHandleZeroSalary()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman(energy: 60.0f, money: 100.0f);
        var work = new Work(0.0f);

        // Act
        var result = work.Apply(human);

        // Assert
        // 60 - 40 = 20
        result.Energy.Should().Be(20.0f);
        // 100 + 0 = 100
        result.Money.Should().Be(100.0f);
    }

    [Fact]
    public void Work_ShouldHandleNegativeMoney()
    {
        // Arrange
        var dailySalary = 50.0f;
        var human = TestDataBuilder.CreateHuman(energy: 70.0f, money: -100.0f);
        var work = new Work(dailySalary);

        // Act
        var result = work.Apply(human);

        // Assert
        // 70 - 40 = 30
        result.Energy.Should().Be(30.0f);
        // -100 + 50 = -50
        result.Money.Should().Be(-50.0f);
    }
}
