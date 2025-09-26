using Bogus;
using FluentAssertions;
using Genelife.Domain;
using Genelife.Main.Domain.Activities;
using Genelife.Main.Usecases;
using Genelife.Tests.TestData;

namespace Genelife.Tests.Usecases;

public class UpdateNeedsTests
{
    private readonly UpdateNeeds _updateNeeds = new();

    [Fact]
    public void Execute_ShouldDecreaseAllNeeds()
    {
        var human = TestDataBuilder.CreateHuman(hunger: 80.0f, energy: 90.0f, hygiene: 85.0f);
        var result = _updateNeeds.Execute(human);
        result.Hunger.Should().BeLessThan(human.Hunger);
        result.Energy.Should().BeLessThan(human.Energy);
        result.Hygiene.Should().BeLessThan(human.Hygiene);
    }

    [Fact]
    public void Execute_ShouldApplyCorrectDecayRates()
    {
        var human = TestDataBuilder.CreateHuman(hunger: 100.0f, energy: 100.0f, hygiene: 100.0f);
        var result = _updateNeeds.Execute(human);
        result.Hunger.Should().Be(97.0f);
        result.Energy.Should().Be(98.2f);
        result.Hygiene.Should().Be(97.6f);
    }

    [Fact]
    public void Execute_ShouldClampNeedsToZero()
    {
        var human = TestDataBuilder.CreateHuman(hunger: 1.0f, energy: 0.5f, hygiene: 2.0f);
        var result = _updateNeeds.Execute(human);
        result.Hunger.Should().Be(0.0f);
        result.Energy.Should().Be(0.0f);
        result.Hygiene.Should().Be(0.0f);
    }

    [Fact]
    public void Execute_ShouldNotExceed100()
    {
        var human = TestDataBuilder.CreateHuman(hunger: 100.0f, energy: 100.0f, hygiene: 100.0f);
        var result = _updateNeeds.Execute(human);
        result.Hunger.Should().BeLessOrEqualTo(100.0f);
        result.Energy.Should().BeLessOrEqualTo(100.0f);
        result.Hygiene.Should().BeLessOrEqualTo(100.0f);
    }

    [Fact]
    public void Execute_ShouldPreserveOtherProperties()
    {
        var human = TestDataBuilder.CreateHuman(money: 1500.0f);
        var result = _updateNeeds.Execute(human);
        result.FirstName.Should().Be(human.FirstName);
        result.LastName.Should().Be(human.LastName);
        result.Birthday.Should().Be(human.Birthday);
        result.Sex.Should().Be(human.Sex);
        result.Money.Should().Be(human.Money);
    }

    [Theory]
    [InlineData(0.0f, 0.0f, 0.0f)]
    [InlineData(50.0f, 50.0f, 50.0f)]
    [InlineData(100.0f, 100.0f, 100.0f)]
    public void Execute_ShouldHandleVariousNeedLevels(float hunger, float energy, float hygiene)
    {
        var human = TestDataBuilder.CreateHuman(hunger: hunger, energy: energy, hygiene: hygiene);
        var result = _updateNeeds.Execute(human);
        result.Hunger.Should().BeInRange(0.0f, 100.0f);
        result.Energy.Should().BeInRange(0.0f, 100.0f);
        result.Hygiene.Should().BeInRange(0.0f, 100.0f);
    }
}

public class ChooseActivityTests
{
    private readonly ChooseActivity _chooseActivity = new();

    [Theory]
    [InlineData(22)]
    [InlineData(23)]
    [InlineData(0)]
    [InlineData(1)]
    public void Execute_ShouldChooseSleepAtNightTime(int hour)
    {
        var human = TestDataBuilder.CreateHuman(energy: 10.0f, hunger: 50.0f, hygiene: 50.0f);
        var activity = _chooseActivity.Execute(human, hour, true);
        activity.Should().BeOfType<Sleep>();
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(21)]
    public void Execute_ShouldChooseShowerDuringHygieneHours(int hour)
    {
        var human = TestDataBuilder.CreateHuman(energy: 80.0f, hunger: 80.0f, hygiene: 10.0f);
        var activity = _chooseActivity.Execute(human, hour, true);
        activity.Should().BeOfType<Shower>();
    }

    [Theory]
    [InlineData(13)]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(21)]
    public void Execute_ShouldChooseEatDuringMealHours(int hour)
    {
        var human = TestDataBuilder.CreateHuman(energy: 80.0f, hunger: 10.0f, hygiene: 80.0f);
        var activity = _chooseActivity.Execute(human, hour, true);
        activity.Should().BeOfType<Eat>();
    }

    [Fact]
    public void Execute_ShouldChooseLowestNeedActivity()
    {
        var human = TestDataBuilder.CreateHuman(energy: 5.0f, hunger: 50.0f, hygiene: 50.0f);
        var activity = _chooseActivity.Execute(human, 22, true);
        activity.Should().BeOfType<Sleep>();
    }

    [Fact]
    public void Execute_ShouldChooseHygieneWhenLowest()
    {
        var human = TestDataBuilder.CreateHuman(energy: 80.0f, hunger: 80.0f, hygiene: 5.0f);
        var activity = _chooseActivity.Execute(human, 7, true);
        activity.Should().BeOfType<Shower>();
    }

    [Fact]
    public void Execute_ShouldChooseHungerWhenLowest()
    {
        var human = TestDataBuilder.CreateHuman(energy: 80.0f, hunger: 5.0f, hygiene: 80.0f);
        var activity = _chooseActivity.Execute(human, 13, true);
        activity.Should().BeOfType<Eat>();
    }

    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    public void Execute_ShouldReturnNullDuringWorkHours(int hour)
    {
        var human = TestDataBuilder.CreateHuman(energy: 50.0f, hunger: 50.0f, hygiene: 50.0f);
        var activity = _chooseActivity.Execute(human, hour, true);
        activity.Should().BeNull();
    }

    [Fact]
    public void Execute_ShouldPrioritizeBasedOnNeedLevel()
    {
        var human = TestDataBuilder.CreateHuman(energy: 30.0f, hunger: 10.0f, hygiene: 40.0f);
        var activity = _chooseActivity.Execute(human, 19, true);
        activity.Should().BeOfType<Eat>();
    }

    [Fact]
    public void Execute_ShouldHandleEqualNeeds()
    {
        var human = TestDataBuilder.CreateHuman(energy: 50.0f, hunger: 30.0f, hygiene: 30.0f);
        var activity = _chooseActivity.Execute(human, 20, true);
        activity.Should().NotBeNull();
        activity.Should().Match(x => x.GetType() == typeof(Eat) || x.GetType() == typeof(Shower));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Execute_ShouldReturnNullDuringEarlyMorning(int hour)
    {
        var human = TestDataBuilder.CreateHuman(energy: 50.0f, hunger: 50.0f, hygiene: 50.0f);
        var activity = _chooseActivity.Execute(human, hour, true);
        activity.Should().BeNull();
    }

    [Fact]
    public void Execute_ShouldHandleExtremeNeedValues()
    {
        var human = TestDataBuilder.CreateHuman(energy: 0.0f, hunger: 0.0f, hygiene: 0.0f);
        var activity = _chooseActivity.Execute(human, 22, true);
        activity.Should().BeOfType<Sleep>();
    }
}