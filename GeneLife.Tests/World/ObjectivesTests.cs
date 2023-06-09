using FluentAssertions;
using GeneLife.Athena.Core.Objectives;
using GeneLife.Athena.Extensions;

namespace GeneLife.Tests.World;

public class ObjectivesTests
{
    [Fact]
    public void Should_Be_Top_Priority()
    {
        var currentObjectives = new IObjective[] { new Eat(5), new Drink(8) };
        currentObjectives.IsHighestPriority(typeof(Eat)).Should().NotBe(true);
        currentObjectives.IsHighestPriority(typeof(Drink)).Should().BeTrue();
    }
}